using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace AutoShortcut
{
    // Global hotkeys. RegisterHotKey (user32.dll) asks Windows to send us a WM_HOTKEY
    // message whenever a combination is pressed, whether or not the app is in the
    // foreground. The message goes to a window: we use a "message-only" window
    // (child of HWND_MESSAGE), which is invisible and only receives messages.
    // Each combination has an id of our choosing; Pressed says which one.
    internal sealed class HotkeyWindow : NativeWindow, IDisposable
    {
        private const int WM_HOTKEY = 0x0312;
        private const uint MOD_NOREPEAT = 0x4000;   // don't repeat while held down
        private const int ProbeId = 0xBFFF;          // highest id an app may use
        private const int ResumeDelayMs = 150;
        private static readonly IntPtr HWND_MESSAGE = new IntPtr(-3);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

        private readonly Dictionary<int, KeyCombo> _registered = new Dictionary<int, KeyCombo>();
        private readonly List<int> _paused = new List<int>();
        private readonly Timer _resume = new Timer();

        public event Action<int> Pressed;

        public HotkeyWindow()
        {
            CreateParams cp = new CreateParams();
            cp.Parent = HWND_MESSAGE;
            CreateHandle(cp);
            _resume.Interval = ResumeDelayMs;
            _resume.Tick += delegate { Resume(); };
        }

        // Returns false if Windows refuses it (usually because another app already uses it).
        public bool Register(int id, KeyCombo combo)
        {
            Unregister(id);
            if (!RegisterHotKey(Handle, id, combo.Modifiers | MOD_NOREPEAT, (uint)combo.Key)) return false;
            _registered[id] = combo;
            return true;
        }

        public void Unregister(int id)
        {
            _paused.Remove(id);
            if (!_registered.Remove(id)) return;
            UnregisterHotKey(Handle, id);
        }

        // True if one of our ids has this combination.
        public bool IsOurs(KeyCombo combo)
        {
            foreach (KeyCombo mine in _registered.Values)
                if (mine.ToString() == combo.ToString()) return true;
            return false;
        }

        // Whether another app has it: try to take it and let go at once.
        public bool IsFree(KeyCombo combo)
        {
            if (!RegisterHotKey(Handle, ProbeId, combo.Modifiers | MOD_NOREPEAT, (uint)combo.Key)) return false;
            UnregisterHotKey(Handle, ProbeId);
            return true;
        }

        // Keys we press ourselves also trigger hotkeys: before pressing a combination
        // that is one of ours, let go of it for a moment.
        public void PauseFor(KeyCombo combo)
        {
            foreach (KeyValuePair<int, KeyCombo> pair in _registered)
            {
                if (pair.Value.ToString() != combo.ToString() || _paused.Contains(pair.Key)) continue;
                UnregisterHotKey(Handle, pair.Key);
                _paused.Add(pair.Key);
            }
            if (_paused.Count > 0)
            {
                _resume.Stop();
                _resume.Start();
            }
        }

        private void Resume()
        {
            _resume.Stop();
            foreach (int id in _paused)
            {
                KeyCombo combo;
                if (_registered.TryGetValue(id, out combo) &&
                    !RegisterHotKey(Handle, id, combo.Modifiers | MOD_NOREPEAT, (uint)combo.Key))
                    _registered.Remove(id);   // someone took it meanwhile
            }
            _paused.Clear();
        }

        protected override void WndProc(ref Message m)
        {
            if (m.Msg == WM_HOTKEY)
            {
                Action<int> handler = Pressed;
                if (handler != null) handler(m.WParam.ToInt32());
                return;
            }
            base.WndProc(ref m);
        }

        public void Dispose()
        {
            _resume.Dispose();
            foreach (int id in new List<int>(_registered.Keys)) Unregister(id);
            DestroyHandle();
        }
    }
}
