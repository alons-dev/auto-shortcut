using System.Drawing;
using Microsoft.Win32;

namespace AutoShortcut
{
    // Windows 11-style colors, light or dark following the user's setting
    // (Settings > Personalization > Colors > "Choose your app mode").
    internal sealed class Theme
    {
        public bool Dark;
        public Color Background;
        public Color Foreground;
        public Color Subtle;       // secondary text
        public Color Hover;        // background of a row or button under the mouse
        public Color Pressed;      // background while clicking
        public Color KeyFill;      // keycap background
        public Color KeyBorder;    // keycap border
        public Color InputBack;    // text boxes and regular buttons
        public Color Border;       // text box and button borders
        public Color Accent;       // primary button (Save)
        public Color AccentText;
        public Color Error;
        public Color MenuBack;     // context menus

        public static Theme Current()
        {
            Theme t = new Theme();
            t.Dark = IsDarkMode();
            if (t.Dark)
            {
                t.Background = Color.FromArgb(32, 32, 32);
                t.Foreground = Color.FromArgb(255, 255, 255);
                t.Subtle = Color.FromArgb(160, 160, 160);
                t.Hover = Color.FromArgb(45, 45, 45);
                t.Pressed = Color.FromArgb(40, 40, 40);
                t.KeyFill = Color.FromArgb(56, 56, 56);
                t.KeyBorder = Color.FromArgb(72, 72, 72);
                t.InputBack = Color.FromArgb(45, 45, 45);
                t.Border = Color.FromArgb(75, 75, 75);
                t.Accent = Color.FromArgb(76, 194, 255);
                t.AccentText = Color.FromArgb(0, 0, 0);
                t.Error = Color.FromArgb(255, 153, 164);
                t.MenuBack = Color.FromArgb(44, 44, 44);
            }
            else
            {
                t.Background = Color.FromArgb(249, 249, 249);
                t.Foreground = Color.FromArgb(27, 27, 27);
                t.Subtle = Color.FromArgb(96, 96, 96);
                t.Hover = Color.FromArgb(234, 234, 234);
                t.Pressed = Color.FromArgb(240, 240, 240);
                t.KeyFill = Color.FromArgb(255, 255, 255);
                t.KeyBorder = Color.FromArgb(212, 212, 212);
                t.InputBack = Color.FromArgb(255, 255, 255);
                t.Border = Color.FromArgb(200, 200, 200);
                t.Accent = Color.FromArgb(0, 95, 184);
                t.AccentText = Color.FromArgb(255, 255, 255);
                t.Error = Color.FromArgb(196, 43, 28);
                t.MenuBack = Color.FromArgb(249, 249, 249);
            }
            return t;
        }

        private static bool IsDarkMode()
        {
            return ReadPersonalize("AppsUseLightTheme") == 0;
        }

        // The taskbar has its own mode ("Choose your default Windows mode"):
        // the tray icon must contrast with it, not with apps.
        public static bool TaskbarIsDark()
        {
            return ReadPersonalize("SystemUsesLightTheme") != 1;
        }

        private static int ReadPersonalize(string name)
        {
            using (RegistryKey key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize"))
            {
                object value = key == null ? null : key.GetValue(name);
                return value is int ? (int)value : -1;
            }
        }
    }
}
