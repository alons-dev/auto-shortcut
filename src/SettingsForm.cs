using System;
using System.Drawing;
using System.Windows.Forms;

namespace AutoShortcut
{
    // Settings: the key combination that opens AutoShortcut, and putting the Windows
    // shortcuts back on their default keys.
    // On save it tries to register it; if something has it, it says what and stays open.
    internal sealed class SettingsForm : ThemedDialog
    {
        private readonly KeyField _hotkey;
        private readonly Func<string, string> _tryApply;

        // tryApply receives the combination and returns null if it could be used,
        // or the reason it couldn't. restoreWindows returns what to show, or null if cancelled.
        public SettingsForm(Theme theme, string currentHotkey, Func<string, string> tryApply, Action editFile,
            Func<string> restoreWindows)
            : base(theme, "AutoShortcut settings", 480, 292)
        {
            _tryApply = tryApply;

            AddLabel("Shortcut to open AutoShortcut", 16, 10);
            _hotkey = new KeyField(theme, Ui.S(448), true, "Shortcut to open AutoShortcut");
            _hotkey.Location = new Point(Ui.S(16), Ui.S(44));
            _hotkey.Value = currentHotkey;
            Controls.Add(_hotkey);

            AddNote("You can also open it by clicking its icon in the system tray.", 16, 108, 448);

            LinkLabel link = new LinkLabel();
            link.Text = "Edit shortcuts.json by hand";
            link.AutoSize = true;
            link.LinkColor = theme.Accent;
            link.ActiveLinkColor = theme.Accent;
            link.Location = new Point(Ui.S(16), Ui.S(134));
            link.LinkClicked += delegate { editFile(); };
            Controls.Add(link);

            AddLabel("Windows shortcuts", 16, 166);
            AddNote("The ones you moved to other keys (right-click > Edit hotkey) go back to their defaults.", 16, 196, 448);
            Button restore = AddButton("Restore Windows shortcuts", 16, 220, 220, false);
            Label done = AddNote("", 248, 226, 216);
            restore.Click += delegate
            {
                string result = restoreWindows();
                if (result != null) done.Text = result;
            };
        }

        protected override string TrySave()
        {
            if (!_hotkey.IsValid) return "Check the key combination.";
            return _tryApply(_hotkey.Value);
        }

        protected override void OnDeactivate(EventArgs e)
        {
            base.OnDeactivate(e);
            _hotkey.StopRecording();
        }
    }
}
