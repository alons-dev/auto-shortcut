using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Drawing.Text;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace AutoShortcut
{
    // Painting that works on acrylic (see Theme.Glass).
    //
    // With acrylic, Windows composes the window using the alpha of every pixel:
    // 0 = the blur shows through, 255 = solid. Classic GDI drawing (TextRenderer,
    // BackColor, native controls) leaves alpha at 0, so text would come out
    // see-through. So each control of the pop-up paints into an off-screen 32-bit
    // buffer with GDI+, which keeps real (premultiplied) alpha, and copies it to
    // the window as is. Without acrylic the same buffer starts opaque and text uses
    // TextRenderer (ClearType), so the pop-up looks exactly as before.
    internal static class Glass
    {
        [StructLayout(LayoutKind.Sequential)]
        private struct BITMAPINFOHEADER
        {
            public int biSize, biWidth, biHeight;
            public short biPlanes, biBitCount;
            public int biCompression, biSizeImage, biXPelsPerMeter, biYPelsPerMeter, biClrUsed, biClrImportant;
        }

        [DllImport("gdi32.dll")]
        private static extern IntPtr CreateDIBSection(IntPtr hdc, ref BITMAPINFOHEADER bmi, uint usage, out IntPtr bits, IntPtr section, uint offset);
        [DllImport("gdi32.dll")]
        private static extern IntPtr CreateCompatibleDC(IntPtr hdc);
        [DllImport("gdi32.dll")]
        private static extern IntPtr SelectObject(IntPtr hdc, IntPtr obj);
        [DllImport("gdi32.dll")]
        private static extern bool DeleteObject(IntPtr obj);
        [DllImport("gdi32.dll")]
        private static extern bool DeleteDC(IntPtr hdc);
        [DllImport("gdi32.dll")]
        private static extern bool BitBlt(IntPtr dest, int x, int y, int w, int h, IntPtr src, int sx, int sy, int rop);

        private const int SRCCOPY = 0x00CC0020;

        // Top-down 32-bit DIB; its memory (bits) is shared with a GDI+ Bitmap.
        internal static IntPtr CreateDib(int width, int height, out IntPtr bits)
        {
            BITMAPINFOHEADER bi = new BITMAPINFOHEADER();
            bi.biSize = Marshal.SizeOf(typeof(BITMAPINFOHEADER));
            bi.biWidth = width;
            bi.biHeight = -height;
            bi.biPlanes = 1;
            bi.biBitCount = 32;
            return CreateDIBSection(IntPtr.Zero, ref bi, 0, out bits, IntPtr.Zero, 0);
        }

        // Copies a DIB onto a device context at (x, y), alpha included.
        internal static void Blit(IntPtr hdc, IntPtr dib, int x, int y, int width, int height)
        {
            IntPtr mem = CreateCompatibleDC(hdc);
            IntPtr old = SelectObject(mem, dib);
            BitBlt(hdc, x, y, width, height, mem, 0, 0, SRCCOPY);
            SelectObject(mem, old);
            DeleteDC(mem);
        }

        internal static void Free(IntPtr dib) { DeleteObject(dib); }

        // Paints the area to repaint (e.ClipRectangle): fills it with the tint (or
        // the background), runs render in control coordinates and copies the result.
        // render may be null: containers only paint their background.
        public static void Paint(PaintEventArgs e, Theme theme, Action<Graphics> render)
        {
            Rectangle clip = e.ClipRectangle;
            if (clip.Width <= 0 || clip.Height <= 0) return;
            IntPtr bits;
            IntPtr dib = CreateDib(clip.Width, clip.Height, out bits);
            if (dib == IntPtr.Zero)
            {
                // Out of GDI resources: paint straight on the window (no transparency).
                e.Graphics.Clear(theme.Background);
                if (render != null) render(e.Graphics);
                return;
            }
            try
            {
                using (Bitmap buffer = new Bitmap(clip.Width, clip.Height, clip.Width * 4, PixelFormat.Format32bppPArgb, bits))
                {
                    buffer.SetResolution(96F * Ui.Scale, 96F * Ui.Scale);   // fonts in points: real size
                    using (Graphics g = Graphics.FromImage(buffer))
                    {
                        g.Clear(theme.Glass ? theme.Tint : theme.Background);
                        g.TranslateTransform(-clip.X, -clip.Y);
                        g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
                        if (render != null) render(g);
                        g.Flush(System.Drawing.Drawing2D.FlushIntention.Sync);
                    }
                }
                IntPtr hdc = e.Graphics.GetHdc();
                try { Blit(hdc, dib, clip.X, clip.Y, clip.Width, clip.Height); }
                finally { e.Graphics.ReleaseHdc(hdc); }
            }
            finally { DeleteObject(dib); }
        }

        // Text inside Paint: GDI+ on acrylic (keeps alpha, grayscale antialiasing like
        // Windows uses on acrylic), TextRenderer otherwise (ClearType, as before).
        public static void Text(Graphics g, Theme theme, string text, Font font, Rectangle bounds, Color color, TextFormatFlags flags)
        {
            if (string.IsNullOrEmpty(text)) return;
            if (!theme.Glass)
            {
                TextRenderer.DrawText(g, text, font, bounds, color, flags | TextFormatFlags.PreserveGraphicsTranslateTransform);
                return;
            }
            using (StringFormat format = FormatFor(flags))
            using (SolidBrush brush = new SolidBrush(color))
                g.DrawString(text, font, brush, bounds, format);
        }

        // The GDI+ equivalent of the TextRenderer flags the app uses.
        public static StringFormat FormatFor(TextFormatFlags flags)
        {
            StringFormat f = new StringFormat();
            if ((flags & TextFormatFlags.HorizontalCenter) != 0) f.Alignment = StringAlignment.Center;
            else if ((flags & TextFormatFlags.Right) != 0) f.Alignment = StringAlignment.Far;
            else f.Alignment = StringAlignment.Near;
            if ((flags & TextFormatFlags.VerticalCenter) != 0) f.LineAlignment = StringAlignment.Center;
            else if ((flags & TextFormatFlags.Bottom) != 0) f.LineAlignment = StringAlignment.Far;
            else f.LineAlignment = StringAlignment.Near;
            if ((flags & TextFormatFlags.WordBreak) == 0) f.FormatFlags |= StringFormatFlags.NoWrap;
            f.Trimming = (flags & (TextFormatFlags.EndEllipsis | TextFormatFlags.WordEllipsis | TextFormatFlags.PathEllipsis)) != 0
                ? StringTrimming.EllipsisCharacter : StringTrimming.None;
            f.HotkeyPrefix = (flags & TextFormatFlags.NoPrefix) != 0 ? HotkeyPrefix.None : HotkeyPrefix.Show;
            return f;
        }
    }

    // A panel that only paints the pop-up background (tint on acrylic).
    internal sealed class GlassPanel : Panel
    {
        private readonly Theme _theme;

        public GlassPanel(Theme theme)
        {
            _theme = theme;
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.Opaque, true);
            BackColor = theme.Background;
        }

        protected override void OnPaint(PaintEventArgs e) { Glass.Paint(e, _theme, null); }
    }

    // One line of text, sized to fit (a Label that can paint on acrylic).
    internal sealed class TextLabel : Control
    {
        private readonly Theme _theme;
        private readonly bool _subtle;

        public TextLabel(Theme theme, bool subtle)
        {
            _theme = theme;
            _subtle = subtle;
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.Opaque | ControlStyles.ResizeRedraw, true);
            SetStyle(ControlStyles.Selectable, false);
            AccessibleRole = AccessibleRole.StaticText;
        }

        private const TextFormatFlags Flags = TextFormatFlags.Left | TextFormatFlags.Top | TextFormatFlags.SingleLine | TextFormatFlags.NoPrefix;

        protected override void OnTextChanged(EventArgs e) { base.OnTextChanged(e); FitText(); }
        protected override void OnFontChanged(EventArgs e) { base.OnFontChanged(e); FitText(); }

        private void FitText()
        {
            Size = TextRenderer.MeasureText(string.IsNullOrEmpty(Text) ? " " : Text, Font, Size.Empty, Flags);
            AccessibleName = Text;
            Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Glass.Paint(e, _theme, delegate(Graphics g)
            {
                Glass.Text(g, _theme, Text, Font, ClientRectangle, _subtle ? _theme.Subtle : _theme.Foreground, Flags);
            });
        }
    }
}
