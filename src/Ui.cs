using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace AutoShortcut
{
    // Ayudas de dibujo y estilo comunes a todas las ventanas.
    internal static class Ui
    {
        // La app es "DPI aware" (ver Program.cs): trabajamos en píxeles reales, así que
        // las medidas pensadas a 100 % se multiplican por la escala de Windows.
        public static readonly float Scale = ReadScale();

        private static float ReadScale()
        {
            using (Graphics g = Graphics.FromHwnd(IntPtr.Zero)) return g.DpiX / 96F;
        }

        // Ui.S(40) = 40 px a 100 %, 60 px a 150 %, 80 px a 200 %...
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

        // ---- Marco de ventana (DWM) ----

        // DWM (Desktop Window Manager) es quien dibuja los marcos de las ventanas.
        // En Windows 11 le podemos pedir esquinas redondeadas y barra de título oscura.
        // En Windows 10 estos atributos no existen: la llamada falla sin más.
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

        // ---- Iconos ----

        // Los iconos de Windows 11 son una fuente: "Segoe Fluent Icons" (en Windows 10,
        // "Segoe MDL2 Assets"). Cada icono es un carácter, p. ej.  = "+".
        public static readonly Font IconFont = CreateIconFont();
        public const string GlyphAdd = "";
        public const string GlyphSettings = "";
        public const string GlyphKeyboard = "";

        [DllImport("user32.dll")]
        private static extern bool DestroyIcon(IntPtr handle);

        // Icono de la bandeja: el teclado de la fuente de iconos, blanco sobre barra
        // de tareas oscura o negro sobre clara, como los iconos del propio Windows.
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
                // Icon.FromHandle no copia el icono: lo clonamos y liberamos el original.
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
                if (f.Name == name) return f;   // si no existe, Windows devuelve otra fuente
                f.Dispose();
            }
            return new Font("Segoe UI Symbol", 10F);
        }

        // ---- Controles con el tema ----

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
