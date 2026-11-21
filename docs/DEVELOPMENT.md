# How AutoShortcut is built

AutoShortcut is a small C# WinForms app that only uses what ships with Windows 10 and 11: Windows PowerShell 5.1 and .NET Framework 4.5+. No AutoHotkey, NuGet or third-party installers.

## Project layout

```
auto-shortcut/
├── src/             The app's C# (WinForms) code
├── assets/          App icon and README screenshots
├── tests/           Logic tests (run-tests.ps1 + LogicTests.cs)
├── docs/            This file
├── run.ps1          Starts the app by compiling src/ in memory (what the installation uses)
├── install.ps1      Downloads, checks and installs (irm ... | iex)
├── uninstall.ps1    Removes the app, its shortcuts and its registry entry
└── build.ps1        Compiles src/ → bin/AutoShortcut.exe (development only)
```

The `.ps1` files are ASCII-only on purpose: Windows PowerShell 5.1 reads scripts without a BOM as ANSI, so non-ASCII characters would come out garbled.

## Main pieces

- **Startup**: [`run.ps1`](../run.ps1) compiles `src/` in memory with `Add-Type` in Windows PowerShell and starts the app. The Start menu and sign-in shortcuts launch it through `conhost.exe --headless`, so no console window shows up. It takes a second or two.
- **Global hotkeys**: `RegisterHotKey` (`user32.dll`), one id for the combination that opens the pop-up, one for each of your shortcuts with a hotkey and one for each Windows shortcut you moved. Windows sends `WM_HOTKEY` even while the app is in the background. Reminders (another app's combination kept with a name) are never registered.
- **Who has a combination**: [`HotkeyRules.cs`](../src/HotkeyRules.cs) checks, in this order, the combination that opens AutoShortcut, your shortcuts (hotkeys and reminders) and the Windows catalog (each shortcut on its current combination plus its other ones). Only then does the app try `RegisterHotKey`; if Windows refuses, another app has it. Saving a taken combination is blocked with that answer.
- **Moved Windows shortcuts**: stored by combination (`"Ctrl+C": "Ctrl+Alt+C"`), so every catalog entry on <kbd>Ctrl</kbd>+<kbd>C</kbd> moves together. The new combination is registered and presses the default; the default isn't captured, so it keeps working until one of your shortcuts takes it. Restoring clears the moves and takes the hotkey off your shortcuts that were on a Windows default.
- **Pop-up**: a borderless form above the taskbar, with Windows 11 rounded corners from `DwmSetWindowAttribute`. It hides when it loses the focus, unless "Keep open" is on; then, each time it's activated, the window that had the focus (from `WM_ACTIVATE`) becomes where clicked shortcuts send their keys.
- **Acrylic**: on Windows 11 with transparency effects on (and no high contrast) the pop-up has a live blur behind it, through the window's accent policy (`SetWindowCompositionAttribute`), plus a 50 % tint of the theme color. The documented backdrop API draws flat gray when the window is opened by the global hotkey, so it isn't used. On acrylic every pixel's alpha counts, and classic GDI drawing leaves it at 0, so the pop-up's controls paint through [`Glass.cs`](../src/Glass.cs): a 32-bit off-screen buffer drawn with GDI+, copied with its alpha; the search `TextBox` is repainted fully opaque. The list scrolls by itself (thin Windows 11-style bar) because the native scrollbar can't draw there. Elsewhere (Windows 10 included) the same code paints opaque with ClearType text, as before. Set `AUTOSHORTCUT_NO_ACRYLIC=1` to force the opaque look.
- **Built-in catalog**: [`WindowsShortcuts.cs`](../src/WindowsShortcuts.cs) has the Windows 11 shortcuts from Microsoft's [Keyboard shortcuts in Windows](https://support.microsoft.com/en-us/accessibility/windows/keyboard-shortcuts-in-windows) page (Windows 11 tab, without the Copilot section), in our own words. Entries that can't be pressed by an app (mouse, number ranges, <kbd>Ctrl</kbd>+<kbd>Alt</kbd>+<kbd>Del</kbd>, <kbd>Win</kbd>+<kbd>L</kbd>…) are reference only. Ids are saved in favorites and recents, so they never change.
- **Views and search**: [`Library.cs`](../src/Library.cs) turns the config and the catalog into what each tab shows, and matches searches (every word must appear in the name, section or keys; `windows`/`control`/`delete`/`escape` also match `win`/`ctrl`/`del`/`esc`).
- **List**: [`ShortcutList.cs`](../src/ShortcutList.cs) is a single owner-drawn, virtualized control (only visible rows are painted). Each row is exposed to screen readers through MSAA child objects.
- **Running a shortcut**: when the pop-up opens it remembers the window in front. On click, focus goes back there and the keys are "pressed" with `SendInput`; programs, folders and URLs open with `Process.Start`. From a hotkey the keys go to the window in front, after letting go of the modifiers you're still holding (an unassigned key, `0xE8`, goes first so releasing <kbd>Alt</kbd> or <kbd>Win</kbd> doesn't open a menu). Keys the app presses would also trigger its own hotkeys, so a hotkey on the same combination is unregistered for 150 ms while they're sent.
- **Recording combinations**: a low-level keyboard hook (`WH_KEYBOARD_LL`), active only while recording.

