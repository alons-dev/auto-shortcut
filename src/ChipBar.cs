using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace AutoShortcut
{
    // A row of small rounded "chips" (filter pills) that scrolls sideways when they
    // don't fit: with the mouse wheel or the arrow buttons at the edges.
    internal sealed class ChipBar : Control
    {
        private readonly Panel _viewport;   // clips the chips
        private readonly Panel _strip;      // holds the chips; moved left/right to scroll
        private readonly Chip[] _chips;
        private readonly IconButton _left;
        private readonly IconButton _right;
        private int _selected;

        public event Action<int> SelectedChanged;   // only when the user picks a chip

        public ChipBar(Theme theme, string[] labels)
        {
            SetStyle(ControlStyles.Selectable, false);
            BackColor = theme.Background;
            Height = Ui.S(40);

            _viewport = new Panel();
            _viewport.BackColor = theme.Background;
            _strip = new Panel();
            _strip.BackColor = theme.Background;

            _chips = new Chip[labels.Length];
            int x = 0;
            for (int i = 0; i < labels.Length; i++)
            {
                Chip chip = new Chip(theme, labels[i]);
                int index = i;
                chip.Click += delegate
                {
                    if (index == _selected) return;
                    Selected = index;
                    Action<int> handler = SelectedChanged;
                    if (handler != null) handler(index);
                };
                chip.Location = new Point(x, (Height - chip.Height) / 2);
                x += chip.Width + Ui.S(6);
                _chips[i] = chip;
                _strip.Controls.Add(chip);
            }
            _strip.Size = new Size(Math.Max(0, x - Ui.S(6)), Height);
            _viewport.Controls.Add(_strip);

            _left = new IconButton(Ui.GlyphLeft, "Scroll left", theme);
            _right = new IconButton(Ui.GlyphRight, "Scroll right", theme);
            _left.Size = _right.Size = new Size(Ui.S(28), Ui.S(28));
            _left.Click += delegate { ScrollBy(-Ui.S(160)); };
            _right.Click += delegate { ScrollBy(Ui.S(160)); };

            Controls.Add(_viewport);
            Controls.Add(_left);
            Controls.Add(_right);
            Selected = 0;
        }

        public int Selected
        {
            get { return _selected; }
            set
            {
                _selected = Math.Max(0, Math.Min(_chips.Length - 1, value));
                for (int i = 0; i < _chips.Length; i++) _chips[i].Active = i == _selected;
                ScrollIntoView(_chips[_selected]);
            }
        }

        private bool Overflows { get { return _strip.Width > Width - Ui.S(20); } }

        protected override void OnLayout(LayoutEventArgs e)
        {
            base.OnLayout(e);
            if (_viewport == null) return;   // called from the constructor too
            int pad = Ui.S(10);
            int y = (Height - _left.Height) / 2;
            if (Overflows)
            {
                _left.Location = new Point(Ui.S(4), y);
                _right.Location = new Point(Width - Ui.S(4) - _right.Width, y);
                _viewport.Bounds = new Rectangle(_left.Right + Ui.S(2), 0, _right.Left - _left.Right - Ui.S(4), Height);
            }
            else
            {
                _viewport.Bounds = new Rectangle(pad, 0, Math.Max(0, Width - 2 * pad), Height);
            }
            _strip.Left = Clamp(_strip.Left);
            UpdateArrows();
        }

        private int Clamp(int left)
        {
            int min = Math.Min(0, _viewport.Width - _strip.Width);
            return Math.Max(min, Math.Min(0, left));
        }

        private void ScrollBy(int delta)
        {
            _strip.Left = Clamp(_strip.Left - delta);
            UpdateArrows();
        }

        private void ScrollIntoView(Chip chip)
        {
            if (_viewport.Width == 0) return;
            int left = chip.Left + _strip.Left;
            if (left < 0) ScrollBy(left - Ui.S(8));
            else if (left + chip.Width > _viewport.Width) ScrollBy(left + chip.Width - _viewport.Width + Ui.S(8));
        }

        private void UpdateArrows()
        {
            bool overflow = Overflows;
            _left.Visible = overflow && _strip.Left < 0;
            _right.Visible = overflow && _strip.Left > _viewport.Width - _strip.Width;
        }

        // The wheel scrolls the chips sideways (WinForms sends it to the control under
        // the mouse's parent chain: handle it here and in each chip).
        protected override void OnMouseWheel(MouseEventArgs e)
        {
            base.OnMouseWheel(e);
            ScrollBy(-e.Delta / 2);
        }

        internal void Wheel(int delta) { ScrollBy(-delta / 2); }

        private sealed class Chip : Control
        {
            private readonly Theme _theme;
            private bool _active;
            private bool _hover;

            public Chip(Theme theme, string label)
            {
                _theme = theme;
                SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                         ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw |
                         ControlStyles.SupportsTransparentBackColor, true);
                SetStyle(ControlStyles.Selectable, false);
                Font = new Font("Segoe UI", 9F);
                Text = label;
                AccessibleName = label;
                AccessibleRole = AccessibleRole.RadioButton;
                BackColor = theme.Background;
                Cursor = Cursors.Hand;
                Size = new Size(TextRenderer.MeasureText(label, Font).Width + Ui.S(24), Ui.S(28));
            }

            public bool Active { set { _active = value; Invalidate(); } }

            protected override void OnMouseEnter(EventArgs e) { base.OnMouseEnter(e); _hover = true; Invalidate(); }
            protected override void OnMouseLeave(EventArgs e) { base.OnMouseLeave(e); _hover = false; Invalidate(); }

            protected override void OnMouseWheel(MouseEventArgs e)
            {
                base.OnMouseWheel(e);
                ChipBar bar = Parent != null && Parent.Parent != null ? Parent.Parent.Parent as ChipBar : null;
                if (bar != null) bar.Wheel(e.Delta);
            }

            protected override void OnPaint(PaintEventArgs e)
            {
                Graphics g = e.Graphics;
                g.SmoothingMode = SmoothingMode.AntiAlias;
                Rectangle r = new Rectangle(0, 0, Width - 1, Height - 1);
                Color fill = _active ? _theme.Accent : (_hover ? _theme.Hover : _theme.KeyFill);
                using (GraphicsPath path = Ui.RoundedRect(r, Height / 2))
                using (SolidBrush brush = new SolidBrush(fill))
                {
                    g.FillPath(brush, path);
                    if (!_active)
                        using (Pen border = new Pen(_theme.KeyBorder))
                            g.DrawPath(border, path);
                }
                TextRenderer.DrawText(g, Text, Font, ClientRectangle, _active ? _theme.AccentText : _theme.Foreground,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine | TextFormatFlags.NoPrefix);
            }
        }
    }
}
