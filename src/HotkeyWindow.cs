using System;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace AutoShortcut
{
    // Global hotkey. RegisterHotKey (user32.dll) asks Windows to send us a WM_HOTKEY
    // message whenever the combination is pressed, whether or not the app is in the
    // foreground. The message goes to a window: we use a "message-only" window
    // (child of HWND_MESSAGE), which is invisible and only receives messages.
    internal sealed class HotkeyWindow : NativeWindow, IDisposable
    {
        private const int WM_HOTKEY = 0x0312;
        private const uint MOD_NOREPEAT = 0x4000;   // don't repeat while held down
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

        // Returns false if Windows refuses it (usually because another app already uses it).
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
