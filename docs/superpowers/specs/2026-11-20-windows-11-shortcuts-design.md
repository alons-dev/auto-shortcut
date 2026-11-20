# Windows 11 shortcuts, tabs, favorites and search — design

Date: 2026-11-20 · Status: draft for approval

## Intent

Today AutoShortcut only shows the user's own list. The owner wants it to also be a
complete, browsable reference of Windows 11 shortcuts that works out of the box:
open the pop-up, find any Windows shortcut (by section or by typing), and click it
to run it. The user's own shortcuts, favorites and recently used ones stay one
click away.

Source of the catalog: Microsoft's page "Keyboard shortcuts in Windows", Windows 11
tab (https://support.microsoft.com/en-us/accessibility/windows/keyboard-shortcuts-in-windows),
**excluding the "Windows Copilot keyboard shortcuts" section**.

## Goals

1. Default hotkey becomes **Win+Shift+Z**.
2. The app ships a **fixed, built-in catalog** of the Windows 11 shortcuts, grouped in
   the same sections as the Microsoft page.
3. The pop-up gets **tabs**: All · Recent · Favorites · Custom · Windows 11. Inside
   Windows 11, a second row of **section chips**.
4. **Favorites**: any shortcut (built-in or custom) can be starred.
5. **Recent**: the last shortcuts run from the pop-up.
6. **Search**: a magnifier button opens a text search over everything.
7. It must keep looking like a native Windows 11 flyout (current dark/light theme,
   Segoe UI, Fluent icons, rounded corners).

## Non-goals

- Editing, renaming, hiding or deleting built-in shortcuts (they can only be starred).
- Windows 10 shortcuts, Copilot shortcuts, per-app catalogs (Word, Chrome...).
- Localizing the app (it stays English).
- Copying a built-in shortcut into the custom list.

## Anti-goals (failure even if it works)

- A pop-up that looks like a web page or a toolbar of big buttons instead of Windows 11.
- A noticeably slower pop-up: opening it or switching tab must still feel instant.
- Text copied verbatim from Microsoft. Names are our own short English wording.

## Decisions taken with the owner

- Catalog is **built into the app** (not copied into `shortcuts.json`). It updates when
  the app updates. `shortcuts.json` stores only custom shortcuts, favorites and recents.
- Layout **A**: underlined text tabs (like Settings/Photos in Windows 11) plus small
  pill chips for the sections, only on the Windows 11 tab.

## Approaches considered

| | Layout | Trade-off |
|---|---|---|
| **A (chosen)** | Underlined tabs + section chips | Closest to the owner's sketch while still native-looking; costs two rows of height |
| B | Pills for both rows | Matches the sketch literally; heavy, both levels look alike, less Windows-like |
| C | One row of tabs + "Section ▾" dropdown, grouped list | Most compact; two clicks to change section, further from the sketch |

Catalog storage: built-in (chosen) vs. copied into the user's JSON (editable, but 225
entries in the user's file and no updates).

## Design

### Pop-up layout

```
╭──────────────────────────────────────────────╮
│ Shortcuts   Win+Shift+Z            🔍  +  ⚙ │  header
│ All  Recent  Favorites  Custom  Windows 11   │  tabs (accent underline on active)
│                                 ──────────   │
│ (All sections)(Text editing)(General)(Win… › │  chips: only on Windows 11 tab
│ TEXT EDITING                                 │  section header row (All / search)
│ Copy                            ☆ [Ctrl][C]  │  ☆ shows on hover, ★ if favorite
│ Paste                           ☆ [Ctrl][V]  │
╰──────────────────────────────────────────────╯
```

- Width grows from 360 to about **440** logical px; list max height grows to about
  **560** px (still capped at 75% of the work area). Scrolls vertically.
- Header: title, hotkey hint (or "... in use" warning, as today), then **search**
  (Fluent glyph E721), **+** and **settings** buttons.
- Tabs: plain text, Segoe UI, active tab in normal foreground with a short accent
  underline, others in subtle color; hover highlight. Order:
  **All · Recent · Favorites · Custom · Windows 11**.
- Chips (Windows 11 tab only): "All sections" + the 9 sections. Rounded pills, the
  selected one filled with the accent color. The row scrolls horizontally (mouse wheel
  and small chevron buttons at the edges when it overflows).
- The pop-up remembers the last tab and chip while the app runs; first open shows **All**.

### What each tab shows

- **All**: custom shortcuts first under a "CUSTOM" header, then every catalog section
  under its own header (TEXT EDITING, GENERAL, ...). Duplicates that Microsoft lists in
  two sections (e.g. Win+E) appear in both.
- **Recent**: the last **10** shortcuts run from the pop-up, newest first, no
  duplicates. Empty state: "Shortcuts you run will show up here."
- **Favorites**: starred shortcuts in the order they were starred. Empty state:
  "Star a shortcut to keep it here."
- **Custom**: the user's own list, exactly like today's pop-up (right-click: Edit,
  Move up, Move down, Delete; + adds here). Empty state as today.
- **Windows 11**: catalog, filtered by the selected chip ("All sections" shows every
  section with headers).

### Sections (chip label → Microsoft section)

1. Text editing → Text editing keyboard shortcuts
2. General → Desktop and other general Windows keyboard shortcuts
3. Windows key → Windows key keyboard shortcuts
4. Command Prompt → Command prompt keyboard shortcuts
5. Dialog boxes → Dialog box keyboard shortcuts
6. File Explorer → File Explorer keyboard shortcuts
7. Desktops → Multiple desktops keyboard shortcuts
8. Taskbar → Taskbar keyboard shortcuts
9. Settings → Settings keyboard shortcuts

(About 225 entries in total. The "Additional keyboard shortcuts" heading has no table.)

### Catalog entries

New file `src/WindowsShortcuts.cs` with static data. Each entry:

- **Id**: stable, e.g. `text.copy`, `winkey.clipboard-history` (used by favorites/recents;
  must never change once released).
- **Section**.
- **Name**: our own short English wording, sentence case, aim for ≤ 32 characters
  (e.g. "Open clipboard history", "Snap window left").
- **Keys**: one runnable combination in KeyCombo syntax, or none.
- **Display**: what the keycaps show when it isn't runnable (e.g. `Ctrl` + `drag`,
  `Win` + `0–9`).
