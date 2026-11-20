# AutoShortcut

A keyboard shortcuts panel for Windows. Press **Win + Shift + A** and a minimalist pop-up appears in the bottom-right corner, with Windows 11-style rounded corners. Click a shortcut and it runs.

- **No dependencies**: it only uses what already ships with Windows 10 and 11 (Windows PowerShell and .NET Framework). No AutoHotkey, NuGet or third-party installers.
- **Your list**: add, edit or remove shortcuts from the pop-up itself.
- **Click = action**: a shortcut can press a key combination, or open a program, a folder or a website.
- **Configurable hotkey**: Win + Shift + A by default, change it to whatever you like.

## Install

Open PowerShell (no administrator rights needed) and run:

```powershell
irm https://raw.githubusercontent.com/alons-dev/auto-shortcut/main/install.ps1 | iex
```

The installer:

1. Downloads the code from this repository (GitHub zip).
2. Copies it to `%LOCALAPPDATA%\Programs\AutoShortcut` and checks that it compiles.
3. Creates a shortcut in the Start menu and another in Windows Startup, so it runs when you sign in.
4. Registers it in **Settings › Apps**, where you can uninstall it.
5. Starts it: its icon shows up in the system tray, next to the clock.

To update, run the same command again. Your shortcut list is left untouched.

### Why there's no `.exe`

Windows 11 ships with **Smart App Control**, which blocks unsigned `.exe` files it doesn't recognize, and an `.exe` compiled on your own machine is always "unknown". That's why AutoShortcut isn't compiled to an `.exe`: it's started by **Windows PowerShell** (signed by Microsoft), which compiles the C# code in memory with `Add-Type` (see [`run.ps1`](run.ps1)). It's the same idea as AutoHotkey: a signed program that runs your code.

The shortcut launches it with `conhost.exe --headless`, the Windows console in windowless mode, so no console shows up (not even when Windows Terminal is your default terminal). Startup takes a second or two, because it compiles the code.

## Use

| Action | How |
|---|---|
| Open / close the pop-up | `Win + Shift + A`, or click the tray icon |
| Run a shortcut | Click it |
| Add a shortcut | The pop-up's **+** button |
| Edit, move or delete a shortcut | Right-click it |
| Change the key that opens the pop-up | The pop-up's gear button (⚙) |
| Close | `Esc` or click outside the pop-up |

When you create or edit a key shortcut, or change the pop-up's hotkey, you can type the combination (`Ctrl+Alt+T`) or press **Record** and do it on the keyboard. Recording works even with combinations Windows reserves for itself, like `Win+Shift+S`.

## Your shortcut list

It's saved in `%APPDATA%\AutoShortcut\shortcuts.json` and created the first time with a few sample shortcuts. Normally you edit it from the pop-up, but you can also do it by hand (right-click the tray icon › **Edit shortcuts.json**). Changes made by hand apply the next time you open the pop-up.

```json
{
  "hotkey": "Win+Shift+A",
  "shortcuts": [
    { "name": "Capture part of the screen", "keys": "Win+Shift+S" },
    { "name": "Notepad", "run": "notepad.exe" },
    { "name": "My documents", "run": "%USERPROFILE%\\Documents" },
    { "name": "GitHub", "run": "https://github.com" },
    { "name": "Ping Google", "run": "cmd.exe", "args": "/k ping google.com" }
  ]
}
```

- `hotkey`: the combination that opens the pop-up. It must include `Win`, `Ctrl` or `Alt`. If another app already uses it, the pop-up says so in its header, and you can open it by clicking the tray icon while you pick another one.
- `keys`: on click, that combination is pressed in the window you had in front (handy for remembering shortcuts of Windows or your apps).
- `run`: on click, that program, file, folder or URL opens, as if you double-clicked it. Supports variables like `%USERPROFILE%`.
- `args` (optional, with `run`): arguments for the program.

