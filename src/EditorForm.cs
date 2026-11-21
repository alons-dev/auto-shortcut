using System;
using System.Drawing;
using System.IO;
using System.Windows.Forms;

namespace AutoShortcut
{
    // Window to create or edit a shortcut.
    //
    //   Name       [Notepad                    ]
    //   Mode       (•) Hotkey
    //              ( ) Open on click
    //   Hotkey     [Ctrl+Alt+N         ] [Record]
    //              ✓ Ctrl + Alt + N
    //   Does       (•) Open a program, file, folder or website
    //              ( ) Press this hotkey (a reminder)
    //   Open       [notepad.exe        ] [Browse…]
    //   Arguments  [                           ]
    //                                     [Cancel] [Save]
    //
    // "Hotkey" + "Open": the hotkey opens it from anywhere (and so does a click).
    // "Hotkey" + "Press": a reminder of another app's combination; the app doesn't
    // listen to it, a click presses it. "Open on click": no hotkey.
    internal sealed class EditorForm : ThemedDialog
    {
        private const int LabelX = 16;
        private const int FieldX = 124;
        private const int FieldW = 340;
        private const int DialogWidth = 480;

        private readonly Func<string, bool, string> _check;
        private readonly TextBox _name;
        private readonly RadioButton _hotkeyMode;
        private readonly RadioButton _clickMode;
        private readonly Label _hotkeyLabel;
        private readonly KeyField _hotkey;
        private readonly Label _doesLabel;
        private readonly Panel _does;
        private readonly RadioButton _openAction;
        private readonly RadioButton _pressAction;
        private readonly Label _pressNote;
        private readonly Label _runLabel;
        private readonly TextBox _run;
        private readonly Button _browse;
        private readonly Label _argsLabel;
        private readonly TextBox _args;
        private readonly Label _argsNote;

        public Shortcut Result;

        // existing == null: new shortcut. check(combination, listens) returns null if
        // the combination is free, or who uses it (listens: the app will register it).
        public EditorForm(Shortcut existing, Theme theme, Func<string, bool, string> check)
            : base(theme, existing == null ? "New shortcut" : "Edit shortcut", DialogWidth, 388)
        {
            _check = check;

            AddLabel("Name", LabelX, 18);
            _name = AddTextBox(FieldX, 18, FieldW, "Name");

            AddLabel("Mode", LabelX, 60);
            Panel mode = AddGroup(58);
            _hotkeyMode = AddRadio(mode, "Hotkey", 0);
            _clickMode = AddRadio(mode, "Open on click, without a hotkey", 24);

            _hotkeyLabel = AddLabel("Hotkey", LabelX, 120);
            _hotkey = new KeyField(theme, Ui.S(FieldW), true, "Hotkey");
            _hotkey.Location = new Point(Ui.S(FieldX), Ui.S(120));
            Controls.Add(_hotkey);

            _doesLabel = AddLabel("Does", LabelX, 180);
            _does = AddGroup(178);
            _openAction = AddRadio(_does, "Open a program, file, folder or website", 0);
            _pressAction = AddRadio(_does, "Press this hotkey (a reminder)", 24);
            _pressNote = AddNote("AutoShortcut doesn't listen to it: clicking the shortcut presses these keys.",
                FieldX, 230, FieldW);

            _runLabel = AddLabel("Open", LabelX, 240);
            _run = AddTextBox(FieldX, 240, FieldW - 104, "Open");
            _browse = AddButton("Browse…", FieldX + FieldW - 96, 240, 96, false);
            _browse.Click += delegate { Browse(); };
            _argsLabel = AddLabel("Arguments", LabelX, 284);
            _args = AddTextBox(FieldX, 284, FieldW, "Arguments");
            _argsNote = AddNote("Optional. Supports variables like %USERPROFILE%.", FieldX, 319, FieldW);

            if (existing == null)
            {
                _hotkeyMode.Checked = true;
                _openAction.Checked = true;
            }
            else
            {
                _name.Text = existing.Name;
                if (existing.IsLaunch)
                {
                    _run.Text = existing.Run;
                    _args.Text = existing.Args;
                    _openAction.Checked = true;
                    if (existing.HasHotkey)
                    {
                        _hotkeyMode.Checked = true;
                        _hotkey.Value = existing.Hotkey;
                    }
                    else
                    {
                        _clickMode.Checked = true;
                    }
                }
                else
                {
                    _hotkeyMode.Checked = true;
                    _pressAction.Checked = true;
                    _hotkey.Value = existing.KeysText;
                }
            }

            _hotkeyMode.CheckedChanged += delegate { UpdateMode(); };
            _openAction.CheckedChanged += delegate { UpdateMode(); };
            UpdateMode();
        }

