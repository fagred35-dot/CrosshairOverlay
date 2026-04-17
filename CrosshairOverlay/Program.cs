using System;
using System.IO;
using System.Windows.Forms;

namespace CrosshairOverlay
{
    internal static class Program
    {
        [STAThread]
        static void Main()
        {
            // Global exception loggers (write to crash.log next to exe or in settings folder).
            Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
            Application.ThreadException += (_, e) => LogAndShow(e.Exception);
            AppDomain.CurrentDomain.UnhandledException += (_, e) => LogAndShow(e.ExceptionObject as Exception);

            Application.SetHighDpiMode(HighDpiMode.PerMonitorV2);
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new OverlayForm());
        }

        private static void LogAndShow(Exception? ex)
        {
            if (ex == null) return;
            try
            {
                string dir = Path.GetDirectoryName(Application.ExecutablePath) ?? ".";
                string path = Path.Combine(dir, "crash.log");
                File.AppendAllText(path,
                    $"[{DateTime.Now:O}] {ex.GetType().Name}: {ex.Message}\n{ex.StackTrace}\n---\n");
            }
            catch { }
            try
            {
                MessageBox.Show(
                    "Произошла ошибка. Подробности в crash.log рядом с exe.\n\n"
                    + ex.GetType().Name + ": " + ex.Message + "\n\n"
                    + ex.StackTrace,
                    "Crosshair Overlay — ошибка",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            catch { }
        }
    }
}