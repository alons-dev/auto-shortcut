using System;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace AutoShortcut
{
    // Hotkey global. RegisterHotKey (user32.dll) le pide a Windows que nos avise
    // con un mensaje WM_HOTKEY cada vez que se pulse la combinación, esté la app
    // en primer plano o no. El aviso llega a una ventana: usamos una "message-only"
    // (hija de HWND_MESSAGE), que es invisible y solo sirve para recibir mensajes.
    internal sealed class HotkeyWindow : NativeWindow, IDisposable
    {
        private const int WM_HOTKEY = 0x0312;
        private const uint MOD_NOREPEAT = 0x4000;   // no repetir mientras se mantiene pulsada
        private const int HotkeyId = 1;
        private static readonly IntPtr HWND_MESSAGE = new IntPtr(-3);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

        private bool _registered;

        public event EventHandler Pressed;

        public HotkeyWindow()
        {
            CreateParams cp = new CreateParams();
            cp.Parent = HWND_MESSAGE;
            CreateHandle(cp);
        }

        // Devuelve false si Windows la rechaza (normalmente porque otra app ya la usa).
        public bool Register(KeyCombo combo)
        {
            Unregister();
            _registered = RegisterHotKey(Handle, HotkeyId, combo.Modifiers | MOD_NOREPEAT, (uint)combo.Key);
            return _registered;
        }

        public void Unregister()
        {
            if (!_registered) return;
            UnregisterHotKey(Handle, HotkeyId);
            _registered = false;
        }

        protected override void WndProc(ref Message m)
        {
            if (m.Msg == WM_HOTKEY && m.WParam.ToInt32() == HotkeyId)
            {
                EventHandler handler = Pressed;
                if (handler != null) handler(this, EventArgs.Empty);
                return;
            }
            base.WndProc(ref m);
        }

        public void Dispose()
        {
            Unregister();
            DestroyHandle();
        }
    }
}
