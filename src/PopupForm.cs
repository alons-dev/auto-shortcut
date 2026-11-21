using System;
using System.Collections.Generic;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace AutoShortcut
{
    // The pop-up: borderless, always-on-top window in the bottom-right corner of the
    // monitor where the mouse is. It hides when it loses focus or on Esc; with the
    // switch on ("Keep open") it stays while you use other windows.
    //
    //   Shortcuts   Win + Shift + Z          [on/off] [search] [+] [gear]
    //   All  Recent  Favorites  Custom  Windows 11
    //   (All sections) (Text editing) (General) ...      <- Windows 11 tab only
    //   list (ShortcutList)
    //
    // It never changes the config itself: it raises events (EntryInvoked,
    // FavoriteToggled, EditRequested...) and TrayApp decides what to do.
    internal sealed class PopupForm : Form
    {
        // Sizes at 100 % scale (multiplied with Ui.S).
        private const int PopupWidth = 440;
        private const int HeaderHeight = 48;
        private const int TabsHeight = 36;
        private const int ChipsHeight = 40;
        private const int MaxListHeight = 560;
        private const int BottomPad = 8;
        private const int EdgeGap = 12;   // gap to the screen edge and the taskbar

        private static readonly string[] TabLabels = { "All", "Recent", "Favorites", "Custom", "Windows 11" };

        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll")]
        private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint processId);

        private const int WM_ACTIVATE = 0x0006;

        private const int WS_EX_TOOLWINDOW = 0x80;   // keep it out of Alt+Tab and the taskbar
        private const int CS_DROPSHADOW = 0x20000;   // soft shadow, like menus

        private readonly Theme _theme;
        private readonly TextLabel _title;
        private readonly TextLabel _hotkeyLabel;
        private readonly ToggleSwitch _pin;
        private readonly IconButton _searchButton;
        private readonly IconButton _addButton;
        private readonly IconButton _settingsButton;
        private readonly InputBox _searchBox;
        private readonly IconButton _closeSearch;
        private readonly TabStrip _tabs;
        private readonly ChipBar _chips;
        private readonly ShortcutList _list;
        private readonly ContextMenuStrip _menu;
        private readonly ToolTip _tips = new ToolTip();

        private Config _config = new Config();
        private ViewTab _tab = ViewTab.All;
        private string _section;          // Windows 11 chip; null = all sections
        private bool _searching;
        private Entry _menuEntry;
        private DateTime _hiddenAt = DateTime.MinValue;
        private IntPtr _previousWindow;
        private bool _settingData;        // SetData updates the switch: not a click

        public event Action<Entry> EntryInvoked;          // click on a runnable shortcut
        public event Action<Entry> FavoriteToggled;       // star or menu
        public event Action AddRequested;                 // + button
        public event Action SettingsRequested;            // settings button
        public event Action<int> EditRequested;           // menu > Edit (custom index)
        public event Action<int> DeleteRequested;         // menu > Delete
        public event Action<int, int> MoveRequested;      // menu > Move (index, -1 up / +1 down)
        public event Action<Entry> WindowsEditRequested;  // menu > Edit hotkey (Windows shortcut)
        public event Action<bool> PinChanged;             // "Keep open" switch

        public PopupForm()
        {
            // Acrylic behind the pop-up where Windows has it (Windows 11 22H2+, with
            // transparency effects on); opaque everywhere else, Windows 10 included.
            _theme = Theme.Current();
            if (Theme.AcrylicAvailable()) _theme.SetGlass(true);

            SuspendLayout();
            AutoScaleMode = AutoScaleMode.None;   // we scale by hand with Ui.S
            Text = "AutoShortcut";
            FormBorderStyle = FormBorderStyle.None;
            ShowInTaskbar = false;
            TopMost = true;
            StartPosition = FormStartPosition.Manual;
            KeyPreview = true;
            Font = new Font("Segoe UI", 9.75F);
            BackColor = _theme.Background;
            ForeColor = _theme.Foreground;

            _title = new TextLabel(_theme, false);
            _title.Font = new Font("Segoe UI Semibold", 12F);
            _title.Text = "Shortcuts";
            _title.Location = new Point(Ui.S(16), Ui.S(12));

            _hotkeyLabel = new TextLabel(_theme, true);
            _hotkeyLabel.Font = new Font("Segoe UI", 8.25F);

            _pin = new ToggleSwitch("Keep open", _theme);
            _pin.CheckedChanged += delegate
            {
                if (_settingData) return;
                Action<bool> h = PinChanged;
                if (h != null) h(_pin.Checked);
            };
            _tips.SetToolTip(_pin, "Keep open: stays visible when you use other windows");

            _searchButton = new IconButton(Ui.GlyphSearch, "Search", _theme);
            _searchButton.Click += delegate { OpenSearch(""); };
            _tips.SetToolTip(_searchButton, "Search (or just start typing)");

            _addButton = new IconButton(Ui.GlyphAdd, "Add shortcut", _theme);
            _addButton.Click += delegate { Raise(AddRequested); };
            _tips.SetToolTip(_addButton, "Add shortcut  (right-click one to edit it)");

            _settingsButton = new IconButton(Ui.GlyphSettings, "Settings", _theme);
            _settingsButton.Click += delegate { Raise(SettingsRequested); };
            _tips.SetToolTip(_settingsButton, "Settings");

            int width = Ui.S(PopupWidth);
            _closeSearch = new IconButton(Ui.GlyphClose, "Close search", _theme);
            _closeSearch.Click += delegate { CloseSearch(); };
            _closeSearch.Visible = false;
            _searchBox = new InputBox(_theme, width - Ui.S(16) - Ui.S(8) - _closeSearch.Width - Ui.S(8), "Search");
            _searchBox.Font = Font;
            _searchBox.Location = new Point(Ui.S(16), (Ui.S(HeaderHeight) - _searchBox.Height) / 2);
            _searchBox.Visible = false;
            _searchBox.Box.TextChanged += delegate { RefreshList(false); };

            _tabs = new TabStrip(_theme, TabLabels);
            _tabs.SelectedChanged += delegate(int i)
            {
                _tab = (ViewTab)i;
                if (_searching) CloseSearch();
                UpdateLayout();
                RefreshList(false);
            };

            string[] chipLabels = new string[WindowsShortcuts.Sections.Length + 1];
            chipLabels[0] = "All sections";
            Array.Copy(WindowsShortcuts.Sections, 0, chipLabels, 1, WindowsShortcuts.Sections.Length);
            _chips = new ChipBar(_theme, chipLabels);
            _chips.SelectedChanged += delegate(int i)
            {
                _section = i == 0 ? null : WindowsShortcuts.Sections[i - 1];
                RefreshList(false);
            };

            _list = new ShortcutList(_theme);
            _list.Font = Font;
            _list.IsFavorite = delegate(Entry e) { return _config.IsFavorite(e.Ref); };
            _list.EntryClicked += delegate(Entry e) { Action<Entry> h = EntryInvoked; if (h != null) h(e); };
            _list.StarClicked += delegate(Entry e) { Action<Entry> h = FavoriteToggled; if (h != null) h(e); };
            _list.EntryContextMenu += ShowMenu;

            _menu = new ContextMenuStrip();
            MenuRenderer.Apply(_menu, _theme);
            // While the menu is open the pop-up ignores losing focus (see OnDeactivate).
            // If the menu then closes because the user went to another window, the
            // pop-up won't get another chance: hide it here.
            _menu.Closed += delegate(object sender, ToolStripDropDownClosedEventArgs e)
            {
                if (e.CloseReason != ToolStripDropDownCloseReason.AppFocusChange &&
                    e.CloseReason != ToolStripDropDownCloseReason.AppClicked) return;
                BeginInvoke((MethodInvoker)delegate
                {
                    if (Form.ActiveForm != this && !Pinned) HidePopup();
                });
            };

            Controls.Add(_title);
            Controls.Add(_hotkeyLabel);
            Controls.Add(_pin);
            Controls.Add(_searchButton);
            Controls.Add(_addButton);
            Controls.Add(_settingsButton);
            Controls.Add(_searchBox);
            Controls.Add(_closeSearch);
            Controls.Add(_tabs);
            Controls.Add(_chips);
            Controls.Add(_list);
            ResumeLayout(false);

            UpdateLayout();
            RefreshList(false);
        }

        // The window that was in front before the pop-up opened (or, kept open, the
        // last one you used): keys are sent there.
        public IntPtr PreviousWindow { get { return _previousWindow; } }

        public bool Pinned { get { return _pin.Checked; } }

        // New config (after loading or saving): same tab, chip, search and scroll.
        public void SetData(Config config, string hotkeyText)
        {
            _config = config;
            _settingData = true;
            _pin.Checked = config.Pinned;
            _settingData = false;
            _hotkeyLabel.Text = hotkeyText ?? "";
            UpdateLayout();
            RefreshList(true);
        }

        public void SelectTab(ViewTab tab)
        {
            if (_searching) CloseSearch();
            _tab = tab;
            _tabs.Selected = (int)tab;
            UpdateLayout();
            RefreshList(false);
        }

        // ---- Layout ----

        private void UpdateLayout()
        {
            int width = Ui.S(PopupWidth);
            Rectangle area = Screen.FromPoint(Cursor.Position).WorkingArea;
            int fixedPart = Ui.S(HeaderHeight) + Ui.S(TabsHeight) + Ui.S(ChipsHeight) + Ui.S(BottomPad);
            int listMax = Math.Min(Ui.S(MaxListHeight), (int)(area.Height * 0.75) - fixedPart);
            int height = fixedPart + Math.Max(Ui.S(120), listMax);

            // Header: "Shortcuts  Win + Shift + Z"  ...  [search] [+] [gear]
            _hotkeyLabel.Location = new Point(_title.Right + Ui.S(6), Ui.S(18));
            _settingsButton.Location = new Point(width - Ui.S(8) - _settingsButton.Width, Ui.S(8));
            _addButton.Location = new Point(_settingsButton.Left - Ui.S(2) - _addButton.Width, Ui.S(8));
            _searchButton.Location = new Point(_addButton.Left - Ui.S(2) - _searchButton.Width, Ui.S(8));
            _pin.Location = new Point(_searchButton.Left - Ui.S(4) - _pin.Width, Ui.S(8));
            _closeSearch.Location = new Point(width - Ui.S(8) - _closeSearch.Width, Ui.S(8));

            _title.Visible = _hotkeyLabel.Visible = !_searching;
            _searchButton.Visible = _addButton.Visible = _settingsButton.Visible = _pin.Visible = !_searching;
            _searchBox.Visible = _closeSearch.Visible = _searching;
            _tabs.Dimmed = _searching;

            int y = Ui.S(HeaderHeight);
            _tabs.Bounds = new Rectangle(0, y, width, Ui.S(TabsHeight));
            y += Ui.S(TabsHeight);

            // The chips only show on the Windows 11 tab; elsewhere the list takes their
            // place, so the window keeps the same size and doesn't jump.
            bool chips = _tab == ViewTab.Windows11 && !_searching;
            _chips.Visible = chips;
            if (chips)
            {
                _chips.Bounds = new Rectangle(0, y, width, Ui.S(ChipsHeight));
                y += Ui.S(ChipsHeight);
            }
            _list.Bounds = new Rectangle(0, y, width, height - Ui.S(BottomPad) - y);

            if (ClientSize != new Size(width, height))
            {
                ClientSize = new Size(width, height);
                if (Visible) PlaceBottomRight();
            }
        }

        private string Query
        {
            get { return _searching ? _searchBox.Text : null; }
        }

        private void RefreshList(bool keepScroll)
        {
            string query = Query;
            bool hasQuery = query != null && query.Replace("+", " ").Trim().Length > 0;
            string empty;
            if (hasQuery) empty = "No shortcuts match \"" + query.Trim() + "\".";
            else if (_searching) empty = "Type to search all shortcuts.";
            else if (_tab == ViewTab.Recent) empty = "Shortcuts you run will show up here.";
            else if (_tab == ViewTab.Favorites) empty = "Star a shortcut to keep it here.";
            else empty = "No shortcuts yet. Add one with +";

            List<ListItem> items = _searching && !hasQuery
                ? new List<ListItem>()
                : Library.View(_config, _tab, _section, query);
            _list.SetItems(items, empty, keepScroll);
        }

        // ---- Search ----

        private void OpenSearch(string initial)
        {
            if (!_searching)
            {
                _searching = true;
                UpdateLayout();
            }
            _searchBox.Text = initial;
            _searchBox.Box.Focus();
            _searchBox.Box.SelectionStart = _searchBox.Text.Length;
            RefreshList(false);
        }

        private void CloseSearch()
        {
            if (!_searching) return;
            _searching = false;
            _searchBox.Text = "";
            UpdateLayout();
            RefreshList(false);
        }

        protected override void OnKeyPress(KeyPressEventArgs e)
        {
            base.OnKeyPress(e);
            // Typing a letter or number anywhere in the pop-up starts a search.
            if (!_searching && char.IsLetterOrDigit(e.KeyChar))
            {
                e.Handled = true;
                OpenSearch(e.KeyChar.ToString());
            }
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);
            if (e.KeyCode != Keys.Escape) return;
            e.Handled = true;
            e.SuppressKeyPress = true;
            if (_searching) CloseSearch();   // first Esc: leave the search
            else HidePopup();                // second Esc: close
        }

        // ---- Right-click menu ----

        private void ShowMenu(Entry entry, Point screen)
        {
            _menuEntry = entry;
            _menu.Items.Clear();
            bool favorite = _config.IsFavorite(entry.Ref);
            string favoriteText = favorite ? "Remove from favorites" : "Add to favorites";

            if (entry.IsCustom)
            {
                int index = entry.CustomIndex;
                _menu.Items.Add("Edit", null, delegate { RaiseLater(EditRequested, index); });
                if (_tab == ViewTab.Custom && !_searching)
                {
                    ToolStripItem up = _menu.Items.Add("Move up", null, delegate { RaiseMove(index, -1); });
                    ToolStripItem down = _menu.Items.Add("Move down", null, delegate { RaiseMove(index, +1); });
                    up.Enabled = index > 0;
                    down.Enabled = index < _config.Shortcuts.Count - 1;
                }
                _menu.Items.Add(new ToolStripSeparator());
                _menu.Items.Add(favoriteText, null, delegate { RaiseFavorite(); });
                _menu.Items.Add(new ToolStripSeparator());
                _menu.Items.Add("Delete", null, delegate { RaiseLater(DeleteRequested, index); });
            }
            else
            {
                if (entry.DefaultKeys != null)
                {
                    _menu.Items.Add("Edit hotkey…", null, delegate { RaiseWindowsEdit(); });
                    _menu.Items.Add(new ToolStripSeparator());
                }
                _menu.Items.Add(favoriteText, null, delegate { RaiseFavorite(); });
            }
            _menu.Show(screen);
        }

        // Menu events are raised once the menu has closed (BeginInvoke), so the
        // handler can open a window or rebuild the list without stepping on it.
        private void RaiseLater(Action<int> handler, int index)
        {
            if (handler != null) BeginInvoke((MethodInvoker)delegate { handler(index); });
        }

        private void RaiseMove(int index, int delta)
        {
            Action<int, int> handler = MoveRequested;
            if (handler != null) BeginInvoke((MethodInvoker)delegate { handler(index, delta); });
        }

        private void RaiseWindowsEdit()
        {
            Entry entry = _menuEntry;
            Action<Entry> handler = WindowsEditRequested;
            if (handler != null && entry != null) BeginInvoke((MethodInvoker)delegate { handler(entry); });
        }

        private void RaiseFavorite()
        {
            Entry entry = _menuEntry;
            Action<Entry> handler = FavoriteToggled;
            if (handler != null && entry != null) BeginInvoke((MethodInvoker)delegate { handler(entry); });
        }

        private void Raise(Action handler)
        {
            if (handler != null) BeginInvoke((MethodInvoker)delegate { handler(); });
        }

        // ---- Window ----

        protected override CreateParams CreateParams
        {
            get
            {
                CreateParams cp = base.CreateParams;
                cp.ExStyle |= WS_EX_TOOLWINDOW;
                cp.ClassStyle |= CS_DROPSHADOW;
                return cp;
            }
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            Ui.ApplyFrame(Handle, _theme.Dark, Ui.CornerRound);
            // If Windows says no, go opaque before anything is painted.
            if (_theme.Glass && !Ui.TryAcrylic(Handle)) _theme.SetGlass(false);
        }

        // The background (and the gaps between controls): tint on acrylic.
        protected override void OnPaintBackground(PaintEventArgs e)
        {
            Glass.Paint(e, _theme, null);
        }

        public void Toggle()
        {
            if (Visible)
            {
                HidePopup();
                return;
            }
            // When the tray icon is clicked while the pop-up is open, the pop-up first
            // loses focus (and hides) and then the click arrives: don't reopen it.
            if ((DateTime.Now - _hiddenAt).TotalMilliseconds < 300) return;
            ShowPopup(true);
        }

        // rememberWindow = false when coming back from one of our dialogs: the
        // "previous" window is still the one that was in front when the pop-up first opened.
        public void ShowPopup(bool rememberWindow)
        {
            if (!Visible && rememberWindow) _previousWindow = GetForegroundWindow();
            UpdateLayout();   // the monitor under the mouse may be a different size
            PlaceBottomRight();
            Show();
            Activate();
            if (rememberWindow) _list.ScrollToTop();
        }

        public void HidePopup()
        {
            if (!Visible) return;
            _menu.Close();
            Hide();
            CloseSearch();
            _hiddenAt = DateTime.Now;
        }

        private void PlaceBottomRight()
        {
            // WorkingArea is the screen minus the taskbar.
            Rectangle area = Screen.FromPoint(Cursor.Position).WorkingArea;
            int gap = Ui.S(EdgeGap);
            Location = new Point(area.Right - Width - gap, area.Bottom - Height - gap);
        }

        protected override void OnDeactivate(EventArgs e)
        {
            base.OnDeactivate(e);
            // The right-click menu takes the focus for a moment: that isn't leaving.
            if (_menu.Visible || Pinned) return;
            HidePopup();
        }

        protected override void WndProc(ref Message m)
        {
            // Kept open, you may use other windows and come back: when it's activated,
            // the window that had the focus (lParam) is where keys should go now.
            // Our own windows (dialogs, menus) don't count.
            if (m.Msg == WM_ACTIVATE && (m.WParam.ToInt64() & 0xFFFF) != 0 && Pinned && IsOtherApp(m.LParam))
                _previousWindow = m.LParam;
            base.WndProc(ref m);
        }

        private static bool IsOtherApp(IntPtr hwnd)
        {
            if (hwnd == IntPtr.Zero) return false;
            uint pid;
            GetWindowThreadProcessId(hwnd, out pid);
            return pid != 0 && pid != (uint)System.Diagnostics.Process.GetCurrentProcess().Id;
        }

        // Closing the window (Alt+F4) only hides it: the app keeps running in the tray.
        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            if (e.CloseReason == CloseReason.UserClosing)
            {
                e.Cancel = true;
                HidePopup();
            }
            base.OnFormClosing(e);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _tips.Dispose();
                _menu.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
