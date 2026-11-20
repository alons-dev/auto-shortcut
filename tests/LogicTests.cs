using System;
using System.Collections.Generic;

namespace AutoShortcut.Tests
{
    // Logic tests: no windows, no keyboard. Run with tests\run-tests.ps1.
    // Each test is a method; Run() calls them all and returns the failure count.
    public static class LogicTests
    {
        private static int _passed;
        private static int _failed;

        public static int Run()
        {
            _passed = 0;
            _failed = 0;
            Test("KeyNames", KeyNames);
            Test("DefaultHotkeyIsZ", DefaultHotkeyIsZ);
            Test("LegacyFileMigrates", LegacyFileMigrates);
            Test("RefsRoundTrip", RefsRoundTrip);
            Test("RecentCapAndOrder", RecentCapAndOrder);
            Test("ToggleFavoriteTwice", ToggleFavoriteTwice);
            Test("RenameMovesRefs", RenameMovesRefs);
            Test("DeleteDropsRefs", DeleteDropsRefs);
            Test("BadRefListsIgnored", BadRefListsIgnored);
            Test("CatalogShape", CatalogShape);

            Console.WriteLine(_passed + " passed, " + _failed + " failed");
            return _failed;
        }

        private static void Test(string name, Action body)
        {
            int before = _failed;
            try
            {
                body();
            }
            catch (Exception ex)
            {
                Fail(name, "threw " + ex.GetType().Name + ": " + ex.Message);
            }
            if (_failed == before)
            {
                _passed++;
                Console.WriteLine("PASS " + name);
            }
        }

        private static void Fail(string name, string detail)
        {
            _failed++;
            Console.WriteLine("FAIL " + name + ": " + detail);
        }

        // Records a failure (the test keeps running so all problems show at once).
        private static void Check(string name, bool ok, string detail)
        {
            if (!ok) Fail(name, detail);
        }

        private static void Equal(string name, object expected, object actual)
        {
            if (!object.Equals(expected, actual))
                Fail(name, "expected <" + expected + "> but got <" + actual + ">");
        }

        // ---- KeyCombo ----

        private static string Display(string text)
        {
            KeyCombo combo;
            if (!KeyCombo.TryParse(text, out combo)) return "(invalid)";
            return string.Join(",", combo.DisplayParts());
        }

        private static string Saved(string text)
        {
            KeyCombo combo;
            return KeyCombo.TryParse(text, out combo) ? combo.ToString() : "(invalid)";
        }

        private static void KeyNames()
        {
            Equal("Win+Plus display", "Win,+", Display("Win+Plus"));
            Equal("Win+Plus saved", "Win+Plus", Saved("win+plus"));
            Equal("Ctrl+NumPlus display", "Ctrl,Num +", Display("Ctrl+NumPlus"));
            Equal("Ctrl+NumPlus saved", "Ctrl+NumPlus", Saved("Ctrl+NumPlus"));
            Equal("Win+Minus display", "Win,-", Display("Win+Minus"));
            Equal("Win+Slash display", "Win,/", Display("Win+Slash"));
            Equal("Win+Slash saved", "Win+Slash", Saved("Win+Slash"));
            Equal("Win+Semicolon display", "Win,;", Display("Win+Semicolon"));
            Equal("Win+Semicolon saved", "Win+Semicolon", Saved("Win+Semicolon"));
            Equal("Win+Pause display", "Win,Pause", Display("Win+Pause"));
            Equal("Win+Comma saved", "Win+,", Saved("Win+Comma"));
            Equal("Win+Period saved", "Win+.", Saved("Win+Period"));
            Equal("Win+. still parses", "Win,.", Display("Win+."));
        }

        // ---- Config ----

        private static string Join(IEnumerable<string> items)
        {
            return string.Join(",", items);
        }

        private static string Names(Config c)
        {
            List<string> names = new List<string>();
            foreach (Shortcut s in c.Shortcuts) names.Add(s.Name);
            return Join(names);
        }

        private static Shortcut Custom(string name)
        {
            Shortcut s = new Shortcut();
            s.Name = name;
            s.Run = "notepad.exe";
            return s;
        }

        private static void DefaultHotkeyIsZ()
        {
            Equal("new hotkey", "Win+Shift+Z", new Config().Hotkey);
            Equal("default customs", "Notepad,Calculator,Downloads folder,GitHub", Names(Config.CreateDefault()));
        }

        private static void LegacyFileMigrates()
        {
            Config c = Config.Parse("{\"hotkey\":\"win+shift+a\",\"shortcuts\":[{\"name\":\"Mine\",\"keys\":\"Ctrl+K\"}]}");
            Equal("migrated hotkey", "Win+Shift+Z", c.Hotkey);
            Check("Migrated flag", c.Migrated, "Migrated should be true");
            Equal("customs kept", "Mine", Names(c));
            Equal("no favorites", 0, c.Favorites.Count);
            Equal("no recent", 0, c.Recent.Count);

            Config other = Config.Parse("{\"hotkey\":\"Ctrl+Alt+K\",\"shortcuts\":[]}");
            Equal("other hotkey kept", "Ctrl+Alt+K", other.Hotkey);
            Check("not migrated", !other.Migrated, "Migrated should be false");
        }

        private static void RefsRoundTrip()
        {
            Config c = Config.CreateDefault();
            c.Favorites.Add("win:text.copy");
            c.Favorites.Add("custom:Mine");
            c.Recent.Add("win:winkey.settings");
            Config back = Config.Parse(c.ToJson());
            Equal("favorites", "win:text.copy,custom:Mine", Join(back.Favorites));
            Equal("recent", "win:winkey.settings", Join(back.Recent));
            Equal("refs", "custom:A b", Config.CustomRef("A b"));
            Equal("win ref", "win:text.copy", Config.WinRef("text.copy"));
        }

