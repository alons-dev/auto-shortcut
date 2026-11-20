using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Windows.Forms;

namespace AutoShortcut
{
    // La aplicación en sí: no tiene ventana principal. Vive en la bandeja del sistema
    // y une las piezas: configuración (JSON) + hotkey global + pop-up + ejecutar atajos
    // + editor.
    internal sealed class TrayApp : ApplicationContext
    {
        private const string AppName = "AutoShortcut";

        private readonly HotkeyWindow _hotkey = new HotkeyWindow();
        private readonly PopupForm _popup = new PopupForm();
        private readonly NotifyIcon _tray = new NotifyIcon();

        private Config _config;
        private string _loadError;            // si el archivo tenía un error al leerlo
        private DateTime _configStamp;        // fecha del archivo cuando se leyó
        private string _registeredHotkey;     // la que está registrada ahora mismo
        private bool _inDialog;               // hay una ventana de edición abierta

        public TrayApp()
        {
            Theme theme = Theme.Current();
            ContextMenuStrip menu = new ContextMenuStrip();
            MenuRenderer.Apply(menu, theme);
            menu.Items.Add("Mostrar", null, delegate { OpenPopup(); });
            menu.Items.Add("Añadir atajo…", null, delegate { EditShortcut(-1); });
            menu.Items.Add("Configuración…", null, delegate { OpenSettings(); });
            menu.Items.Add("Editar shortcuts.json", null, delegate { EditConfigFile(); });
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add("Salir", null, delegate { ExitThread(); });

            _tray.Icon = Ui.CreateTrayIcon();
            _tray.Text = AppName;
            _tray.ContextMenuStrip = menu;
            _tray.MouseClick += delegate(object sender, MouseEventArgs e)
            {
                if (e.Button == MouseButtons.Left) TogglePopup();
            };
            _tray.Visible = true;

            _hotkey.Pressed += delegate { TogglePopup(); };
            _popup.ShortcutInvoked += OnShortcutInvoked;
            _popup.AddRequested += delegate { EditShortcut(-1); };
            _popup.EditRequested += EditShortcut;
            _popup.DeleteRequested += DeleteShortcut;
            _popup.MoveRequested += MoveShortcut;
            _popup.SettingsRequested += OpenSettings;

            LoadConfig();
        }

        private void TogglePopup()
        {
            if (_inDialog) return;
            if (!_popup.Visible) ReloadIfChanged();
            _popup.Toggle();
        }

        private void OpenPopup()
        {
            if (_inDialog) return;
            ReloadIfChanged();
            _popup.ShowPopup(true);
        }

        private void OnShortcutInvoked(Shortcut shortcut)
        {
            IntPtr target = _popup.PreviousWindow;
            _popup.HidePopup();
            string error = ActionRunner.Execute(shortcut, target);
            if (error != null) Notify(error, ToolTipIcon.Error);
        }

        // ---- Editar la lista ----

        // Oculta el pop-up, abre una ventana nuestra y al terminar vuelve al pop-up.
        private void RunDialog(Action dialog)
        {
            if (_inDialog) return;
            bool cameFromPopup = _popup.Visible;
            _inDialog = true;
            _popup.HidePopup();
            try
            {
                ReloadIfChanged();
                dialog();
            }
            finally
            {
                _inDialog = false;
            }
            if (cameFromPopup) _popup.ShowPopup(false);
        }

        private void EditShortcut(int index)
        {
            RunDialog(delegate
            {
                bool isNew = index < 0 || index >= _config.Shortcuts.Count;
                using (EditorForm form = new EditorForm(isNew ? null : _config.Shortcuts[index], Theme.Current()))
                {
                    if (form.ShowDialog() != DialogResult.OK) return;
                    if (!ConfirmOverwriteBrokenFile()) return;
                    if (isNew) _config.Shortcuts.Add(form.Result);
                    else _config.Shortcuts[index] = form.Result;
                    SaveConfig();
                }
            });
        }

        private void DeleteShortcut(int index)
        {
            if (index < 0 || index >= _config.Shortcuts.Count) return;
            RunDialog(delegate
            {
                string name = _config.Shortcuts[index].Name;
                if (!Ask("¿Borrar el atajo \"" + name + "\"?", MessageBoxIcon.Question)) return;
                if (!ConfirmOverwriteBrokenFile()) return;
                _config.Shortcuts.RemoveAt(index);
                SaveConfig();
            });
        }

        // Mover no abre ninguna ventana: el pop-up sigue abierto y se redibuja.
        private void MoveShortcut(int index, int delta)
        {
            int target = index + delta;
            if (index < 0 || target < 0 || index >= _config.Shortcuts.Count || target >= _config.Shortcuts.Count) return;
            if (_loadError != null) return;   // no reescribir un archivo con errores sin preguntar
            Shortcut item = _config.Shortcuts[index];
            _config.Shortcuts.RemoveAt(index);
            _config.Shortcuts.Insert(target, item);
            SaveConfig();
        }

        private void OpenSettings()
        {
            RunDialog(delegate
            {
                using (SettingsForm form = new SettingsForm(Theme.Current(), _config.Hotkey, TryChangeHotkey, EditConfigFile))
                    form.ShowDialog();
            });
        }

