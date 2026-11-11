using System;
using System.IO;
using System.Net;
using System.Text.RegularExpressions;
using System.Windows;

namespace VoiceBox
{
    // Downloads free voice models straight into the app (no browser hop where the
    // host allows a plain HTTP file).
    public static class Downloader
    {
        public static string Root
        {
            get
            {
                string p = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "VoiceBox", "Voices");
                Directory.CreateDirectory(p);
                return p;
            }
        }

        public static void Start(VoiceModel vm, Action<int> progress, Action<string> done)
        {
            if (vm == null || vm.Bundled || vm.Downloading) return;
            vm.Downloading = true;
            vm.DownloadPct = -1;

            if (vm.Size == "directory")
            {
                vm.Downloading = false;
                done("That row is an index, not a single model — pick a singer above it.");
                return;
            }

            Action<int> ui = delegate(int pct)
            {
                Application.Current.Dispatcher.BeginInvoke(new Action(delegate
                {
                    vm.DownloadPct = pct;
                    progress(pct);
                }));
            };

            System.Threading.ThreadPool.QueueUserWorkItem(delegate
            {
                string error = null;
                try { error = Run(vm, ui); }
                catch (Exception ex) { error = Message(ex); }
                Application.Current.Dispatcher.BeginInvoke(new Action(delegate
                {
                    vm.Downloading = false;
                    vm.DownloadPct = -1;
                    done(error);
                }));
            });
        }

        static string Message(Exception ex)
        {
            string m = ex == null ? "Download failed." : ex.Message;
            if (m.IndexOf("mega.nz", StringComparison.OrdinalIgnoreCase) >= 0 ||
                m.IndexOf("MEGA", StringComparison.OrdinalIgnoreCase) >= 0)
                return "MEGA links need a browser, so this one cannot be fetched here.";
            if (m.IndexOf("timed out", StringComparison.OrdinalIgnoreCase) >= 0 ||
                m.IndexOf("timeout", StringComparison.OrdinalIgnoreCase) >= 0)
                return "The server did not answer in time.";
            return m.Length > 130 ? m.Substring(0, 130) : m;
        }

        // Returns null once the model is on disk, or a short notice when the page had to be
        // handed to the browser instead.
        static string Run(VoiceModel vm, Action<int> progress)
        {
            string direct = vm.DirectUrl;
            if (string.IsNullOrEmpty(direct)) direct = Resolve(vm.Url);
            if (string.IsNullOrEmpty(direct))
            {
                // Official voicebank pages hand off to Drive, MEGA or a click-through, so there is
                // no archive to fetch here. Opening the page is the only honest outcome, and it
                // beats a dead-end error the user cannot act on.
                string notice = "its page has no direct file to fetch, so the page was opened in your browser";
                try
                {
                    System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(vm.Url) { UseShellExecute = true });
                }
                catch
                {
                    notice = "its page has no direct file to fetch, and the browser could not be opened";
                }
                return notice;
            }

            string folder = Path.Combine(Root, Safe(vm.Name));
            Directory.CreateDirectory(folder);

            string ext = "";
            try { ext = Path.GetExtension(new Uri(direct).AbsolutePath); }
            catch { ext = ""; }
            if (ext.Length < 2 || ext.Length > 6) ext = ".zip";

            string file = Path.Combine(folder, Safe(vm.Name) + ext);
            Fetch(direct, file, progress);

