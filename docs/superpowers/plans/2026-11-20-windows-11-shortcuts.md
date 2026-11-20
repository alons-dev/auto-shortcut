# Windows 11 Shortcuts, Tabs, Favorites and Search Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Ship a built-in, searchable Windows 11 shortcut catalog with tabs (All · Recent · Favorites · Custom · Windows 11), section chips, favorites, recents and Win+Shift+Z as the default hotkey.

**Architecture:** Catalog data lives in a static C# file. A small model layer (`Entry`, `Library`, search) turns config + catalog into list items for a view (tab, section, query). The pop-up gets a tab strip, a chip bar, a header search box and a single owner-drawn, virtualized list that replaces the per-row controls. `TrayApp` keeps owning config changes (recents, favorites, custom edits).

**Tech Stack:** C# 5 (old csc / Add-Type: no `$""`, no `?.`, no expression-bodied members), WinForms, .NET Framework 4.5+, Windows PowerShell 5.1.

**Spec:** `docs/superpowers/specs/2026-11-20-windows-11-shortcuts-design.md`

## Global Constraints

- C# 5 syntax only; files compile both with `build.ps1` (csc) and `run.ps1` (Add-Type).
- `.ps1` files ASCII-only. `.cs` files UTF-8.
- All UI text in English. Catalog names are our own wording, sentence case, aim ≤ 32 chars; never Microsoft's text verbatim.
- Exclude the "Windows Copilot keyboard shortcuts" section.
- Catalog ids are permanent once released (`section.action` pattern, e.g. `text.copy`).
- Default hotkey `Win+Shift+Z`; a saved `Win+Shift+A` migrates to `Win+Shift+Z`.
- Recent keeps 10 entries. Refs are `win:<id>` / `custom:<name>`.
- Pop-up width 440 logical px (`Ui.S`), list max height 560 px capped at 75% of the work area.
- Native Windows 11 look: theme colors from `Theme`, Segoe UI, Fluent glyphs (`Ui.Glyph*`).
- No AI attribution in commits.

## Review Focus

