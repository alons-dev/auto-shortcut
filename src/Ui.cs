using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace AutoShortcut
{
    // Drawing and styling helpers shared by every window.
    internal static class Ui
    {
        // The app is DPI aware (see Program.cs): we work in real pixels, so sizes
        // designed at 100 % are multiplied by the Windows display scale.
        public static readonly float Scale = ReadScale();

        private static float ReadScale()
        {
            using (Graphics g = Graphics.FromHwnd(IntPtr.Zero)) return g.DpiX / 96F;
        }

        // Ui.S(40) = 40 px at 100 %, 60 px at 150 %, 80 px at 200 %...
        public static int S(int value)
        {
            return (int)Math.Round(value * Scale);
        }

        public static GraphicsPath RoundedRect(Rectangle r, int radius)
        {
            int d = radius * 2;
            GraphicsPath path = new GraphicsPath();
            path.AddArc(r.X, r.Y, d, d, 180, 90);
            path.AddArc(r.Right - d, r.Y, d, d, 270, 90);
            path.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
            path.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
            path.CloseFigure();
            return path;
        }

        // ---- Window frame (DWM) ----

        // DWM (Desktop Window Manager) draws window frames. On Windows 11 we can ask
        // it for rounded corners and a dark title bar. On Windows 10 these attributes
        // don't exist: the call just fails.
        [DllImport("dwmapi.dll")]
        private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int size);

        private const int DWMWA_USE_IMMERSIVE_DARK_MODE = 20;
        private const int DWMWA_WINDOW_CORNER_PREFERENCE = 33;
        public const int CornerDefault = 0;
        public const int CornerRound = 2;
        public const int CornerRoundSmall = 3;

        public static void ApplyFrame(IntPtr hwnd, bool dark, int corner)
        {
            int value = dark ? 1 : 0;
            DwmSetWindowAttribute(hwnd, DWMWA_USE_IMMERSIVE_DARK_MODE, ref value, sizeof(int));
            if (corner != CornerDefault)
            {
                value = corner;
                DwmSetWindowAttribute(hwnd, DWMWA_WINDOW_CORNER_PREFERENCE, ref value, sizeof(int));
            }
        }

        // ---- Icons ----

        // Windows 11 icons are a font: "Segoe Fluent Icons" ("Segoe MDL2 Assets" on
        // Windows 10). Each icon is a character, e.g. U+E710 = "+".
        public static readonly Font IconFont = CreateIconFont();
        public static readonly string GlyphAdd = ((char)0xE710).ToString();
        public static readonly string GlyphSettings = ((char)0xE713).ToString();
        public static readonly string GlyphKeyboard = ((char)0xE765).ToString();
        public static readonly string GlyphSearch = ((char)0xE721).ToString();
        public static readonly string GlyphStar = ((char)0xE734).ToString();
        public static readonly string GlyphStarFilled = ((char)0xE735).ToString();
        public static readonly string GlyphClose = ((char)0xE711).ToString();
        public static readonly string GlyphLeft = ((char)0xE76B).ToString();
        public static readonly string GlyphRight = ((char)0xE76C).ToString();

        [DllImport("user32.dll")]
        private static extern bool DestroyIcon(IntPtr handle);

        // Tray icon: the keyboard glyph, white on a dark taskbar or black on a light
        // one, like Windows' own tray icons.
        public static Icon CreateTrayIcon()
        {
            int size = SystemInformation.SmallIconSize.Width;
            Color color = Theme.TaskbarIsDark() ? Color.White : Color.Black;
            using (Bitmap bmp = new Bitmap(size, size))
            {
                using (Graphics g = Graphics.FromImage(bmp))
                using (Font font = new Font(IconFont.FontFamily, size * 0.8F, GraphicsUnit.Pixel))
                using (SolidBrush brush = new SolidBrush(color))
                using (StringFormat center = new StringFormat())
                {
                    g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;
                    center.Alignment = StringAlignment.Center;
                    center.LineAlignment = StringAlignment.Center;
                    g.DrawString(GlyphKeyboard, font, brush, new RectangleF(0, 0, size, size), center);
                }
                // Icon.FromHandle doesn't copy the icon: clone it and free the original.
                IntPtr handle = bmp.GetHicon();
                try { using (Icon temp = Icon.FromHandle(handle)) return (Icon)temp.Clone(); }
                finally { DestroyIcon(handle); }
            }
        }

        private static Font CreateIconFont()
        {
            foreach (string name in new[] { "Segoe Fluent Icons", "Segoe MDL2 Assets" })
            {
                Font f = new Font(name, 10F);
                if (f.Name == name) return f;   // if it doesn't exist, Windows substitutes another font
                f.Dispose();
            }
            return new Font("Segoe UI Symbol", 10F);
        }

        // ---- Themed controls ----

        public static void StyleButton(Button b, Theme t, bool primary)
        {
            b.FlatStyle = FlatStyle.Flat;
            b.UseVisualStyleBackColor = false;
            b.Cursor = Cursors.Hand;
            b.BackColor = primary ? t.Accent : t.InputBack;
            b.ForeColor = primary ? t.AccentText : t.Foreground;
            b.FlatAppearance.BorderColor = primary ? t.Accent : t.Border;
            b.FlatAppearance.MouseOverBackColor = primary ? ControlPaint.Light(t.Accent, 0.2F) : t.Hover;
            b.FlatAppearance.MouseDownBackColor = primary ? ControlPaint.Dark(t.Accent, 0.05F) : t.Pressed;
        }
    }
}
