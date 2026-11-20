using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;

namespace AutoShortcut
{
    internal static class Program
    {
        // Without this, Windows renders the app blurry on scaled displays (125 %, 150 %...).
        [DllImport("user32.dll")]
        private static extern bool SetProcessDPIAware();

        [STAThread]
        private static void Main()
        {
            // Single instance: if one is already running, this one exits silently.
            bool isFirst;
            using (Mutex mutex = new Mutex(true, @"Local\AutoShortcut", out isFirst))
            {
                if (!isFirst) return;

                SetProcessDPIAware();
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);

                // If something fails inside the app, don't crash or fail silently:
                // write it to error.log and tell the user.
                Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
                Application.ThreadException += delegate(object sender, ThreadExceptionEventArgs e)
                {
                    string log = LogError(e.Exception);
                    MessageBox.Show("Something went wrong: " + e.Exception.Message +
                        (log != null ? "\n\nDetails in " + log : ""),
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