1. A v0.1 `shortcuts.json` (no `favorites`/`recent`, hotkey `Win+Shift+A`, user's own shortcuts) must load, migrate the hotkey, keep every custom shortcut. → Task 2 test `LegacyFileMigrates`.
2. Renaming or deleting a favorited / recent custom shortcut must carry or drop its refs, never leave a dead star. → Task 2 tests `RenameMovesRefs`, `DeleteDropsRefs`.
3. Hand-edited JSON with `favorites` not a list, non-string items or unknown refs must not crash and unknown refs vanish on save. → Task 2 test `BadRefListsIgnored`, Task 4 test `PruneDropsUnknownRefs`.
4. Search input of only spaces, `+`, or punctuation must not crash; blank query means "no search". → Task 4 test `OddQueries`.
5. With a broken `shortcuts.json` (load error), running a shortcut must not prompt or overwrite the file to store a recent. → Task 8 step "broken file" check.

---

### Task 1: Logic test harness + new key names

**Files:**
- Create: `tests/run-tests.ps1`, `tests/LogicTests.cs`
- Modify: `src/KeyCombo.cs` (Aliases, SaveNames, ShowNames)

**Interfaces:**
- Produces: `public static class AutoShortcut.Tests.LogicTests { public static int Run(); }` returns failure count and prints `PASS name` / `FAIL name: detail` lines and a summary `N passed, M failed`. Helpers `Check(string name, bool ok, string detail)` and `Equal(string name, object expected, object actual)`. Later tasks add test methods and call them from `Run()`.
- `tests/run-tests.ps1`: throws if run on PowerShell 7; compiles `src\*.cs` + `tests\*.cs` with `Add-Type -ReferencedAssemblies System.Windows.Forms, System.Drawing, System.Web.Extensions -WarningAction SilentlyContinue`, calls `[AutoShortcut.Tests.LogicTests]::Run()`, `exit` with the failure count. ASCII only.
- KeyCombo accepts (case-insensitive): `Plus`→`Keys.Oemplus`, `NumPlus`→`Keys.Add`, `Minus`→`Keys.OemMinus`, `Comma`→`Keys.Oemcomma`, `Period`→`Keys.OemPeriod`, `Slash`→`Keys.OemQuestion`, `Semicolon`→`Keys.OemSemicolon`, `Pause` (enum name, already parses). Display: `+`, `Num +`, `-`, `,`, `.`, `/`, `;`, `Pause`. Save names: `Plus`, `NumPlus`, `-`, `,`, `.`, `Slash`, `Semicolon`.

- [ ] **Step 1: Write failing tests** `KeyNames`: `TryParse("Win+Plus")` → DisplayParts `Win,+`; `"Ctrl+NumPlus"` → `Ctrl,Num +`; `"Win+Slash"` → `Win,/`, ToString `Win+Slash`; `"Win+Semicolon"` → `Win,;`; `"Win+Pause"` parses; `"Win+Comma"` → ToString `Win+,`; existing `"Win+."` still parses.
- [ ] **Step 2: Run** `powershell -NoProfile -ExecutionPolicy Bypass -File tests\run-tests.ps1` → Expected: FAIL lines for the new names, exit code > 0.
- [ ] **Step 3: Add the aliases and names** in `src/KeyCombo.cs`.
- [ ] **Step 4: Run tests** → Expected: `… 0 failed`, exit 0. Also `.\build.ps1` → `OK -> …`.
- [ ] **Step 5: Commit** `git add tests src/KeyCombo.cs` → `git commit -m "Add logic tests and more key names"`

### Task 2: Config — default hotkey, migration, defaults, favorites and recents

**Files:**
- Modify: `src/Config.cs`, `tests/LogicTests.cs`

**Interfaces:**
- `Config.DefaultHotkey = "Win+Shift+Z"`; `const string OldDefaultHotkey = "Win+Shift+A"`; `const int MaxRecent = 10`.
- `public List<string> Favorites`, `public List<string> Recent` (never null).
- `public bool Migrated` — set by `Parse` when the hotkey normalizes (via `KeyCombo`) to `Win+Shift+A` and was replaced; `Load()` saves when `Migrated`.
- `public bool IsFavorite(string reference)`; `public void ToggleFavorite(string reference)` (append at end / remove); `public void AddRecent(string reference)` (move to front, dedupe, cap `MaxRecent`); `public void RenameCustom(string oldName, string newName)` (rewrites `custom:old` refs); `public void RemoveCustom(string name)` (drops `custom:name` refs unless another custom shortcut still has that name); `public void Prune(Predicate<string> exists)`.
- `public static string CustomRef(string name)` → `"custom:" + name`; `public static string WinRef(string id)` → `"win:" + id`.
- JSON: `"favorites": [...]` and `"recent": [...]` after `"shortcuts"`, each on one line; non-list values or non-string items are ignored on read.
- `CreateDefault()`: only Notepad (`notepad.exe`), Calculator (`calc.exe`), Downloads folder (`shell:Downloads`), GitHub (`https://github.com`).
- Update the header comment example to `Win+Shift+Z`.

- [ ] **Step 1: Write failing tests:**
  - `DefaultHotkeyIsZ`: `new Config().Hotkey == "Win+Shift+Z"`; `CreateDefault()` names = `Notepad,Calculator,Downloads folder,GitHub`.
  - `LegacyFileMigrates`: parse `{"hotkey":"win+shift+a","shortcuts":[{"name":"Mine","keys":"Ctrl+K"}]}` → `Hotkey == "Win+Shift+Z"`, `Migrated`, 1 shortcut, empty Favorites/Recent. Parse with `"Ctrl+Alt+K"` → unchanged, `!Migrated`.
  - `RefsRoundTrip`: set Favorites `win:text.copy, custom:Mine`, Recent `win:winkey.settings`; `Parse(c.ToJson())` gives the same lists.
  - `RecentCapAndOrder`: AddRecent `a`..`l` (12) then `c` → count 10, first `c`, no duplicates.
  - `ToggleFavoriteTwice`: toggle `x` → favorite, toggle again → not.
  - `RenameMovesRefs`: favorite+recent `custom:A`, `RenameCustom("A","B")` → both lists hold `custom:B`.
  - `DeleteDropsRefs`: two customs named `A` and `A`; `RemoveCustom("A")` after removing one keeps refs; after removing both, refs gone.
  - `BadRefListsIgnored`: `"favorites": "oops", "recent": [1, null, "win:text.copy"]` → Favorites empty, Recent = `win:text.copy`.
- [ ] **Step 2: Run tests** → Expected: the new tests FAIL.
- [ ] **Step 3: Implement** the members above in `src/Config.cs`.
- [ ] **Step 4: Run tests** → Expected: 0 failed. `.\build.ps1` OK.
- [ ] **Step 5: Commit** `git commit -am "Config: Win+Shift+Z, favorites and recents"`

### Task 3: Windows 11 catalog

**Files:**
- Create: `src/WindowsShortcuts.cs`
- Modify: `tests/LogicTests.cs`

**Interfaces:**
- `internal sealed class CatalogItem { public string Id, Section, Name, Keys, Display, Also, Note; public bool Runnable { get { return Keys != null; } } }`
- `internal static class WindowsShortcuts { public static readonly string[] Sections; public static readonly IList<CatalogItem> All; }`
- `Sections` in this order: `Text editing`, `General`, `Windows key`, `Command Prompt`, `Dialog boxes`, `File Explorer`, `Desktops`, `Taskbar`, `Settings`.
- Id prefixes per section: `text.`, `general.`, `winkey.`, `cmd.`, `dialog.`, `explorer.`, `desktops.`, `taskbar.`, `settings.`.
- Private builders: `K(section, id, name, keys, also)` for runnable; `R(section, id, name, display, note)` for reference-only (`Keys = null`, `Display` uses `+` between keycaps, e.g. `Ctrl+drag`, `Win+0-9`).
- Notes (exact copy, one per reference-only entry):
  - `Reference only: needs the mouse` — mouse drag / click / scroll rows.
  - `Reference only: add a number` — `Ctrl+1, 2, 3`, `Win+number`, `Ctrl+Number (1-9)` and similar ranges.
  - `Reference only: use any arrow key` — rows that say "arrow keys" without a direction.
  - `Reference only: press Alt and the underlined letter` — `Alt+underlined letter`, `Alt+selection key`.
  - `Reference only: Windows reserves it` — `Ctrl+Alt+Del`, `Win+L`.
  - `Reference only: works on the sign-in screen` — `Alt+F8`.
  - `Reference only: a key on its own can't be sent` — `Windows key` alone, `Ctrl+Shift` alone.
- Content: every row of the Microsoft Windows 11 tables for the 9 sections (source rows extracted to the session scratchpad `ms/w11-en.txt`), one entry per row, in page order. Two combos for one action → first in `Keys`, the rest in `Also` (e.g. Copy: `Ctrl+C`, Also `Ctrl+Insert`). Context in parentheses goes in the name (e.g. "Go to buffer start (Mark mode)"). `Ctrl+Plus` in File Explorer is runnable as `Ctrl+NumPlus`.

- [ ] **Step 1: Write failing tests** `CatalogShape`: Sections.Length == 9; every Section is in `Sections`; ids unique and match `^[a-z]+\.[a-z0-9-]+$` and start with their section prefix; every runnable Keys parses with `KeyCombo.TryParse`; every reference-only has Display and Note; names non-empty, ≤ 40 chars; no item text contains "Copilot"; total count between 200 and 240; contains `winkey.clipboard-history` with Keys `Win+V`, `text.copy` with Also containing `Ctrl+Insert`, `general.task-manager` with Keys `Ctrl+Shift+Escape`.
- [ ] **Step 2: Run tests** → FAIL (type missing → compile error counts as failure).
- [ ] **Step 3: Write `src/WindowsShortcuts.cs`** with all entries.
- [ ] **Step 4: Run tests** → 0 failed. `.\build.ps1` OK.
- [ ] **Step 5: Commit** `git add src/WindowsShortcuts.cs tests/LogicTests.cs` → `git commit -m "Add the Windows 11 shortcut catalog"`

### Task 4: Entries, views and search

**Files:**
- Create: `src/Library.cs`
- Modify: `tests/LogicTests.cs`

**Interfaces:**
- `internal enum ViewTab { All, Recent, Favorites, Custom, Windows11 }`
- `internal sealed class Entry { public string Ref, Name, Section, Target, Also, Note; public string[] Keycaps; public Shortcut Shortcut; public int CustomIndex = -1; public bool Runnable { get { return Shortcut != null; } } public bool IsCustom { get { return CustomIndex >= 0; } } }` — `Shortcut` is the config shortcut for customs and a new `Shortcut { Name, KeysText = Keys }` for runnable catalog items. `Keycaps` from `KeyCombo.DisplayParts()` or `Display.Split('+')`; `Target` (gray text) for launch customs and `"invalid key combination"` for unparseable customs (logic moved from `ShortcutRow`). Customs get `Section = "Custom"`.
- `internal sealed class ListItem { public string Header; public Entry Entry; }` (exactly one set).
- `internal static class Library`:
  - `public static List<Entry> Customs(Config c)`
  - `public static IList<Entry> Catalog()` (built once, cached)
  - `public static Entry Find(Config c, string reference)` (null if unknown)
  - `public static List<ListItem> View(Config c, ViewTab tab, string section, string query)` — `section == null` means all sections; non-blank `query` ignores tab and section.
  - `public static bool Matches(Entry e, string query)`
- View rules: All = header `Custom` + customs (only if any), then per section header + entries. Windows11 + null section = headers + all catalog; Windows11 + section = that section's entries, no header. Recent / Favorites = refs resolved in list order, unknown skipped, no headers. Custom = customs, no header. Search = same grouping as All over matching entries.
- Matching: lowercase; tokens split on whitespace and `+`, empty tokens dropped; token synonyms `windows→win`, `control→ctrl`, `delete→del`, `escape→esc`; each token must be a substring of `name + " " + section + " " + string.Join(" ", keycaps) + " " + also` (lowercased). No tokens → `Matches` returns true and `View` treats the query as blank.

- [ ] **Step 1: Write failing tests:**
  - `ViewAllGrouping`: default config → first item Header `Custom`, then 4 customs; 9 more headers in `Sections` order.
  - `ViewWindowsSection`: `View(c, Windows11, "Desktops", null)` → no headers, all ids start `desktops.`.
  - `ViewRecentAndFavorites`: Recent `win:text.copy, win:nope, custom:Notepad` → 2 entries in that order.
  - `SearchFinds`: `"win v"` includes `win:winkey.clipboard-history`; `"COPY"` includes `win:text.copy`; `"ctrl insert"` includes `win:text.copy`; `"control c"` includes `win:text.copy`; `"notepad"` includes `custom:Notepad`; `"zzzz"` → 0 entries.
  - `OddQueries`: `"   "`, `"+"`, `"++ "` → same as no query; `"("`, `"*"`, `"\\"` don't throw.
  - `PruneDropsUnknownRefs`: Favorites `win:nope, win:text.copy` → `c.Prune(r => Library.Find(c, r) != null)` leaves `win:text.copy`.
- [ ] **Step 2: Run tests** → FAIL.
- [ ] **Step 3: Implement `src/Library.cs`.**
- [ ] **Step 4: Run tests** → 0 failed. `.\build.ps1` OK.
- [ ] **Step 5: Commit** `git commit -am "Add entries, views and search"` (after `git add src/Library.cs`).

### Task 5: Owner-drawn shortcut list

**Files:**
- Create: `src/ShortcutList.cs`
- Delete: `src/ShortcutRow.cs` (drawing moves here)
- Modify: `src/Ui.cs` (glyphs)

**Interfaces:**
- `Ui`: `GlyphSearch` E721, `GlyphStar` E734, `GlyphStarFilled` E735, `GlyphClose` E711, `GlyphLeft` E76B, `GlyphRight` E76C.
- `internal sealed class ShortcutList : ScrollableControl`:
  - `public ShortcutList(Theme theme)`; `public void SetItems(IList<ListItem> items, string emptyText)`; `public void ScrollToTop()`; `public Predicate<Entry> IsFavorite`.
  - Events: `public event Action<Entry> EntryClicked` (left click on a runnable entry, outside the star), `public event Action<Entry> StarClicked`, `public event Action<Entry, Point> EntryContextMenu` (screen point).
- Behavior: row height 40, header height 32 (Segoe UI Semibold 9pt, `Theme.Subtle`); paints only visible items; `AutoScrollMinSize` drives the native vertical scrollbar (dark via `SetWindowTheme` as today); mouse wheel scrolls. Row drawing as today's `ShortcutRow` (hover/pressed rounded background, keycaps right, gray target, ellipsis). Star: outline on hover, filled + accent when favorite, 28×28 hit box left of keycaps/target. Reference-only: keycaps text in `Theme.Subtle`, arrow cursor, no pressed state; hand cursor on runnable rows. Tooltip (`ToolTip`, shown on hover after the usual delay): `Also: <also>` and/or the note. Empty: centered `emptyText` in `Theme.Subtle`.
- Accessibility: override `CreateAccessibilityInstance` so each visible item is a child with Name (entry name or header text), Role `ListItem` / `StaticText`, `Bounds` in screen coords and `DoDefaultAction` = click. The UI test finds rows by name.

- [ ] **Step 1: Implement `ShortcutList` and glyphs; remove `ShortcutRow.cs`.** (No logic test: verified through the UI in Task 7/8.)
- [ ] **Step 2: Build** `.\build.ps1` → OK (PopupForm still references ShortcutRow until Task 7: do Task 5's removal of `ShortcutRow.cs` together with Task 7 if the build breaks — commit Tasks 5–7 together if needed).
- [ ] **Step 3: Run logic tests** → 0 failed.
- [ ] **Step 4: Commit** `git add -A src` → `git commit -m "Add owner-drawn shortcut list"`

### Task 6: Tab strip and chip bar

**Files:**
- Create: `src/TabStrip.cs`, `src/ChipBar.cs`

**Interfaces:**
- `internal sealed class TabStrip : Control` — `public TabStrip(Theme theme, string[] labels)`, `public int Selected { get; set; }` (setter raises nothing), `public event Action<int> SelectedChanged` (user clicks). Child controls per tab (custom-painted, `AccessibleName` = label, `AccessibleRole.PageTab`). Look: Segoe UI 9.75pt; active = `Theme.Foreground` + 3 px rounded accent underline, 16 px wide centered; inactive = `Theme.Subtle`; hover background like rows. Height 36.
- `internal sealed class ChipBar : Control` — `public ChipBar(Theme theme, string[] labels)`, `public int Selected`, `public event Action<int> SelectedChanged`. Child chip controls (`AccessibleName` = label, `AccessibleRole.RadioButton`), pill radius = height/2, height 28; selected = `Theme.Accent` fill with white text; others = `Theme.KeyFill` + `Theme.KeyBorder`. Horizontal scroll: mouse wheel and `IconButton` chevrons (`GlyphLeft`/`GlyphRight`, names `Scroll left`/`Scroll right`) visible only when chips overflow; selecting a chip scrolls it into view. Height 40.

- [ ] **Step 1: Implement both controls.**
- [ ] **Step 2: Build** → OK. Logic tests → 0 failed.
- [ ] **Step 3: Commit** `git add src/TabStrip.cs src/ChipBar.cs` → `git commit -m "Add tab strip and chip bar"`

### Task 7: Pop-up layout, tabs, chips and search

**Files:**
- Modify: `src/PopupForm.cs`

**Interfaces:**
- Consumes: `ShortcutList`, `TabStrip`, `ChipBar`, `Library.View`, `ViewTab`, `WindowsShortcuts.Sections`, `InputBox`, `IconButton`.
- Replace `SetShortcuts` with `public void SetData(Config config, string hotkeyText)` — keeps the current tab, chip, search text and scroll position unless the view changed.
- `public void SelectTab(ViewTab tab)`.
- Events: `public event Action<Entry> EntryInvoked`, `public event Action<Entry> FavoriteToggled`, `AddRequested`, `SettingsRequested`, `EditRequested(int customIndex)`, `DeleteRequested(int customIndex)`, `MoveRequested(int customIndex, int delta)`.
- Layout (logical px): width 440; header 48 (title, hotkey label, then `Search` IconButton E721 tooltip `Search (or just start typing)`, `Add shortcut`, `Settings`); tabs row 36 labels `All`, `Recent`, `Favorites`, `Custom`, `Windows 11`; chip row 40 only on Windows 11 tab and not searching, labels `All sections` + `WindowsShortcuts.Sections`; list fills the rest. Total height fixed = header + tabs + chips(40, always reserved so the pop-up doesn't jump) + list max (`min(560, 75% of work area)`) + 8.
- Search mode: title, hotkey label and the three header buttons hide; an `InputBox` (accessible name `Search`) and a close `IconButton` (`GlyphClose`, name `Close search`) take the header; tabs stay visible but dimmed (no tab selected look change needed); list shows `Library.View(config, tab, chip, query)`; no results text `No shortcuts match "<query>".`
- Opening search: Search button, or a letter/digit `KeyPress` while not searching (the char becomes the initial text, caret at end).
- Esc: search open → close search; else hide pop-up. Hiding the pop-up closes the search.
- Empty texts: Recent `Shortcuts you run will show up here.`, Favorites `Star a shortcut to keep it here.`, Custom `No shortcuts yet. Add one with +`.
- Context menu: customs → `Edit`, `Move up`/`Move down` (Custom tab only, enabled by position), separator, `Add to favorites`/`Remove from favorites`, separator, `Delete`; catalog → only the favorite item. Menu events raised with `BeginInvoke` as today.
- First open: tab All. Tab and chip are remembered while the app runs; `ShowPopup(true)` scrolls the list to the top.

- [ ] **Step 1: Rewrite `PopupForm` per above.**
- [ ] **Step 2: Build** `.\build.ps1` → OK (TrayApp updated in Task 8; do both before building if needed).
- [ ] **Step 3: Commit together with Task 8.**

### Task 8: TrayApp wiring, installer, README, UI test

**Files:**
- Modify: `src/TrayApp.cs`, `install.ps1`, `README.md`
- Modify (session scratchpad, not in repo): `test-phase4.ps1`

**Interfaces:**
- `EntryInvoked(Entry e)`: hide pop-up, `ActionRunner.Execute(e.Shortcut, previousWindow)`, then `_config.AddRecent(e.Ref)` and save **only if `_loadError == null`** (no prompt).
- `FavoriteToggled(Entry e)`: `ConfirmOverwriteBrokenFile()` then `ToggleFavorite` + save; the pop-up stays open.
- Edit: after saving an edited custom whose name changed → `RenameCustom(old, new)`. Delete → `RemoveCustom(name)` after removing it. Add → save, then `_popup.SelectTab(ViewTab.Custom)`.
- Before every save: `_config.Prune(r => Library.Find(_config, r) != null)`.
- `ApplyConfig` calls `_popup.SetData(_config, label)`.
- `install.ps1`: `$Version = '0.2.0'`; final message default hotkey `Win+Shift+Z`, and if the read hotkey is `Win+Shift+A` show `Win+Shift+Z`.
- README: hotkey `Win+Shift+Z` everywhere; Use table rows for tabs, section chips, search (button or typing), favorites (star / right-click), recent; section "Built-in Windows 11 shortcuts" (source page link, 9 sections, reference-only rows); JSON example with `favorites` and `recent`; key list adds `Plus`, `NumPlus`, `Minus`, `Slash`, `Semicolon`, `Comma`, `Period`, `Pause`; file table adds `WindowsShortcuts.cs`, `Library.cs`, `ShortcutList.cs`, `TabStrip.cs`, `ChipBar.cs`, removes `ShortcutRow.cs`; Develop section adds `powershell -ExecutionPolicy Bypass -File .\tests\run-tests.ps1`.

- [ ] **Step 1: Implement TrayApp wiring, installer and README changes.**
- [ ] **Step 2: Build and logic tests** → `OK`, `0 failed`. `.ps1` ASCII check: `grep -nP '[^\x00-\x7F]' *.ps1 tests/*.ps1` → no output.
- [ ] **Step 3: Update the UI test** (`test-phase4.ps1`, run on a copy of `src` with the mutex renamed as before): Custom-tab flows as today (add → lands in Custom tab, record, save, move, delete, settings, hotkey); plus checks: tabs `All`/`Recent`/`Favorites`/`Custom`/`Windows 11` exist; clicking `Windows 11` shows chip `Text editing`; clicking chip `Desktops` shows `Create a new desktop`-style row (use the catalog name) and not a Text editing row; typing `clipboard` opens search and shows the clipboard history row; Esc closes search, second Esc hides; star a catalog row via right-click → `favorites` in JSON has its ref and the Favorites tab shows it; running a custom row adds `custom:<name>` to `recent`; broken file check: write invalid JSON, open pop-up, run a shortcut → file content unchanged and no message box.
- [ ] **Step 4: Run UI test** → every line `OK`.
- [ ] **Step 5: Screenshots** of All, Windows 11 + chip, search, Favorites (light check of the look).
- [ ] **Step 6: Commit** `git add -A` → `git commit -m "Tabs, favorites, recents and search in the pop-up"`; also commit the spec and this plan (`docs/superpowers`). Ask before pushing.
