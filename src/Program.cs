using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;

namespace AutoShortcut
{
    internal static class Program
    {
        // Sin esto Windows dibuja la app borrosa en pantallas con zoom (125 %, 150 %...).
        [DllImport("user32.dll")]
        private static extern bool SetProcessDPIAware();

        [STAThread]
        private static void Main()
        {
            // Una sola instancia: si ya hay una abierta, esta se cierra sin hacer nada.
            bool isFirst;
            using (Mutex mutex = new Mutex(true, @"Local\AutoShortcut", out isFirst))
            {
                if (!isFirst) return;

                SetProcessDPIAware();
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);

                // Si algo falla dentro de la app, no cerrarla ni quedarse callada:
                // se apunta en error.log y se avisa.
                Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
                Application.ThreadException += delegate(object sender, ThreadExceptionEventArgs e)
                {
                    string log = LogError(e.Exception);
                    MessageBox.Show("Algo ha fallado: " + e.Exception.Message +
                        (log != null ? "\n\nDetalles en " + log : ""),
                        "AutoShortcut", MessageBoxButtons.OK, MessageBoxIcon.Error);
                };

                Application.Run(new TrayApp());
            }
        }

        private static string LogError(Exception ex)
        {
            try
            {
                Directory.CreateDirectory(Config.Folder);
                string path = Path.Combine(Config.Folder, "error.log");
                File.AppendAllText(path, DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + "  " + ex + "\r\n\r\n");
                return path;
            }
            catch
            {
                return null;
            }
        }
    }
}
