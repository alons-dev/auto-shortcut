using System;
using System.Collections.Generic;
using System.IO;

namespace AutoShortcut
{
    internal enum ViewTab { All, Recent, Favorites, Custom, Windows11 }

    // Something the pop-up can show and run: one of the user's shortcuts or a
    // built-in Windows one. Same shape for both, so the list doesn't care which.
    internal sealed class Entry
    {
        public string Ref;           // "custom:<name>" or "win:<id>" (see Config)
        public string Name;
        public string Section;       // "Custom" or a WindowsShortcuts section
        public string[] Keycaps;     // keys to draw, or null
        public string Target;        // gray text instead of keycaps (launch shortcuts)
        public string Also;          // other combinations for the same action
        public string Note;          // why it can't be run (reference only)
        public string Detail;        // what it does, for the tooltip ("Opens notepad.exe")
        public string DefaultKeys;   // Windows shortcuts that run: their default combination
        public Shortcut Shortcut;    // what runs on click; null = reference only
        public int CustomIndex = -1; // position in Config.Shortcuts, -1 if built in
        internal string SearchText;  // lowercase text the search looks in

        public bool Runnable { get { return Shortcut != null; } }

        internal Entry MemberwiseCloneEntry() { return (Entry)MemberwiseClone(); }
        public bool IsCustom { get { return CustomIndex >= 0; } }
    }

    // One line of the list: a section header or an entry.
    internal sealed class ListItem
    {
        public string Header;
        public Entry Entry;

        public static ListItem ForHeader(string text) { ListItem i = new ListItem(); i.Header = text; return i; }
        public static ListItem ForEntry(Entry e) { ListItem i = new ListItem(); i.Entry = e; return i; }
    }

    // Turns the config + the built-in catalog into what each tab shows.
    internal static class Library
    {
        public const string CustomSection = "Custom";

        private static List<Entry> _catalog;
        private static Dictionary<string, Entry> _catalogByRef;

        public static IList<Entry> Catalog()
        {
            if (_catalog == null)
            {
                List<Entry> list = new List<Entry>();
                Dictionary<string, Entry> byRef = new Dictionary<string, Entry>();
                foreach (CatalogItem item in WindowsShortcuts.All)
                {
                    Entry e = FromCatalog(item);
                    list.Add(e);
                    byRef[e.Ref] = e;
                }
                _catalogByRef = byRef;
                _catalog = list;
            }
            return _catalog;
        }

        public static List<Entry> Customs(Config c)
        {
            List<Entry> list = new List<Entry>();
            for (int i = 0; i < c.Shortcuts.Count; i++) list.Add(FromCustom(c.Shortcuts[i], i));
            return list;
        }

        // null if the ref points to nothing (unknown id, deleted shortcut).
        public static Entry Find(Config c, string reference)
        {
            if (reference == null) return null;
            if (reference.StartsWith("win:"))
            {
                Catalog();
                Entry e;
                return _catalogByRef.TryGetValue(reference, out e) ? Moved(c, e) : null;
            }
            if (reference.StartsWith("custom:"))
            {
                string name = reference.Substring("custom:".Length);
                for (int i = 0; i < c.Shortcuts.Count; i++)
                    if (c.Shortcuts[i].Name == name) return FromCustom(c.Shortcuts[i], i);
            }
            return null;
        }

        // section: null = every section (Windows 11 tab). A query that isn't blank
        // searches everything and ignores tab and section.
        public static List<ListItem> View(Config c, ViewTab tab, string section, string query)
        {
            List<string> tokens = Tokens(query);
            if (tokens.Count > 0)
            {
                List<Entry> matches = new List<Entry>();
                foreach (Entry e in Customs(c)) if (Matches(e, tokens)) matches.Add(e);
                foreach (Entry e in Windows(c)) if (Matches(e, tokens)) matches.Add(e);
                return Grouped(matches);
            }

            switch (tab)
            {
                case ViewTab.Recent: return Resolved(c, c.Recent);
                case ViewTab.Favorites: return Resolved(c, c.Favorites);
                case ViewTab.Custom: return Plain(Customs(c));
                case ViewTab.Windows11:
                    if (section == null) return Grouped(Windows(c));
                    List<Entry> inSection = new List<Entry>();
                    foreach (Entry e in Windows(c)) if (e.Section == section) inSection.Add(e);
                    return Plain(inSection);
                default:
                    List<Entry> everything = Customs(c);
                    everything.AddRange(Windows(c));
                    return Grouped(everything);
            }
        }

        public static bool Matches(Entry e, string query)
        {
            return Matches(e, Tokens(query));
        }

        // The catalog as this config sees it: with the Windows shortcuts the user moved
        // to another combination.
        private static List<Entry> Windows(Config c)
        {
            List<Entry> list = new List<Entry>();
            foreach (Entry e in Catalog()) list.Add(Moved(c, e));
            return list;
        }

