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

        // Voice character, so a tuned voice comes back the way it was left rather than at the
        // factory values. The ranges match what the faders and the renderer accept.
        public static double VoiceVibScale = 1.0;
        public static double VoiceBreath = 42;
        public static double VoiceTension = 18;
        public static double VoiceGender = -6;

        // Equalizer bands and bypass, so a shaped take comes back shaped.
        public static double EqLow;
        public static double EqMid;
        public static double EqHigh;
        public static bool EqBypass;

        // The style that was active at the last close, rebuilt by Load and consumed once by the
        // window. It is written field by field because a learned style has no name on disk to
        // look up, and a preset may have been renamed or deleted since.
        public static StyleProfile StyleToRestore;

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
                StyleProfile sp = null;
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
                    else if (k == "voice.vibScale")
                    {
                        double d;
                        if (double.TryParse(v, NumberStyles.Float, CultureInfo.InvariantCulture, out d)) VoiceVibScale = Math.Max(0, Math.Min(2, d));
                    }
                    else if (k == "voice.breath")
                    {
                        double d;
                        if (double.TryParse(v, NumberStyles.Float, CultureInfo.InvariantCulture, out d)) VoiceBreath = Math.Max(0, Math.Min(100, d));
                    }
                    else if (k == "voice.tension")
                    {
                        double d;
                        if (double.TryParse(v, NumberStyles.Float, CultureInfo.InvariantCulture, out d)) VoiceTension = Math.Max(0, Math.Min(100, d));
                    }
                    else if (k == "voice.gender")
                    {
                        double d;
                        if (double.TryParse(v, NumberStyles.Float, CultureInfo.InvariantCulture, out d)) VoiceGender = Math.Max(-50, Math.Min(50, d));
                    }
                    else if (k == "eq.low")
                    {
                        double d;
                        if (double.TryParse(v, NumberStyles.Float, CultureInfo.InvariantCulture, out d)) EqLow = Math.Max(-12, Math.Min(12, d));
                    }
                    else if (k == "eq.mid")
                    {
                        double d;
                        if (double.TryParse(v, NumberStyles.Float, CultureInfo.InvariantCulture, out d)) EqMid = Math.Max(-12, Math.Min(12, d));
                    }
                    else if (k == "eq.high")
                    {
                        double d;
                        if (double.TryParse(v, NumberStyles.Float, CultureInfo.InvariantCulture, out d)) EqHigh = Math.Max(-12, Math.Min(12, d));
                    }
                    else if (k == "eq.bypass")
                        EqBypass = v == "1" || v == "true";
                    else if (k.StartsWith("style."))
                    {
                        if (sp == null) sp = new StyleProfile();
                        double d;
                        if (k == "style.source") sp.Source = v;
                        else if (k == "style.low" && double.TryParse(v, NumberStyles.Float, CultureInfo.InvariantCulture, out d)) sp.LowNote = d;
                        else if (k == "style.high" && double.TryParse(v, NumberStyles.Float, CultureInfo.InvariantCulture, out d)) sp.HighNote = d;
                        else if (k == "style.median" && double.TryParse(v, NumberStyles.Float, CultureInfo.InvariantCulture, out d)) sp.MedianNote = d;
                        else if (k == "style.vibRate" && double.TryParse(v, NumberStyles.Float, CultureInfo.InvariantCulture, out d)) sp.VibratoRate = d;
                        else if (k == "style.vibDepth" && double.TryParse(v, NumberStyles.Float, CultureInfo.InvariantCulture, out d)) sp.VibratoDepth = d;
                        else if (k == "style.brightness" && double.TryParse(v, NumberStyles.Float, CultureInfo.InvariantCulture, out d)) sp.Brightness = d;
                        else if (k == "style.breath" && double.TryParse(v, NumberStyles.Float, CultureInfo.InvariantCulture, out d)) sp.Breath = d;
                    }
                }
                if (sp != null)
                {
                    sp.Valid = true;
                    StyleToRestore = sp;
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
                sb.AppendLine("voice.vibScale=" + VoiceVibScale.ToString("0.###", CultureInfo.InvariantCulture));
                sb.AppendLine("voice.breath=" + VoiceBreath.ToString("0.###", CultureInfo.InvariantCulture));
                sb.AppendLine("voice.tension=" + VoiceTension.ToString("0.###", CultureInfo.InvariantCulture));
                sb.AppendLine("voice.gender=" + VoiceGender.ToString("0.###", CultureInfo.InvariantCulture));
                sb.AppendLine("eq.low=" + EqLow.ToString("0.###", CultureInfo.InvariantCulture));
                sb.AppendLine("eq.mid=" + EqMid.ToString("0.###", CultureInfo.InvariantCulture));
                sb.AppendLine("eq.high=" + EqHigh.ToString("0.###", CultureInfo.InvariantCulture));
                sb.AppendLine("eq.bypass=" + (EqBypass ? "1" : "0"));
                StyleProfile st = RenderStyle.Current;
                if (st != null)
                {
                    sb.AppendLine("style.source=" + (st.Source ?? ""));
                    sb.AppendLine("style.median=" + st.MedianNote.ToString("0.###", CultureInfo.InvariantCulture));
                    sb.AppendLine("style.low=" + st.LowNote.ToString("0.###", CultureInfo.InvariantCulture));
                    sb.AppendLine("style.high=" + st.HighNote.ToString("0.###", CultureInfo.InvariantCulture));
                    sb.AppendLine("style.vibRate=" + st.VibratoRate.ToString("0.###", CultureInfo.InvariantCulture));
                    sb.AppendLine("style.vibDepth=" + st.VibratoDepth.ToString("0.###", CultureInfo.InvariantCulture));
                    sb.AppendLine("style.brightness=" + st.Brightness.ToString("0.###", CultureInfo.InvariantCulture));
                    sb.AppendLine("style.breath=" + st.Breath.ToString("0.###", CultureInfo.InvariantCulture));
                }
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
