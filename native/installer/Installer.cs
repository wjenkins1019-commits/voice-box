using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Microsoft.Win32;

// Voice Box setup. The payload (VoiceBox.exe) is appended to this binary, followed by a
// 40-byte trailer: magic, payload offset, payload length and the version numbers. The same
// executable installs and, when copied into the install folder as the uninstaller, removes:
// the app, the Start-menu shortcut and the Add/Remove Programs entry.
namespace VoiceBoxSetup
{
    public static class Program
    {
        const string TrailerMagic = "VBSETUP1";
        const int TrailerSize = 40;
        const string AppName = "Voice Box";
        const string ExeName = "VoiceBox.exe";
        const string UninstallName = "Uninstall Voice Box.exe";
        const string UninstallKey = @"Software\Microsoft\Windows\CurrentVersion\Uninstall\VoiceBox";

        static byte[] _payload;
        static string _self;
        static string _version = "0.0.0";

        [STAThread]
        public static int Main(string[] args)
        {
            bool uninstall = false, silent = false;
            foreach (string a in args)
            {
                string s = a.ToLowerInvariant();
                if (s == "/uninstall" || s == "--uninstall" || s == "-u") uninstall = true;
                if (s == "/silent" || s == "--silent" || s == "-s") silent = true;
            }

            _self = Process.GetCurrentProcess().MainModule.FileName;
            if (!ReadPayload())
            {
                Log("not a setup package");
                // Never put up a dialog in silent mode: nobody is there to dismiss it and the
                // process would hang instead of returning an error code.
                if (!silent) MessageBox.Show("This file is not a Voice Box setup package.", AppName);
                return 2;
            }

            if (silent)
            {
                Log("silent start, uninstall=" + uninstall + ", version " + _version);
                try
                {
                    if (uninstall) Uninstall();
                    else Install();
                    Log((uninstall ? "uninstall" : "install") + " ok, version " + _version);
                    return 0;
                }
                catch (Exception ex)
                {
                    Log("failed: " + ex.Message);
                    return 1;
                }
            }

            try
            {
                Application app = new Application();
                app.ShutdownMode = ShutdownMode.OnMainWindowClose;
                app.Run(BuildWindow(uninstall));
                Log("window closed");
                return 0;
            }
            catch (Exception ex)
            {
                Log("ui failed: " + ex);
                MessageBox.Show(ex.Message, AppName);
                return 3;
            }
        }

        // ---- payload ------------------------------------------------------

        static bool ReadPayload()
        {
            try
            {
                FileInfo info = new FileInfo(_self);
                if (info.Length < TrailerSize) return false;
                using (FileStream fs = File.OpenRead(_self))
                {
                    fs.Seek(-TrailerSize, SeekOrigin.End);
                    byte[] trailer = new byte[TrailerSize];
                    int got = 0;
                    while (got < TrailerSize)
                    {
                        int n = fs.Read(trailer, got, TrailerSize - got);
                        if (n <= 0) break;
                        got += n;
                    }
                    if (got != TrailerSize) return false;
                    if (Encoding.ASCII.GetString(trailer, 0, 8) != TrailerMagic) return false;

                    long offset = BitConverter.ToInt64(trailer, 8);
                    long length = BitConverter.ToInt64(trailer, 16);
                    if (offset <= 0 || length <= 0 || length > int.MaxValue) return false;
                    if (offset + length > info.Length) return false;

                    int major = BitConverter.ToInt32(trailer, 24);
                    int minor = BitConverter.ToInt32(trailer, 28);
                    int patch = BitConverter.ToInt32(trailer, 32);
                    _version = major + "." + minor + "." + patch;

                    int total = (int)length;
                    _payload = new byte[total];
                    fs.Seek(offset, SeekOrigin.Begin);
                    int read = 0;
                    while (read < total)
                    {
                        int n = fs.Read(_payload, read, total - read);
                        if (n <= 0) break;
                        read += n;
                    }
                    return read == total;
                }
            }
            catch (Exception ex)
            {
                Log("payload read failed: " + ex.Message);
                return false;
            }
        }

