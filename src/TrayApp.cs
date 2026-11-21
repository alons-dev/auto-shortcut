using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Windows.Forms;

namespace AutoShortcut
{
    // The application itself: it has no main window. It lives in the system tray and
    // ties the pieces together: config (JSON) + global hotkey + pop-up + running
    // shortcuts + editor.
    internal sealed class TrayApp : ApplicationContext
    {
        private const string AppName = "AutoShortcut";
        private const int AppHotkeyId = 1;
        private const int FirstTriggerId = 100;   // your shortcuts and moved Windows ones
        private const string InUseByApp = "Already used by another app";

        private readonly HotkeyWindow _hotkey = new HotkeyWindow();
        private readonly Dictionary<int, Trigger> _triggers = new Dictionary<int, Trigger>();
        private string _triggerCombos;        // what is registered now, to skip re-registering
        private readonly WarnOnce _triggerWarning = new WarnOnce();
        private readonly PopupForm _popup = new PopupForm();
        private readonly NotifyIcon _tray = new NotifyIcon();

        private Config _config;
        private string _loadError;            // set if the file had an error when read
        private DateTime _configStamp;        // file timestamp when it was read
        private string _registeredHotkey;     // the hotkey currently registered
        private bool _inDialog;               // an editing window is open
        private readonly WarnOnce _hotkeyWarning = new WarnOnce();

