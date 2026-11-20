using System;
using System.Drawing;
using System.IO;
using System.Windows.Forms;

namespace AutoShortcut
{
    // Ventana para crear o editar un atajo.
    //
    //   Nombre        [Capturar pantalla          ]
    //   Al hacer clic (•) Pulsar unas teclas
    //                 ( ) Abrir un programa, archivo, carpeta o web
    //   Teclas        [Win+Shift+S        ] [Grabar]
    //                 ✓ Win + Shift + S
    //                                 [Cancelar] [Guardar]
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

        // existing == null: atajo nuevo.
        public EditorForm(Shortcut existing, Theme theme)
            : base(theme, existing == null ? "Nuevo atajo" : "Editar atajo", 480, 268)
        {
            AddLabel("Nombre", LabelX, 18);
            _name = AddTextBox(FieldX, 18, FieldW, "Nombre");

            AddLabel("Al hacer clic", LabelX, 60);
            _keysMode = AddRadio("Pulsar unas teclas", 58);
            _runMode = AddRadio("Abrir un programa, archivo, carpeta o web", 82);

            // Modo teclas
            _keysLabel = AddLabel("Teclas", LabelX, 120);
            _keys = new KeyField(theme, Ui.S(FieldX + FieldW) - Ui.S(FieldX), false, "Teclas");
            _keys.Location = new Point(Ui.S(FieldX), Ui.S(120));
            Controls.Add(_keys);

            // Modo abrir
            Label runLabel = AddLabel("Abrir", LabelX, 120);
            _run = AddTextBox(FieldX, 120, FieldW - 104, "Abrir");
            Button browse = AddButton("Examinar…", FieldX + FieldW - 96, 120, 96, false);
            browse.Click += delegate { Browse(); };
            Label argsLabel = AddLabel("Argumentos", LabelX, 164);
            _args = AddTextBox(FieldX, 164, FieldW, "Argumentos");
            Label argsNote = AddNote("Opcional. Admite variables como %USERPROFILE%.", FieldX, 199, FieldW);
            // _run y _args viven dentro de su InputBox (Parent): se oculta el cuadro entero.
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
                dlg.Title = "Elige qué abrir";
                dlg.Filter = "Programas y accesos directos|*.exe;*.lnk;*.bat;*.cmd;*.ps1|Todos los archivos|*.*";
                dlg.DereferenceLinks = false;   // guardar el .lnk, no su destino
                if (dlg.ShowDialog(this) != DialogResult.OK) return;
                _run.Text = dlg.FileName;
                if (_name.Text.Trim().Length == 0) _name.Text = Path.GetFileNameWithoutExtension(dlg.FileName);
            }
        }

        protected override string TrySave()
        {
            Shortcut s = new Shortcut();
            s.Name = _name.Text.Trim();
            if (s.Name.Length == 0) return "Ponle un nombre.";

            if (_keysMode.Checked)
            {
                KeyCombo combo;
                if (!KeyCombo.TryParse(_keys.Value, out combo)) return "Revisa las teclas.";
                s.KeysText = combo.ToString();
            }
            else
            {
                s.Run = _run.Text.Trim();
                if (s.Run.Length == 0) return "Indica qué abrir.";
                string args = _args.Text.Trim();
                s.Args = args.Length == 0 ? null : args;
            }
            Result = s;
            return null;
        }

        protected override void OnDeactivate(EventArgs e)
        {
            base.OnDeactivate(e);
            _keys.StopRecording();   // nunca dejar el hook activo sin la ventana delante
        }
    }
}
