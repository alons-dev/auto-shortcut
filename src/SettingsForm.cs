using System;
using System.Drawing;
using System.Windows.Forms;

namespace AutoShortcut
{
    // Configuración: la combinación que abre AutoShortcut.
    // Al guardar se prueba a registrarla; si otra app la tiene, se avisa y no se cierra.
    internal sealed class SettingsForm : ThemedDialog
    {
        private readonly KeyField _hotkey;
        private readonly Func<string, string> _tryApply;

        // tryApply recibe la combinación y devuelve null si se ha podido usar,
        // o el motivo por el que no.
        public SettingsForm(Theme theme, string currentHotkey, Func<string, string> tryApply, Action editFile)
            : base(theme, "Configuración de AutoShortcut", 480, 214)
        {
            _tryApply = tryApply;

            AddLabel("Tecla para abrir AutoShortcut", 16, 10);
            _hotkey = new KeyField(theme, Ui.S(448), true, "Tecla para abrir AutoShortcut");
            _hotkey.Location = new Point(Ui.S(16), Ui.S(44));
            _hotkey.Value = currentHotkey;
            Controls.Add(_hotkey);

            AddNote("También se abre con clic en su icono de la bandeja del sistema.", 16, 108, 448);

            LinkLabel link = new LinkLabel();
            link.Text = "Editar shortcuts.json a mano";
            link.AutoSize = true;
            link.LinkColor = theme.Accent;
            link.ActiveLinkColor = theme.Accent;
            link.Location = new Point(Ui.S(16), Ui.S(134));
            link.LinkClicked += delegate { editFile(); };
            Controls.Add(link);
        }

        protected override string TrySave()
        {
            if (!_hotkey.IsValid) return "Revisa la combinación.";
            return _tryApply(_hotkey.Value);
        }

        protected override void OnDeactivate(EventArgs e)
        {
            base.OnDeactivate(e);
            _hotkey.StopRecording();
        }
    }
}