        // The catalog entry itself, or a copy showing the combination it was moved to.
        private static Entry Moved(Config c, Entry e)
        {
            string now = HotkeyRules.Current(c, e.DefaultKeys);
            KeyCombo combo;
            if (now == e.DefaultKeys || !KeyCombo.TryParse(now, out combo)) return e;
            KeyCombo original;
            KeyCombo.TryParse(e.DefaultKeys, out original);

            Entry copy = e.MemberwiseCloneEntry();
            copy.Keycaps = combo.DisplayParts();
            copy.Detail = "Windows default: " + string.Join(" + ", original.DisplayParts());
            copy.SearchText = SearchTextOf(copy);
            return copy;
        }

        // ---- Building entries ----

        private static Entry FromCatalog(CatalogItem item)
        {
            Entry e = new Entry();
            e.Ref = Config.WinRef(item.Id);
            e.Name = item.Name;
            e.Section = item.Section;
            e.Also = item.Also;
            e.Note = item.Note;
            KeyCombo combo;
            if (item.Runnable && KeyCombo.TryParse(item.Keys, out combo))
            {
                e.Keycaps = combo.DisplayParts();
                Shortcut s = new Shortcut();
                s.Name = item.Name;
                s.KeysText = combo.ToString();
                e.Shortcut = s;
                e.DefaultKeys = s.KeysText;
            }
            else
            {
                e.Keycaps = (item.Display ?? item.Keys ?? "").Split('+');
            }
            e.SearchText = SearchTextOf(e);
            return e;
        }

        private static Entry FromCustom(Shortcut s, int index)
        {
            Entry e = new Entry();
            e.Ref = Config.CustomRef(s.Name);
            e.Name = s.Name;
            e.Section = CustomSection;
            e.CustomIndex = index;
            e.Shortcut = s;
            KeyCombo combo;
            if (s.HasHotkey)
            {
                if (KeyCombo.TryParse(s.Hotkey, out combo)) e.Keycaps = combo.DisplayParts();
                else e.Target = "invalid hotkey";
                e.Detail = "Opens " + DescribeTarget(s.Run);
            }
            else if (s.IsLaunch)
            {
                e.Target = DescribeTarget(s.Run);
            }
            else if (KeyCombo.TryParse(s.KeysText, out combo))
            {
                e.Keycaps = combo.DisplayParts();
                e.Detail = "Presses " + string.Join(" + ", combo.DisplayParts());
            }
            else
            {
                e.Target = "invalid key combination";
            }
            e.SearchText = SearchTextOf(e);
            return e;
        }

        // "C:\x\y\app.exe" -> "app.exe", "https://www.github.com/x" -> "github.com".
        public static string DescribeTarget(string run)
        {
            Uri uri;
            if (Uri.TryCreate(run, UriKind.Absolute, out uri) && (uri.Scheme == "http" || uri.Scheme == "https"))
            {
                string host = uri.Host;
                return host.StartsWith("www.") ? host.Substring(4) : host;
            }
            if (run.IndexOf('\\') >= 0 || run.IndexOf('/') >= 0)
            {
                try
                {
                    string name = Path.GetFileName(run.TrimEnd('\\', '/'));
                    if (name.Length > 0) return name;
                }
                catch (ArgumentException) { }
            }
            return run;
        }

        // ---- Lists ----

        private static List<ListItem> Plain(List<Entry> entries)
        {
            List<ListItem> items = new List<ListItem>();
            foreach (Entry e in entries) items.Add(ListItem.ForEntry(e));
            return items;
        }

        // A header before each section (customs first, then catalog order).
        private static List<ListItem> Grouped(List<Entry> entries)
        {
            List<ListItem> items = new List<ListItem>();
            string current = null;
            foreach (Entry e in entries)
            {
                if (e.Section != current)
                {
                    current = e.Section;
                    items.Add(ListItem.ForHeader(current));
                }
                items.Add(ListItem.ForEntry(e));
            }
            return items;
        }

        private static List<ListItem> Resolved(Config c, List<string> refs)
        {
            List<ListItem> items = new List<ListItem>();
            foreach (string r in refs)
            {
                Entry e = Find(c, r);
                if (e != null) items.Add(ListItem.ForEntry(e));
            }
            return items;
        }

        // ---- Search ----

        private static readonly Dictionary<string, string> Synonyms = new Dictionary<string, string>
        {
            { "windows", "win" }, { "control", "ctrl" }, { "delete", "del" }, { "escape", "esc" },
        };

        private static List<string> Tokens(string query)
        {
            List<string> tokens = new List<string>();
            if (query == null) return tokens;
            foreach (string raw in query.ToLowerInvariant().Split(new char[] { ' ', '\t', '+' }, StringSplitOptions.RemoveEmptyEntries))
            {
                string t;
                tokens.Add(Synonyms.TryGetValue(raw, out t) ? t : raw);
            }
            return tokens;
        }

        private static bool Matches(Entry e, List<string> tokens)
        {
            foreach (string t in tokens)
                if (e.SearchText.IndexOf(t, StringComparison.Ordinal) < 0) return false;
            return true;
        }

        private static string SearchTextOf(Entry e)
        {
            string keys = e.Keycaps == null ? "" : string.Join(" ", e.Keycaps);
            return (e.Name + " " + e.Section + " " + keys + " " + e.Target + " " + e.Also + " " + e.Detail).ToLowerInvariant();
        }
    }
}
