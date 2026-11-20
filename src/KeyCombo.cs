using System;
using System.Collections.Generic;
using System.Text;
using System.Windows.Forms;

namespace AutoShortcut
{
    // Una combinación de teclas escrita como texto, p. ej. "Win+Shift+A":
    // varios modificadores (Win, Ctrl, Alt, Shift) y una sola tecla normal.
    // La usan la hotkey global (RegisterHotKey) y los atajos que se envían con SendInput.
    internal sealed class KeyCombo
    {
        // Valores de los modificadores tal como los espera RegisterHotKey.
        public const uint MOD_ALT = 0x1;
        public const uint MOD_CONTROL = 0x2;
        public const uint MOD_SHIFT = 0x4;
        public const uint MOD_WIN = 0x8;

        // Nombres que se aceptan al leer, además de los del enum Keys de .NET.
        private static readonly Dictionary<string, Keys> Aliases = new Dictionary<string, Keys>(StringComparer.OrdinalIgnoreCase)
        {
            { "Esc", Keys.Escape },        { "Del", Keys.Delete },       { "Supr", Keys.Delete },
            { "Ins", Keys.Insert },        { "PgUp", Keys.PageUp },      { "PgDn", Keys.PageDown },
            { "RePág", Keys.PageUp },      { "AvPág", Keys.PageDown },   { "Inicio", Keys.Home },
            { "Fin", Keys.End },           { "Espacio", Keys.Space },    { "Retroceso", Keys.Back },
            { "Backspace", Keys.Back },    { "PrtSc", Keys.PrintScreen },{ "ImprPant", Keys.PrintScreen },
            { "CapsLock", Keys.CapsLock }, { "↑", Keys.Up },             { "↓", Keys.Down },
            { "←", Keys.Left },            { "→", Keys.Right },
            { ",", Keys.Oemcomma },        { ".", Keys.OemPeriod },      { "-", Keys.OemMinus },
        };

        // Cómo se guarda cada tecla en el JSON (cuando el nombre del enum no es el ideal).
        private static readonly Dictionary<Keys, string> SaveNames = new Dictionary<Keys, string>
        {
            { Keys.PageDown, "PageDown" }, { Keys.PageUp, "PageUp" },   { Keys.Enter, "Enter" },
            { Keys.CapsLock, "CapsLock" }, { Keys.PrintScreen, "PrintScreen" }, { Keys.Back, "Backspace" },
            { Keys.Oemcomma, "," },        { Keys.OemPeriod, "." },     { Keys.OemMinus, "-" },
        };

        // Cómo se muestra cada tecla en el pop-up.
        private static readonly Dictionary<Keys, string> ShowNames = new Dictionary<Keys, string>
        {
            { Keys.Escape, "Esc" },        { Keys.Delete, "Supr" },     { Keys.Insert, "Insert" },
            { Keys.PageUp, "RePág" },      { Keys.PageDown, "AvPág" },  { Keys.Home, "Inicio" },
            { Keys.End, "Fin" },           { Keys.Space, "Espacio" },   { Keys.Back, "Retroceso" },
            { Keys.Enter, "Enter" },       { Keys.PrintScreen, "ImprPant" }, { Keys.CapsLock, "Bloq Mayús" },
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
                    case "shift": case "mayús": mods |= MOD_SHIFT; continue;
                }
                if (key != Keys.None) return false;   // solo se admite una tecla normal
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
            // "1" -> Keys.D1 (el enum llama D0..D9 a la fila de números).
            if (s.Length == 1 && s[0] >= '0' && s[0] <= '9') return Keys.D0 + (s[0] - '0');

            Keys k;
            if (Aliases.TryGetValue(s, out k)) return k;
            int dummy;
            if (int.TryParse(s, out dummy)) return Keys.None;   // no aceptar códigos numéricos
            if (Enum.TryParse(s, true, out k) && Enum.IsDefined(typeof(Keys), k) && (k & Keys.Modifiers) == 0)
                return k;
            return Keys.None;
        }

        // Texto para guardar: "Win+Shift+A".
        public override string ToString()
        {
            return string.Join("+", Parts(SaveNames));
        }

        // Piezas para dibujar como teclas: { "Win", "Shift", "A" }.
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