            if (ext.ToLowerInvariant() == ".zip")
            {
                try { System.IO.Compression.ZipFile.ExtractToDirectory(file, folder); }
                catch { /* keep the archive if it will not unpack */ }
            }
            vm.LocalPath = folder;
            return null;
        }

        static void Fetch(string url, string path, Action<int> progress)
        {
            HttpWebRequest req = (HttpWebRequest)WebRequest.Create(url);
            req.UserAgent = "VoiceBox/2.4";
            req.Timeout = 30000;
            req.ReadWriteTimeout = 60000;
            req.AllowAutoRedirect = true;

            string part = path + ".part";
            using (HttpWebResponse res = (HttpWebResponse)req.GetResponse())
            using (Stream input = res.GetResponseStream())
            using (FileStream output = new FileStream(part, FileMode.Create, FileAccess.Write))
            {
                long total = res.ContentLength;
                byte[] buffer = new byte[65536];
                long got = 0;
                int last = -2;
                int read;
                while ((read = input.Read(buffer, 0, buffer.Length)) > 0)
                {
                    output.Write(buffer, 0, read);
                    got += read;
                    int pct = total > 0 ? (int)(got * 100 / total) : -1;
                    if (pct != last && progress != null)
                    {
                        last = pct;
                        progress(pct);
                    }
                }
            }
            if (File.Exists(path)) File.Delete(path);
            File.Move(part, path);
        }

        // Turns an artist page, Drive link or listing into a direct file URL.
        public static string Resolve(string pageUrl)
        {
            if (string.IsNullOrEmpty(pageUrl)) return null;
            if (pageUrl.IndexOf("mega.nz", StringComparison.OrdinalIgnoreCase) >= 0)
                throw new Exception("MEGA links need a browser");
            if (Regex.IsMatch(pageUrl, @"\.(zip|rar|7z|uar|exe|vrm)(\?|$)", RegexOptions.IgnoreCase))
                return pageUrl;

            Match gh = Regex.Match(pageUrl, @"github\.com/([^/]+)/([^/]+)/releases", RegexOptions.IgnoreCase);
            if (gh.Success)
            {
                string api = "https://api.github.com/repos/" + gh.Groups[1].Value + "/" +
                             gh.Groups[2].Value + "/releases/latest";
                try
                {
                    string json = GetText(api);
                    Match asset = Regex.Match(json, @"browser_download_url\W+(https[^\u0022]+?\.(?:zip|rar|7z))");
                    if (asset.Success) return asset.Groups[1].Value;
                }
                catch { }
            }

            string html = GetText(pageUrl);
            if (html.IndexOf("mega.nz/", StringComparison.OrdinalIgnoreCase) >= 0 &&
                html.IndexOf(".zip", StringComparison.OrdinalIgnoreCase) < 0)
                throw new Exception("MEGA links need a browser");

            Match m = Regex.Match(html, @"drive\.google\.com/file/d/([A-Za-z0-9_-]{10,})");
            if (m.Success)
                return "https://drive.usercontent.google.com/download?id=" + m.Groups[1].Value +
                       "&export=download&confirm=t";

            m = Regex.Match(html, @"https://download[0-9]*\.mediafire\.com/[^\u0022'\\ <>]+", RegexOptions.IgnoreCase);
            if (m.Success) return m.Value.Replace("&amp;", "&");

            // Archive links on the page, skipping 3D models, artwork and logos.
            MatchCollection links = Regex.Matches(html, @"href=\u0022([^\u0022]+?\.(?:zip|rar|7z|uar))\u0022", RegexOptions.IgnoreCase);
            foreach (Match link in links)
            {
                string href = link.Groups[1].Value.Replace("&amp;", "&");
                string abs = href.StartsWith("http", StringComparison.OrdinalIgnoreCase)
                    ? href
                    : new Uri(new Uri(pageUrl), href).ToString();
                if (IsVoiceAsset(abs)) return abs;
            }

            MatchCollection any = Regex.Matches(html, @"https?://[^\u0022'\\ <>]+?\.(?:zip|rar|7z|uar)", RegexOptions.IgnoreCase);
            foreach (Match a in any)
            {
                string abs = a.Value.Replace("&amp;", "&");
                if (IsVoiceAsset(abs)) return abs;
            }
            return null;
        }

        static bool IsVoiceAsset(string url)
        {
            string low = url.ToLowerInvariant();
            string[] skip = { "mmd", "model", "illust", "artwork", "logo", "icon", "banner", "theme", "wallpaper" };
            for (int i = 0; i < skip.Length; i++)
                if (low.Contains(skip[i])) return false;
            return true;
        }

        static string GetText(string url)
        {
            HttpWebRequest req = (HttpWebRequest)WebRequest.Create(url);
            req.UserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) VoiceBox/2.4";
            req.Timeout = 20000;
            req.AllowAutoRedirect = true;
            using (HttpWebResponse res = (HttpWebResponse)req.GetResponse())
            using (StreamReader reader = new StreamReader(res.GetResponseStream()))
                return reader.ReadToEnd();
        }

        static string Safe(string name)
        {
            char[] bad = Path.GetInvalidFileNameChars();
            string s = name;
            for (int i = 0; i < bad.Length; i++) s = s.Replace(bad[i], '_');
            return s.Trim();
        }
    }
}
