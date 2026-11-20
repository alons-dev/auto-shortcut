using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Windows.Forms;

namespace AutoShortcut
{
    // Una fila del pop-up, dibujada a mano: nombre a la izquierda y, a la derecha,
    // las teclas como "keycaps" (atajos de teclas) o el destino en gris (atajos de abrir).
    // Toda la fila es clicable.
    internal sealed class ShortcutRow : Control
    {
        private readonly Shortcut _shortcut;
        private readonly Theme _theme;
        private readonly string[] _keys;      // null si es de abrir
        private readonly string _target;      // texto gris para los de abrir
        private readonly Font _smallFont = new Font("Segoe UI", 8.25F);
        private bool _hover;
        private bool _pressed;

        public ShortcutRow(Shortcut shortcut, int index, Theme theme)
        {
            _shortcut = shortcut;
            Index = index;
            _theme = theme;

            if (shortcut.IsLaunch)
            {
                _target = DescribeTarget(shortcut.Run);
            }
            else
            {
                KeyCombo combo;
                if (KeyCombo.TryParse(shortcut.KeysText, out combo)) _keys = combo.DisplayParts();
                else _target = "combinación no válida";
            }

            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            SetStyle(ControlStyles.Selectable, false);
            Cursor = Cursors.Hand;
            BackColor = theme.Background;
            ForeColor = theme.Foreground;
            Text = shortcut.Name;
            AccessibleName = shortcut.Name;
            AccessibleRole = AccessibleRole.PushButton;
        }

        public Shortcut Shortcut { get { return _shortcut; } }

        // Posición en la lista de la configuración (para editar, mover o borrar).
        public readonly int Index;

        // "C:\x\y\app.exe" -> "app.exe", "https://www.github.com/x" -> "github.com".
        private static string DescribeTarget(string run)
        {
            Uri uri;
            if (Uri.TryCreate(run, UriKind.Absolute, out uri) && (uri.Scheme == "http" || uri.Scheme == "https"))
            {
                string host = uri.Host;
                return host.StartsWith("www.") ? host.Substring(4) : host;
            }
            if (run.IndexOf('\\') >= 0 || run.IndexOf('/') >= 0)
            {
                try
                {
                    string name = Path.GetFileName(run.TrimEnd('\\', '/'));
                    if (name.Length > 0) return name;
                }
                catch (ArgumentException) { }
            }
            return run;
        }

        protected override void OnMouseEnter(EventArgs e) { base.OnMouseEnter(e); _hover = true; Invalidate(); }
        protected override void OnMouseLeave(EventArgs e) { base.OnMouseLeave(e); _hover = false; _pressed = false; Invalidate(); }
        protected override void OnMouseDown(MouseEventArgs e) { base.OnMouseDown(e); if (e.Button == MouseButtons.Left) { _pressed = true; Invalidate(); } }
        protected override void OnMouseUp(MouseEventArgs e) { base.OnMouseUp(e); _pressed = false; Invalidate(); }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            Rectangle r = ClientRectangle;
            int pad = Ui.S(10);

            if (_hover)
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                Rectangle bg = new Rectangle(r.X, r.Y + Ui.S(2), r.Width - 1, r.Height - Ui.S(4) - 1);
                using (GraphicsPath path = Ui.RoundedRect(bg, Ui.S(5)))
                using (SolidBrush brush = new SolidBrush(_pressed ? _theme.Pressed : _theme.Hover))
                    g.FillPath(brush, path);
            }

            int right = r.Right - pad;
            if (_keys != null)
            {
                right = DrawKeys(g, right, r) - Ui.S(10);
            }
            else if (_target != null)
            {
                const TextFormatFlags flags = TextFormatFlags.Right | TextFormatFlags.VerticalCenter |
                    TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix;
                int max = (int)(r.Width * 0.45);
                int w = Math.Min(max, TextRenderer.MeasureText(g, _target, _smallFont).Width);
                Rectangle tr = new Rectangle(right - w, r.Y, w, r.Height);
                TextRenderer.DrawText(g, _target, _smallFont, tr, _theme.Subtle, flags);
                right = tr.Left - Ui.S(10);
            }

            Rectangle nameRect = new Rectangle(r.X + pad, r.Y, Math.Max(0, right - r.X - pad), r.Height);
            TextRenderer.DrawText(g, Text, Font, nameRect, ForeColor,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine |
                TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);
        }

        // Dibuja las teclas de derecha a izquierda; devuelve dónde ha terminado.
        private int DrawKeys(Graphics g, int right, Rectangle r)
        {
            const TextFormatFlags flags = TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter |
                TextFormatFlags.SingleLine | TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix;
            int h = Ui.S(22);
            int y = r.Y + (r.Height - h) / 2;
            int x = right;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            for (int i = _keys.Length - 1; i >= 0; i--)
            {
                Size text = TextRenderer.MeasureText(g, _keys[i], _smallFont, Size.Empty, flags);
                int w = Math.Max(h, text.Width + Ui.S(14));
                Rectangle key = new Rectangle(x - w, y, w, h);
                Rectangle edge = new Rectangle(key.X, key.Y, key.Width - 1, key.Height - 1);

                using (GraphicsPath path = Ui.RoundedRect(edge, Ui.S(4)))
                using (SolidBrush fill = new SolidBrush(_theme.KeyFill))
                using (Pen border = new Pen(_theme.KeyBorder))
                {
                    g.FillPath(fill, path);
                    g.DrawPath(border, path);
                }
                TextRenderer.DrawText(g, _keys[i], _smallFont, key, ForeColor, flags);
                x = key.Left - Ui.S(4);
            }
            return x;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) _smallFont.Dispose();
            base.Dispose(disposing);
        }
    }
}