- **Also** (optional): alternative combinations Microsoft lists for the same action
  (e.g. Ctrl+Insert for Copy). Shown in the tooltip, searchable, not run.

When Microsoft gives two combinations for one action, the first one is runnable and the
rest go in **Also**.

**Reference-only entries** (not runnable, about 20): those that need the mouse
(Ctrl+drag, Shift+right-click...), ranges (Win+0–9, Ctrl+1,2,3...), "Alt+underlined
letter", modifier-only combos (Windows key alone, Ctrl+Shift), protected ones
(Ctrl+Alt+Del, Win+L) and Alt+F8 (only works on the sign-in screen). They show their
keycaps in a dimmer color, the cursor stays an arrow, and clicking does nothing; the
tooltip says why (e.g. "Reference only: needs the mouse").

KeyCombo must parse every runnable entry. New key names needed: `Plus` (numpad
Add for Ctrl+Plus; Oemplus for Win+Plus), `Minus`, `Slash` (`/`), `Semicolon` (`;`),
`Comma`, `Pause`. OEM keys are sent by virtual-key code, so they work on any keyboard
layout.

### Favorites and recents in `shortcuts.json`

```json
{
  "hotkey": "Win+Shift+Z",
  "shortcuts": [ ... ],
  "favorites": ["win:winkey.clipboard-history", "custom:Notepad"],
  "recent": ["win:text.copy", "custom:GitHub"]
}
```

- Refs are `win:<catalog id>` or `custom:<shortcut name>`. Renaming a custom shortcut
  updates its refs; deleting it removes them. Unknown refs (e.g. an id removed in a
  future version) are ignored and dropped on next save.
- Star: hovering a row shows an outline star (E734) left of the keycaps; a starred row
  always shows a filled star (E735). Clicking the star toggles it without running the
  shortcut. The right-click menu also gets "Add to favorites" / "Remove from favorites"
  (built-in rows get only this item).
- A shortcut is added to **recent** when it is run from the pop-up (a click on a
  reference-only row doesn't count).

### Search

- The magnifier button, or typing any letter/number while the pop-up has focus, replaces
  the title with a search box (InputBox style) focused, with an × to close it.
- Searches **everything** (custom + catalog), regardless of the active tab. Results
  are grouped with the same section headers as All.
- Match: case-insensitive; every word typed must appear in the name, section, keys or
  "Also" text. Key words match common spellings: "win"/"windows", "ctrl"/"control",
  "del"/"delete", "esc"/"escape".
- No results: "No shortcuts match "<text>"."
- **Esc** clears and closes the search; a second Esc closes the pop-up (today's behavior).
- Closing the pop-up resets the search.

### Hotkey

- `Config.DefaultHotkey` becomes `Win+Shift+Z` (new installs).
- Migration: if the saved hotkey is exactly `Win+Shift+A` (the old default, which
  Windows 11 itself uses for "focus a Windows tip"), it is changed to `Win+Shift+Z` and
  saved. Any other saved hotkey is kept.
- Installer final message and README use the saved hotkey as today.

### Default custom shortcuts (new installs only)

Notepad, Calculator, Downloads folder, GitHub. The Windows ones in today's defaults are
dropped because the catalog has them. Existing `shortcuts.json` files are not touched
(beyond the hotkey migration and the new fields).

### Smaller calls (made by Claude, owner can override)

- Recent keeps 10 entries; a run from any tab or from search counts.
- Right-click on a custom shortcut shows Edit / Delete / favorite toggle in every tab;
  Move up / Move down only in the Custom tab (it's the only tab in custom order).
- After adding a shortcut with **+**, the pop-up switches to the Custom tab.
- If two custom shortcuts share a name, `custom:<name>` refers to the first one.

### Other

- README: describe tabs, chips, search, favorites, recents, the built-in catalog, the new
  JSON fields, the new key names, and the Win+Shift+Z default.
- Accessibility/testing: tabs, chips, search box and star buttons have UIA names
  ("All", "Windows 11", "Text editing", "Search", "Add to favorites"...).
- Tests: every runnable catalog entry parses with KeyCombo; ids are unique; UI test
  covers tab switching, chips, search, starring, recents, Custom tab editing (today's
  flows) and the default hotkey.

## Left to the builder

- Whether the list keeps one control per row or becomes a single custom-drawn
  (virtualized) list. Requirement: opening the pop-up and switching tab/chip must feel
  instant with ~250 rows; a single owner-drawn list is the recommended way.
- Exact names for the ~225 entries (rules above), exact pixel sizes, chip scrolling details.
- Exact ids, following the `section.action` pattern.
