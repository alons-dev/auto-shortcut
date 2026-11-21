using System;
using System.Drawing;
using System.Windows.Forms;

namespace AutoShortcut
{
    // Moves a Windows shortcut to another combination (right-click > Edit hotkey).
    //
    //   Copy
    //   Windows default: Ctrl + C
    //   Hotkey   [Ctrl+Alt+C          ] [Record]
    //            ✓ Ctrl + Alt + C
    //            Use the default
    //                                     [Cancel] [Save]
    //
    // Pressing the new combination makes AutoShortcut press the default one. The
    // default keeps working until one of your shortcuts takes it.
    internal sealed class WindowsHotkeyForm : ThemedDialog
    {
        private const int LabelX = 16;
        private const int FieldX = 124;
        private const int FieldW = 340;

        private readonly string _defaultKeys;
        private readonly Func<string, string> _check;
        private readonly KeyField _hotkey;

        public string Result;

        // check(combination) returns null if it can be used, or who uses it.
        public WindowsHotkeyForm(Theme theme, string name, string defaultKeys, string currentKeys, Func<string, string> check)
            : base(theme, "Edit hotkey", 480, 222)
        {
            _defaultKeys = defaultKeys;
            _check = check;

            Label title = AddLabel(name, LabelX, 10);
            title.Font = new Font("Segoe UI Semibold", 10.5F);
            KeyCombo original;
            string shown = KeyCombo.TryParse(defaultKeys, out original)
                ? string.Join(" + ", original.DisplayParts()) : defaultKeys;
            AddNote("Windows default: " + shown, LabelX, 42, 448);

            AddLabel("Hotkey", LabelX, 72);
            _hotkey = new KeyField(theme, Ui.S(FieldW), true, "Hotkey");
            _hotkey.Location = new Point(Ui.S(FieldX), Ui.S(72));
            _hotkey.Value = currentKeys;
            Controls.Add(_hotkey);
            // Windows' own combination doesn't need Win, Ctrl or Alt (e.g. Delete).
            _hotkey.ValueChanged += delegate { UpdateRule(); };
            UpdateRule();

            LinkLabel useDefault = new LinkLabel();
            useDefault.Text = "Use the default";
            useDefault.AutoSize = true;
            useDefault.LinkColor = theme.Accent;
            useDefault.ActiveLinkColor = theme.Accent;
            useDefault.Location = new Point(Ui.S(FieldX), Ui.S(130));
            useDefault.LinkClicked += delegate { _hotkey.Value = _defaultKeys; };
            Controls.Add(useDefault);
        }

        private bool IsDefault
        {
            get { return HotkeyRules.Normalize(_hotkey.Value) == _defaultKeys; }
        }

        private void UpdateRule()
        {
            bool require = !IsDefault;
            if (_hotkey.RequireModifier != require) _hotkey.RequireModifier = require;
        }

        protected override string TrySave()
        {
            if (!_hotkey.IsValid) return "Check the hotkey.";
            string combo = HotkeyRules.Normalize(_hotkey.Value);
            string used = _check(combo);
            if (used != null) return used + ".";
            Result = combo;
            return null;
        }

        protected override void OnDeactivate(EventArgs e)
        {
            base.OnDeactivate(e);
            _hotkey.StopRecording();
        }
    }
}