        private static void RecentCapAndOrder()
        {
            Config c = new Config();
            foreach (char ch in "abcdefghijkl") c.AddRecent(ch.ToString());
            c.AddRecent("c");
            Equal("count", Config.MaxRecent, c.Recent.Count);
            Equal("cap is 10", 10, Config.MaxRecent);
            Equal("first", "c", c.Recent[0]);
            Equal("order", "c,l,k,j,i,h,g,f,e,d", Join(c.Recent));
        }

        private static void ToggleFavoriteTwice()
        {
            Config c = new Config();
            c.ToggleFavorite("x");
            Check("starred", c.IsFavorite("x"), "x should be a favorite");
            c.ToggleFavorite("y");
            Equal("order kept", "x,y", Join(c.Favorites));
            c.ToggleFavorite("x");
            Check("unstarred", !c.IsFavorite("x"), "x should not be a favorite");
        }

        private static void RenameMovesRefs()
        {
            Config c = new Config();
            c.Shortcuts.Add(Custom("B"));
            c.ToggleFavorite("custom:A");
            c.AddRecent("custom:A");
            c.RenameCustom("A", "B");
            Equal("favorites", "custom:B", Join(c.Favorites));
            Equal("recent", "custom:B", Join(c.Recent));
        }

        private static void DeleteDropsRefs()
        {
            Config c = new Config();
            c.Shortcuts.Add(Custom("A"));
            c.Shortcuts.Add(Custom("A"));
            c.ToggleFavorite("custom:A");
            c.AddRecent("custom:A");
            c.Shortcuts.RemoveAt(0);
            c.RemoveCustom("A");
            Equal("one left: favorite kept", "custom:A", Join(c.Favorites));
            c.Shortcuts.RemoveAt(0);
            c.RemoveCustom("A");
            Equal("none left: favorite gone", "", Join(c.Favorites));
            Equal("none left: recent gone", "", Join(c.Recent));
        }

        // ---- Windows 11 catalog ----

        private static readonly string[] SectionPrefixes =
            { "text.", "general.", "winkey.", "cmd.", "dialog.", "explorer.", "desktops.", "taskbar.", "settings." };

        private static CatalogItem Item(string id)
        {
            foreach (CatalogItem item in WindowsShortcuts.All)
                if (item.Id == id) return item;
            return null;
        }

        private static void CatalogShape()
        {
            Equal("sections", "Text editing,General,Windows key,Command Prompt,Dialog boxes,File Explorer,Desktops,Taskbar,Settings",
                string.Join(",", WindowsShortcuts.Sections));
            int count = WindowsShortcuts.All.Count;
            Check("count", count >= 200 && count <= 240, "count " + count + " outside 200-240");

            HashSet<string> ids = new HashSet<string>();
            System.Text.RegularExpressions.Regex idPattern = new System.Text.RegularExpressions.Regex("^[a-z]+\\.[a-z0-9-]+$");
            foreach (CatalogItem item in WindowsShortcuts.All)
            {
                string who = item.Id ?? "(null id)";
                Check("unique " + who, ids.Add(who), "duplicate id");
                Check("id pattern " + who, idPattern.IsMatch(who), "bad id");
                int section = Array.IndexOf(WindowsShortcuts.Sections, item.Section);
                Check("section " + who, section >= 0, "unknown section " + item.Section);
                if (section >= 0) Check("prefix " + who, who.StartsWith(SectionPrefixes[section]), "id must start with " + SectionPrefixes[section]);
                Check("name " + who, !string.IsNullOrEmpty(item.Name) && item.Name.Length <= 40, "name empty or > 40: " + item.Name);
                if (item.Runnable)
                {
                    KeyCombo combo;
                    Check("keys " + who, KeyCombo.TryParse(item.Keys, out combo), "keys don't parse: " + item.Keys);
                }
                else
                {
                    Check("display " + who, !string.IsNullOrEmpty(item.Display), "reference-only needs Display");
                    Check("note " + who, item.Note != null && item.Note.StartsWith("Reference only: "), "reference-only needs a note");
                }
                string all = item.Name + " " + item.Keys + " " + item.Display + " " + item.Also + " " + item.Note;
                Check("no Copilot " + who, all.IndexOf("Copilot", StringComparison.OrdinalIgnoreCase) < 0, "mentions Copilot");
            }

            CatalogItem clip = Item("winkey.clipboard-history");
            Check("clipboard history exists", clip != null, "missing winkey.clipboard-history");
            if (clip != null) Equal("clipboard keys", "Win+V", clip.Keys);
            CatalogItem copy = Item("text.copy");
            Check("copy exists", copy != null, "missing text.copy");
            if (copy != null) Check("copy also", copy.Also != null && copy.Also.Contains("Ctrl+Insert"), "Also should list Ctrl+Insert");
            CatalogItem tm = Item("general.task-manager");
            Check("task manager exists", tm != null, "missing general.task-manager");
            if (tm != null) Equal("task manager keys", "Ctrl+Shift+Escape", tm.Keys);
        }

        private static void BadRefListsIgnored()
        {
            Config c = Config.Parse("{\"hotkey\":\"Ctrl+Alt+K\",\"shortcuts\":[],\"favorites\":\"oops\",\"recent\":[1,null,\"win:text.copy\"]}");
            Equal("favorites", 0, c.Favorites.Count);
            Equal("recent", "win:text.copy", Join(c.Recent));
        }
    }
}
