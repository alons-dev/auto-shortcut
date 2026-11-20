using System;
using System.Drawing;
using System.IO;
using System.Windows.Forms;

namespace AutoShortcut
{
    // Window to create or edit a shortcut.
    //
    //   Name          [Capture the screen         ]
    //   On click      (•) Press keys
    //                 ( ) Open a program, file, folder or website
    //   Keys          [Win+Shift+S        ] [Record]
    //                 ✓ Win + Shift + S
    //                                     [Cancel] [Save]
    internal sealed class EditorForm : ThemedDialog
    {
        private const int LabelX = 16;
        private const int FieldX = 124;
        private const int FieldW = 340;

        private readonly TextBox _name;
        private readonly RadioButton _keysMode;
        private readonly RadioButton _runMode;
        private readonly KeyField _keys;
        private readonly Label _keysLabel;
        private readonly Control[] _runControls;
        private readonly TextBox _run;
        private readonly TextBox _args;

        public Shortcut Result;

        // existing == null: new shortcut.
        public EditorForm(Shortcut existing, Theme theme)
            : base(theme, existing == null ? "New shortcut" : "Edit shortcut", 480, 268)
        {
            AddLabel("Name", LabelX, 18);
            _name = AddTextBox(FieldX, 18, FieldW, "Name");

            AddLabel("On click", LabelX, 60);
            _keysMode = AddRadio("Press keys", 58);
            _runMode = AddRadio("Open a program, file, folder or website", 82);

            // Keys mode
            _keysLabel = AddLabel("Keys", LabelX, 120);
            _keys = new KeyField(theme, Ui.S(FieldW), false, "Keys");
            _keys.Location = new Point(Ui.S(FieldX), Ui.S(120));
            Controls.Add(_keys);

            // Launch mode
            Label runLabel = AddLabel("Open", LabelX, 120);
            _run = AddTextBox(FieldX, 120, FieldW - 104, "Open");
            Button browse = AddButton("Browse…", FieldX + FieldW - 96, 120, 96, false);
            browse.Click += delegate { Browse(); };
            Label argsLabel = AddLabel("Arguments", LabelX, 164);
            _args = AddTextBox(FieldX, 164, FieldW, "Arguments");
            Label argsNote = AddNote("Optional. Supports variables like %USERPROFILE%.", FieldX, 199, FieldW);
            // _run and _args live inside their InputBox (Parent): hide the whole box.
            _runControls = new Control[] { runLabel, _run.Parent, browse, argsLabel, _args.Parent, argsNote };

            _keysMode.CheckedChanged += delegate { UpdateMode(); };

            if (existing != null)
            {
                _name.Text = existing.Name;
                if (existing.IsLaunch)
                {
                    _runMode.Checked = true;
                    _run.Text = existing.Run;
                    _args.Text = existing.Args;
                }
                else
                {
                    _keysMode.Checked = true;
                    _keys.Value = existing.KeysText;
                }
            }
            else
            {
                _keysMode.Checked = true;
            }
            UpdateMode();
        }

        private RadioButton AddRadio(string text, int y)
        {
            RadioButton r = new RadioButton();
            r.Text = text;
            r.AutoSize = true;
            r.FlatStyle = FlatStyle.Flat;
            r.ForeColor = T.Foreground;
            r.Location = new Point(Ui.S(FieldX), Ui.S(y) + Ui.S(2));
            Controls.Add(r);
            return r;
        }

        private void UpdateMode()
        {
            bool keys = _keysMode.Checked;
            _keysLabel.Visible = keys;
            _keys.Visible = keys;
            foreach (Control c in _runControls) c.Visible = !keys;
            ErrorLabel.Text = "";
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

            if (_keysMode.Checked)
            {
                KeyCombo combo;
                if (!KeyCombo.TryParse(_keys.Value, out combo)) return "Check the keys.";
                s.KeysText = combo.ToString();
            }
            else
            {
                s.Run = _run.Text.Trim();
                if (s.Run.Length == 0) return "Choose what to open.";
                string args = _args.Text.Trim();
                s.Args = args.Length == 0 ? null : args;
            }
            Result = s;
            return null;
        }

        protected override void OnDeactivate(EventArgs e)
        {
            base.OnDeactivate(e);
            _keys.StopRecording();   // never leave the hook active without the window in front
        }
    }
}
