using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace AutoShortcut
{
    // Cuadro de texto al estilo Windows 11: 32 px de alto (como los botones), borde
    // redondeado y una línea de color abajo cuando tiene el foco. Por dentro es un
    // TextBox normal sin borde, centrado verticalmente.
    internal sealed class InputBox : Panel
    {
        private readonly Theme _theme;
        private readonly TextBox _box;

        public InputBox(Theme theme, int width, string accessibleName)
        {
            _theme = theme;
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            BackColor = theme.Background;
            Size = new Size(width, Ui.S(32));
            Cursor = Cursors.IBeam;

            _box = new TextBox();
            _box.BorderStyle = BorderStyle.None;
            _box.BackColor = theme.InputBack;
            _box.ForeColor = theme.Foreground;
            _box.AccessibleName = accessibleName;
            _box.GotFocus += delegate { Invalidate(); };
            _box.LostFocus += delegate { Invalidate(); };
            Controls.Add(_box);

            Click += delegate { _box.Focus(); };
        }

        public TextBox Box { get { return _box; } }

        public override string Text
        {
            get { return _box == null ? "" : _box.Text; }
            set { if (_box != null) _box.Text = value; }
        }

        protected override void OnFontChanged(EventArgs e)
        {
            base.OnFontChanged(e);
            if (_box != null) _box.Font = Font;
            LayoutBox();
        }

        protected override void OnLayout(LayoutEventArgs e)
        {
            base.OnLayout(e);
            LayoutBox();
        }

        private void LayoutBox()
        {
            if (_box == null) return;   // WinForms llama a OnLayout desde el constructor, antes de crear _box
            int pad = Ui.S(10);
            _box.Width = Math.Max(0, Width - pad * 2);
            _box.Location = new Point(pad, (Height - _box.Height) / 2);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            Rectangle r = new Rectangle(0, 0, Width - 1, Height - 1);
            using (GraphicsPath path = Ui.RoundedRect(r, Ui.S(4)))
            using (SolidBrush fill = new SolidBrush(_theme.InputBack))
            using (Pen border = new Pen(_theme.Border))
            {
                g.FillPath(fill, path);
                g.DrawPath(border, path);
            }
            if (_box.Focused)
            {
                // Línea de acento abajo, dentro de las esquinas redondeadas.
                g.SmoothingMode = SmoothingMode.None;
                using (SolidBrush accent = new SolidBrush(_theme.Accent))
                    g.FillRectangle(accent, Ui.S(3), Height - Ui.S(2), Width - Ui.S(6), Ui.S(2));
            }
        }
    }
}
