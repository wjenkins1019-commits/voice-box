using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;

namespace VoiceBox
{
    // A snapshot of the track mix: level, pan, mute and solo per track id.
    public class MixPreset
    {
        public Dictionary<string, double> Gain = new Dictionary<string, double>();
        public Dictionary<string, double> Pan = new Dictionary<string, double>();
        public Dictionary<string, bool> Muted = new Dictionary<string, bool>();
        public Dictionary<string, bool> Solo = new Dictionary<string, bool>();
    }

    // Named mixes on disk, so several balances can be compared and swapped.
    public static class MixStore
    {
        public static string Folder
        {
            get
            {
                string p = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "VoiceBox", "Mix Presets");
                Directory.CreateDirectory(p);
                return p;
            }
        }

        public static List<string> Names()
        {
            List<string> names = new List<string>();
            try
            {
                foreach (string f in Directory.GetFiles(Folder, "*.mix"))
                    names.Add(Path.GetFileNameWithoutExtension(f));
            }
            catch { }
            names.Sort(StringComparer.OrdinalIgnoreCase);
            return names;
        }

        public static MixPreset Capture()
        {
            MixPreset p = new MixPreset();
            foreach (Track t in Project.Tracks)
            {
                if (t == null || string.IsNullOrEmpty(t.Id)) continue;
                p.Gain[t.Id] = t.GainDb;
                p.Pan[t.Id] = t.Pan;
                p.Muted[t.Id] = t.Muted;
                p.Solo[t.Id] = t.Solo;
            }
            return p;
        }

        public static void Apply(MixPreset p)
        {
            if (p == null) return;
            foreach (Track t in Project.Tracks)
            {
                if (t == null || string.IsNullOrEmpty(t.Id)) continue;
                double g, pan;
                bool m, s;
                if (p.Gain.TryGetValue(t.Id, out g)) t.GainDb = Clamp(g, -24, 12);
                if (p.Pan.TryGetValue(t.Id, out pan)) t.Pan = Clamp(pan, -1, 1);
                if (p.Muted.TryGetValue(t.Id, out m)) t.Muted = m;
                if (p.Solo.TryGetValue(t.Id, out s)) t.Solo = s;
            }
        }

        public static bool Save(string name, MixPreset p)
        {
            if (p == null || string.IsNullOrEmpty(name)) return false;
            try
            {
                StringBuilder sb = new StringBuilder();
                sb.AppendLine("# Voice Box mix preset");
                foreach (KeyValuePair<string, double> kv in p.Gain)
                    sb.AppendLine("track." + kv.Key + ".gain=" + Num(kv.Value));
                foreach (KeyValuePair<string, double> kv in p.Pan)
                    sb.AppendLine("track." + kv.Key + ".pan=" + Num(kv.Value));
                foreach (KeyValuePair<string, bool> kv in p.Muted)
                    sb.AppendLine("track." + kv.Key + ".mute=" + (kv.Value ? "1" : "0"));
                foreach (KeyValuePair<string, bool> kv in p.Solo)
                    sb.AppendLine("track." + kv.Key + ".solo=" + (kv.Value ? "1" : "0"));
                File.WriteAllText(Path.Combine(Folder, Safe(name) + ".mix"), sb.ToString());
                return true;
            }
            catch { return false; }
        }

        public static MixPreset Load(string name)
        {
            try
            {
                string path = Path.Combine(Folder, Safe(name) + ".mix");
                if (!File.Exists(path)) return null;

                MixPreset p = new MixPreset();
                bool saw = false;
                foreach (string raw in File.ReadAllLines(path))
                {
                    string line = raw.Trim();
                    if (line.Length == 0 || line[0] == '#') continue;
                    int eq = line.IndexOf('=');
                    if (eq <= 0) continue;

                    string[] parts = line.Substring(0, eq).Trim().Split('.');
                    if (parts.Length != 3 || parts[0] != "track") continue;
                    string id = parts[1], field = parts[2];
                    string v = line.Substring(eq + 1).Trim();

                    if (field == "gain") { p.Gain[id] = D(v, 0); saw = true; }
                    else if (field == "pan") { p.Pan[id] = D(v, 0); saw = true; }
                    else if (field == "mute") { p.Muted[id] = v == "1" || v == "true"; saw = true; }
                    else if (field == "solo") { p.Solo[id] = v == "1" || v == "true"; saw = true; }
                }
                return saw ? p : null;
            }
            catch { return null; }
        }

        public static bool Exists(string name)
        {
            if (string.IsNullOrEmpty(name)) return false;
            return File.Exists(Path.Combine(Folder, Safe(name) + ".mix"));
        }

        public static void Delete(string name)
        {
            try
            {
                string path = Path.Combine(Folder, Safe(name) + ".mix");
                if (File.Exists(path)) File.Delete(path);
            }
            catch { }
        }

        static double Clamp(double v, double a, double b) { return v < a ? a : (v > b ? b : v); }

        static string Num(double v) { return v.ToString("0.###", CultureInfo.InvariantCulture); }

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
            return s.Length == 0 ? "mix" : s;
        }
    }
}
