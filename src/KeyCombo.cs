using System;
using System.Collections.Generic;
using System.Windows.Forms;

namespace AutoShortcut
{
    // A key combination written as text, e.g. "Win+Shift+A":
    // any modifiers (Win, Ctrl, Alt, Shift) plus exactly one regular key.
    // Used by the global hotkey (RegisterHotKey) and by shortcuts sent with SendInput.
    internal sealed class KeyCombo
    {
        // Modifier values as RegisterHotKey expects them.
        public const uint MOD_ALT = 0x1;
        public const uint MOD_CONTROL = 0x2;
        public const uint MOD_SHIFT = 0x4;
        public const uint MOD_WIN = 0x8;

        // Names accepted when parsing, on top of the names in .NET's Keys enum.
        private static readonly Dictionary<string, Keys> Aliases = new Dictionary<string, Keys>(StringComparer.OrdinalIgnoreCase)
        {
            { "Esc", Keys.Escape },        { "Del", Keys.Delete },       { "Ins", Keys.Insert },
            { "PgUp", Keys.PageUp },       { "PgDn", Keys.PageDown },    { "Backspace", Keys.Back },
            { "PrtSc", Keys.PrintScreen }, { "CapsLock", Keys.CapsLock },
            { "↑", Keys.Up },              { "↓", Keys.Down },           { "←", Keys.Left },
            { "→", Keys.Right },
            { ",", Keys.Oemcomma },        { ".", Keys.OemPeriod },      { "-", Keys.OemMinus },
        };

        // How each key is saved in the JSON (when the enum name isn't the best one).
        private static readonly Dictionary<Keys, string> SaveNames = new Dictionary<Keys, string>
        {
            { Keys.PageDown, "PageDown" }, { Keys.PageUp, "PageUp" },   { Keys.Enter, "Enter" },
            { Keys.CapsLock, "CapsLock" }, { Keys.PrintScreen, "PrintScreen" }, { Keys.Back, "Backspace" },
            { Keys.Oemcomma, "," },        { Keys.OemPeriod, "." },     { Keys.OemMinus, "-" },
        };

        // How each key is shown in the pop-up.
        private static readonly Dictionary<Keys, string> ShowNames = new Dictionary<Keys, string>
        {
            { Keys.Escape, "Esc" },        { Keys.Delete, "Del" },      { Keys.Insert, "Ins" },
            { Keys.PageUp, "PgUp" },       { Keys.PageDown, "PgDn" },   { Keys.Back, "Backspace" },
            { Keys.Enter, "Enter" },       { Keys.PrintScreen, "PrtSc" }, { Keys.CapsLock, "Caps Lock" },
            { Keys.Up, "↑" },              { Keys.Down, "↓" },          { Keys.Left, "←" },
            { Keys.Right, "→" },
            { Keys.Oemcomma, "," },        { Keys.OemPeriod, "." },     { Keys.OemMinus, "-" },
        };

        private readonly uint _modifiers;
        private readonly Keys _key;

        public KeyCombo(uint modifiers, Keys key)
        {
            _modifiers = modifiers;
            _key = key;
        }

        public uint Modifiers { get { return _modifiers; } }
        public Keys Key { get { return _key; } }

        public bool Has(uint modifier) { return (_modifiers & modifier) != 0; }

        public static bool TryParse(string text, out KeyCombo combo)
        {
            combo = null;
            if (string.IsNullOrWhiteSpace(text)) return false;

            uint mods = 0;
            Keys key = Keys.None;
            foreach (string raw in text.Split('+'))
            {
                string part = raw.Trim();
                switch (part.ToLowerInvariant())
                {
                    case "win": case "windows": mods |= MOD_WIN; continue;
                    case "ctrl": case "control": mods |= MOD_CONTROL; continue;
                    case "alt": mods |= MOD_ALT; continue;
                    case "shift": mods |= MOD_SHIFT; continue;
                }
                if (key != Keys.None) return false;   // only one regular key allowed
                key = ParseKey(part);
                if (key == Keys.None) return false;
            }
            if (key == Keys.None) return false;

            combo = new KeyCombo(mods, key);
            return true;
        }

        private static Keys ParseKey(string s)
        {
            if (s.Length == 0) return Keys.None;
            // "1" -> Keys.D1 (the enum calls the number row D0..D9).
            if (s.Length == 1 && s[0] >= '0' && s[0] <= '9') return Keys.D0 + (s[0] - '0');

            Keys k;
            if (Aliases.TryGetValue(s, out k)) return k;
            int dummy;
            if (int.TryParse(s, out dummy)) return Keys.None;   // don't accept numeric key codes
            if (Enum.TryParse(s, true, out k) && Enum.IsDefined(typeof(Keys), k) && (k & Keys.Modifiers) == 0)
                return k;
            return Keys.None;
        }

        // Text for saving: "Win+Shift+A".
        public override string ToString()
        {
            return string.Join("+", Parts(SaveNames));
        }

        // Pieces to draw as keycaps: { "Win", "Shift", "A" }.
        public string[] DisplayParts()
        {
            return Parts(ShowNames).ToArray();
        }

        private List<string> Parts(Dictionary<Keys, string> names)
        {
            List<string> parts = new List<string>();
            if (Has(MOD_WIN)) parts.Add("Win");
            if (Has(MOD_CONTROL)) parts.Add("Ctrl");
            if (Has(MOD_ALT)) parts.Add("Alt");
            if (Has(MOD_SHIFT)) parts.Add("Shift");

            string name;
            if (_key >= Keys.D0 && _key <= Keys.D9) name = ((char)('0' + (_key - Keys.D0))).ToString();
            else if (!names.TryGetValue(_key, out name)) name = _key.ToString();
            parts.Add(name);
            return parts;
        }
    }
}