        // ---- paths --------------------------------------------------------

        static string InstallDir
        {
            get
            {
                return Path.Combine(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs"), "VoiceBox");
            }
        }

        static string TargetExe { get { return Path.Combine(InstallDir, ExeName); } }
        static string TargetUninstall { get { return Path.Combine(InstallDir, UninstallName); } }

        static string StartMenuShortcut
        {
            get { return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Programs), "Voice Box.lnk"); }
        }

        // ---- install / uninstall ------------------------------------------

        static void Install()
        {
            Directory.CreateDirectory(InstallDir);
            File.WriteAllBytes(TargetExe, _payload);
            File.Copy(_self, TargetUninstall, true);
            CreateShortcut();
            WriteUninstallEntry();
            Log("installed to " + InstallDir);
        }

        static void Uninstall()
        {
            try { if (File.Exists(StartMenuShortcut)) File.Delete(StartMenuShortcut); }
            catch (Exception ex) { Log("shortcut delete failed: " + ex.Message); }
            try { Registry.CurrentUser.DeleteSubKeyTree(UninstallKey, false); }
            catch (Exception ex) { Log("registry delete failed: " + ex.Message); }
            try { if (File.Exists(TargetExe)) File.Delete(TargetExe); }
            catch (Exception ex) { Log("exe delete failed: " + ex.Message); }

            // The uninstaller cannot delete the file it is running from, so the folder is
            // removed by a detached shell once this process has exited.
            try
            {
                ProcessStartInfo psi = new ProcessStartInfo("cmd.exe",
                    "/c timeout /t 2 /nobreak >nul & rmdir /s /q \"" + InstallDir + "\"");
                psi.CreateNoWindow = true;
                psi.UseShellExecute = false;
                psi.WindowStyle = ProcessWindowStyle.Hidden;
                Process.Start(psi);
            }
            catch (Exception ex) { Log("cleanup spawn failed: " + ex.Message); }
        }

        static void CreateShortcut()
        {
            Type shellType = Type.GetTypeFromProgID("WScript.Shell");
            if (shellType == null) throw new Exception("WScript.Shell is unavailable, so the Start-menu shortcut could not be created");
            object shell = Activator.CreateInstance(shellType);
            object link = shellType.InvokeMember("CreateShortcut", BindingFlags.InvokeMethod, null, shell, new object[] { StartMenuShortcut });
            Type linkType = link.GetType();
            linkType.InvokeMember("TargetPath", BindingFlags.SetProperty, null, link, new object[] { TargetExe });
            linkType.InvokeMember("WorkingDirectory", BindingFlags.SetProperty, null, link, new object[] { InstallDir });
            linkType.InvokeMember("Description", BindingFlags.SetProperty, null, link, new object[] { "Voice Box singing-synthesis workstation" });
            linkType.InvokeMember("IconLocation", BindingFlags.SetProperty, null, link, new object[] { TargetExe + ",0" });
            linkType.InvokeMember("Save", BindingFlags.InvokeMethod, null, link, null);
            Log("shortcut written to " + StartMenuShortcut);
        }

        static void WriteUninstallEntry()
        {
            using (RegistryKey key = Registry.CurrentUser.CreateSubKey(UninstallKey))
            {
                key.SetValue("DisplayName", AppName);
                key.SetValue("DisplayVersion", _version);
                key.SetValue("Publisher", "Voice Box");
                key.SetValue("DisplayIcon", TargetExe + ",0");
                key.SetValue("InstallLocation", InstallDir);
                key.SetValue("UninstallString", "\"" + TargetUninstall + "\" /uninstall");
                key.SetValue("QuietUninstallString", "\"" + TargetUninstall + "\" /uninstall /silent");
                key.SetValue("NoModify", 1, RegistryValueKind.DWord);
                key.SetValue("NoRepair", 1, RegistryValueKind.DWord);
                key.SetValue("EstimatedSize", (int)(_payload.Length / 1024), RegistryValueKind.DWord);
            }
            Log("uninstall entry written");
        }

        // ---- window -------------------------------------------------------

