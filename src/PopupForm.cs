using System;
using System.Collections.Generic;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace AutoShortcut
{
    // The pop-up: borderless, always-on-top window in the bottom-right corner of the
    // monitor where the mouse is. It shows the shortcut list and hides when it loses
    // focus or on Esc.
    //
    // It never changes the list itself: it raises events (ShortcutInvoked,
    // EditRequested...) and TrayApp decides what to do.
    internal sealed class PopupForm : Form
    {
        // Sizes at 100 % scale (multiplied with Ui.S).
        private const int PopupWidth = 360;
        private const int HeaderHeight = 48;
        private const int RowHeight = 40;
        private const int SidePad = 6;
        private const int BottomPad = 8;
        private const int MaxListHeight = 480;
        private const int EdgeGap = 12;   // gap to the screen edge and the taskbar

        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();

        // Dark scrollbar (same trick File Explorer uses).
        [DllImport("uxtheme.dll", CharSet = CharSet.Unicode)]
        private static extern int SetWindowTheme(IntPtr hwnd, string appName, string idList);

        private const int WS_EX_TOOLWINDOW = 0x80;   // keep it out of Alt+Tab and the taskbar
        private const int CS_DROPSHADOW = 0x20000;   // soft shadow, like menus

        private readonly Theme _theme;
        private readonly Label _title;
        private readonly Label _hotkeyLabel;
        private readonly IconButton _addButton;
        private readonly IconButton _settingsButton;
        private readonly Panel _list;
        private readonly ContextMenuStrip _rowMenu;
        private readonly ToolStripItem _moveUpItem;
        private readonly ToolStripItem _moveDownItem;
        private readonly ToolTip _tips = new ToolTip();
        private int _rowCount;
        private DateTime _hiddenAt = DateTime.MinValue;
        private IntPtr _previousWindow;

        public event Action<Shortcut> ShortcutInvoked;   // click on a shortcut
        public event Action AddRequested;                 // + button
        public event Action SettingsRequested;            // settings button
        public event Action<int> EditRequested;           // menu > Edit (index)
        public event Action<int> DeleteRequested;         // menu > Delete
        public event Action<int, int> MoveRequested;      // menu > Move (index, -1 up / +1 down)

        public PopupForm()
        {
            _theme = Theme.Current();

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

            _title = new Label();
            _title.Text = "Shortcuts";
            _title.Font = new Font("Segoe UI Semibold", 12F);
            _title.AutoSize = true;
            _title.Location = new Point(Ui.S(16), Ui.S(12));

            _hotkeyLabel = new Label();
            _hotkeyLabel.AutoSize = true;
            _hotkeyLabel.ForeColor = _theme.Subtle;
            _hotkeyLabel.Font = new Font("Segoe UI", 8.25F);

            _addButton = new IconButton(Ui.GlyphAdd, "Add shortcut", _theme);
            _addButton.Click += delegate { Raise(AddRequested); };
            _tips.SetToolTip(_addButton, "Add shortcut  (right-click one to edit it)");

            _settingsButton = new IconButton(Ui.GlyphSettings, "Settings", _theme);
            _settingsButton.Click += delegate { Raise(SettingsRequested); };
            _tips.SetToolTip(_settingsButton, "Settings");

            _list = new Panel();
            _list.AutoScroll = true;
            _list.Padding = new Padding(Ui.S(SidePad), 0, Ui.S(SidePad), 0);
            _list.HandleCreated += delegate
            {
                SetWindowTheme(_list.Handle, _theme.Dark ? "DarkMode_Explorer" : "Explorer", null);
            };

            // Right-click menu on a shortcut.
            _rowMenu = new ContextMenuStrip();
            MenuRenderer.Apply(_rowMenu, _theme);
            _rowMenu.Items.Add("Edit", null, delegate { RaiseForMenuRow(EditRequested); });
            _moveUpItem = _rowMenu.Items.Add("Move up", null, delegate { RaiseMove(-1); });
            _moveDownItem = _rowMenu.Items.Add("Move down", null, delegate { RaiseMove(+1); });
            _rowMenu.Items.Add(new ToolStripSeparator());
            _rowMenu.Items.Add("Delete", null, delegate { RaiseForMenuRow(DeleteRequested); });
            _rowMenu.Opening += delegate
            {
                ShortcutRow row = _rowMenu.SourceControl as ShortcutRow;
                _moveUpItem.Enabled = row != null && row.Index > 0;
                _moveDownItem.Enabled = row != null && row.Index < _rowCount - 1;
            };

            Controls.Add(_title);
            Controls.Add(_hotkeyLabel);
            Controls.Add(_addButton);
            Controls.Add(_settingsButton);
            Controls.Add(_list);
            ResumeLayout(false);

            SetShortcuts(new List<Shortcut>(), "");
        }

        // The window that was in front before the pop-up opened: keys are sent there.
        public IntPtr PreviousWindow { get { return _previousWindow; } }

        public void SetShortcuts(IList<Shortcut> shortcuts, string hotkeyText)
        {
            SuspendLayout();
            _list.SuspendLayout();

            Control[] old = new Control[_list.Controls.Count];
            _list.Controls.CopyTo(old, 0);
            foreach (Control c in old) c.Dispose();

            int width = Ui.S(PopupWidth);
            int rowHeight = Ui.S(RowHeight);
            int listHeight;
            _rowCount = shortcuts.Count;

            if (shortcuts.Count == 0)
            {
                Label empty = new Label();
                empty.Text = "No shortcuts yet. Add one with +";
                empty.ForeColor = _theme.Subtle;
                empty.AutoSize = true;
                empty.Location = new Point(Ui.S(10), Ui.S(4));
                _list.Controls.Add(empty);
                listHeight = Ui.S(32);
            }
            else
            {
                // With Dock = Top the last control added ends up on top,
                // so rows are added from the bottom up.
                for (int i = shortcuts.Count - 1; i >= 0; i--)
                {
                    ShortcutRow row = new ShortcutRow(shortcuts[i], i, _theme);
                    row.Font = Font;
                    row.Height = rowHeight;
                    row.Dock = DockStyle.Top;
                    row.MouseClick += OnRowClick;
                    row.ContextMenuStrip = _rowMenu;
                    _list.Controls.Add(row);
                }
                int maxHeight = Math.Min(Ui.S(MaxListHeight), (int)(Screen.PrimaryScreen.WorkingArea.Height * 0.75));
                listHeight = Math.Min(shortcuts.Count * rowHeight, maxHeight);
            }

            // Header: "Shortcuts  Win + Shift + A"  ...  [+] [gear]
            _hotkeyLabel.Text = hotkeyText ?? "";
            _hotkeyLabel.Location = new Point(_title.Left + _title.PreferredWidth + Ui.S(6), Ui.S(18));
            _settingsButton.Location = new Point(width - Ui.S(8) - _settingsButton.Width, Ui.S(8));
            _addButton.Location = new Point(_settingsButton.Left - Ui.S(2) - _addButton.Width, Ui.S(8));

            _list.Bounds = new Rectangle(0, Ui.S(HeaderHeight), width, listHeight);
            ClientSize = new Size(width, Ui.S(HeaderHeight) + listHeight + Ui.S(BottomPad));

            _list.ResumeLayout(true);
            ResumeLayout(true);
            if (Visible) PlaceBottomRight();   // height changed: reposition
        }

        private void OnRowClick(object sender, MouseEventArgs e)
        {
            if (e.Button != MouseButtons.Left) return;
            ShortcutRow row = (ShortcutRow)sender;
            Action<Shortcut> handler = ShortcutInvoked;
            if (handler != null) handler(row.Shortcut);
        }

        // Menu events are raised once the menu has closed (BeginInvoke), so the
        // handler can open a window or rebuild the list without stepping on it.
        private void RaiseForMenuRow(Action<int> handler)
        {
            ShortcutRow row = _rowMenu.SourceControl as ShortcutRow;
            if (row == null || handler == null) return;
            int index = row.Index;
            BeginInvoke((MethodInvoker)delegate { handler(index); });
        }

        private void RaiseMove(int delta)
        {
            ShortcutRow row = _rowMenu.SourceControl as ShortcutRow;
            Action<int, int> handler = MoveRequested;
            if (row == null || handler == null) return;
            int index = row.Index;
            BeginInvoke((MethodInvoker)delegate { handler(index, delta); });
        }

        private void Raise(Action handler)
        {
            if (handler != null) BeginInvoke((MethodInvoker)delegate { handler(); });
        }

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
            PlaceBottomRight();
            Show();
            Activate();
            if (rememberWindow) _list.AutoScrollPosition = Point.Empty;
        }

        public void HidePopup()
        {
            if (!Visible) return;
            _rowMenu.Close();
            Hide();
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
            HidePopup();
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);
            if (e.KeyCode == Keys.Escape) HidePopup();
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
                _rowMenu.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
