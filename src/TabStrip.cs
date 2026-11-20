using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace AutoShortcut
{
    // Row of text tabs like the ones in Windows 11 Settings or Photos: the active
    // one in normal text with a short accent line under it, the rest dimmer.
    internal sealed class TabStrip : Control
    {
        private readonly Theme _theme;
        private readonly TabButton[] _tabs;
        private int _selected;

        public event Action<int> SelectedChanged;   // only when the user picks a tab

        public TabStrip(Theme theme, string[] labels)
        {
            _theme = theme;
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.Opaque, true);
            SetStyle(ControlStyles.Selectable, false);
            BackColor = theme.Background;
            Height = Ui.S(36);
            AccessibleRole = AccessibleRole.PageTabList;

            _tabs = new TabButton[labels.Length];
            int x = Ui.S(10);
            for (int i = 0; i < labels.Length; i++)
            {
                TabButton tab = new TabButton(theme, labels[i]);
                int index = i;
                tab.Click += delegate
                {
                    if (index == _selected) return;
                    Selected = index;
                    Action<int> handler = SelectedChanged;
                    if (handler != null) handler(index);
                };
                tab.Location = new Point(x, 0);
                tab.Height = Height;
                x += tab.Width + Ui.S(2);
                _tabs[i] = tab;
                Controls.Add(tab);
            }
            Selected = 0;
        }

        public int Selected
        {
            get { return _selected; }
            set
            {
                _selected = Math.Max(0, Math.Min(_tabs.Length - 1, value));
                for (int i = 0; i < _tabs.Length; i++) _tabs[i].Active = i == _selected;
            }
        }

        // Dims the tabs while the search box is open (the search ignores tabs).
        public bool Dimmed
        {
            set { foreach (TabButton t in _tabs) t.Dimmed = value; }
        }

        protected override void OnPaint(PaintEventArgs e) { Glass.Paint(e, _theme, null); }

        private sealed class TabButton : Control
        {
            private readonly Theme _theme;
            private bool _active;
            private bool _dimmed;
            private bool _hover;

            public TabButton(Theme theme, string label)
            {
                _theme = theme;
                SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                         ControlStyles.Opaque | ControlStyles.ResizeRedraw, true);
                SetStyle(ControlStyles.Selectable, false);
                Font = new Font("Segoe UI", 9.75F);
                Text = label;
                AccessibleName = label;
                AccessibleRole = AccessibleRole.PageTab;
                BackColor = theme.Background;
                Cursor = Cursors.Hand;
                Width = TextRenderer.MeasureText(label, Font).Width + Ui.S(16);
            }

            public bool Active { set { _active = value; Invalidate(); } }
            public bool Dimmed { set { _dimmed = value; Invalidate(); } }

            protected override void OnMouseEnter(EventArgs e) { base.OnMouseEnter(e); _hover = true; Invalidate(); }
            protected override void OnMouseLeave(EventArgs e) { base.OnMouseLeave(e); _hover = false; Invalidate(); }

            protected override void OnPaint(PaintEventArgs e)
            {
                Glass.Paint(e, _theme, delegate(Graphics g)
                {
                    g.SmoothingMode = SmoothingMode.AntiAlias;
                    if (_hover)
                    {
                        Rectangle bg = new Rectangle(0, Ui.S(4), Width - 1, Height - Ui.S(8) - 1);
                        using (GraphicsPath path = Ui.RoundedRect(bg, Ui.S(5)))
                        using (SolidBrush brush = new SolidBrush(_theme.Hover))
                            g.FillPath(brush, path);
                    }
                    Color color = _active && !_dimmed ? _theme.Foreground : _theme.Subtle;
                    Glass.Text(g, _theme, Text, Font, new Rectangle(0, 0, Width, Height - Ui.S(4)), color,
                        TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine | TextFormatFlags.NoPrefix);
                    if (_active && !_dimmed)
                    {
                        int w = Ui.S(16), h = Ui.S(3);
                        Rectangle line = new Rectangle((Width - w) / 2, Height - h - Ui.S(3), w, h);
                        using (GraphicsPath path = Ui.RoundedRect(line, h / 2 + 1))
                        using (SolidBrush brush = new SolidBrush(_theme.Accent))
                            g.FillPath(brush, path);
                    }
                });
            }
        }
    }
}
