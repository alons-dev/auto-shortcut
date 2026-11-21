namespace AutoShortcut
{
    // One of the user's shortcuts. There are three kinds:
    //   - open with a hotkey: Run = "notepad.exe" (a URL, a folder...) and
    //                         Hotkey = "Ctrl+Alt+N", which works everywhere
    //   - open on click:      Run set, no Hotkey
    //   - reminder:           KeysText = "Ctrl+Shift+T", a combination of another app
    //                         kept with a title; clicking it presses it
    internal sealed class Shortcut
    {
        public string Name;
        public string KeysText;
        public string Run;
        public string Args;
        public string Hotkey;

        public bool IsLaunch
        {
            get { return !string.IsNullOrEmpty(Run); }
        }

        public bool HasHotkey
        {
            get { return IsLaunch && !string.IsNullOrEmpty(Hotkey); }
        }
    }
}
