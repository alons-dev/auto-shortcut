using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Web.Script.Serialization;

namespace AutoShortcut
{
    // The configuration lives in %APPDATA%\AutoShortcut\shortcuts.json:
    //
    //   {
    //     "hotkey": "Win+Shift+Z",
    //     "shortcuts": [
    //       { "name": "Notepad", "run": "notepad.exe" },
    //       { "name": "Reopen closed tab", "keys": "Ctrl+Shift+T" }
    //     ],
    //     "favorites": ["win:winkey.clipboard-history", "custom:Notepad"],
    //     "recent": ["win:text.copy"]
    //   }
    //
    // Favorites and recents are "refs": "win:<id>" for a built-in Windows shortcut
    // (see WindowsShortcuts.cs) and "custom:<name>" for one of the user's shortcuts.
    //
    // Reading uses JavaScriptSerializer, which ships with .NET Framework
    // (System.Web.Extensions.dll). Writing builds the text by hand so the file
    // is indented with one shortcut per line, easy to edit.
    internal sealed class Config
    {
        public const string DefaultHotkey = "Win+Shift+Z";
        // The default up to 0.1. Windows 11 itself uses it ("focus a Windows tip"),
        // so a saved Win+Shift+A is moved to the new default.
        public const string OldDefaultHotkey = "Win+Shift+A";
        public const int MaxRecent = 10;

        public string Hotkey = DefaultHotkey;
        public List<Shortcut> Shortcuts = new List<Shortcut>();
        public List<string> Favorites = new List<string>();
        public List<string> Recent = new List<string>();

        // Set by Parse when the hotkey was moved off the old default: Load saves it.
        public bool Migrated;

        public static string CustomRef(string name) { return "custom:" + name; }
        public static string WinRef(string id) { return "win:" + id; }

        public bool IsFavorite(string reference)
        {
            return Favorites.Contains(reference);
        }

        public void ToggleFavorite(string reference)
        {
            if (!Favorites.Remove(reference)) Favorites.Add(reference);
        }

        // Newest first, no duplicates, at most MaxRecent.
        public void AddRecent(string reference)
        {
            Recent.Remove(reference);
            Recent.Insert(0, reference);
            if (Recent.Count > MaxRecent) Recent.RemoveRange(MaxRecent, Recent.Count - MaxRecent);
        }

        // Call after renaming a custom shortcut: its refs follow the new name.
        public void RenameCustom(string oldName, string newName)
        {
            if (oldName == newName) return;
            string from = CustomRef(oldName), to = CustomRef(newName);
            RenameIn(Favorites, from, to);
            RenameIn(Recent, from, to);
        }

        private static void RenameIn(List<string> refs, string from, string to)
        {
            int i = refs.IndexOf(from);
            if (i < 0) return;
            if (refs.Contains(to)) refs.RemoveAt(i);   // already there: don't duplicate
            else refs[i] = to;
        }

        // Call after deleting a custom shortcut: drops its refs unless another
        // custom shortcut still has that name.
        public void RemoveCustom(string name)
        {
            foreach (Shortcut s in Shortcuts)
                if (s.Name == name) return;
            string r = CustomRef(name);
            Favorites.Remove(r);
            Recent.Remove(r);
        }

        // Drops refs that no longer point to anything (e.g. edited by hand).
        public void Prune(Predicate<string> exists)
        {
            Favorites.RemoveAll(r => !exists(r));
            Recent.RemoveAll(r => !exists(r));
        }

        public static string Folder
        {
            get
            {
                string appData = Environment.GetEnvironmentVariable("APPDATA");
                if (string.IsNullOrEmpty(appData))
                    appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
                return Path.Combine(appData, "AutoShortcut");
            }
        }

        public static string FilePath
        {
            get { return Path.Combine(Folder, "shortcuts.json"); }
        }

        // The first time, creates the file with some example shortcuts.
        // If the file has an error, throws FormatException and leaves it untouched.
        public static Config Load()
        {
            if (!File.Exists(FilePath))
            {
                Config fresh = CreateDefault();
                fresh.Save();
                return fresh;
            }
            Config c = Parse(File.ReadAllText(FilePath, Encoding.UTF8));
            if (c.Migrated) c.Save();
            return c;
        }

        public void Save()
        {
            Directory.CreateDirectory(Folder);
            // Write to a temp file and then swap it in: if something fails halfway,
            // the good file is still there.
            string tmp = FilePath + ".tmp";
            File.WriteAllText(tmp, ToJson(), new UTF8Encoding(false));
            if (File.Exists(FilePath)) File.Replace(tmp, FilePath, null);
            else File.Move(tmp, FilePath);
        }

        public static Config CreateDefault()
        {
            // The Windows shortcuts come built in (WindowsShortcuts.cs): the
            // examples here are things only a custom shortcut can do.
            Config c = new Config();
            c.AddRun("Notepad", "notepad.exe");
            c.AddRun("Calculator", "calc.exe");
            c.AddRun("Downloads folder", "shell:Downloads");
            c.AddRun("GitHub", "https://github.com");
            return c;
        }

