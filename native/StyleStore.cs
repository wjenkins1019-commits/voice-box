using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;

namespace VoiceBox
{
    // Learned styles persisted under %LOCALAPPDATA%\VoiceBox\Styles.
    public static class StyleStore
    {
        public static string Folder
        {
            get
            {
                string p = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "VoiceBox", "Styles");
                Directory.CreateDirectory(p);
                return p;
            }
        }

        public static List<string> Names()
        {
            List<string> names = new List<string>();
            try
            {
                foreach (string f in Directory.GetFiles(Folder, "*.style"))
                    names.Add(Path.GetFileNameWithoutExtension(f));
            }
            catch { }
            names.Sort(StringComparer.OrdinalIgnoreCase);
            return names;
        }

        public static void Save(string name, StyleProfile p)
        {
            if (p == null || string.IsNullOrEmpty(name)) return;
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("source=" + (p.Source ?? ""));
            sb.AppendLine("median=" + F(p.MedianNote));
            sb.AppendLine("low=" + F(p.LowNote));
            sb.AppendLine("high=" + F(p.HighNote));
            sb.AppendLine("vibRate=" + F(p.VibratoRate));
            sb.AppendLine("vibDepth=" + F(p.VibratoDepth));
            sb.AppendLine("brightness=" + F(p.Brightness));
            sb.AppendLine("breath=" + F(p.Breath));
            File.WriteAllText(Path.Combine(Folder, Safe(name) + ".style"), sb.ToString());
        }

        public static StyleProfile Load(string name)
        {
            try
            {
                string path = Path.Combine(Folder, Safe(name) + ".style");
                if (!File.Exists(path)) return null;
                return Parse(File.ReadAllText(path));
            }
            catch { return null; }
        }

        // Shared by Load and Import so a preset file means the same thing wherever it came from.
        static StyleProfile Parse(string body)
        {
            if (string.IsNullOrEmpty(body)) return null;

            StyleProfile p = new StyleProfile();
            bool sawValue = false;
            string[] lines = body.Replace("\r", "").Split('\n');
            for (int i = 0; i < lines.Length; i++)
            {
                string line = lines[i].Trim();
                int eq = line.IndexOf('=');
                if (eq <= 0) continue;
                string k = line.Substring(0, eq).Trim();
                string v = line.Substring(eq + 1).Trim();

                if (k == "source") p.Source = v;
                else if (k == "median") { p.MedianNote = D(v, p.MedianNote); sawValue = true; }
                else if (k == "low") { p.LowNote = D(v, p.LowNote); sawValue = true; }
                else if (k == "high") { p.HighNote = D(v, p.HighNote); sawValue = true; }
                else if (k == "vibRate") { p.VibratoRate = D(v, p.VibratoRate); sawValue = true; }
                else if (k == "vibDepth") { p.VibratoDepth = D(v, p.VibratoDepth); sawValue = true; }
                else if (k == "brightness") { p.Brightness = D(v, p.Brightness); sawValue = true; }
                else if (k == "breath") { p.Breath = D(v, p.Breath); sawValue = true; }
            }
            if (!sawValue) return null;

            p.Valid = true;
            return p;
        }

        public static bool Exists(string name)
        {
            if (string.IsNullOrEmpty(name)) return false;
            return File.Exists(Path.Combine(Folder, Safe(name) + ".style"));
        }

        // Renaming keeps the learned profile and just moves it to a new file name.
        public static bool Rename(string oldName, string newName)
        {
            if (string.IsNullOrEmpty(oldName) || string.IsNullOrEmpty(newName)) return false;
            if (string.Compare(Safe(oldName), Safe(newName), true) == 0) return false;
            if (!Exists(oldName) || Exists(newName)) return false;

            StyleProfile p = Load(oldName);
            if (p == null) return false;
            Save(newName, p);
            if (!Exists(newName)) return false;
            Delete(oldName);
            return true;
        }

        public static bool Export(string name, string path)
        {
            try
            {
                if (string.IsNullOrEmpty(path) || !Exists(name)) return false;
                File.Copy(Path.Combine(Folder, Safe(name) + ".style"), path, true);
                return true;
            }
            catch { return false; }
        }

        // Copies a .style file into the library and reports the name it was given.
        public static string Import(string path)
        {
            try
            {
                if (string.IsNullOrEmpty(path) || !File.Exists(path)) return null;

                StyleProfile p = Parse(File.ReadAllText(path));
                if (p == null) return null;

                string baseName = Path.GetFileNameWithoutExtension(path);
                if (string.IsNullOrEmpty(baseName)) baseName = "Imported style";
                string name = Unique(baseName);
                Save(name, p);
                return Exists(name) ? name : null;
            }
            catch { return null; }
        }

        static string Unique(string name)
        {
            string candidate = name;
            int n = 2;
            while (Exists(candidate) && n < 1000)
            {
                candidate = name + " " + n;
                n++;
            }
            return candidate;
        }

        public static void Delete(string name)
        {
            try
            {
                string path = Path.Combine(Folder, Safe(name) + ".style");
                if (File.Exists(path)) File.Delete(path);
            }
            catch { }
        }

        static string F(double v)
        {
            return v.ToString("R", CultureInfo.InvariantCulture);
        }

        static double D(string s, double fallback)
        {
            double v;
            return double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out v) ? v : fallback;
        }

        static string Safe(string name)
        {
            char[] bad = Path.GetInvalidFileNameChars();
            string s = (name ?? "").Trim();
            for (int i = 0; i < bad.Length; i++) s = s.Replace(bad[i], '_');
            return s.Length == 0 ? "style" : s;
        }
    }
}
