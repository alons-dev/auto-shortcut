using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;

namespace AutoShortcut
{
    // Ejecuta un atajo. Devuelve null si ha ido bien o un mensaje de error para el usuario.
    internal static class ActionRunner
    {
        [DllImport("user32.dll")]
        private static extern bool SetForegroundWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern bool IsWindow(IntPtr hWnd);

        // previousWindow: la ventana que estaba delante antes de abrir el pop-up.
        public static string Execute(Shortcut shortcut, IntPtr previousWindow)
        {
            return shortcut.IsLaunch ? Launch(shortcut) : PressKeys(shortcut, previousWindow);
        }

        private static string PressKeys(Shortcut shortcut, IntPtr previousWindow)
        {
            KeyCombo combo;
            if (!KeyCombo.TryParse(shortcut.KeysText, out combo))
                return "No entiendo la combinación \"" + shortcut.KeysText + "\" de \"" + shortcut.Name + "\".";

            // Las teclas van a la ventana que tiene el foco. Al hacer clic el foco es
            // nuestro, así que se lo devolvemos a la ventana de antes y le damos un
            // instante para activarse antes de pulsar.
            if (previousWindow != IntPtr.Zero && IsWindow(previousWindow))
                SetForegroundWindow(previousWindow);
            Thread.Sleep(80);

            if (!KeySender.Send(combo))
                return "Windows no ha dejado enviar " + combo + " (pasa con ventanas abiertas como administrador).";
            return null;
        }

        private static string Launch(Shortcut shortcut)
        {
            // UseShellExecute = como hacer doble clic: vale para programas, archivos,
            // carpetas, URLs (navegador por defecto) y rutas tipo "shell:Downloads".
            ProcessStartInfo psi = new ProcessStartInfo(Environment.ExpandEnvironmentVariables(shortcut.Run));
            psi.UseShellExecute = true;
            if (!string.IsNullOrEmpty(shortcut.Args))
                psi.Arguments = Environment.ExpandEnvironmentVariables(shortcut.Args);
            try
            {
                Process p = Process.Start(psi);
                if (p != null) p.Dispose();
                return null;
            }
            catch (Win32Exception ex)
            {
                return "No se pudo abrir \"" + shortcut.Run + "\": " + ex.Message;
            }
            catch (InvalidOperationException ex)
            {
                return "No se pudo abrir \"" + shortcut.Run + "\": " + ex.Message;
            }
        }
    }
}
