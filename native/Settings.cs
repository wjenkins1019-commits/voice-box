using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;

namespace VoiceBox
{
    // Small remembered preferences: where the last export went and what each mode was called.
    public static class AppSettings
    {
        public static string LastExportDir = "";
        public static string LastExportMode = "";
        public static string NameWav = "";
        public static string NameRaw = "";
        public static string NameStems = "";
        public static double FadeMs = 48;
        public static bool FadeLinear;

        // Per-track mix, keyed by track id, so levels and pans survive a restart.
        public static Dictionary<string, double> MixGain = new Dictionary<string, double>();
        public static Dictionary<string, double> MixPan = new Dictionary<string, double>();
        public static Dictionary<string, bool> MixMuted = new Dictionary<string, bool>();
        public static Dictionary<string, bool> MixSolo = new Dictionary<string, bool>();

        // Set the first time the shortcut hint is shown, so it never nags a returning user.
        public static bool HelpHintSeen;

        public static string FilePath
        {
            get
            {
                string dir = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "VoiceBox");
                Directory.CreateDirectory(dir);
                return Path.Combine(dir, "settings.txt");
            }
        }

        public static void Load()
        {
            try
            {
                if (!File.Exists(FilePath)) return;
                foreach (string line in File.ReadAllLines(FilePath))
                {
                    int eq = line.IndexOf('=');
                    if (eq <= 0) continue;
                    string k = line.Substring(0, eq).Trim();
                    string v = line.Substring(eq + 1).Trim();
                    if (k == "exportDir") LastExportDir = v;
                    else if (k == "exportMode") LastExportMode = v;
                    else if (k == "wavName") NameWav = v;
                    else if (k == "rawName") NameRaw = v;
                    else if (k == "stemsName") NameStems = v;
                    else if (k == "fade.ms")
                    {
                        double d;
                        if (double.TryParse(v, NumberStyles.Float, CultureInfo.InvariantCulture, out d)) FadeMs = Math.Max(0, Math.Min(300, d));
                    }
                    else if (k == "fade.curve") FadeLinear = v == "linear";
                    else if (k.StartsWith("mix.") && k.EndsWith(".gain"))
                    {
                        double d;
                        if (double.TryParse(v, NumberStyles.Float, CultureInfo.InvariantCulture, out d))
                            MixGain[k.Substring(4, k.Length - 9)] = Math.Max(-24, Math.Min(12, d));
                    }
                    else if (k.StartsWith("mix.") && k.EndsWith(".pan"))
                    {
                        double d;
                        if (double.TryParse(v, NumberStyles.Float, CultureInfo.InvariantCulture, out d))
                            MixPan[k.Substring(4, k.Length - 8)] = Math.Max(-1, Math.Min(1, d));
                    }
                    else if (k.StartsWith("mix.") && k.EndsWith(".mute"))
                        MixMuted[k.Substring(4, k.Length - 9)] = v == "1" || v == "true";
                    else if (k.StartsWith("mix.") && k.EndsWith(".solo"))
                        MixSolo[k.Substring(4, k.Length - 9)] = v == "1" || v == "true";
                    else if (k == "hint.help")
                        HelpHintSeen = v == "1" || v == "true";
                }
            }
            catch { }
        }

        public static void Save()
        {
            try
            {
                StringBuilder sb = new StringBuilder();
                sb.AppendLine("exportDir=" + LastExportDir);
                sb.AppendLine("exportMode=" + LastExportMode);
                sb.AppendLine("wavName=" + NameWav);
                sb.AppendLine("rawName=" + NameRaw);
                sb.AppendLine("stemsName=" + NameStems);
                sb.AppendLine("fade.ms=" + FadeMs.ToString("0.###", CultureInfo.InvariantCulture));
                sb.AppendLine("fade.curve=" + (FadeLinear ? "linear" : "equal-power"));
                foreach (KeyValuePair<string, double> kv in MixGain)
                    sb.AppendLine("mix." + kv.Key + ".gain=" + kv.Value.ToString("0.###", CultureInfo.InvariantCulture));
                foreach (KeyValuePair<string, double> kv in MixPan)
                    sb.AppendLine("mix." + kv.Key + ".pan=" + kv.Value.ToString("0.###", CultureInfo.InvariantCulture));
                foreach (KeyValuePair<string, bool> kv in MixMuted)
                    sb.AppendLine("mix." + kv.Key + ".mute=" + (kv.Value ? "1" : "0"));
                foreach (KeyValuePair<string, bool> kv in MixSolo)
                    sb.AppendLine("mix." + kv.Key + ".solo=" + (kv.Value ? "1" : "0"));
                if (HelpHintSeen) sb.AppendLine("hint.help=1");
                File.WriteAllText(FilePath, sb.ToString());
            }
            catch { }
        }

        // The name to offer for a mode: whatever was used last time, or the built-in default.
        public static string ExportName(string mode, string fallback)
        {
            string remembered = "";
            if (mode == "wav") remembered = NameWav;
            else if (mode == "raw") remembered = NameRaw;
            else if (mode == "stems") remembered = NameStems;
            return string.IsNullOrEmpty(remembered) ? fallback : remembered;
        }

        public static double MixGainOf(string id)
        {
            double v;
            return MixGain.TryGetValue(id, out v) ? v : 0;
        }

        public static double MixPanOf(string id)
        {
            double v;
            return MixPan.TryGetValue(id, out v) ? v : 0;
        }

        public static bool MixMutedOf(string id)
        {
            bool v;
            return MixMuted.TryGetValue(id, out v) && v;
        }

        public static bool MixSoloOf(string id)
        {
            bool v;
            return MixSolo.TryGetValue(id, out v) && v;
        }

        public static void RememberExport(string mode, string fullPath)
        {
            try
            {
                LastExportDir = Path.GetDirectoryName(fullPath);
                LastExportMode = mode;
                string name = Path.GetFileName(fullPath);
                if (mode == "wav") NameWav = name;
                else if (mode == "raw") NameRaw = name;
                else if (mode == "stems") NameStems = name;
                Save();
            }
            catch { }
        }
    }
}