| File | What it does |
|---|---|
| `Program.cs` | Entry point: single instance, sharp rendering on scaled displays, error logging |
| `TrayApp.cs` | Tray icon; ties together config, hotkeys, pop-up, actions and dialogs |
| `PopupForm.cs` | The pop-up: header, search, tabs, chips, list and right-click menu |
| `ShortcutList.cs` | Owner-drawn list with headers, keycaps and stars |
| `TabStrip.cs` / `ChipBar.cs` | Underlined tabs / scrolling section chips |
| `ToggleSwitch.cs` | Windows 11-style on/off switch ("Keep open") |
| `WindowsShortcuts.cs` | The built-in Windows 11 catalog |
| `Library.cs` | Entries, views per tab, search |
| `Config.cs` / `Shortcut.cs` | Read and save `shortcuts.json` (your shortcuts, moved Windows shortcuts, favorites, recents) |
| `EditorForm.cs` / `SettingsForm.cs` | Windows to edit a shortcut, and to change the hotkey or restore the Windows shortcuts |
| `WindowsHotkeyForm.cs` | Moves a Windows shortcut to another combination |
| `HotkeyRules.cs` | Who uses a combination (app, your shortcuts, Windows), restoring defaults, what to listen to |
| `ThemedDialog.cs` / `InputBox.cs` / `MenuRenderer.cs` / `IconButton.cs` | Themed windows, text boxes, menus, buttons |
| `KeyField.cs` / `KeyRecorder.cs` | Combination field with the Record button |
| `KeyCombo.cs` | Text `"Win+Shift+Z"` ⇄ keys |
| `KeySender.cs` / `ActionRunner.cs` | Run a shortcut |
| `HotkeyWindow.cs` | Global hotkeys (one id each), paused while the app presses its own keys |
| `Theme.cs` / `Ui.cs` | Light/dark (and acrylic) colors, scaling, icons, window frame |
| `Glass.cs` | Painting that keeps alpha, for the acrylic pop-up |

If something goes wrong inside the app, it's logged in `%APPDATA%\AutoShortcut\error.log`.

## `shortcuts.json`

Saved in `%APPDATA%\AutoShortcut\shortcuts.json`. It holds only what's yours; the Windows shortcuts are built in. You can edit it by hand (tray icon menu › **Edit shortcuts.json**); changes apply the next time the pop-up opens.

