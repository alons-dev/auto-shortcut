using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace AutoShortcut
{
    // Square button with an icon from the Windows icon font (see Ui.IconFont) and a
    // rounded background on hover, like the title bar buttons in Windows 11.
    internal sealed class IconButton : Control
    {
        private readonly Theme _theme;
        private readonly string _glyph;
        private bool _hover;
        private bool _pressed;

        public IconButton(string glyph, string name, Theme theme)
        {
            _glyph = glyph;
            _theme = theme;
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            SetStyle(ControlStyles.Selectable, false);
            Size = new Size(Ui.S(32), Ui.S(32));
            Cursor = Cursors.Hand;
            BackColor = theme.Background;
            Text = name;              // not drawn, but it's the name screen readers announce
            AccessibleName = name;
            AccessibleRole = AccessibleRole.PushButton;
        }

        protected override void OnMouseEnter(EventArgs e) { base.OnMouseEnter(e); _hover = true; Invalidate(); }
        protected override void OnMouseLeave(EventArgs e) { base.OnMouseLeave(e); _hover = false; _pressed = false; Invalidate(); }
        protected override void OnMouseDown(MouseEventArgs e) { base.OnMouseDown(e); if (e.Button == MouseButtons.Left) { _pressed = true; Invalidate(); } }
        protected override void OnMouseUp(MouseEventArgs e) { base.OnMouseUp(e); _pressed = false; Invalidate(); }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            if (_hover)
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                Rectangle bg = new Rectangle(0, 0, Width - 1, Height - 1);
                using (GraphicsPath path = Ui.RoundedRect(bg, Ui.S(5)))
                using (SolidBrush brush = new SolidBrush(_pressed ? _theme.Pressed : _theme.Hover))
                    g.FillPath(brush, path);
            }
            TextRenderer.DrawText(g, _glyph, Ui.IconFont, ClientRectangle, _theme.Foreground,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
        }
    }
}
