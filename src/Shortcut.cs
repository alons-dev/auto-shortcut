namespace AutoShortcut
{
    // One shortcut in the list. There are two kinds:
    //   - keys:    KeysText = "Win+Shift+S"  -> clicking it presses that key combination
    //   - launch:  Run = "notepad.exe", a URL or a folder (Args optional)
    internal sealed class Shortcut
    {
        public string Name;
        public string KeysText;
        public string Run;
        public string Args;

        public bool IsLaunch
        {
            get { return !string.IsNullOrEmpty(Run); }
        }
    }
}