        private Panel AddGroup(int y)
        {
            // Each pair of options in its own panel: radio buttons only exclude the
            // others in the same container.
            Panel p = new Panel();
            p.BackColor = T.Background;
            p.Bounds = new Rectangle(Ui.S(FieldX), Ui.S(y) + Ui.S(2), Ui.S(FieldW), Ui.S(48));
            Controls.Add(p);
            return p;
        }

        private RadioButton AddRadio(Panel parent, string text, int y)
        {
            RadioButton r = new RadioButton();
            r.Text = text;
            r.AutoSize = true;
            r.FlatStyle = FlatStyle.Flat;
            r.ForeColor = T.Foreground;
            r.Location = new Point(0, Ui.S(y));
            parent.Controls.Add(r);
            return r;
        }

        // Shows the rows for the current mode, one under the other.
        private void UpdateMode()
        {
            bool hotkey = _hotkeyMode.Checked;
            bool open = !hotkey || _openAction.Checked;

            _hotkeyLabel.Visible = _hotkey.Visible = hotkey;
            _doesLabel.Visible = _does.Visible = hotkey;
            _pressNote.Visible = hotkey && !open;
            _hotkey.RequireModifier = open;

            Control[] runControls = { _runLabel, _run.Parent, _browse, _argsLabel, _args.Parent, _argsNote };
            foreach (Control c in runControls) c.Visible = open;

            int y = hotkey ? 240 : 120;   // where "Open" goes
            PlaceField(_runLabel, _run.Parent, y);
            _browse.Top = Ui.S(y);
            PlaceField(_argsLabel, _args.Parent, y + 44);
            _argsNote.Top = Ui.S(y + 79);

            int bottom = open ? y + 99 : 250;
            SetHeight(bottom + 20 + 44);
            ErrorLabel.Text = "";
        }

        private static void PlaceField(Label label, Control box, int y)
        {
            label.Top = Ui.S(y) + Ui.S(6);
            box.Top = Ui.S(y);
        }

        private void Browse()
        {
            using (OpenFileDialog dlg = new OpenFileDialog())
            {
                dlg.Title = "Choose what to open";
                dlg.Filter = "Programs and shortcuts|*.exe;*.lnk;*.bat;*.cmd;*.ps1|All files|*.*";
                dlg.DereferenceLinks = false;   // keep the .lnk, not its target
                if (dlg.ShowDialog(this) != DialogResult.OK) return;
                _run.Text = dlg.FileName;
                if (_name.Text.Trim().Length == 0) _name.Text = Path.GetFileNameWithoutExtension(dlg.FileName);
            }
        }

        protected override string TrySave()
        {
            Shortcut s = new Shortcut();
            s.Name = _name.Text.Trim();
            if (s.Name.Length == 0) return "Give it a name.";

            bool hotkey = _hotkeyMode.Checked;
            bool open = !hotkey || _openAction.Checked;
            string combo = null;
            if (hotkey)
            {
                if (!_hotkey.IsValid) return "Check the hotkey.";
                combo = HotkeyRules.Normalize(_hotkey.Value);
                string used = _check(combo, open);
                if (used != null) return used + ".";
            }

            if (open)
            {
                s.Run = _run.Text.Trim();
                if (s.Run.Length == 0) return "Choose what to open.";
                string args = _args.Text.Trim();
                s.Args = args.Length == 0 ? null : args;
                s.Hotkey = combo;
            }
            else
            {
                s.KeysText = combo;
            }
            Result = s;
            return null;
        }

        protected override void OnDeactivate(EventArgs e)
        {
            base.OnDeactivate(e);
            _hotkey.StopRecording();   // never leave the hook active without the window in front
        }
    }
}
