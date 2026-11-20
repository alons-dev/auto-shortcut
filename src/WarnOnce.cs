namespace AutoShortcut
{
    // Remembers the last warning shown so the same one doesn't pop up on every
    // save (running a shortcut or starring one saves the config).
    internal sealed class WarnOnce
    {
        private string _last;

        // true if this warning is new and should be shown.
        public bool ShouldWarn(string warning)
        {
            if (warning == _last) return false;
            _last = warning;
            return true;
        }

        // The problem is gone: warn again if it comes back.
        public void Clear()
        {
            _last = null;
        }
    }
}
