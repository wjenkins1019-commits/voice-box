using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;

namespace VoiceBox
{
    public static class Program
    {
        [DllImport("shcore.dll")]
        static extern int SetProcessDpiAwareness(int value);

        [STAThread]
        public static void Main()
        {
            string log = Path.Combine(Path.GetTempPath(), "voicebox_startup.log");
            try
            {
                File.WriteAllText(log, "start " + DateTime.Now.ToString("HH:mm:ss") + Environment.NewLine);
                try { SetProcessDpiAwareness(2); } catch { }

                Application app = new Application();
                app.ShutdownMode = ShutdownMode.OnMainWindowClose;
                MainWindow w = new MainWindow();

                // Warm the piano tone tables off the UI thread: the first render after launch
                // would otherwise pay for building every table the part needs.
                System.Threading.Thread warm = new System.Threading.Thread(delegate()
                {
                    try { VocalSynth.WarmPianoTables(Project.Notes); } catch { }
                });
                warm.IsBackground = true;
                warm.Priority = System.Threading.ThreadPriority.BelowNormal;
                warm.Start();

                File.AppendAllText(log, "window created " + DateTime.Now.ToString("HH:mm:ss") + Environment.NewLine);
                app.Run(w);
                File.AppendAllText(log, "run returned" + Environment.NewLine);
            }
            catch (Exception ex)
            {
                try { File.AppendAllText(log, ex.ToString() + Environment.NewLine); } catch { }
                MessageBox.Show(ex.ToString(), "Voice Box failed to start");
            }
        }
    }
}