```json
{
  "hotkey": "Win+Shift+Z",
  "shortcuts": [
    { "name": "Notepad", "run": "notepad.exe", "hotkey": "Ctrl+Alt+N" },
    { "name": "My documents", "run": "%USERPROFILE%\\Documents" },
    { "name": "Ping Google", "run": "cmd.exe", "args": "/k ping google.com" },
    { "name": "Reopen closed tab", "keys": "Ctrl+Shift+T" }
  ],
  "windowsHotkeys": { "Ctrl+C": "Ctrl+Alt+C" },
  "pinned": true,
  "favorites": ["win:winkey.clipboard-history", "custom:Notepad"],
  "recent": ["win:text.copy"]
}
```

- `hotkey`: opens the pop-up. Must include `Win`, `Ctrl` or `Alt`. A saved `Win+Shift+A` (the old default, used by Windows 11 itself) is moved to `Win+Shift+Z`.
- `run`: program, file, folder or URL (variables like `%USERPROFILE%` work). `args`: optional, with `run`. `hotkey`: optional, with `run`; opens it from anywhere. Without it, it opens on click.
- `keys`: a reminder of another app's combination. Clicking it presses the keys in the window you had in front; the app doesn't listen to them.
- `windowsHotkeys`: Windows shortcuts moved to other keys, default › new. Only written when there is one.
- `pinned`: the "Keep open" switch; the pop-up doesn't hide when another window takes the focus. Only written when on.
- `favorites` / `recent`: `win:<catalog id>` or `custom:<name>`. Unknown entries are dropped on save.
- If the file has an error, the app warns and keeps using the last good list; it never overwrites it without asking.

**Key names:** `Win`, `Ctrl`, `Alt`, `Shift`, letters, numbers, `F1`–`F24`, `Up` `Down` `Left` `Right` (or `↑ ↓ ← →`), `Enter`, `Esc`, `Tab`, `Space`, `Home`, `End`, `PageUp` / `PgUp`, `PageDown` / `PgDn`, `Insert` / `Ins`, `Delete` / `Del`, `Backspace`, `PrintScreen` / `PrtSc`, `CapsLock`, `Pause`, `Plus`, `NumPlus` (keypad +), `Minus`, `Comma`, `Period`, `Slash`, `Semicolon`, `.` `,` `-` `/` `;`.

## Develop

```powershell
git clone https://github.com/alons-dev/auto-shortcut.git
cd auto-shortcut
powershell -ExecutionPolicy Bypass -File .\tests\run-tests.ps1   # logic tests
powershell -ExecutionPolicy Bypass -File .\run.ps1               # start the app from the code
powershell -ExecutionPolicy Bypass -File .\install.ps1           # install your local copy
.\build.ps1                                                      # compile with csc.exe → bin\AutoShortcut.exe
```

Use **Windows PowerShell** (`powershell`), not PowerShell 7 (`pwsh`): the app targets .NET Framework. Only one copy can run at a time, so close an installed AutoShortcut first (tray icon menu › Exit).

`Add-Type` treats compiler warnings as errors and needs the `Accessibility` assembly referenced, so keep the code warning-free.

## Limitations

- Windows doesn't let a regular app send keys to windows running as administrator.
- If another app already uses a combination, Windows won't register it: the editor and Settings say so, and at startup a notification says which of yours couldn't be taken.
- Some combinations are kept by Windows itself (<kbd>Win</kbd>+<kbd>E</kbd>, <kbd>Win</kbd>+<kbd>D</kbd>…) and show up as used by another app.
- Giving one of your shortcuts a Windows default (after moving that Windows shortcut) captures it everywhere: <kbd>Ctrl</kbd>+<kbd>C</kbd> then stops copying except through its new combination.
- <kbd>Ctrl</kbd>+<kbd>Alt</kbd>+<kbd>Del</kbd> and <kbd>Win</kbd>+<kbd>L</kbd> are handled by Windows at a protected level and can't be simulated.
- Laptop <kbd>Fn</kbd> keys aren't Windows keys and can't be sent.