        static Window BuildWindow(bool uninstall)
        {
            Window w = new Window();
            w.Title = uninstall ? "Uninstall Voice Box" : "Voice Box setup";
            w.Width = 540;
            w.Height = 320;
            w.WindowStartupLocation = WindowStartupLocation.CenterScreen;
            w.ResizeMode = ResizeMode.NoResize;
            w.Background = new SolidColorBrush(Color.FromRgb(10, 20, 36));
            w.Foreground = new SolidColorBrush(Color.FromRgb(226, 240, 255));
            w.FontFamily = new FontFamily("Segoe UI");

            StackPanel root = new StackPanel();
            root.Margin = new Thickness(26, 22, 26, 18);

            TextBlock title = new TextBlock();
            title.Text = "VOICE BOX";
            title.FontSize = 20;
            title.FontWeight = FontWeights.Bold;
            title.Foreground = new SolidColorBrush(Color.FromRgb(120, 200, 255));

            TextBlock sub = new TextBlock();
            sub.Text = (uninstall ? "Remove Voice Box " : "Install Voice Box ") + _version;
            sub.Margin = new Thickness(0, 4, 0, 16);
            sub.FontSize = 13;

            TextBlock body = new TextBlock();
            body.Text = uninstall
                ? "This removes the application, the Start-menu shortcut and the\nuninstall entry.\n\n" + InstallDir
                : "Installs for the current user only - no administrator rights.\n\n" + InstallDir +
                  "\n\nA Start-menu shortcut and an entry in Apps & features are added.";
            body.TextWrapping = TextWrapping.Wrap;
            body.FontSize = 12;
            body.LineHeight = 18;
            body.Foreground = new SolidColorBrush(Color.FromRgb(170, 195, 220));

            TextBlock status = new TextBlock();
            status.Margin = new Thickness(0, 14, 0, 0);
            status.FontSize = 12;
            status.TextWrapping = TextWrapping.Wrap;
            status.Foreground = new SolidColorBrush(Color.FromRgb(130, 220, 170));

            StackPanel buttons = new StackPanel();
            buttons.Orientation = Orientation.Horizontal;
            buttons.HorizontalAlignment = HorizontalAlignment.Right;
            buttons.Margin = new Thickness(0, 16, 0, 0);

            Button close = MakeButton("CLOSE");
            Button go = MakeButton(uninstall ? "UNINSTALL" : "INSTALL");

            close.Click += delegate { w.Close(); };
            go.Click += delegate
            {
                try
                {
                    if (uninstall)
                    {
                        Uninstall();
                        status.Text = "Removed. The folder disappears in a moment.";
                    }
                    else
                    {
                        Install();
                        status.Text = "Installed. Voice Box is on the Start menu.";
                    }
                    go.IsEnabled = false;
                }
                catch (Exception ex)
                {
                    status.Text = "Failed: " + ex.Message;
                }
            };

            Border gap = new Border();
            gap.Width = 8;

            buttons.Children.Add(close);
            buttons.Children.Add(gap);
            buttons.Children.Add(go);

            root.Children.Add(title);
            root.Children.Add(sub);
            root.Children.Add(body);
            root.Children.Add(status);
            root.Children.Add(buttons);

            w.Content = root;
            return w;
        }

        static Button MakeButton(string text)
        {
            Button b = new Button();
            b.Content = text;
            b.Padding = new Thickness(16, 7, 16, 7);
            b.FontSize = 12;
            b.FontWeight = FontWeights.SemiBold;
            b.Background = new SolidColorBrush(Color.FromRgb(24, 60, 100));
            b.Foreground = new SolidColorBrush(Color.FromRgb(226, 240, 255));
            b.BorderBrush = new SolidColorBrush(Color.FromRgb(60, 120, 180));
            return b;
        }

        static void Log(string message)
        {
            try
            {
                File.AppendAllText(Path.Combine(Path.GetTempPath(), "voicebox_setup.log"),
                    DateTime.Now.ToString("HH:mm:ss") + " " + message + Environment.NewLine);
            }
            catch { }
        }
    }
}
