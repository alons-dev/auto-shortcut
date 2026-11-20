using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace AutoShortcut
{
    // Windows 11-style text box: 32 px tall (like buttons), rounded border and a
    // colored line at the bottom when focused. Inside it's a regular borderless
    // TextBox, vertically centered.
    internal sealed class InputBox : Panel
    {
        private readonly Theme _theme;
        private readonly GlassTextBox _box;

        public InputBox(Theme theme, int width, string accessibleName)
        {
            _theme = theme;
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.Opaque | ControlStyles.ResizeRedraw, true);
            BackColor = theme.Background;
            Size = new Size(width, Ui.S(32));
            Cursor = Cursors.IBeam;

            _box = new GlassTextBox(theme);
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
            if (_box == null) return;   // WinForms calls OnLayout from the constructor, before _box exists
            int pad = Ui.S(10);
            _box.Width = Math.Max(0, Width - pad * 2);
            _box.Location = new Point(pad, (Height - _box.Height) / 2);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Glass.Paint(e, _theme, delegate(Graphics g)
            {
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
                    // Accent line at the bottom, inside the rounded corners.
                    g.SmoothingMode = SmoothingMode.None;
                    using (SolidBrush accent = new SolidBrush(_theme.Accent))
                        g.FillRectangle(accent, Ui.S(3), Height - Ui.S(2), Width - Ui.S(6), Ui.S(2));
                }
            });
        }
    }

    // A TextBox that can sit on acrylic (Theme.Glass). The edit control draws with
    // GDI, which leaves every pixel's alpha at 0, so on acrylic its text would be
    // see-through. Here it paints into an off-screen buffer (WM_PRINTCLIENT), the
    // buffer is made fully opaque and copied to the window. The edit control also
    // draws by itself, outside WM_PAINT, when typing or selecting: after those
    // messages it is repainted the right way.
    internal sealed class GlassTextBox : TextBox
    {
        [StructLayout(LayoutKind.Sequential)]
        private struct PAINTSTRUCT
        {
            public IntPtr hdc;
            public int fErase, left, top, right, bottom, fRestore, fIncUpdate;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 32)] public byte[] reserved;
        }

        [DllImport("user32.dll")]
        private static extern IntPtr BeginPaint(IntPtr hwnd, out PAINTSTRUCT ps);
        [DllImport("user32.dll")]
        private static extern bool EndPaint(IntPtr hwnd, ref PAINTSTRUCT ps);
        [DllImport("user32.dll")]
        private static extern IntPtr SendMessage(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam);

        private const int WM_PAINT = 0x000F;
        private const int WM_PRINTCLIENT = 0x0318;
        private const int PRF_CLIENT = 0x4;
        private const int PRF_ERASEBKGND = 0x8;

        private readonly Theme _theme;

        public GlassTextBox(Theme theme) { _theme = theme; }

        protected override void WndProc(ref Message m)
        {
            if (!_theme.Glass)
            {
                base.WndProc(ref m);
                return;
            }
            if (m.Msg == WM_PAINT)
            {
                PAINTSTRUCT ps;
                IntPtr hdc = BeginPaint(Handle, out ps);
                try { PaintOpaque(hdc); }
                finally { EndPaint(Handle, ref ps); }
                return;
            }
            base.WndProc(ref m);
            if (DrawsByItself(m.Msg)) Invalidate();
        }

        // Messages after which the edit control has drawn straight on the window.
        private bool DrawsByItself(int msg)
        {
            switch (msg)
            {
                case 0x0007: // WM_SETFOCUS
                case 0x0008: // WM_KILLFOCUS
                case 0x000C: // WM_SETTEXT
                case 0x0100: // WM_KEYDOWN
                case 0x0102: // WM_CHAR
                case 0x010F: // WM_IME_COMPOSITION
                case 0x0201: // WM_LBUTTONDOWN
                case 0x0202: // WM_LBUTTONUP
                case 0x0203: // WM_LBUTTONDBLCLK
                case 0x0300: // WM_CUT
                case 0x0302: // WM_PASTE
                case 0x0303: // WM_CLEAR
                case 0x0304: // WM_UNDO
                case 0x00B1: // EM_SETSEL
                case 0x00C2: // EM_REPLACESEL
                case 0x00C7: // EM_UNDO
                    return true;
                case 0x0200: // WM_MOUSEMOVE: only while selecting with the mouse
                    return MouseButtons != MouseButtons.None;
                default:
                    return false;
            }
        }

        private void PaintOpaque(IntPtr hdc)
        {
            int w = ClientSize.Width, h = ClientSize.Height;
            if (w <= 0 || h <= 0) return;
            IntPtr bits;
            IntPtr dib = Glass.CreateDib(w, h, out bits);
            if (dib == IntPtr.Zero) return;
            try
            {
                // Let the edit control paint itself into the buffer...
                IntPtr mem = CreateCompatibleDC(hdc);
                IntPtr old = SelectObject(mem, dib);
                SendMessage(Handle, WM_PRINTCLIENT, mem, (IntPtr)(PRF_CLIENT | PRF_ERASEBKGND));
                SelectObject(mem, old);
                DeleteDC(mem);
                // ...then make every pixel solid.
                byte[] px = new byte[w * h * 4];
                Marshal.Copy(bits, px, 0, px.Length);
                for (int i = 3; i < px.Length; i += 4) px[i] = 255;
                Marshal.Copy(px, 0, bits, px.Length);
                Glass.Blit(hdc, dib, 0, 0, w, h);
            }
            finally { Glass.Free(dib); }
        }

        [DllImport("gdi32.dll")]
        private static extern IntPtr CreateCompatibleDC(IntPtr hdc);
        [DllImport("gdi32.dll")]
        private static extern IntPtr SelectObject(IntPtr hdc, IntPtr obj);
        [DllImport("gdi32.dll")]
        private static extern bool DeleteDC(IntPtr hdc);
    }
}
