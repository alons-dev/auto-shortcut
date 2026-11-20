using System.Collections.Generic;

namespace AutoShortcut
{
    // One built-in Windows 11 shortcut.
    //   Keys:    the combination to press when clicked (KeyCombo syntax), or null
    //            when it can't be run from the pop-up ("reference only").
    //   Display: the keycaps to draw for reference-only entries ("Ctrl+drag").
    //   Also:    other combinations that do the same thing (shown and searchable).
    //   Note:    why a reference-only entry can't be run.
    internal sealed class CatalogItem
    {
        public string Id;
        public string Section;
        public string Name;
        public string Keys;
        public string Display;
        public string Also;
        public string Note;

        public bool Runnable { get { return Keys != null; } }
    }

    // The Windows 11 keyboard shortcuts, grouped like Microsoft's page "Keyboard
    // shortcuts in Windows" (Windows 11 tab), without the Copilot section:
    // https://support.microsoft.com/en-us/accessibility/windows/keyboard-shortcuts-in-windows
    //
    // Names are our own short wording. Ids are saved in shortcuts.json (favorites,
    // recents): never change an id once released.
    internal static class WindowsShortcuts
    {
        public static readonly string[] Sections =
        {
            "Text editing", "General", "Windows key", "Command Prompt", "Dialog boxes",
            "File Explorer", "Desktops", "Taskbar", "Settings",
        };

        private const string Mouse = "Reference only: needs the mouse";
        private const string Number = "Reference only: add a number";
        private const string Arrow = "Reference only: use any arrow key";
        private const string Letter = "Reference only: press Alt and the underlined letter";
        private const string Reserved = "Reference only: Windows reserves it";
        private const string SignIn = "Reference only: works on the sign-in screen";
        private const string Alone = "Reference only: a key on its own can't be sent";

        public static readonly IList<CatalogItem> All = Build();

        private static List<CatalogItem> _items;
        private static string _section;