        public TrayApp()
        {
            Theme theme = Theme.Current();
            ContextMenuStrip menu = new ContextMenuStrip();
            MenuRenderer.Apply(menu, theme);
            menu.Items.Add("Show", null, delegate { OpenPopup(); });
            menu.Items.Add("Add shortcut…", null, delegate { EditShortcut(-1); });
            menu.Items.Add("Settings…", null, delegate { OpenSettings(); });
            menu.Items.Add("Edit shortcuts.json", null, delegate { EditConfigFile(); });
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add("Exit", null, delegate { ExitThread(); });

            _tray.Icon = Ui.CreateTrayIcon();
            _tray.Text = AppName;
            _tray.ContextMenuStrip = menu;
            _tray.MouseClick += delegate(object sender, MouseEventArgs e)
            {
                if (e.Button == MouseButtons.Left) TogglePopup();
            };
            _tray.Visible = true;

            _hotkey.Pressed += OnHotkey;
            _popup.EntryInvoked += OnEntryInvoked;
            _popup.WindowsEditRequested += EditWindowsHotkey;
            _popup.PinChanged += SetPinned;
            _popup.FavoriteToggled += ToggleFavorite;
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

        private void OnEntryInvoked(Entry entry)
        {
            if (!entry.Runnable) return;
            IntPtr target = _popup.PreviousWindow;
            _popup.HidePopup();
            PauseOwnHotkey(entry.Shortcut);
            Ran(entry.Ref, ActionRunner.Execute(entry.Shortcut, target));
        }

        private void OnHotkey(int id)
        {
            if (id == AppHotkeyId)
            {
                TogglePopup();
                return;
            }
            Trigger t;
            if (_inDialog || !_triggers.TryGetValue(id, out t)) return;
            PauseOwnHotkey(t.Shortcut);
            // If the pop-up has the focus, act on the window that was in front before it.
            // Kept open, it stays; otherwise it goes away as with a click.
            IntPtr target = _popup.Visible && Form.ActiveForm == _popup ? _popup.PreviousWindow : IntPtr.Zero;
            if (_popup.Visible && !_popup.Pinned)
            {
                target = _popup.PreviousWindow;
                _popup.HidePopup();
            }
            Ran(t.Ref, ActionRunner.ExecuteFromHotkey(t.Shortcut, target));
        }

        // "Keep open" switch. With a broken file it only lasts until the app restarts.
        private void SetPinned(bool pinned)
        {
            _config.Pinned = pinned;
            if (_loadError == null) SaveConfig();
        }

        // Pressing Ctrl+C when a shortcut of yours took Ctrl+C would run that shortcut.
        private void PauseOwnHotkey(Shortcut shortcut)
        {
            KeyCombo combo;
            if (!shortcut.IsLaunch && KeyCombo.TryParse(shortcut.KeysText, out combo)) _hotkey.PauseFor(combo);
        }

        private void Ran(string reference, string error)
        {
            if (error != null)
            {
                Notify(error, ToolTipIcon.Error);
                return;
            }
            // Remember it in Recent. Never for a file with an error: that would mean
            // asking before overwriting it on every click.
            if (_loadError != null) return;
            _config.AddRecent(reference);
            SaveConfig();
        }

        // The pop-up stays open: the star just changes.
        private void ToggleFavorite(Entry entry)
        {
            if (_loadError == null)
            {
                _config.ToggleFavorite(entry.Ref);
                SaveConfig();
                return;
            }
            // The file has an error: ask first (in a dialog, with the pop-up out of the way).
            RunDialog(delegate
            {
                if (!ConfirmOverwriteBrokenFile()) return;
                _config.ToggleFavorite(entry.Ref);
                SaveConfig();
            });
        }

        // ---- Editing the list ----

        // Hides the pop-up, opens one of our windows and comes back to the pop-up after.
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
                HotkeySelf self = HotkeyRules.Custom(isNew ? -1 : index);
                Func<string, bool, string> check = delegate(string combo, bool listens) { return CheckHotkey(combo, self, listens); };
                using (EditorForm form = new EditorForm(isNew ? null : _config.Shortcuts[index], Theme.Current(), check))
                {
                    if (form.ShowDialog() != DialogResult.OK) return;
                    if (!ConfirmOverwriteBrokenFile()) return;
                    if (isNew)
                    {
                        _config.Shortcuts.Add(form.Result);
                    }
                    else
                    {
                        string oldName = _config.Shortcuts[index].Name;
                        _config.Shortcuts[index] = form.Result;
                        _config.RenameCustom(oldName, form.Result.Name);
                    }
                    SaveConfig();
                    if (isNew) _popup.SelectTab(ViewTab.Custom);   // show where it went
                }
            });
        }

        private void DeleteShortcut(int index)
        {
            if (index < 0 || index >= _config.Shortcuts.Count) return;
            RunDialog(delegate
            {
                string name = _config.Shortcuts[index].Name;
                if (!Ask("Delete the shortcut \"" + name + "\"?", MessageBoxIcon.Question)) return;
                if (!ConfirmOverwriteBrokenFile()) return;
                _config.Shortcuts.RemoveAt(index);
                _config.RemoveCustom(name);
                SaveConfig();
            });
        }

        // Moving opens no window: the pop-up stays open and redraws.
        private void MoveShortcut(int index, int delta)
        {
            int target = index + delta;
            if (index < 0 || target < 0 || index >= _config.Shortcuts.Count || target >= _config.Shortcuts.Count) return;
            if (_loadError != null) return;   // don't overwrite a broken file without asking
            Shortcut item = _config.Shortcuts[index];
            _config.Shortcuts.RemoveAt(index);
            _config.Shortcuts.Insert(target, item);
            SaveConfig();
        }

        // Right-click > Edit hotkey on a Windows shortcut.
        private void EditWindowsHotkey(Entry entry)
        {
            if (entry.DefaultKeys == null) return;
            RunDialog(delegate
            {
                string defaultKeys = entry.DefaultKeys;
                HotkeySelf self = HotkeyRules.Windows(defaultKeys);
                Func<string, string> check = delegate(string combo)
                {
                    // Back to its default: Windows handles it, nothing to register.
                    return CheckHotkey(combo, self, HotkeyRules.Normalize(combo) != defaultKeys);
                };
                using (WindowsHotkeyForm form = new WindowsHotkeyForm(Theme.Current(), entry.Name, defaultKeys,
                    HotkeyRules.Current(_config, defaultKeys), check))
                {
                    if (form.ShowDialog() != DialogResult.OK) return;
                    if (!ConfirmOverwriteBrokenFile()) return;
                    HotkeyRules.SetWindowsHotkey(_config, defaultKeys, form.Result);
                    SaveConfig();
                }
            });
        }

        // null if the combination can be used, or why not. listens: the app will
        // register it (not for reminders, nor for a Windows default).
        private string CheckHotkey(string text, HotkeySelf self, bool listens)
        {
            string used = HotkeyRules.UsedBy(_config, text, self);
            if (used != null || !listens) return used;
            KeyCombo combo;
            if (!KeyCombo.TryParse(text, out combo)) return null;
            // Ours and not anyone else's in the list: it's the one being edited.
            if (_hotkey.IsOurs(combo) || _hotkey.IsFree(combo)) return null;
            return InUseByApp;
        }

        private void OpenSettings()
        {
            RunDialog(delegate
            {
                using (SettingsForm form = new SettingsForm(Theme.Current(), _config.Hotkey, TryChangeHotkey,
                    EditConfigFile, RestoreWindowsShortcuts))
                    form.ShowDialog();
            });
        }

        // Settings > Restore Windows shortcuts. Returns what to show, or null if cancelled.
        private string RestoreWindowsShortcuts()
        {
            if (_config.WindowsHotkeys.Count == 0) return "Windows shortcuts already use their default keys.";
            Config preview = Config.Parse(_config.ToJson());
            List<string> losing = HotkeyRules.RestoreWindows(preview);
            string question = "Put every Windows shortcut back on its default keys?";
            if (losing.Count > 0)
                question += "\n\nThese shortcuts of yours use one of those keys and will lose their hotkey " +
                            "(they will still open on click): " + string.Join(", ", losing) + ".";
            if (!Ask(question, MessageBoxIcon.Question)) return null;
            if (!ConfirmOverwriteBrokenFile()) return null;
            HotkeyRules.RestoreWindows(_config);
            SaveConfig();
            return "Windows shortcuts restored.";
        }

        // Returns null if the new combination is registered and saved,
        // or the reason it couldn't be.
        private string TryChangeHotkey(string text)
        {
            KeyCombo combo;
            if (!KeyCombo.TryParse(text, out combo)) return "Check the key combination.";
            string used = HotkeyRules.UsedBy(_config, text, HotkeyRules.App);
            if (used != null) return used + ".";
            if (!ConfirmOverwriteBrokenFile()) return "Not saved.";

            string wanted = combo.ToString();
            if (wanted != _registeredHotkey)
            {
                KeyCombo previous = null;
                if (_registeredHotkey != null) KeyCombo.TryParse(_registeredHotkey, out previous);
                if (!_hotkey.Register(AppHotkeyId, combo))
                {
                    if (previous != null) _hotkey.Register(AppHotkeyId, previous);   // never end up with none
                    return InUseByApp + ".";
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
            return Ask("shortcuts.json has an error (" + _loadError + ").\n\n" +
                "If you save, it will be replaced with the list shown in AutoShortcut and " +
                "any changes made by hand will be lost. Continue?", MessageBoxIcon.Warning);
        }

        // Yes/No question (No by default). The invisible, always-on-top owner window
        // keeps the message from ending up behind other windows.
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

        // ---- Configuration ----

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
                // Don't overwrite the user's file: keep using the last good list.
                if (_config == null) _config = Config.CreateDefault();
                _loadError = ex.Message;
                Notify("shortcuts.json has an error and was not loaded: " + ex.Message, ToolTipIcon.Error);
            }
            _configStamp = FileStamp();
            ApplyConfig();
        }

        private void SaveConfig()
        {
            try
            {
                Config c = _config;
                c.Prune(delegate(string r) { return Library.Find(c, r) != null; });
                c.Save();
                _loadError = null;
            }
            catch (Exception ex)
            {
                Notify("Couldn't save shortcuts.json: " + ex.Message, ToolTipIcon.Error);
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
                _hotkey.Unregister(AppHotkeyId);
                _registeredHotkey = null;
                label = "Invalid shortcut";
                if (_hotkeyWarning.ShouldWarn(label + _config.Hotkey))
                    Notify("The shortcut \"" + _config.Hotkey + "\" is not valid. Change it in Settings.", ToolTipIcon.Warning);
            }
            else if (combo.ToString() == _registeredHotkey)
            {
                label = string.Join(" + ", combo.DisplayParts());   // unchanged
            }
            else if (_hotkey.Register(AppHotkeyId, combo))
            {
                _registeredHotkey = combo.ToString();
                label = string.Join(" + ", combo.DisplayParts());
                _hotkeyWarning.Clear();
            }
            else
            {
                _registeredHotkey = null;
                label = combo + " in use";
                // Saving (e.g. to remember a recent shortcut) comes back here: say it once.
                if (_hotkeyWarning.ShouldWarn(label))
                    Notify(combo + " is already used by another app. Pick another one in Settings " +
                           "(gear button in the panel). Meanwhile, open it by clicking here.", ToolTipIcon.Warning);
            }

            _tray.Text = Truncate(AppName + " (" + label + ")", 63);   // Windows limit
            ApplyTriggers();
            _popup.SetData(_config, label);
        }

        // Registers the hotkeys of your shortcuts and of the Windows shortcuts you moved.
        // Saving (e.g. a recent) comes back here: only re-register when they changed.
        private void ApplyTriggers()
        {
            List<Trigger> wanted = HotkeyRules.Triggers(_config);
            List<string> combos = new List<string>();
            foreach (Trigger t in wanted) combos.Add(t.Combo + "=" + t.Ref);
            string signature = string.Join("\n", combos);

            if (signature == _triggerCombos)
            {
                // Same combinations: just point them at the current shortcuts.
                int same = FirstTriggerId;
                foreach (Trigger t in wanted)
                {
                    if (_triggers.ContainsKey(same)) _triggers[same] = t;
                    same++;
                }
                return;
            }

            foreach (int old in _triggers.Keys) _hotkey.Unregister(old);
            _triggers.Clear();
            _triggerCombos = signature;

            List<string> failed = new List<string>();
            int id = FirstTriggerId;
            foreach (Trigger t in wanted)
            {
                KeyCombo combo;
                if (KeyCombo.TryParse(t.Combo, out combo) && _hotkey.Register(id, combo)) _triggers[id] = t;
                else failed.Add(t.InUseMessage());
                id++;
            }
            if (failed.Count == 0)
            {
                _triggerWarning.Clear();
                return;
            }
            string message = string.Join("\n", failed) + ". Change it by editing the shortcut.";
            if (_triggerWarning.ShouldWarn(message)) Notify(Truncate(message, 255), ToolTipIcon.Warning);
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
                Notify("Couldn't open Notepad: " + ex.Message, ToolTipIcon.Error);
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
            _tray.Visible = false;   // otherwise a "ghost" icon stays until the mouse passes over it
            _tray.Dispose();
            _popup.Dispose();
            base.ExitThreadCore();
        }
    }
}
