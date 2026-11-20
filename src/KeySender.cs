using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace AutoShortcut
{
    // Presses a key combination as if the user did it, using SendInput (user32.dll):
    // modifiers down, key down and up, then modifiers up in reverse order.
    internal static class KeySender
    {
        [StructLayout(LayoutKind.Sequential)]
        private struct KEYBDINPUT
        {
            public ushort wVk;
            public ushort wScan;
            public uint dwFlags;
            public uint time;
            public IntPtr dwExtraInfo;
        }

        // INPUT is a union of mouse/keyboard/hardware input: it must be as large as
        // the biggest member (MOUSEINPUT) or SendInput rejects the call on 64-bit.
        [StructLayout(LayoutKind.Sequential)]
        private struct MOUSEINPUT
        {
            public int dx;
            public int dy;
            public uint mouseData;
            public uint dwFlags;
            public uint time;
            public IntPtr dwExtraInfo;
        }

        [StructLayout(LayoutKind.Explicit)]
        private struct InputUnion
        {
            [FieldOffset(0)] public MOUSEINPUT mi;
            [FieldOffset(0)] public KEYBDINPUT ki;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct INPUT
        {
            public uint type;
            public InputUnion u;
        }

        private const uint INPUT_KEYBOARD = 1;
        private const uint KEYEVENTF_EXTENDEDKEY = 0x1;
        private const uint KEYEVENTF_KEYUP = 0x2;

        [DllImport("user32.dll", SetLastError = true)]
        private static extern uint SendInput(uint count, INPUT[] inputs, int size);

        [DllImport("user32.dll")]
        private static extern uint MapVirtualKey(uint code, uint mapType);

        public static bool Send(KeyCombo combo)
        {
            List<Keys> mods = new List<Keys>();
            if (combo.Has(KeyCombo.MOD_WIN)) mods.Add(Keys.LWin);
            if (combo.Has(KeyCombo.MOD_CONTROL)) mods.Add(Keys.LControlKey);
            if (combo.Has(KeyCombo.MOD_ALT)) mods.Add(Keys.LMenu);
            if (combo.Has(KeyCombo.MOD_SHIFT)) mods.Add(Keys.LShiftKey);

            List<INPUT> inputs = new List<INPUT>();
            foreach (Keys m in mods) inputs.Add(Make(m, false));
            inputs.Add(Make(combo.Key, false));
            inputs.Add(Make(combo.Key, true));
            for (int i = mods.Count - 1; i >= 0; i--) inputs.Add(Make(mods[i], true));

            INPUT[] array = inputs.ToArray();
            uint sent = SendInput((uint)array.Length, array, Marshal.SizeOf(typeof(INPUT)));
            return sent == array.Length;
        }

        private static INPUT Make(Keys key, bool up)
        {
            INPUT input = new INPUT();
            input.type = INPUT_KEYBOARD;
            input.u.ki.wVk = (ushort)key;
            input.u.ki.wScan = (ushort)MapVirtualKey((uint)key, 0);   // some apps (games) read the scan code
            input.u.ki.dwFlags = (up ? KEYEVENTF_KEYUP : 0) | (IsExtended(key) ? KEYEVENTF_EXTENDEDKEY : 0);
            return input;
        }

        // "Extended" keys: without this flag, e.g. the Right arrow arrives as numpad 6.
        private static bool IsExtended(Keys key)
        {
            switch (key)
            {
                case Keys.Up: case Keys.Down: case Keys.Left: case Keys.Right:
                case Keys.Home: case Keys.End: case Keys.PageUp: case Keys.PageDown:
                case Keys.Insert: case Keys.Delete: case Keys.LWin: case Keys.RWin:
                case Keys.Apps: case Keys.Divide: case Keys.NumLock: case Keys.PrintScreen:
                case Keys.RControlKey: case Keys.RMenu:
                    return true;
                default:
                    return false;
            }
        }
    }
}
