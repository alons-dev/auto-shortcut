using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace AutoShortcut
{
    // The scrolling list of the pop-up, drawn by hand in a single control: section
    // headers and rows (name on the left; keycaps, or the target in gray, on the
    // right; a star to add it to favorites). Only the visible part is painted, so
    // a few hundred rows cost nothing.
    //
    // It only reports what the user did (EntryClicked, StarClicked,
    // EntryContextMenu); PopupForm decides what happens.
    //
    // It scrolls by itself with a thin Windows 11-style bar on the right (a line
    // that widens under the mouse; drag it, click above/below it, or use the wheel).
    // The native scrollbar can't be drawn on acrylic (see Glass).
    internal sealed class ShortcutList : Control
    {
        private const int RowHeight = 40;
        private const int HeaderHeight = 32;
        private const int SidePad = 6;
        private const int StarBox = 28;
        private const int BarZone = 12;    // px from the right edge where the mouse uses the bar
        private const int WheelLine = 20;  // px per wheel "line" (Windows usually scrolls 3 per notch)

        private readonly Theme _theme;
        private readonly Font _smallFont = new Font("Segoe UI", 8.25F);
        private readonly Font _headerFont = new Font("Segoe UI Semibold", 9F);
        private readonly Font _starFont = new Font(Ui.IconFont.FontFamily, 10F);
        private readonly ToolTip _tip = new ToolTip();
        private IList<ListItem> _items = new List<ListItem>();
        private int[] _tops = new int[0];
        private string _emptyText = "";
        private int _hover = -1;
        private bool _hoverStar;
        private int _pressed = -1;
        private int _scroll;               // px scrolled from the top
        private int _contentHeight;
        private bool _barHover;
        private int _dragFrom = -1;        // mouse Y where a thumb drag started; -1 = not dragging
        private int _dragScroll;

        public Predicate<Entry> IsFavorite { get; set; }

        public event Action<Entry> EntryClicked;
        public event Action<Entry> StarClicked;
        public event Action<Entry, Point> EntryContextMenu;   // screen point

        public ShortcutList(Theme theme)
        {
            _theme = theme;
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.Opaque | ControlStyles.ResizeRedraw, true);
            SetStyle(ControlStyles.Selectable, false);
            BackColor = theme.Background;
            ForeColor = theme.Foreground;
            Text = "Shortcut list";   // not drawn; the window's name for accessibility tools
            AccessibleRole = AccessibleRole.List;
            AccessibleName = "Shortcut list";
        }

        public int ItemCount { get { return _items.Count; } }

        // keepScroll: same view refreshed (e.g. a star toggled) - stay where we were.
        public void SetItems(IList<ListItem> items, string emptyText, bool keepScroll)
        {
            int scroll = _scroll;
            _items = items ?? new List<ListItem>();
            _emptyText = emptyText ?? "";
            _tops = new int[_items.Count + 1];
            int y = 0;
            for (int i = 0; i < _items.Count; i++)
            {
                _tops[i] = y;
                y += _items[i].Header != null ? Ui.S(HeaderHeight) : Ui.S(RowHeight);
            }
            _tops[_items.Count] = y;
            _hover = -1;
            _pressed = -1;
            _contentHeight = y;
            _scroll = Clamp(keepScroll ? scroll : 0);
            UpdateHover();
            Invalidate();
            if (IsHandleCreated) AccessibilityNotifyClients(AccessibleEvents.Reorder, -1);
        }

        public void ScrollToTop()
        {
            ScrollTo(0);
        }

        // ---- Scrolling ----

        private int MaxScroll { get { return Math.Max(0, _contentHeight - ClientSize.Height); } }

        private int Clamp(int scroll) { return Math.Max(0, Math.Min(MaxScroll, scroll)); }

        private void ScrollTo(int scroll)
        {
            scroll = Clamp(scroll);
            if (scroll == _scroll) return;
            _scroll = scroll;
            UpdateHover();
            Invalidate();
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            _scroll = Clamp(_scroll);
        }

        private bool Overflows { get { return _contentHeight > ClientSize.Height; } }

        private bool InBarZone(Point p)
        {
            return Overflows && ClientRectangle.Contains(p) && p.X >= ClientSize.Width - Ui.S(BarZone);
        }

        // Thumb position; the height is proportional to how much of the list is visible.
        private void ThumbSpan(out int top, out int height, out int track)
        {
            int pad = Ui.S(4);
            track = Math.Max(1, ClientSize.Height - 2 * pad);
            height = Math.Min(track, Math.Max(Ui.S(24), (int)((long)track * ClientSize.Height / Math.Max(1, _contentHeight))));
            int max = MaxScroll;
            top = pad + (max == 0 ? 0 : (int)((long)(track - height) * _scroll / max));
        }

        private Rectangle ThumbBounds()
        {
            if (!Overflows) return Rectangle.Empty;
            int top, height, track;
            ThumbSpan(out top, out height, out track);
            int w = _barHover || _dragFrom >= 0 ? Ui.S(6) : Ui.S(2);
            return new Rectangle(ClientSize.Width - Ui.S(4) - w, top, w, height);
        }

        // ---- Geometry ----

        private int Offset { get { return -_scroll; } }   // <= 0

        private Rectangle ItemBounds(int i)
        {
            return new Rectangle(Ui.S(SidePad), _tops[i] + Offset, ClientSize.Width - 2 * Ui.S(SidePad), _tops[i + 1] - _tops[i]);
        }

        private int IndexAt(Point p)
        {
            int y = p.Y - Offset;
            if (p.X < 0 || p.X >= ClientSize.Width || y < 0) return -1;
            for (int i = 0; i < _items.Count; i++)
                if (y < _tops[i + 1]) return i;
            return -1;
        }

        // Where a row's keycaps (or gray target) start, measured from the right.
        private int KeysLeft(Entry e, Rectangle r)
        {
            int right = r.Right - Ui.S(10);
            if (e.Keycaps != null)
            {
                for (int i = e.Keycaps.Length - 1; i >= 0; i--)
                    right -= KeycapWidth(e.Keycaps[i]) + Ui.S(4);
                return right + Ui.S(4);
            }
            if (e.Target != null)
                return right - Math.Min((int)(r.Width * 0.45), TextRenderer.MeasureText(e.Target, _smallFont).Width);
            return right;
        }

        private Rectangle StarBounds(Entry e, Rectangle r)
        {
            int box = Ui.S(StarBox);
            return new Rectangle(KeysLeft(e, r) - Ui.S(6) - box, r.Y + (r.Height - box) / 2, box, box);
        }

        private const TextFormatFlags KeyFlags = TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter |
            TextFormatFlags.SingleLine | TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix;

        private int KeycapWidth(string key)
        {
            int h = Ui.S(22);
            return Math.Max(h, TextRenderer.MeasureText(key, _smallFont, Size.Empty, KeyFlags).Width + Ui.S(14));
        }

        // ---- Mouse ----

        private void UpdateHover()
        {
            Point p = PointToClient(Cursor.Position);
            bool bar = _dragFrom >= 0 || InBarZone(p);
            if (bar != _barHover)
            {
                _barHover = bar;
                Invalidate();
            }
            int index = ClientRectangle.Contains(p) && !bar ? IndexAt(p) : -1;
            Entry entry = index >= 0 ? _items[index].Entry : null;
            if (entry == null) index = -1;
            bool star = entry != null && StarBounds(entry, ItemBounds(index)).Contains(p);

            if (index != _hover)
            {
                _tip.Hide(this);
                _tip.SetToolTip(this, entry != null ? TipFor(entry) : null);
            }
            if (index != _hover || star != _hoverStar)
            {
                _hover = index;
                _hoverStar = star;
                Invalidate();
            }
            Cursor = entry != null && (star || entry.Runnable) ? Cursors.Hand : Cursors.Default;
        }

        private static string TipFor(Entry e)
        {
            List<string> lines = new List<string>();
            if (e.Also != null) lines.Add("Also: " + e.Also);
            if (e.Note != null) lines.Add(e.Note);
            return lines.Count == 0 ? null : string.Join("\n", lines);
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            if (_dragFrom >= 0)
            {
                int top, height, track;
                ThumbSpan(out top, out height, out track);
                int room = Math.Max(1, track - height);
                ScrollTo(_dragScroll + (int)((long)(e.Y - _dragFrom) * MaxScroll / room));
                return;
            }
            UpdateHover();
        }

        protected override void OnMouseLeave(EventArgs e) { base.OnMouseLeave(e); _pressed = -1; UpdateHover(); }

        protected override void OnMouseWheel(MouseEventArgs e)
        {
            base.OnMouseWheel(e);
            int lines = SystemInformation.MouseWheelScrollLines;
            int step = lines < 0
                ? e.Delta * ClientSize.Height / 120                    // "one screen at a time"
                : e.Delta * Math.Max(1, lines) * Ui.S(WheelLine) / 120;
            ScrollTo(_scroll - step);
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (e.Button != MouseButtons.Left) return;
            if (InBarZone(e.Location))
            {
                Rectangle thumb = ThumbBounds();
                if (e.Y >= thumb.Top && e.Y < thumb.Bottom)
                {
                    _dragFrom = e.Y;   // the control keeps the mouse until it's released
                    _dragScroll = _scroll;
                    Invalidate();
                }
                else
                {
                    int page = Math.Max(Ui.S(RowHeight), ClientSize.Height - Ui.S(RowHeight));
                    ScrollTo(_scroll + (e.Y < thumb.Top ? -page : page));
                }
                return;
            }
            _pressed = _hover;
            Invalidate();
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            if (_dragFrom >= 0)
            {
                _dragFrom = -1;
                UpdateHover();
                Invalidate();
                return;
            }
            if (e.Button == MouseButtons.Left && InBarZone(e.Location)) return;
            int index = IndexAt(e.Location);
            Entry entry = index >= 0 ? _items[index].Entry : null;
            if (e.Button == MouseButtons.Left)
            {
                bool same = index == _pressed;
                _pressed = -1;
                Invalidate();
                if (entry == null || !same) return;
                if (StarBounds(entry, ItemBounds(index)).Contains(e.Location)) Raise(StarClicked, entry);
                else if (entry.Runnable) Raise(EntryClicked, entry);
            }
            else if (e.Button == MouseButtons.Right && entry != null)
            {
                Action<Entry, Point> handler = EntryContextMenu;
                if (handler != null) handler(entry, PointToScreen(e.Location));
            }
        }

        private static void Raise(Action<Entry> handler, Entry entry)
        {
            if (handler != null) handler(entry);
        }

        // Keyboard / screen reader "click" on item i.
        internal void Activate(int i)
        {
            if (i < 0 || i >= _items.Count) return;
            Entry entry = _items[i].Entry;
            if (entry != null && entry.Runnable) Raise(EntryClicked, entry);
        }

        // ---- Painting ----

        protected override void OnPaint(PaintEventArgs e)
        {
            Rectangle clip = e.ClipRectangle;
            Glass.Paint(e, _theme, delegate(Graphics g)
            {
                if (_items.Count == 0)
                {
                    Rectangle r = new Rectangle(Ui.S(16), Ui.S(16), ClientSize.Width - Ui.S(32), Ui.S(60));
                    Glass.Text(g, _theme, _emptyText, Font, r, _theme.Subtle,
                        TextFormatFlags.HorizontalCenter | TextFormatFlags.Top | TextFormatFlags.WordBreak | TextFormatFlags.NoPrefix);
                    return;
                }
                for (int i = 0; i < _items.Count; i++)
                {
                    Rectangle r = ItemBounds(i);
                    if (r.Bottom < clip.Top) continue;
                    if (r.Top > clip.Bottom) break;
                    if (_items[i].Header != null) PaintHeader(g, _items[i].Header, r, i > 0);
                    else PaintRow(g, _items[i].Entry, r, i);
                }
                PaintScrollbar(g);
            });
        }

        private void PaintScrollbar(Graphics g)
        {
            Rectangle thumb = ThumbBounds();
            if (thumb.IsEmpty) return;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            bool wide = _barHover || _dragFrom >= 0;
            if (wide)
            {
                Rectangle track = new Rectangle(thumb.X - Ui.S(2), Ui.S(2), thumb.Width + Ui.S(4), ClientSize.Height - Ui.S(4));
                using (GraphicsPath path = Ui.RoundedRect(track, track.Width / 2))
                using (SolidBrush brush = new SolidBrush(_theme.Hover))
                    g.FillPath(brush, path);
            }
            Color c = _theme.Subtle;
            using (GraphicsPath path = Ui.RoundedRect(thumb, Math.Max(1, thumb.Width / 2)))
            using (SolidBrush brush = new SolidBrush(Color.FromArgb(wide ? c.A : c.A * 2 / 3, c.R, c.G, c.B)))
                g.FillPath(brush, path);
        }

        // A section title; from the second section on, a thin line above it closes
        // the previous one (with "All sections" they would otherwise run together).
        private void PaintHeader(Graphics g, string text, Rectangle r, bool divider)
        {
            if (divider)
                using (SolidBrush line = new SolidBrush(_theme.Divider))
                    g.FillRectangle(line, r.X + Ui.S(10), r.Y + Ui.S(4), r.Width - Ui.S(20), Math.Max(1, Ui.S(1)));
            Rectangle tr = new Rectangle(r.X + Ui.S(10), r.Y + Ui.S(8), r.Width - Ui.S(20), r.Height - Ui.S(8));
            Glass.Text(g, _theme, text, _headerFont, tr, _theme.Subtle,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine | TextFormatFlags.NoPrefix);
        }

        private void PaintRow(Graphics g, Entry entry, Rectangle r, int index)
        {
            bool hover = index == _hover;
            if (hover)
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                bool pressed = _pressed == index && entry.Runnable && !_hoverStar;
                Rectangle bg = new Rectangle(r.X, r.Y + Ui.S(2), r.Width - 1, r.Height - Ui.S(4) - 1);
                using (GraphicsPath path = Ui.RoundedRect(bg, Ui.S(5)))
                using (SolidBrush brush = new SolidBrush(pressed ? _theme.Pressed : _theme.Hover))
                    g.FillPath(brush, path);
            }

            int keysLeft = KeysLeft(entry, r);
            if (entry.Keycaps != null) DrawKeys(g, entry, r);
            else if (entry.Target != null)
            {
                Rectangle tr = new Rectangle(keysLeft, r.Y, r.Right - Ui.S(10) - keysLeft, r.Height);
                Glass.Text(g, _theme, entry.Target, _smallFont, tr, _theme.Subtle,
                    TextFormatFlags.Right | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine |
                    TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);
            }

            bool favorite = IsFavorite != null && IsFavorite(entry);
            Rectangle star = StarBounds(entry, r);
            if (hover || favorite)
            {
                if (hover && _hoverStar)
                {
                    g.SmoothingMode = SmoothingMode.AntiAlias;
                    using (GraphicsPath path = Ui.RoundedRect(new Rectangle(star.X, star.Y, star.Width - 1, star.Height - 1), Ui.S(4)))
                    using (SolidBrush brush = new SolidBrush(_theme.Pressed))
                        g.FillPath(brush, path);
                }
                Glass.Text(g, _theme, favorite ? Ui.GlyphStarFilled : Ui.GlyphStar, _starFont, star,
                    favorite ? _theme.Accent : _theme.Subtle, KeyFlags);
            }

            int nameRight = (hover || favorite ? star.Left : keysLeft) - Ui.S(6);
            Rectangle nameRect = new Rectangle(r.X + Ui.S(10), r.Y, Math.Max(0, nameRight - r.X - Ui.S(10)), r.Height);
            Glass.Text(g, _theme, entry.Name, Font, nameRect, ForeColor,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine |
                TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);
        }

        // Keycaps from right to left. Reference-only entries get dimmer text.
        private void DrawKeys(Graphics g, Entry entry, Rectangle r)
        {
            int h = Ui.S(22);
            int y = r.Y + (r.Height - h) / 2;
            int x = r.Right - Ui.S(10);
            Color text = entry.Runnable ? ForeColor : _theme.Subtle;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            for (int i = entry.Keycaps.Length - 1; i >= 0; i--)
            {
                int w = KeycapWidth(entry.Keycaps[i]);
                Rectangle key = new Rectangle(x - w, y, w, h);
                using (GraphicsPath path = Ui.RoundedRect(new Rectangle(key.X, key.Y, key.Width - 1, key.Height - 1), Ui.S(4)))
                using (SolidBrush fill = new SolidBrush(_theme.KeyFill))
                using (Pen border = new Pen(_theme.KeyBorder))
                {
                    g.FillPath(fill, path);
                    g.DrawPath(border, path);
                }
                Glass.Text(g, _theme, entry.Keycaps[i], _smallFont, key, text, KeyFlags);
                x = key.Left - Ui.S(4);
            }
        }

        // ---- Accessibility: each item is a child, so screen readers (and the UI
        // tests) can find rows by name, see where they are and click them. ----

        protected override AccessibleObject CreateAccessibilityInstance()
        {
            return new ListAccessible(this);
        }

        private sealed class ListAccessible : ControlAccessibleObject
        {
            private readonly ShortcutList _list;
            public ListAccessible(ShortcutList list) : base(list) { _list = list; }
            public override int GetChildCount() { return _list._items.Count; }
            public override AccessibleObject GetChild(int index)
            {
                return index >= 0 && index < _list._items.Count ? new ItemAccessible(_list, this, index) : null;
            }
        }

        private sealed class ItemAccessible : AccessibleObject
        {
            private readonly ShortcutList _list;
            private readonly AccessibleObject _parent;
            private readonly int _index;

            public ItemAccessible(ShortcutList list, AccessibleObject parent, int index)
            {
                _list = list;
                _parent = parent;
                _index = index;
            }

            private ListItem Item { get { return _list._items[_index]; } }
            public override string Name { get { return Item.Header ?? Item.Entry.Name; } set { } }
            public override AccessibleRole Role { get { return Item.Header != null ? AccessibleRole.StaticText : AccessibleRole.ListItem; } }
            public override AccessibleObject Parent { get { return _parent; } }
            public override string DefaultAction { get { return Item.Entry != null && Item.Entry.Runnable ? "Run" : null; } }
            public override void DoDefaultAction() { _list.Activate(_index); }

            public override Rectangle Bounds
            {
                get { return _list.RectangleToScreen(Rectangle.Intersect(_list.ItemBounds(_index), _list.ClientRectangle)); }
            }

            public override AccessibleStates State
            {
                get
                {
                    Rectangle r = _list.ItemBounds(_index);
                    bool visible = r.Bottom > 0 && r.Top < _list.ClientSize.Height;
                    return visible ? AccessibleStates.None : AccessibleStates.Offscreen | AccessibleStates.Invisible;
                }
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _tip.Dispose();
                _smallFont.Dispose();
                _headerFont.Dispose();
                _starFont.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
