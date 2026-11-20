using System;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace AutoShortcut
{
    // Graba la siguiente combinación que pulse el usuario, incluidas las que Windows
    // se reserva (Win+Shift+S, Win+V...). Para eso usa un hook de teclado de bajo nivel
    // (WH_KEYBOARD_LL): Windows nos pasa cada pulsación ANTES de procesar sus atajos
    // y podemos "tragárnosla" devolviendo 1.
    //
    // Solo está activo mientras se graba: se suelta con la primera combinación
    // completa, con Esc, o llamando a Stop() (el campo lo hace al cerrar o a los 10 s).
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

        // Hay que guardar el delegado en un campo: si el recolector de basura lo
        // liberase, Windows llamaría a una función que ya no existe.
        private readonly LowLevelKeyboardProc _proc;
        private IntPtr _hook;
        private uint _mods;      // modificadores pulsados ahora mismo
        private bool _done;      // ya tenemos la combinación: dejar pasar el resto

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
                return (IntPtr)1;   // tragada: Windows no la ve
            }
            if (!down) return (IntPtr)1;

            // Primera tecla normal: ya tenemos la combinación.
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