Inside JSON, `\` is written twice (`\\`). If the file has an error, the app warns you and keeps using the last good list; it doesn't overwrite the file.

**Keys it understands:** `Win`, `Ctrl`, `Alt`, `Shift`, letters, numbers, `F1`–`F24`, `Up` `Down` `Left` `Right` (or `↑ ↓ ← →`), `Enter`, `Esc`, `Tab`, `Space`, `Home`, `End`, `PageUp` / `PgUp`, `PageDown` / `PgDn`, `Insert` / `Ins`, `Delete` / `Del`, `Backspace`, `PrintScreen` / `PrtSc`, `CapsLock`, `.` `,` `-`.

## How it's built

```
auto-shortcut/
├── src/             The app's C# (WinForms) code
├── run.ps1          Starts the app by compiling src/ in memory (what the installation uses)
├── install.ps1      Downloads, checks and installs (the command above)
├── uninstall.ps1    Removes the app, its shortcuts and its registry entry
├── build.ps1        Compiles src/ → bin/AutoShortcut.exe (development only)
├── README.md
└── LICENSE
```

The `.ps1` files are ASCII-only on purpose: Windows PowerShell 5.1 reads scripts without a BOM as ANSI, so non-ASCII characters would come out garbled.

The main pieces:

- **Global hotkey** — `RegisterHotKey` from the Win32 API (`user32.dll`). Windows notifies the app with a `WM_HOTKEY` message even while it's in the background.
- **Pop-up** — a borderless WinForms form, placed above the taskbar on the right. It asks Windows 11 for rounded corners with `DwmSetWindowAttribute` (the same API system windows use).
- **Shortcut list** — JSON read with `JavaScriptSerializer` (included in .NET Framework) and written by hand so it stays readable. No external libraries.
- **Running** — when the pop-up opens it remembers which window was in front. On click, focus goes back to it and the combination is "pressed" with `SendInput`; programs, folders and URLs open with `Process.Start`.
- **Rows** — a custom control drawn with GDI+: the name on the left and the keys as "keycaps" on the right.
- **Recording combinations** — a low-level keyboard hook (`SetWindowsHookEx` with `WH_KEYBOARD_LL`) gets the keys before Windows shortcuts do. It's only active while you record: it's released with the first combination, with `Esc`, after 10 seconds or if the window loses focus.
- **Windows 11 style** — icons from the *Segoe Fluent Icons* font, menus and text boxes drawn with the theme colors, and a dark title bar requested from DWM.

| File | What it does |
|---|---|
| `Program.cs` | Entry point: single instance, sharp rendering on scaled displays, error logging |
| `TrayApp.cs` | Tray icon; ties together settings, hotkey, pop-up, actions and editor |
| `HotkeyWindow.cs` | Global hotkey with `RegisterHotKey` |
| `PopupForm.cs` | The pop-up: position, rounded corners, list, buttons and menu |
| `ShortcutRow.cs` / `IconButton.cs` | A clickable row / an icon button |
| `EditorForm.cs` / `SettingsForm.cs` | Windows to edit a shortcut and change the hotkey |
| `ThemedDialog.cs` / `InputBox.cs` / `MenuRenderer.cs` | Themed windows, text boxes and menus |
| `KeyField.cs` / `KeyRecorder.cs` | Combination field with the Record button |
| `Config.cs` / `Shortcut.cs` | Read and save `shortcuts.json` |
| `KeyCombo.cs` | Text `"Win+Shift+A"` ⇄ keys |
| `KeySender.cs` / `ActionRunner.cs` | Run a shortcut |
| `Theme.cs` / `Ui.cs` | Light/dark colors, scaling and drawing |

If something goes wrong inside the app, it's logged in `%APPDATA%\AutoShortcut\error.log`.

### Develop

```powershell
git clone https://github.com/alons-dev/auto-shortcut.git
cd auto-shortcut
powershell -ExecutionPolicy Bypass -File .\run.ps1      # start the app from the code
powershell -ExecutionPolicy Bypass -File .\install.ps1  # install your local copy
.\build.ps1                                             # compile with csc.exe → bin\AutoShortcut.exe
```

`run.ps1` needs **Windows PowerShell** (`powershell`), not PowerShell 7 (`pwsh`), because it uses .NET Framework. If AutoShortcut is already installed and running, close it first (tray icon menu › Exit): only one copy can be open.

`bin\AutoShortcut.exe` works if Smart App Control is off; if it's on, Windows may block it.

## Uninstall

From **Settings › Apps › AutoShortcut › Uninstall**, or with:

```powershell
& "$env:LOCALAPPDATA\Programs\AutoShortcut\uninstall.ps1"
```

Your list stays in `%APPDATA%\AutoShortcut` in case you come back. To delete it too, add `-RemoveSettings` to the command above. If you already uninstalled without that option (the uninstaller deletes itself), delete the folder directly:

```powershell
Remove-Item "$env:APPDATA\AutoShortcut" -Recurse -Force
```

## Limitations

- Windows doesn't let a regular app send keys to windows running as administrator.
- If another app already uses the chosen combination, Windows won't register it: the pop-up will warn you so you can pick another one.
- `Ctrl+Alt+Del` and `Win+L` are handled by Windows at a protected level and can't be simulated.
- Laptop `Fn` keys (brightness, vendor volume…) aren't Windows keys and can't be sent.

## License

[MIT](LICENSE)