        private void AddRun(string name, string run)
        {
            Shortcut s = new Shortcut();
            s.Name = name;
            s.Run = run;
            Shortcuts.Add(s);
        }

        // ---- Reading ----

        public static Config Parse(string json)
        {
            object root;
            try
            {
                root = new JavaScriptSerializer().DeserializeObject(json);
            }
            catch (Exception ex)
            {
                throw new FormatException("invalid JSON (" + ex.Message + ")");
            }

            IDictionary<string, object> obj = root as IDictionary<string, object>;
            if (obj == null) throw new FormatException("it must start with { and end with }");

            Config c = new Config();
            string hotkey = GetString(obj, "hotkey");
            if (!string.IsNullOrEmpty(hotkey)) c.Hotkey = hotkey;
            KeyCombo saved;
            if (KeyCombo.TryParse(c.Hotkey, out saved) && saved.ToString() == OldDefaultHotkey)
            {
                c.Hotkey = DefaultHotkey;
                c.Migrated = true;
            }
            c.Favorites = GetStrings(obj, "favorites");
            c.Recent = GetStrings(obj, "recent");

            object list;
            if (obj.TryGetValue("shortcuts", out list) && list != null)
            {
                IList items = list as IList;
                if (items == null) throw new FormatException("\"shortcuts\" must be a list [ ... ]");
                foreach (object item in items)
                {
                    IDictionary<string, object> entry = item as IDictionary<string, object>;
                    if (entry == null) continue;

                    Shortcut s = new Shortcut();
                    s.Name = GetString(entry, "name");
                    s.KeysText = GetString(entry, "keys");
                    s.Run = GetString(entry, "run");
                    s.Args = GetString(entry, "args");
                    if (string.IsNullOrEmpty(s.Name)) s.Name = s.IsLaunch ? s.Run : s.KeysText;
                    if (string.IsNullOrEmpty(s.Name)) continue;   // empty entry
                    c.Shortcuts.Add(s);
                }
            }
            return c;
        }

        private static string GetString(IDictionary<string, object> obj, string key)
        {
            object value;
            if (!obj.TryGetValue(key, out value) || value == null) return null;
            string s = Convert.ToString(value, System.Globalization.CultureInfo.InvariantCulture).Trim();
            return s.Length == 0 ? null : s;
        }

        // A list of strings. Anything else (not a list, numbers, null) is ignored:
        // these lists are only conveniences, never a reason to reject the file.
        private static List<string> GetStrings(IDictionary<string, object> obj, string key)
        {
            List<string> result = new List<string>();
            object value;
            if (!obj.TryGetValue(key, out value)) return result;
            IList items = value as IList;
            if (items == null) return result;
            foreach (object item in items)
            {
                string s = item as string;
                if (!string.IsNullOrEmpty(s) && !result.Contains(s)) result.Add(s);
            }
            return result;
        }

        // ---- Writing ----

        public string ToJson()
        {
            StringBuilder sb = new StringBuilder();
            sb.Append("{\r\n");
            sb.Append("  \"hotkey\": ").Append(Quote(Hotkey)).Append(",\r\n");
            sb.Append("  \"shortcuts\": [");
            for (int i = 0; i < Shortcuts.Count; i++)
            {
                Shortcut s = Shortcuts[i];
                sb.Append(i == 0 ? "\r\n" : ",\r\n");
                sb.Append("    { \"name\": ").Append(Quote(s.Name));
                if (s.IsLaunch)
                {
                    sb.Append(", \"run\": ").Append(Quote(s.Run));
                    if (!string.IsNullOrEmpty(s.Args)) sb.Append(", \"args\": ").Append(Quote(s.Args));
                }
                else
                {
                    sb.Append(", \"keys\": ").Append(Quote(s.KeysText));
                }
                sb.Append(" }");
            }
            sb.Append(Shortcuts.Count == 0 ? "],\r\n" : "\r\n  ],\r\n");
            sb.Append("  \"favorites\": ").Append(QuoteList(Favorites)).Append(",\r\n");
            sb.Append("  \"recent\": ").Append(QuoteList(Recent)).Append("\r\n");
            sb.Append("}\r\n");
            return sb.ToString();
        }

        private static string QuoteList(List<string> items)
        {
            List<string> quoted = new List<string>();
            foreach (string s in items) quoted.Add(Quote(s));
            return "[" + string.Join(", ", quoted) + "]";
        }

        private static string Quote(string s)
        {
            if (s == null) return "\"\"";
            StringBuilder sb = new StringBuilder("\"");
            foreach (char ch in s)
            {
                switch (ch)
                {
                    case '"': sb.Append("\\\""); break;
                    case '\\': sb.Append("\\\\"); break;
                    case '\n': sb.Append("\\n"); break;
                    case '\r': sb.Append("\\r"); break;
                    case '\t': sb.Append("\\t"); break;
                    default:
                        if (ch < 0x20) sb.Append("\\u").Append(((int)ch).ToString("x4"));
                        else sb.Append(ch);
                        break;
                }
            }
            return sb.Append('"').ToString();
        }
    }
}
