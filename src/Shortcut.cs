namespace AutoShortcut
{
    // Un atajo de la lista. Hay dos tipos:
    //   - de teclas:   KeysText = "Win+Shift+S"  -> al hacer clic se pulsa esa combinación
    //   - de abrir:    Run = "notepad.exe" o una URL o carpeta (Args opcional)
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