        // Devuelve null si la nueva combinación ya está registrada y guardada,
        // o el motivo por el que no se ha podido.
        private string TryChangeHotkey(string text)
        {
            KeyCombo combo;
            if (!KeyCombo.TryParse(text, out combo)) return "Revisa la combinación.";
            if (!ConfirmOverwriteBrokenFile()) return "No se ha guardado.";

            string wanted = combo.ToString();
            if (wanted != _registeredHotkey)
            {
                KeyCombo previous = null;
                if (_registeredHotkey != null) KeyCombo.TryParse(_registeredHotkey, out previous);
                if (!_hotkey.Register(combo))
                {
                    if (previous != null) _hotkey.Register(previous);   // no quedarnos sin ninguna
                    return wanted + " ya la usa otra aplicación. Prueba otra.";
                }
                _registeredHotkey = wanted;
            }
            _config.Hotkey = wanted;
            SaveConfig();
            return null;
        }

        private bool ConfirmOverwriteBrokenFile()
        {
            if (_loadError == null) return true;
            return Ask("shortcuts.json tiene un error (" + _loadError + ").\n\n" +
                "Si guardas, se sustituirá por la lista que ves en AutoShortcut y se perderán " +
                "los cambios hechos a mano. ¿Continuar?", MessageBoxIcon.Warning);
        }

        // Pregunta Sí/No (por defecto No). La ventana dueña, invisible y "siempre encima",
        // hace que el mensaje no quede tapado por otras ventanas.
        private static bool Ask(string text, MessageBoxIcon icon)
        {
            using (Form owner = new Form())
            {
                owner.TopMost = true;
                owner.ShowInTaskbar = false;
                owner.CreateControl();
                return MessageBox.Show(owner, text, AppName, MessageBoxButtons.YesNo, icon,
                    MessageBoxDefaultButton.Button2) == DialogResult.Yes;
            }
        }

        // ---- Configuración ----

        private void ReloadIfChanged()
        {
            if (FileStamp() != _configStamp) LoadConfig();
        }

        private static DateTime FileStamp()
        {
            return File.Exists(Config.FilePath) ? File.GetLastWriteTimeUtc(Config.FilePath) : DateTime.MinValue;
        }

        private void LoadConfig()
        {
            try
            {
                _config = Config.Load();
                _loadError = null;
            }
            catch (Exception ex)
            {
                // No machacamos el archivo del usuario: seguimos con la última lista buena.
                if (_config == null) _config = Config.CreateDefault();
                _loadError = ex.Message;
                Notify("shortcuts.json tiene un error y no se ha cargado: " + ex.Message, ToolTipIcon.Error);
            }
            _configStamp = FileStamp();
            ApplyConfig();
        }

        private void SaveConfig()
        {
            try
            {
                _config.Save();
                _loadError = null;
            }
            catch (Exception ex)
            {
                Notify("No se ha podido guardar shortcuts.json: " + ex.Message, ToolTipIcon.Error);
            }
            _configStamp = FileStamp();
            ApplyConfig();
        }

        private void ApplyConfig()
        {
            string label;
            KeyCombo combo;
            if (!KeyCombo.TryParse(_config.Hotkey, out combo))
            {
                _hotkey.Unregister();
                _registeredHotkey = null;
                label = "Tecla no válida";
                Notify("La tecla \"" + _config.Hotkey + "\" no es válida. Cámbiala en Configuración.", ToolTipIcon.Warning);
            }
            else if (combo.ToString() == _registeredHotkey)
            {
                label = string.Join(" + ", combo.DisplayParts());   // sin cambios
            }
            else if (_hotkey.Register(combo))
            {
                _registeredHotkey = combo.ToString();
                label = string.Join(" + ", combo.DisplayParts());
            }
            else
            {
                _registeredHotkey = null;
                label = combo + " ocupada";
                Notify(combo + " ya la usa otra aplicación. Elige otra en Configuración " +
                       "(botón de la rueda del panel). Mientras, ábrelo con clic aquí.", ToolTipIcon.Warning);
            }

            _tray.Text = Truncate(AppName + " (" + label + ")", 63);   // límite de Windows
            _popup.SetShortcuts(_config.Shortcuts, label);
        }

        private void EditConfigFile()
        {
            if (!File.Exists(Config.FilePath)) LoadConfig();
            try
            {
                Process.Start("notepad.exe", "\"" + Config.FilePath + "\"");
            }
            catch (Exception ex)
            {
                Notify("No se pudo abrir el Bloc de notas: " + ex.Message, ToolTipIcon.Error);
            }
        }

        private void Notify(string message, ToolTipIcon icon)
        {
            _tray.ShowBalloonTip(6000, AppName, message, icon);
        }

        private static string Truncate(string s, int max)
        {
            return s.Length <= max ? s : s.Substring(0, max);
        }

        protected override void ExitThreadCore()
        {
            _hotkey.Dispose();
            _tray.Visible = false;   // si no, el icono se queda "fantasma" hasta pasar el ratón
            _tray.Dispose();
            _popup.Dispose();
            base.ExitThreadCore();
        }
    }
}