        private static List<CatalogItem> Build()
        {
            _items = new List<CatalogItem>();

            _section = "Text editing";
            K("text.delete-left", "Delete character to the left", "Backspace");
            K("text.select-all", "Select all", "Ctrl+A");
            K("text.bold", "Bold", "Ctrl+B");
            K("text.delete-word-left", "Delete word to the left", "Ctrl+Backspace");
            K("text.copy", "Copy", "Ctrl+C", "Ctrl+Insert");
            K("text.delete-word-right", "Delete word to the right", "Ctrl+Delete");
            K("text.next-paragraph", "Go to next paragraph", "Ctrl+Down");
            K("text.document-end", "Go to end of document", "Ctrl+End");
            K("text.find", "Find", "Ctrl+F");
            K("text.replace", "Find and replace", "Ctrl+H");
            K("text.document-start", "Go to start of document", "Ctrl+Home");
            K("text.italic", "Italic", "Ctrl+I");
            K("text.previous-word", "Go to previous word", "Ctrl+Left");
            K("text.next-word", "Go to next word", "Ctrl+Right");
            K("text.paste-plain", "Paste as plain text", "Ctrl+Shift+V");
            K("text.underline", "Underline", "Ctrl+U");
            K("text.previous-paragraph", "Go to previous paragraph", "Ctrl+Up");
            K("text.paste", "Paste", "Ctrl+V", "Shift+Insert");
            K("text.cut", "Cut", "Ctrl+X");
            K("text.redo", "Redo", "Ctrl+Y");
            K("text.undo", "Undo", "Ctrl+Z");
            K("text.delete-right", "Delete character to the right", "Delete");
            K("text.next-line", "Go to next line", "Down");
            K("text.line-end", "Go to end of line", "End");
            K("text.line-start", "Go to start of line", "Home");
            K("text.previous-char", "Go to previous character", "Left");
            K("text.page-down", "Go down one page", "PageDown");
            K("text.page-up", "Go up one page", "PageUp");
            K("text.next-char", "Go to next character", "Right");
            K("text.select-paragraph-down", "Select to next paragraph", "Ctrl+Shift+Down");
            K("text.select-to-document-end", "Select to end of document", "Ctrl+Shift+End");
            K("text.select-to-document-start", "Select to start of document", "Ctrl+Shift+Home");
            K("text.select-word-left", "Select previous word", "Ctrl+Shift+Left");
            K("text.select-word-right", "Select next word", "Ctrl+Shift+Right");
            K("text.select-paragraph-up", "Select to previous paragraph", "Ctrl+Shift+Up");
            K("text.select-line-down", "Select to next line", "Shift+Down");
            K("text.select-to-line-end", "Select to end of line", "Shift+End");
            K("text.select-to-line-start", "Select to start of line", "Shift+Home");
            K("text.select-char-left", "Select previous character", "Shift+Left");
            K("text.select-page-down", "Select one page down", "Shift+PageDown");
            K("text.select-page-up", "Select one page up", "Shift+PageUp");
            K("text.select-char-right", "Select next character", "Shift+Right");
            K("text.select-line-up", "Select to previous line", "Shift+Up");
            K("text.indent", "Indent", "Tab");
            K("text.previous-line", "Go to previous line", "Up");

            _section = "General";
            K("general.suggested-actions", "Focus suggested actions", "Alt+A");
            K("general.cycle-windows", "Cycle windows in opening order", "Alt+Escape");
            K("general.close-window", "Close window", "Alt+F4");
            R("general.show-password", "Show password at sign-in", "Alt+F8", SignIn);
            K("general.back", "Go back", "Alt+Left");
            K("general.screen-down", "Move down one screen", "Alt+PageDown");
            K("general.screen-up", "Move up one screen", "Alt+PageUp");
            K("general.screenshot-window", "Screenshot active window", "Alt+PrintScreen");
            K("general.forward", "Go forward", "Alt+Right");
            R("general.move-start-group", "Move Start menu group", "Alt+Shift+Arrows", Arrow);
            K("general.sandbox-host", "Leave Windows Sandbox focus", "Ctrl+Alt+Insert");
            K("general.window-menu", "Open window menu", "Alt+Space");
            K("general.switch-windows", "Switch windows", "Alt+Tab");
            R("general.menu-letter", "Run menu command by letter", "Alt+letter", Letter);
            K("general.select-all", "Select all items", "Ctrl+A");
            R("general.security-screen", "Open security screen", "Ctrl+Alt+Del", Reserved);
            K("general.app-thumbnails", "View all open apps", "Ctrl+Alt+Tab");
            K("general.start-menu", "Open Start menu", "Ctrl+Escape");
            K("general.close-document", "Close document", "Ctrl+F4");
            K("general.refresh", "Refresh window", "Ctrl+F5", "Ctrl+R");
            R("general.keyboard-layout", "Switch keyboard layout", "Ctrl+Shift", Alone);
            R("general.start-folder", "Make Start menu folder", "Ctrl+Shift+Arrows", Arrow);
            K("general.task-manager", "Open Task Manager", "Ctrl+Shift+Escape");
            K("general.chinese-ime", "Toggle Chinese IME", "Ctrl+Space");
            K("general.redo", "Redo", "Ctrl+Y");
            K("general.undo", "Undo", "Ctrl+Z");
            K("general.cancel", "Cancel or close dialog", "Escape");
            K("general.refresh-f5", "Refresh active window", "F5");
            K("general.cycle-elements", "Cycle window elements", "F6");
            K("general.menu-bar", "Activate menu bar", "F10");
            K("general.snip-screen", "Snip part of the screen", "PrintScreen");

            _section = "Windows key";
            R("winkey.start", "Open or close Start menu", "Win", Alone);
            K("winkey.action-center", "Open action center", "Win+A");
            K("winkey.tip-focus", "Focus a Windows tip", "Win+Shift+A");
            K("winkey.hdr", "Turn HDR on or off", "Win+Alt+B");
            K("winkey.date-time", "Show date and time on desktop", "Win+Alt+D");
            K("winkey.snap-bottom", "Snap window to bottom half", "Win+Alt+Down");
            K("winkey.voice-keyboard", "Focus keyboard in voice typing", "Win+Alt+H");
            K("winkey.mute-mic", "Mute or unmute microphone", "Win+Alt+K");
            K("winkey.snap-top", "Snap window to top half", "Win+Alt+Up");
            K("winkey.peek-desktop", "Peek at the desktop", "Win+Comma");
            K("winkey.color-filters", "Turn color filters on or off", "Win+Ctrl+C");
            K("winkey.narrator", "Open Narrator", "Win+Ctrl+Enter");
            K("winkey.find-devices", "Find devices on a network", "Win+Ctrl+F");
            K("winkey.quick-assist", "Open Quick Assist", "Win+Ctrl+Q");
            K("winkey.wake-screen", "Wake up a blank screen", "Win+Ctrl+Shift+B");
            K("winkey.previous-input", "Previous input option", "Win+Ctrl+Space");
            K("winkey.sound-output", "Open sound output settings", "Win+Ctrl+V");
            K("winkey.desktop", "Show or hide the desktop", "Win+D");
            K("winkey.minimize", "Minimize window", "Win+Down");
            K("winkey.explorer", "Open File Explorer", "Win+E");
            K("winkey.close-magnifier", "Close Magnifier", "Win+Escape");
            K("winkey.feedback-hub", "Open Feedback Hub", "Win+F");
            K("winkey.ime-reconversion", "Start IME reconversion", "Win+Slash");
            K("winkey.game-bar", "Open Game Bar", "Win+G");
            K("winkey.voice-typing", "Open voice typing", "Win+H");
            K("winkey.minimize-others", "Minimize all but active window", "Win+Home");
            K("winkey.settings", "Open Settings", "Win+I");
            K("winkey.recall", "Open Recall", "Win+J");
            K("winkey.cast", "Cast to a display", "Win+K");
            R("winkey.lock", "Lock the PC", "Win+L", Reserved);
            K("winkey.snap-left", "Snap window left", "Win+Left");
            K("winkey.minimize-all", "Minimize all windows", "Win+M");
            K("winkey.zoom-out", "Zoom out with Magnifier", "Win+Minus");
            K("winkey.notifications", "Open notifications and calendar", "Win+N");
            K("winkey.lock-rotation", "Lock screen rotation", "Win+O");
            K("winkey.project", "Choose a display mode", "Win+P");
            K("winkey.about", "Open System > About", "Win+Pause");
            K("winkey.emoji", "Open emoji panel", "Win+Period", "Win+;");
            K("winkey.zoom-in", "Zoom in with Magnifier", "Win+Plus");
            K("winkey.screenshot-file", "Save a full-screen screenshot", "Win+PrintScreen");
            K("winkey.search-q", "Open search", "Win+Q");
            K("winkey.run", "Open the Run dialog", "Win+R");
            K("winkey.snap-right", "Snap window right", "Win+Right");
            K("winkey.search", "Open search", "Win+S");
            K("winkey.restore", "Restore snapped window", "Win+Shift+Down");
            K("winkey.app-full-screen", "Make app full screen", "Win+Shift+Enter");
            K("winkey.monitor-left", "Move window to left monitor", "Win+Shift+Left");
            K("winkey.restore-minimized", "Restore minimized windows", "Win+Shift+M");
            K("winkey.record-screen", "Record part of the screen", "Win+Shift+R");
            K("winkey.monitor-right", "Move window to right monitor", "Win+Shift+Right");
            K("winkey.snip", "Snip part of the screen", "Win+Shift+S");
            K("winkey.previous-layout", "Previous keyboard layout", "Win+Shift+Space");
            K("winkey.stretch", "Stretch window top to bottom", "Win+Shift+Up");
            K("winkey.cycle-notifications", "Cycle through notifications", "Win+Shift+V");
            K("winkey.next-layout", "Next keyboard layout", "Win+Space");
            K("winkey.task-view", "Open Task View", "Win+Tab");
            K("winkey.accessibility", "Open Accessibility settings", "Win+U");
            K("winkey.maximize", "Maximize window", "Win+Up");
            K("winkey.clipboard-history", "Open clipboard history", "Win+V");
            K("winkey.widgets", "Open Widgets", "Win+W");
            K("winkey.quick-link", "Open Quick Link menu", "Win+X");
            K("winkey.mixed-reality", "Switch Mixed Reality input", "Win+Y");
            K("winkey.snap-layouts", "Open snap layouts", "Win+Z");

            _section = "Command Prompt";
            K("cmd.copy", "Copy", "Ctrl+C", "Ctrl+Insert");
            K("cmd.paste", "Paste", "Ctrl+V", "Shift+Insert");
            K("cmd.mark-mode", "Enter Mark mode", "Ctrl+M");
            R("cmd.block-select", "Select a block of text", "Alt+select", Mouse);
            R("cmd.move-cursor", "Move the cursor", "Arrows", Arrow);
            K("cmd.page-up", "Move up one page", "PageUp");
            K("cmd.page-down", "Move down one page", "PageDown");
            K("cmd.buffer-start", "Go to buffer start (Mark mode)", "Ctrl+Home");
            K("cmd.buffer-end", "Go to buffer end (Mark mode)", "Ctrl+End");
            K("cmd.history-up", "Scroll output up one line", "Ctrl+Up");
            K("cmd.history-down", "Scroll output down one line", "Ctrl+Down");
            K("cmd.history-start", "Go to top or delete to the left", "Ctrl+Home");
            K("cmd.history-end", "Go to prompt or delete to the right", "Ctrl+End");

            _section = "Dialog boxes";
            K("dialog.open-list", "Show the items in a list", "F4");
            K("dialog.next-tab", "Next tab", "Ctrl+Tab");
            K("dialog.previous-tab", "Previous tab", "Ctrl+Shift+Tab");
            R("dialog.go-to-tab", "Go to tab by number", "Ctrl+1-9", Number);
            K("dialog.next-option", "Next option", "Tab");
            K("dialog.previous-option", "Previous option", "Shift+Tab");
            R("dialog.option-letter", "Choose option by letter", "Alt+letter", Letter);
            K("dialog.toggle-checkbox", "Check or uncheck a box", "Space");
            K("dialog.folder-up", "Up one folder in Open or Save", "Backspace");
            R("dialog.option-buttons", "Pick an option button", "Arrows", Arrow);

            _section = "File Explorer";
            K("explorer.address-bar", "Select the address bar", "Alt+D");
            K("explorer.properties", "Show properties", "Alt+Enter");
            K("explorer.back", "Previous folder", "Alt+Left", "Backspace");
            R("explorer.drag-shortcut", "Drop a file as a shortcut", "Alt+drag", Mouse);
            K("explorer.preview-pane", "Show or hide preview pane", "Alt+P");
            K("explorer.forward", "Next folder", "Alt+Right");
            K("explorer.details-pane", "Show or hide details pane", "Alt+Shift+P");
            K("explorer.up", "Up one folder", "Alt+Up");
            R("explorer.pick-items", "Select items one by one", "Ctrl+Arrows+Space", Arrow);
            K("explorer.delete", "Delete to the Recycle Bin", "Ctrl+D", "Delete");
            K("explorer.search", "Select the search box", "Ctrl+E", "Ctrl+F");
            K("explorer.focus-address", "Focus the address bar", "Ctrl+L");
            R("explorer.drag-copy", "Drop a file as a copy", "Ctrl+drag", Mouse);
            R("explorer.icon-size", "Change icon size", "Ctrl+wheel", Mouse);
            K("explorer.new-window", "New window", "Ctrl+N");
            R("explorer.go-to-tab", "Go to tab by number", "Ctrl+1-9", Number);
            K("explorer.fit-columns", "Fit all columns to their text", "Ctrl+NumPlus");
            K("explorer.expand-all", "Expand all folders", "Ctrl+Shift+E");
            K("explorer.new-folder", "New folder", "Ctrl+Shift+N");
            R("explorer.view-style", "Change the view style", "Ctrl+Shift+1-9", Number);
            K("explorer.previous-tab", "Previous tab", "Ctrl+Shift+Tab");
            K("explorer.new-tab", "New tab", "Ctrl+T");
            K("explorer.close-tab", "Close tab", "Ctrl+W");
            K("explorer.next-tab", "Next tab", "Ctrl+Tab");
            K("explorer.bottom", "Scroll to the bottom", "End");
            K("explorer.rename", "Rename", "F2");
            K("explorer.find", "Search for files", "F3");
            K("explorer.edit-path", "Edit the path", "F4");
            K("explorer.refresh", "Refresh", "F5");
            K("explorer.cycle", "Cycle window elements", "F6");
            K("explorer.full-screen", "Maximize or restore window", "F11");
            K("explorer.top", "Scroll to the top", "Home");
            K("explorer.collapse", "Collapse or go to parent", "Left");
            K("explorer.expand", "Expand or go to subfolder", "Right");
            R("explorer.select-range", "Select a range of items", "Shift+Arrows", Arrow);
            K("explorer.delete-permanently", "Delete permanently", "Shift+Delete");
            K("explorer.context-menu", "Open the context menu", "Shift+F10");
            R("explorer.drag-move", "Drop a file to move it", "Shift+drag", Mouse);
            R("explorer.more-options", "Show more options menu", "Shift+right-click", Mouse);
            K("explorer.open", "Open File Explorer", "Win+E");

            _section = "Desktops";
            K("desktops.task-view", "Open Task View", "Win+Tab");
            K("desktops.new", "Create a new desktop", "Win+Ctrl+D");
            K("desktops.next", "Next desktop", "Win+Ctrl+Right");
            K("desktops.previous", "Previous desktop", "Win+Ctrl+Left");
            K("desktops.close", "Close current desktop", "Win+Ctrl+F4");

            _section = "Taskbar";
            R("taskbar.move-icon", "Move a taskbar icon", "Alt+Shift+Arrows", Arrow);
            R("taskbar.cycle-group", "Cycle a group's windows", "Ctrl+click", Mouse);
            R("taskbar.run-as-admin", "Open app as administrator", "Ctrl+Shift+click", Mouse);
            R("taskbar.window-menu", "Show an app's window menu", "Shift+right-click", Mouse);
            R("taskbar.group-menu", "Show a group's window menu", "Shift+right-click", Mouse);
            R("taskbar.another-instance", "Open another instance", "Shift+click", Mouse);
            K("taskbar.settings", "Open taskbar settings", "Win+Alt+Enter");
            R("taskbar.jump-list", "Open a pinned app's jump list", "Win+Alt+0-9", Number);
            K("taskbar.notification-area", "Focus the notification area", "Win+B");
            R("taskbar.last-window", "Last window of a pinned app", "Win+Ctrl+0-9", Number);
            R("taskbar.admin-instance", "Pinned app as administrator", "Win+Ctrl+Shift+0-9", Number);
            R("taskbar.open-pinned", "Open a pinned app", "Win+0-9", Number);
            R("taskbar.new-pinned-instance", "New instance of a pinned app", "Win+Shift+0-9", Number);
            K("taskbar.cycle-apps", "Cycle through taskbar apps", "Win+T");
            K("taskbar.previous-app", "Previous taskbar app", "Win+Shift+T");

            _section = "Settings";
            R("settings.move-focus", "Move focus or scroll", "Arrows", Arrow);
            K("settings.back", "Go back", "Backspace");
            K("settings.select", "Select the focused item", "Enter", "Space");
            K("settings.previous-region", "Previous region", "Shift+Tab");
            K("settings.next-region", "Next region", "Tab");
            K("settings.open", "Open Settings", "Win+I");

            List<CatalogItem> result = _items;
            _items = null;
            return result;
        }

        // A shortcut that runs when clicked.
        private static void K(string id, string name, string keys, string also = null)
        {
            CatalogItem item = new CatalogItem();
            item.Id = id;
            item.Section = _section;
            item.Name = name;
            item.Keys = keys;
            item.Also = also;
            _items.Add(item);
        }

        // A reference-only shortcut: shown, searchable, but not runnable.
        private static void R(string id, string name, string display, string note)
        {
            CatalogItem item = new CatalogItem();
            item.Id = id;
            item.Section = _section;
            item.Name = name;
            item.Display = display;
            item.Note = note;
            _items.Add(item);
        }
    }
}
