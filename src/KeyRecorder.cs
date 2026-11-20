using System;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace AutoShortcut
{
    // Records the next key combination the user presses, including the ones Windows
    // reserves for itself (Win+Shift+S, Win+V...). It uses a low-level keyboard hook
    // (WH_KEYBOARD_LL): Windows hands us every key press BEFORE processing its own
    // shortcuts, and we can "swallow" it by returning 1.
    //
    // It is only active while recording: it lets go after the first complete
    // combination, on Esc, or when Stop() is called (the field does that on close
    // or after 10 s).
    internal sealed class KeyRecorder : IDisposable
    {
        private delegate IntPtr LowLevelKeyboardProc(int nCode, IntPtr wParam, IntPtr lParam);

        [StructLayout(LayoutKind.Sequential)]
        private struct KBDLLHOOKSTRUCT
        {
            public uint vkCode;
            public uint scanCode;
            public uint flags;
            public uint time;
            public IntPtr dwExtraInfo;
        }

        private const int WH_KEYBOARD_LL = 13;
        private const int WM_KEYDOWN = 0x0100;
        private const int WM_SYSKEYDOWN = 0x0104;

        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr SetWindowsHookEx(int idHook, LowLevelKeyboardProc fn, IntPtr hMod, uint threadId);

        [DllImport("user32.dll")]
        private static extern bool UnhookWindowsHookEx(IntPtr hook);

        [DllImport("user32.dll")]
        private static extern IntPtr CallNextHookEx(IntPtr hook, int nCode, IntPtr wParam, IntPtr lParam);

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
        private static extern IntPtr GetModuleHandle(string name);

        // The delegate must be kept in a field: if the garbage collector freed it,
        // Windows would call a function that no longer exists.
        private readonly LowLevelKeyboardProc _proc;
        private IntPtr _hook;
        private uint _mods;      // modifiers currently held down
        private bool _done;      // we already have the combination: let the rest through

        public event Action<KeyCombo> Recorded;
        public event Action Cancelled;

        public KeyRecorder()
        {
            _proc = HookProc;
        }

        public bool Active { get { return _hook != IntPtr.Zero; } }

        public bool Start()
        {
            if (Active) return true;
            _mods = 0;
            _done = false;
            _hook = SetWindowsHookEx(WH_KEYBOARD_LL, _proc, GetModuleHandle("user32.dll"), 0);
            return Active;
        }

        public void Stop()
        {
            if (!Active) return;
            UnhookWindowsHookEx(_hook);
            _hook = IntPtr.Zero;
        }

        private IntPtr HookProc(int nCode, IntPtr wParam, IntPtr lParam)
        {
            if (nCode < 0 || _done) return CallNextHookEx(_hook, nCode, wParam, lParam);

            KBDLLHOOKSTRUCT info = (KBDLLHOOKSTRUCT)Marshal.PtrToStructure(lParam, typeof(KBDLLHOOKSTRUCT));
            int msg = wParam.ToInt32();
            bool down = msg == WM_KEYDOWN || msg == WM_SYSKEYDOWN;
            Keys key = (Keys)info.vkCode;

            uint mod = ModifierOf(key);
            if (mod != 0)
            {
                if (down) _mods |= mod; else _mods &= ~mod;
                return (IntPtr)1;   // swallowed: Windows never sees it
            }
            if (!down) return (IntPtr)1;

            // First regular key: we have the combination.
            _done = true;
            if (key == Keys.Escape && _mods == 0)
            {
                Action cancelled = Cancelled;
                if (cancelled != null) cancelled();
            }
            else
            {
                Action<KeyCombo> recorded = Recorded;
                if (recorded != null) recorded(new KeyCombo(_mods, key));
            }
            return (IntPtr)1;
        }

        private static uint ModifierOf(Keys key)
        {
            switch (key)
            {
                case Keys.LWin: case Keys.RWin: return KeyCombo.MOD_WIN;
                case Keys.LControlKey: case Keys.RControlKey: return KeyCombo.MOD_CONTROL;
                case Keys.LMenu: case Keys.RMenu: return KeyCombo.MOD_ALT;
                case Keys.LShiftKey: case Keys.RShiftKey: return KeyCombo.MOD_SHIFT;
                default: return 0;
            }
        }

        public void Dispose()
        {
            Stop();
        }
    }
}
