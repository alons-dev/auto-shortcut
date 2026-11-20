using System;
using System.Drawing;
using System.Windows.Forms;
using Microsoft.Win32;

namespace AutoShortcut
{
    // Windows 11-style colors, light or dark following the user's setting
    // (Settings > Personalization > Colors > "Choose your app mode").
    //
    // The pop-up can also be "glass": Windows' acrylic (the blur of the taskbar and
    // the Start menu) behind it, with Tint on top. Then the colors drawn over it
    // that should let it through (secondary text, hover, keycaps, borders) are
    // translucent. Foreground, input background and accent are the same in both.
    internal sealed class Theme
    {
        public bool Dark;
        public bool Glass;
        public Color Tint;         // layer over the acrylic (glass only)
        public Color Background;
        public Color Foreground;
        public Color Subtle;       // secondary text
        public Color Hover;        // background of a row or button under the mouse
        public Color Pressed;      // background while clicking
        public Color KeyFill;      // keycap background
        public Color KeyBorder;    // keycap border
        public Color InputBack;    // text boxes and regular buttons
        public Color Border;       // text box and button borders
        public Color Divider;      // line between sections of the list
        public Color Accent;       // primary button (Save)
        public Color AccentText;
        public Color Error;
        public Color MenuBack;     // context menus

        // Windows 11 (see Ui.TryAcrylic). Windows 10 has the same blur, but without
        // rounded corners and laggy, so it keeps the opaque look.
        public const int AcrylicMinBuild = 22000;

        public static Theme Current()
        {
            Theme t = new Theme();
            t.Dark = IsDarkMode();
            t.SetGlass(false);
            return t;
        }

        public void SetGlass(bool glass)
        {
            Glass = glass;
            if (Dark)
            {
                Background = Color.FromArgb(32, 32, 32);
                Foreground = Color.FromArgb(255, 255, 255);
                Subtle = Color.FromArgb(160, 160, 160);
                Hover = Color.FromArgb(45, 45, 45);
                Pressed = Color.FromArgb(40, 40, 40);
                KeyFill = Color.FromArgb(56, 56, 56);
                KeyBorder = Color.FromArgb(72, 72, 72);
                InputBack = Color.FromArgb(45, 45, 45);
                Border = Color.FromArgb(75, 75, 75);
                Divider = Color.FromArgb(64, 64, 64);
                Accent = Color.FromArgb(76, 194, 255);
                AccentText = Color.FromArgb(0, 0, 0);
                Error = Color.FromArgb(255, 153, 164);
                MenuBack = Color.FromArgb(44, 44, 44);
                if (glass)
                {
                    Tint = Color.FromArgb(128, 32, 32, 32);
                    Subtle = Color.FromArgb(197, 255, 255, 255);
                    Hover = Color.FromArgb(16, 255, 255, 255);
                    Pressed = Color.FromArgb(10, 255, 255, 255);
                    KeyFill = Color.FromArgb(24, 255, 255, 255);
                    KeyBorder = Color.FromArgb(36, 255, 255, 255);
                    Border = Color.FromArgb(48, 255, 255, 255);
                    Divider = Color.FromArgb(40, 255, 255, 255);
                }
            }
            else
            {
                Background = Color.FromArgb(249, 249, 249);
                Foreground = Color.FromArgb(27, 27, 27);
                Subtle = Color.FromArgb(96, 96, 96);
                Hover = Color.FromArgb(234, 234, 234);
                Pressed = Color.FromArgb(240, 240, 240);
                KeyFill = Color.FromArgb(255, 255, 255);
                KeyBorder = Color.FromArgb(212, 212, 212);
                InputBack = Color.FromArgb(255, 255, 255);
                Border = Color.FromArgb(200, 200, 200);
                Divider = Color.FromArgb(221, 221, 221);
                Accent = Color.FromArgb(0, 95, 184);
                AccentText = Color.FromArgb(255, 255, 255);
                Error = Color.FromArgb(196, 43, 28);
                MenuBack = Color.FromArgb(249, 249, 249);
                if (glass)
                {
                    Tint = Color.FromArgb(128, 243, 243, 243);
                    Subtle = Color.FromArgb(158, 0, 0, 0);
                    Hover = Color.FromArgb(12, 0, 0, 0);
                    Pressed = Color.FromArgb(8, 0, 0, 0);
                    KeyFill = Color.FromArgb(176, 255, 255, 255);
                    KeyBorder = Color.FromArgb(28, 0, 0, 0);
                    Border = Color.FromArgb(36, 0, 0, 0);
                    Divider = Color.FromArgb(30, 0, 0, 0);
                }
            }
            if (!glass) Tint = Background;
        }

        // Pure rule, so it can be tested: Windows 11 or later, "Transparency
        // effects" on (-1 = value missing = Windows' default, on) and no high contrast.
        public static bool CanUseAcrylic(int build, int enableTransparency, bool highContrast)
        {
            return build >= AcrylicMinBuild && enableTransparency != 0 && !highContrast;
        }

        // The rule applied to this PC. AUTOSHORTCUT_NO_ACRYLIC=1 forces the opaque look.
        public static bool AcrylicAvailable()
        {
            if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("AUTOSHORTCUT_NO_ACRYLIC"))) return false;
            return CanUseAcrylic(WindowsBuild(), ReadPersonalize("EnableTransparency"), SystemInformation.HighContrast);
        }

        // Environment.OSVersion can report an older version to some hosts; the
        // registry always has the real build number.
        private static int WindowsBuild()
        {
            try
            {
                using (RegistryKey key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows NT\CurrentVersion"))
                {
                    object value = key == null ? null : key.GetValue("CurrentBuildNumber");
                    int build;
                    return value != null && int.TryParse(value.ToString(), out build) ? build : 0;
                }
            }
            catch (System.Security.SecurityException) { return 0; }
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
