using System.Collections.Generic;

namespace AutoShortcut
{
    // Who is asking when a combination is checked: it doesn't clash with itself.
    internal sealed class HotkeySelf
    {
        public bool App;                 // the combination that opens AutoShortcut
        public int Custom = -1;          // one of the user's shortcuts (index)
        public string WindowsDefault;    // a Windows shortcut, by its default combination
    }

    // A combination the app listens to (besides its own), and what it runs.
    internal sealed class Trigger
    {
        public string Combo;
        public string Name;
        public string Ref;
        public Shortcut Shortcut;   // a launch shortcut, or the Windows keys to press

        public string InUseMessage()
        {
            return Combo + " for \"" + Name + "\" is already used by another app";
        }
    }

    // The rules for hotkeys, kept free of windows so they can be tested.
    //
    // A combination belongs to, in this order: AutoShortcut itself, then one of your
    // shortcuts (its hotkey, or the keys of a reminder), then Windows (each built-in
    // shortcut with its current combination: the default, or the one you moved it to,
    // plus its other combinations). Whether another app has it is only known by
    // trying to register it, which TrayApp does.
    internal static class HotkeyRules
    {
        public static readonly HotkeySelf Nobody = new HotkeySelf();
        public static readonly HotkeySelf App = new HotkeySelf { App = true };

        public static HotkeySelf Custom(int index) { return new HotkeySelf { Custom = index }; }
        public static HotkeySelf Windows(string defaultCombo) { return new HotkeySelf { WindowsDefault = Normalize(defaultCombo) }; }

        // null if nothing uses it (or it isn't a valid combination: that is checked elsewhere).
        public static string UsedBy(Config c, string comboText, HotkeySelf self)
        {
            string combo = Normalize(comboText);
            if (combo == null) return null;

            if (!self.App && Normalize(c.Hotkey) == combo) return "Already used to open AutoShortcut";

            for (int i = 0; i < c.Shortcuts.Count; i++)
            {
                if (i == self.Custom) continue;
                Shortcut s = c.Shortcuts[i];
                string taken = s.IsLaunch ? (s.HasHotkey ? s.Hotkey : null) : s.KeysText;
                if (Normalize(taken) == combo) return "Already used by your shortcut \"" + s.Name + "\"";
            }

            foreach (CatalogItem item in WindowsShortcuts.All)
            {
                foreach (string own in OwnCombos(c, item, self.WindowsDefault))
                    if (own == combo) return "Already used by Windows: " + item.Name;
            }
            return null;
        }

        // Moves a Windows shortcut (all the ones with that default) to another combination.
        // Moving it back to its default forgets the change.
        public static void SetWindowsHotkey(Config c, string defaultCombo, string combo)
        {
            string from = Normalize(defaultCombo), to = Normalize(combo);
            if (from == null || to == null) return;
            if (from == to) c.WindowsHotkeys.Remove(from);
            else c.WindowsHotkeys[from] = to;
        }

        // Puts every Windows shortcut back on its default combination. Your shortcuts
        // whose hotkey is one of those lose it (they still open on click); their names
        // are returned. Reminders are left alone: the app doesn't listen to them.
        public static List<string> RestoreWindows(Config c)
        {
            c.WindowsHotkeys.Clear();
            HashSet<string> defaults = new HashSet<string>();
            foreach (CatalogItem item in WindowsShortcuts.All)
                foreach (string own in OwnCombos(c, item, null)) defaults.Add(own);

            List<string> lost = new List<string>();
            foreach (Shortcut s in c.Shortcuts)
            {
                if (!s.HasHotkey || !defaults.Contains(Normalize(s.Hotkey))) continue;
                s.Hotkey = null;
                lost.Add(s.Name);
            }
            return lost;
        }

        // What the app has to listen to: your shortcuts' hotkeys, then the Windows
        // shortcuts you moved. Invalid combinations are left out.
        public static List<Trigger> Triggers(Config c)
        {
            List<Trigger> list = new List<Trigger>();
            foreach (Shortcut s in c.Shortcuts)
            {
                if (!s.HasHotkey || Normalize(s.Hotkey) == null) continue;
                Trigger t = new Trigger();
                t.Combo = Normalize(s.Hotkey);
                t.Name = s.Name;
                t.Ref = Config.CustomRef(s.Name);
                t.Shortcut = s;
                list.Add(t);
            }
            foreach (KeyValuePair<string, string> pair in c.WindowsHotkeys)
            {
                CatalogItem item = FirstWithDefault(pair.Key);
                if (item == null) continue;   // not a Windows shortcut (edited by hand)
                Shortcut keys = new Shortcut();
                keys.Name = item.Name;
                keys.KeysText = pair.Key;
                Trigger t = new Trigger();
                t.Combo = pair.Value;
                t.Name = item.Name;
                t.Ref = Config.WinRef(item.Id);
                t.Shortcut = keys;
                list.Add(t);
            }
            return list;
        }

        // The combination a Windows default is on now.
        public static string Current(Config c, string defaultCombo)
        {
            string moved;
            return defaultCombo != null && c.WindowsHotkeys.TryGetValue(defaultCombo, out moved) ? moved : defaultCombo;
        }

        public static string Normalize(string text)
        {
            KeyCombo combo;
            return KeyCombo.TryParse(text, out combo) ? combo.ToString() : null;
        }

        private static CatalogItem FirstWithDefault(string combo)
        {
            foreach (CatalogItem item in WindowsShortcuts.All)
                if (item.Runnable && Normalize(item.Keys) == combo) return item;
            return null;
        }

        // The combinations a Windows shortcut uses now. skipDefault: the default being
        // edited, whose combinations don't count.
        private static List<string> OwnCombos(Config c, CatalogItem item, string skipDefault)
        {
            List<string> combos = new List<string>();
            if (item.Runnable)
            {
                string main = Normalize(item.Keys);
                if (main == null || main == skipDefault) return combos;
                combos.Add(Current(c, main));
                string also = Normalize(item.Also);
                if (also != null && also != skipDefault) combos.Add(Current(c, also));
            }
            else
            {
                string shown = Normalize(item.Display);   // "Win+L"; "Win+number" isn't one
                if (shown != null && shown != skipDefault) combos.Add(shown);
            }
            return combos;
        }
    }
}
