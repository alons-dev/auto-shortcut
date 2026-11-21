using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;

namespace AutoShortcut
{
    // Runs a shortcut. Returns null on success, or an error message for the user.
    internal static class ActionRunner
    {
        [DllImport("user32.dll")]
        private static extern bool SetForegroundWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern bool IsWindow(IntPtr hWnd);

        // previousWindow: the window that was in front before the pop-up opened.
        public static string Execute(Shortcut shortcut, IntPtr previousWindow)
        {
            return shortcut.IsLaunch ? Launch(shortcut) : PressKeys(shortcut, previousWindow);
        }

        // From a hotkey: keys go to the window in front, which still has the focus,
        // or to target when the pop-up has it. The hotkey's modifiers may still be held.
        public static string ExecuteFromHotkey(Shortcut shortcut, IntPtr target)
        {
            if (shortcut.IsLaunch) return Launch(shortcut);
            KeyCombo combo;
            if (!KeyCombo.TryParse(shortcut.KeysText, out combo)) return CantParse(shortcut);
            if (target != IntPtr.Zero && IsWindow(target))
            {
                SetForegroundWindow(target);
                Thread.Sleep(80);
            }
            return KeySender.Send(combo, true) ? null : Blocked(combo);
        }

        private static string CantParse(Shortcut shortcut)
        {
            return "Can't understand the key combination \"" + shortcut.KeysText + "\" in \"" + shortcut.Name + "\".";
        }

        private static string Blocked(KeyCombo combo)
        {
            return "Windows blocked sending " + combo + " (this happens with windows running as administrator).";
        }

        private static string PressKeys(Shortcut shortcut, IntPtr previousWindow)
        {
            KeyCombo combo;
            if (!KeyCombo.TryParse(shortcut.KeysText, out combo)) return CantParse(shortcut);

            // Keys go to the window that has focus. When you click, the focus is ours,
            // so we hand it back to the previous window and give it a moment to
            // activate before pressing the keys.
            if (previousWindow != IntPtr.Zero && IsWindow(previousWindow))
                SetForegroundWindow(previousWindow);
            Thread.Sleep(80);

            return KeySender.Send(combo) ? null : Blocked(combo);
        }

        private static string Launch(Shortcut shortcut)
        {
            // UseShellExecute = like double-clicking: works for programs, files,
            // folders, URLs (default browser) and paths like "shell:Downloads".
            ProcessStartInfo psi = new ProcessStartInfo(Environment.ExpandEnvironmentVariables(shortcut.Run));
            psi.UseShellExecute = true;
            if (!string.IsNullOrEmpty(shortcut.Args))
                psi.Arguments = Environment.ExpandEnvironmentVariables(shortcut.Args);
            try
            {
                Process p = Process.Start(psi);
                if (p != null) p.Dispose();
                return null;
            }
            catch (Win32Exception ex)
            {
                return "Couldn't open \"" + shortcut.Run + "\": " + ex.Message;
            }
            catch (InvalidOperationException ex)
            {
                return "Couldn't open \"" + shortcut.Run + "\": " + ex.Message;
            }
        }
    }
}
