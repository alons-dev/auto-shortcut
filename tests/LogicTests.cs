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
            Test("ViewAllGrouping", ViewAllGrouping);
            Test("ViewWindowsSection", ViewWindowsSection);
            Test("ViewRecentAndFavorites", ViewRecentAndFavorites);
            Test("SearchFinds", SearchFinds);
            Test("OddQueries", OddQueries);
            Test("PruneDropsUnknownRefs", PruneDropsUnknownRefs);
            Test("AppIconFound", AppIconFound);
            Test("MigrationSurvivesReadOnlyFile", MigrationSurvivesReadOnlyFile);
            Test("WarnOnceUntilChanged", WarnOnceUntilChanged);

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

        // ---- Views and search ----

        private static List<string> Headers(List<ListItem> items)
        {
            List<string> result = new List<string>();
            foreach (ListItem item in items) if (item.Header != null) result.Add(item.Header);
            return result;
        }

        private static List<string> Refs(List<ListItem> items)
        {
            List<string> result = new List<string>();
            foreach (ListItem item in items) if (item.Entry != null) result.Add(item.Entry.Ref);
            return result;
        }

        private static void ViewAllGrouping()
        {
            Config c = Config.CreateDefault();
            List<ListItem> items = Library.View(c, ViewTab.All, null, null);
            Equal("first header", "Custom", items[0].Header);
            Equal("customs follow", "custom:Notepad,custom:Calculator,custom:Downloads folder,custom:GitHub",
                Join(Refs(items).GetRange(0, 4)));
            Equal("headers", "Custom," + string.Join(",", WindowsShortcuts.Sections), Join(Headers(items)));
            Equal("entry count", 4 + WindowsShortcuts.All.Count, Refs(items).Count);

            Config empty = new Config();
            Equal("no Custom header without customs", WindowsShortcuts.Sections[0],
                Library.View(empty, ViewTab.All, null, null)[0].Header);

            Entry notepad = items[1].Entry;
            Check("custom index", notepad.CustomIndex == 0 && notepad.IsCustom, "Notepad should be custom #0");
            Equal("custom section", "Custom", notepad.Section);
            Equal("launch target", "notepad.exe", notepad.Target);
            Check("custom runnable", notepad.Runnable, "Notepad should be runnable");
        }

        private static void ViewWindowsSection()
        {
            Config c = Config.CreateDefault();
            List<ListItem> items = Library.View(c, ViewTab.Windows11, "Desktops", null);
            Equal("no headers", 0, Headers(items).Count);
            Equal("count", 5, items.Count);
            foreach (string r in Refs(items))
                Check("desktops only " + r, r.StartsWith("win:desktops."), "unexpected " + r);

            List<ListItem> all = Library.View(c, ViewTab.Windows11, null, null);
            Equal("all sections headers", string.Join(",", WindowsShortcuts.Sections), Join(Headers(all)));

            Equal("custom tab", "custom:Notepad,custom:Calculator,custom:Downloads folder,custom:GitHub",
                Join(Refs(Library.View(c, ViewTab.Custom, null, null))));

            Entry copy = Library.Find(c, "win:text.copy");
            Equal("copy keycaps", "Ctrl,C", string.Join(",", copy.Keycaps));
            Equal("copy shortcut keys", "Ctrl+C", copy.Shortcut.KeysText);
            Entry lockPc = Library.Find(c, "win:winkey.lock");
            Check("lock not runnable", !lockPc.Runnable, "Win+L must be reference only");
            Equal("lock keycaps", "Win,L", string.Join(",", lockPc.Keycaps));
        }

        private static void ViewRecentAndFavorites()
        {
            Config c = Config.CreateDefault();
            c.Recent.Add("win:text.copy");
            c.Recent.Add("win:nope");
            c.Recent.Add("custom:Notepad");
            Equal("recent", "win:text.copy,custom:Notepad", Join(Refs(Library.View(c, ViewTab.Recent, null, null))));
            Equal("recent no headers", 0, Headers(Library.View(c, ViewTab.Recent, null, null)).Count);
            c.Favorites.Add("custom:GitHub");
            c.Favorites.Add("win:winkey.settings");
            Equal("favorites", "custom:GitHub,win:winkey.settings", Join(Refs(Library.View(c, ViewTab.Favorites, null, null))));
        }

        private static bool Found(string query, string reference)
        {
            return Refs(Library.View(Config.CreateDefault(), ViewTab.Recent, null, query)).Contains(reference);
        }

        private static void SearchFinds()
        {
            Check("win v", Found("win v", "win:winkey.clipboard-history"), "win v should find clipboard history");
            Check("COPY", Found("COPY", "win:text.copy"), "COPY should find text.copy");
            Check("ctrl insert", Found("ctrl insert", "win:text.copy"), "Also text is searchable");
            Check("control c", Found("control c", "win:text.copy"), "control = ctrl");
            Check("windows+e", Found("windows+e", "win:winkey.explorer"), "windows = win, + splits");
            Check("notepad", Found("notepad", "custom:Notepad"), "customs are searched");
            Check("section words", Found("taskbar settings", "win:taskbar.settings"), "section text searchable");
            Equal("nothing", 0, Refs(Library.View(Config.CreateDefault(), ViewTab.All, null, "zzzz")).Count);
            Check("search grouped", Headers(Library.View(Config.CreateDefault(), ViewTab.Recent, null, "copy")).Count > 0,
                "search results have section headers");
        }

        private static void OddQueries()
        {
            Config c = Config.CreateDefault();
            int all = Library.View(c, ViewTab.Custom, null, null).Count;
            foreach (string q in new string[] { "   ", "+", "++ " })
                Equal("blank-like '" + q + "'", all, Library.View(c, ViewTab.Custom, null, q).Count);
            foreach (string q in new string[] { "(", "*", "\\", "[", "\"" })
                Library.View(c, ViewTab.All, null, q);   // must not throw
        }

        private static void PruneDropsUnknownRefs()
        {
            Config c = Config.CreateDefault();
            c.Favorites.Add("win:nope");
            c.Favorites.Add("win:text.copy");
            c.Recent.Add("custom:Gone");
            c.Prune(r => Library.Find(c, r) != null);
            Equal("favorites", "win:text.copy", Join(c.Favorites));
            Equal("recent", "", Join(c.Recent));
        }

        // ---- App icon ----

        private static void AppIconFound()
        {
            string saved = Environment.GetEnvironmentVariable("AUTOSHORTCUT_HOME");
            try
            {
                string root = Environment.GetEnvironmentVariable("AUTOSHORTCUT_TEST_ROOT");   // set by run-tests.ps1
                Environment.SetEnvironmentVariable("AUTOSHORTCUT_HOME", root);
                string path = Ui.AppIconPath();
                Check("found", path != null && System.IO.File.Exists(path), "icon not found under " + root);
                Check("png", path != null && path.EndsWith("assets\\icon.png", StringComparison.OrdinalIgnoreCase), "unexpected path " + path);
                using (System.Drawing.Icon icon = Ui.CreateTrayIcon())
                    Check("tray icon built", icon != null && icon.Width > 0, "no tray icon");

                Environment.SetEnvironmentVariable("AUTOSHORTCUT_HOME", "C:\\does-not-exist");
                Equal("missing -> null", null, Ui.AppIconPath());
                using (System.Drawing.Icon icon = Ui.CreateTrayIcon())
                    Check("glyph fallback", icon != null, "fallback icon missing");
            }
            finally
            {
                Environment.SetEnvironmentVariable("AUTOSHORTCUT_HOME", saved);
            }
        }

        // A v0.1 file that can't be written (read-only, locked) must still load:
        // the migration is only saved when possible.
        private static void MigrationSurvivesReadOnlyFile()
        {
            string savedAppData = Environment.GetEnvironmentVariable("APPDATA");
            string dir = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "autoshortcut-test-" + Guid.NewGuid().ToString("N"));
            try
            {
                Environment.SetEnvironmentVariable("APPDATA", dir);
                System.IO.Directory.CreateDirectory(Config.Folder);
                System.IO.File.WriteAllText(Config.FilePath,
                    "{\"hotkey\":\"Win+Shift+A\",\"shortcuts\":[{\"name\":\"Mine\",\"keys\":\"Ctrl+K\"}]}");
                System.IO.File.SetAttributes(Config.FilePath, System.IO.FileAttributes.ReadOnly);
                Config c = Config.Load();
                Equal("customs kept", "Mine", Names(c));
                Equal("hotkey migrated in memory", "Win+Shift+Z", c.Hotkey);
            }
            finally
            {
                if (System.IO.File.Exists(System.IO.Path.Combine(dir, "AutoShortcut", "shortcuts.json")))
                    System.IO.File.SetAttributes(System.IO.Path.Combine(dir, "AutoShortcut", "shortcuts.json"), System.IO.FileAttributes.Normal);
                Environment.SetEnvironmentVariable("APPDATA", savedAppData);
                try { System.IO.Directory.Delete(dir, true); } catch (System.IO.IOException) { }
            }
        }

        private static void WarnOnceUntilChanged()
        {
            WarnOnce w = new WarnOnce();
            Check("first time", w.ShouldWarn("Win+Shift+Z in use"), "should warn the first time");
            Check("same again", !w.ShouldWarn("Win+Shift+Z in use"), "must not repeat the same warning");
            Check("different", w.ShouldWarn("Invalid shortcut"), "a different problem warns");
            w.Clear();   // the hotkey worked
            Check("after clear", w.ShouldWarn("Invalid shortcut"), "warns again after it was fixed and broke again");
        }

        private static void BadRefListsIgnored()
        {
            Config c = Config.Parse("{\"hotkey\":\"Ctrl+Alt+K\",\"shortcuts\":[],\"favorites\":\"oops\",\"recent\":[1,null,\"win:text.copy\"]}");
            Equal("favorites", 0, c.Favorites.Count);
            Equal("recent", "win:text.copy", Join(c.Recent));
        }
    }
}
