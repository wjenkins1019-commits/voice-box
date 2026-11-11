using System;
using System.Collections.Generic;
using System.Linq;

namespace VoiceBox
{
    public class Note
    {
        public int Id;
        public string Track;
        public double Start;
        public double Dur;
        public int Midi;
        public string Word;
        public string Phon;
        public string Vowel;
        public double Vib;
        public double Rate;
        public double Drift;
        public double Velocity = 1.0;   // 0..1, how hard the note is played

        public Note Copy()
        {
            Note n = new Note();
            n.Id = Id; n.Track = Track; n.Start = Start; n.Dur = Dur; n.Midi = Midi;
            n.Word = Word; n.Phon = Phon; n.Vowel = Vowel; n.Vib = Vib; n.Rate = Rate; n.Drift = Drift;
            n.Velocity = Velocity;
            return n;
        }
    }

    public class Clip
    {
        public double Start;
        public double Dur;
        public Clip(double s, double d) { Start = s; Dur = d; }
    }

    public class Track
    {
        public string Id;
        public string Name;
        public string Voice;
        public string Color;
        public bool Muted;
        public bool Solo;
        public double GainDb;   // per-track level, 0 = unity
        public double Pan;      // -1 hard left, 0 centre, +1 hard right
        public List<Clip> Clips = new List<Clip>();
    }

    public static class Project
    {
        public const int BeatsPerBar = 4;
        public const int TotalBeats = 128;
        public const int TopMidi = 74;
        public const int BottomMidi = 36;

        public static double Bpm = 96;
        public static int Rows { get { return TopMidi - BottomMidi + 1; } }
        public static double Spb { get { return 60.0 / Bpm; } }

        public static List<Track> Tracks = new List<Track>();
        public static List<Note> Notes = new List<Note>();
        public static List<Note> BaseNotes = new List<Note>();
        public static string Language = "en";
        public static string VoiceName = "Hayden 2";

        static int _nextId = 1;

        private class Raw
        {
            public double S, D;
            public int M;
            public string W, P, V;
            public Raw(double s, double d, int m, string w, string p, string v)
            { S = s; D = d; M = m; W = w; P = p; V = v; }
        }

        static Project()
        {
            BuildTracks();
            BuildNotes();
        }

        static void BuildTracks()
        {
            Track lead = new Track();
            lead.Id = "v1"; lead.Name = "Lead Vocal"; lead.Voice = "Hayden 2"; lead.Color = "#2f93f5";
            lead.Clips.Add(new Clip(0, 16)); lead.Clips.Add(new Clip(16, 16)); lead.Clips.Add(new Clip(48, 16));

            Track harm = new Track();
            harm.Id = "v2"; harm.Name = "Harmony"; harm.Voice = "Natalie 2"; harm.Color = "#7c86ff";
            harm.Clips.Add(new Clip(8, 16)); harm.Clips.Add(new Clip(32, 16));

            Track piano = new Track();
            piano.Id = "p1"; piano.Name = "Grand Piano"; piano.Voice = "Modelled Grand"; piano.Color = "#2b8fb0";
            piano.Clips.Add(new Clip(0, 48)); piano.Clips.Add(new Clip(56, 28));

            Tracks.Add(lead);
            Tracks.Add(harm);
            Tracks.Add(piano);
        }

        static List<Raw> LeadRows()
        {
            List<Raw> r = new List<Raw>();
            r.Add(new Raw(0.0, 0.5, 59, "at", "ae t", "ae"));
            r.Add(new Raw(0.5, 0.5, 59, "the", "dh ax", "ax"));
            r.Add(new Raw(1.0, 0.5, 64, "sta", "s t ey", "ey"));
            r.Add(new Raw(1.5, 1.0, 64, "tion", "sh ax n", "ax"));
            r.Add(new Raw(2.5, 0.5, 62, "I've", "ay v", "ay"));
            r.Add(new Raw(3.0, 0.5, 62, "been", "b iy n", "iy"));
            r.Add(new Raw(3.5, 0.5, 64, "wait", "w ey t", "ey"));
            r.Add(new Raw(4.0, 1.5, 66, "ing", "ih ng", "ih"));
            r.Add(new Raw(5.5, 0.5, 64, "for", "f ao r", "ao"));
            r.Add(new Raw(6.0, 0.5, 62, "a", "ax", "ax"));
            r.Add(new Raw(6.5, 0.5, 59, "sig", "s ih g", "ih"));
            r.Add(new Raw(7.0, 1.0, 59, "nal", "n ax l", "ax"));
            r.Add(new Raw(8.0, 0.5, 59, "in", "ih n", "ih"));
            r.Add(new Raw(8.5, 0.5, 62, "the", "dh ax", "ax"));
            r.Add(new Raw(9.0, 1.5, 64, "rain", "r ey n", "ey"));
            r.Add(new Raw(11.0, 0.5, 66, "ev", "eh v", "eh"));
            r.Add(new Raw(11.5, 0.5, 66, "ery", "r iy", "iy"));
            r.Add(new Raw(12.0, 0.5, 64, "sto", "s t ao", "ao"));
            r.Add(new Raw(12.5, 0.5, 64, "ry", "r iy", "iy"));
            r.Add(new Raw(13.0, 0.5, 62, "that", "dh ae t", "ae"));
            r.Add(new Raw(13.5, 0.5, 62, "we", "w iy", "iy"));
            r.Add(new Raw(14.0, 1.0, 59, "made", "m ey d", "ey"));
            r.Add(new Raw(15.0, 1.0, 59, "ech", "eh k", "eh"));
            r.Add(new Raw(16.0, 0.5, 59, "oes", "ow z", "ow"));
            r.Add(new Raw(16.5, 0.5, 57, "back", "b ae k", "ae"));
            r.Add(new Raw(17.0, 1.0, 59, "a", "ax", "ax"));
            r.Add(new Raw(18.0, 1.0, 62, "gain", "g eh n", "eh"));
            r.Add(new Raw(19.5, 0.5, 64, "and", "ae n d", "ae"));
            r.Add(new Raw(20.0, 0.5, 64, "the", "dh ax", "ax"));
            r.Add(new Raw(20.5, 0.75, 66, "night", "n ay t", "ay"));
            r.Add(new Raw(21.25, 1.25, 64, "grows", "g r ow z", "ow"));
            r.Add(new Raw(22.75, 0.5, 62, "cold", "k ow l d", "ow"));
            r.Add(new Raw(23.25, 1.0, 62, "a", "ax", "ax"));
            r.Add(new Raw(24.5, 0.5, 59, "gain", "g eh n", "eh"));
            r.Add(new Raw(25.0, 0.5, 61, "I", "ay", "ay"));
            r.Add(new Raw(25.5, 1.5, 64, "hear", "h iy r", "iy"));
            r.Add(new Raw(27.0, 0.5, 66, "your", "y ao r", "ao"));
            r.Add(new Raw(27.5, 0.5, 64, "name", "n ey m", "ey"));
            r.Add(new Raw(28.0, 2.0, 62, "call", "k ao l", "ao"));
            r.Add(new Raw(30.0, 1.5, 59, "ing", "ih ng", "ih"));
            return r;
        }

        static Note ToNote(Raw r, string track, double offset, int transpose)
        {
            Note n = new Note();
            n.Id = _nextId++;
            n.Track = track;
            n.Start = r.S + offset;
            n.Dur = r.D;
            n.Midi = Math.Max(BottomMidi, Math.Min(TopMidi, r.M + transpose));
            n.Word = r.W;
            n.Phon = r.P;
            n.Vowel = r.V;
            n.Vib = r.D >= 1.0 ? 0.62 : 0.3;
            n.Rate = 5.4;
            n.Drift = ((n.Id % 3) - 1) * 1.6;
            return n;
        }

        static void BuildNotes()
        {
            List<Raw> lead = LeadRows();
            for (int i = 0; i < lead.Count; i++) Notes.Add(ToNote(lead[i], "v1", 0, 0));

            for (int i = 0; i < lead.Count && i < 26; i++)
                if (i % 2 == 0) { Note n = ToNote(lead[i], "v2", 8, -4); Notes.Add(n); }

            for (int i = 0; i < lead.Count - 15; i++) { Note n = ToNote(lead[i], "v1", 48, 2); Notes.Add(n); }

            BuildPiano();

            BaseNotes = Notes.Select(n => n.Copy()).ToList();
        }

        // A bar-by-bar accompaniment for the Grand Piano track, so the instrument has a real
        // part to play instead of an empty clip.
        static void BuildPiano()
        {
            int[][] chords = new int[][]
            {
                new int[] { 59, 62, 66 },   // Bm
                new int[] { 55, 59, 62 },   // G
                new int[] { 62, 66, 69 },   // D
                new int[] { 57, 61, 64 }    // A
            };

            for (int bar = 0; bar < TotalBeats / BeatsPerBar; bar++)
            {
                int[] ch = chords[bar % 4];
                // A phrase swell across the 16 bars, with the root of each chord struck hardest.
                double phrase = 0.85 + 0.15 * Math.Sin(bar * Math.PI / 8.0);
                for (int v = 0; v < ch.Length; v++)
                {
                    Note n = new Note();
                    n.Id = _nextId++;
                    n.Track = "p1";
                    n.Start = bar * 4;
                    n.Dur = 3.6;
                    n.Midi = ch[v];
                    n.Vowel = "ah";
                    n.Velocity = Math.Round(phrase * (v == 0 ? 0.85 : 0.66), 2);
                    Notes.Add(n);
                }

                Note bass = new Note();
                bass.Id = _nextId++;
                bass.Track = "p1";
                bass.Start = bar * 4 + 2;
                bass.Dur = 1.8;
                bass.Midi = ch[0] - 12;
                bass.Vowel = "ah";
                bass.Velocity = Math.Round(phrase * 0.55, 2);
                Notes.Add(bass);
            }
        }

        public static List<Note> VisibleNotes(string track)
        {
            return Notes.Where(n => n.Track == track).ToList();
        }

        public static Note Find(int id)
        {
            return Notes.FirstOrDefault(n => n.Id == id);
        }

        public static int NewId()
        {
            return _nextId++;
        }

        public static void RestoreBase()
        {
            Notes = BaseNotes.Select(n => n.Copy()).ToList();
        }
    }

    public static class Langs
    {
        public static readonly string[] Keys = { "en", "ja", "zh", "yue", "ko", "es" };
        public static readonly string[] Labels =
        {
            "English (default)", "日本語 · Japanese", "普通话 · Mandarin",
            "廣東話 · Cantonese", "한국어 · Korean", "Español · Spanish"
        };
        public static readonly string[] Shorts = { "EN", "JA", "ZH", "YUE", "KO", "ES" };

        static Dictionary<string, string> Make(Dictionary<string, string> baseMap, params string[] overrides)
        {
            // overrides: pairs of vowelId, glyph
            Dictionary<string, string> d = new Dictionary<string, string>(baseMap);
            for (int i = 0; i + 1 < overrides.Length; i += 2) d[overrides[i]] = overrides[i + 1];
            return d;
        }

        static readonly Dictionary<string, string> En = new Dictionary<string, string>
        {
            {"ae","ae"},{"eh","eh"},{"ey","ey"},{"iy","iy"},{"ay","ay"},
            {"ow","ow"},{"ao","ao"},{"ax","ax"},{"ih","ih"},{"uw","uw"}
        };

        static readonly Dictionary<string, Dictionary<string, string>> Maps =
            new Dictionary<string, Dictionary<string, string>>
            {
                {"en", En},
                {"ja", Make(En, "ae","a","eh","e","ey","e","iy","i","ay","ai","ow","o","ao","o","ax","a","ih","i","uw","u")},
                {"zh", Make(En, "ae","a","eh","e","ey","ei","iy","i","ay","ai","ow","ou","ao","ao","ax","e","ih","i","uw","u")},
                {"yue", Make(En, "ae","aa","eh","e","ey","ei","iy","i","ay","aai","ow","ou","ao","ou","ax","aa","ih","i","uw","u")},
                {"ko", Make(En, "ae","ㅐ","eh","ㅔ","ey","ㅔ","iy","ㅣ","ay","ㅏ","ow","ㅗ","ao","ㅗ","ax","ㅓ","ih","ㅣ","uw","ㅜ")},
                {"es", Make(En, "ae","a","eh","e","ey","e","iy","i","ay","ai","ow","o","ao","o","ax","a","ih","i","uw","u")}
            };

        public static string Vowel(string lang, string vowel)
        {
            if (vowel == null) return "";
            Dictionary<string, string> m;
            if (!Maps.TryGetValue(lang, out m)) m = En;
            string v;
            return m.TryGetValue(vowel, out v) ? v : vowel;
        }

        public static string Label(string key)
        {
            int i = Array.IndexOf(Keys, key);
            return i < 0 ? Labels[0] : Labels[i];
        }

        public static string Short(string key)
        {
            int i = Array.IndexOf(Keys, key);
            return i < 0 ? "EN" : Shorts[i];
        }
    }

    public class VoiceModel
    {
        public string Name;
        public string Ini;
        public string Langs;
        public string Engine;
        public string Size;
        public string Url;
        public string Sub;
        public bool Installed;
        public bool Bundled;

        public string DirectUrl;      // verified file link, skips page resolution
        public bool Downloading;
        public int DownloadPct = -1;  // -1 = unknown total
        public string LocalPath;      // set once the files land on disk
    }

    // Turns a typed lyric line into one syllable per note.
    public static class Lyrics
    {
        public static bool IsVowel(char c)
        {
            char l = char.ToLowerInvariant(c);
            return l == 'a' || l == 'e' || l == 'i' || l == 'o' || l == 'u' || l == 'y';
        }

        public static List<string> Syllables(string text)
        {
            List<string> result = new List<string>();
            if (string.IsNullOrEmpty(text)) return result;

            char[] breaks = { ' ', '\n', '\r', '\t', ',', '.', '!', '?', ';', ':', '-', '—', '/', '(', ')', '"' };
            string[] words = text.Split(breaks, StringSplitOptions.RemoveEmptyEntries);

            foreach (string raw in words)
            {
                string word = "";
                for (int i = 0; i < raw.Length; i++)
                {
                    char c = raw[i];
                    if ((c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z')) word += char.ToLowerInvariant(c);
                }
                if (word.Length == 0) continue;

                string cur = "";
                bool hasVowel = false;
                for (int i = 0; i < word.Length; i++)
                {
                    char c = word[i];
                    bool v = IsVowel(c);
                    if (!v && hasVowel && cur.Length >= 2)
                    {
                        bool nextVowel = i + 1 < word.Length && IsVowel(word[i + 1]);
                        if (nextVowel)
                        {
                            result.Add(cur);
                            cur = "";
                            hasVowel = false;
                        }
                    }
                    cur += c;
                    if (v) hasVowel = true;
                }
                if (cur.Length > 0) result.Add(cur);
            }
            return result;
        }

        public static string VowelOf(string syl)
        {
            string s = syl.ToLowerInvariant();
            if (s.Contains("eau") || s.Contains("oa")) return "ow";
            if (s.Contains("ee") || s.Contains("ea") || s.Contains("ie")) return "iy";
            if (s.Contains("oo") || s.Contains("ew") || s.Contains("ue")) return "uw";
            if (s.Contains("ai") || s.Contains("ay") || s.Contains("ei")) return "ey";
            if (s.Contains("ou") || s.Contains("ow") || s.Contains("oa")) return "ow";
            if (s.Contains("au") || s.Contains("aw")) return "ao";
            for (int i = 0; i < s.Length; i++)
            {
                char c = s[i];
                if (c == 'a') return "ae";
                if (c == 'e') return "eh";
                if (c == 'i' || c == 'y') return "iy";
                if (c == 'o') return "ow";
                if (c == 'u') return "uw";
            }
            return "ax";
        }

        static string PhonOf(string syl)
        {
            string s = syl.ToLowerInvariant();
            string v = VowelOf(s);
            if (s.Length == 0) return v;
            char c = s[0];
            string onset;
            if (c == 'c') onset = "k";
            else if (c == 'j') onset = "jh";
            else if (c == 'q') onset = "k";
            else if (c == 'x') onset = "k s";
            else if (c == 'b' || c == 'd' || c == 'f' || c == 'g' || c == 'h' || c == 'k' || c == 'l' ||
                     c == 'm' || c == 'n' || c == 'p' || c == 'r' || c == 's' || c == 't' || c == 'v' ||
                     c == 'w' || c == 'z')
                onset = c.ToString();
            else onset = "";
            return onset.Length > 0 ? onset + " " + v : v;
        }

        // One syllable per note, in time order. Extra notes hold the last vowel.
        public static int Assign(List<Note> notes, string text)
        {
            List<string> syl = Syllables(text);
            List<Note> targets = notes.Where(n => n.Track != "p1").OrderBy(n => n.Start).ToList();
            if (syl.Count == 0 || targets.Count == 0) return 0;

            int used = Math.Min(syl.Count, targets.Count);
            for (int i = 0; i < used; i++)
            {
                targets[i].Word = syl[i];
                targets[i].Vowel = VowelOf(syl[i]);
                targets[i].Phon = PhonOf(syl[i]);
            }
            for (int i = used; i < targets.Count; i++)
            {
                targets[i].Word = "·";
                if (i > 0)
                {
                    targets[i].Vowel = targets[i - 1].Vowel;
                    targets[i].Phon = targets[i - 1].Phon;
                }
            }
            return used;
        }
    }

    public static class Library
    {
        public static readonly string[] Engines = { "", "DiffSinger", "UTAU", "NNSVS", "SynthV Lite", "Voice Box" };

        public static List<VoiceModel> Voices = Build();

        static VoiceModel V(string name, string ini, string langs, string engine, string size, string url, bool installed, bool bundled, string sub)
        {
            VoiceModel v = new VoiceModel();
            v.Name = name; v.Ini = ini; v.Langs = langs; v.Engine = engine;
            v.Size = size; v.Url = url; v.Installed = installed; v.Bundled = bundled; v.Sub = sub;
            return v;
        }

        static List<VoiceModel> Build()
        {
            List<VoiceModel> l = new List<VoiceModel>();

            // bundled
            l.Add(V("Hayden 2", "H2", "EN JA ZH KO ES", "Voice Box", "bundled", null, true, true, "Bundled · multilingual male"));
            l.Add(V("Natalie 2", "N2", "EN JA ZH KO ES", "Voice Box", "bundled", null, true, true, "Bundled · multilingual female"));

            // DiffSinger
            l.Add(V("Amano Naoki DS", "AN", "EN JA ZH KO ES", "DiffSinger", "", "https://amanonaoki.com/diffsinger", false, false, "Official · multilingual"));
            l.Add(V("No.7 (SEVEN)", "N7", "JA EN", "DiffSinger", "", "https://sites.google.com/view/haru0l/diffsinger", false, false, "Official page · JA EN"));
            l.Add(V("Namine Ritsu DS", "NR", "JA EN", "DiffSinger", "", "https://www.canon-voice.com/", false, false, "Official · JA EN"));
            l.Add(V("Chiaki", "CH", "28 languages", "DiffSinger", "", "https://chiaki.ch/ja/works/voicebank", false, false, "Official · 28 languages"));
            l.Add(V("Murin DS", "MU", "FR +11", "DiffSinger", "", "https://utaufrance.com/voicebank/murin", false, false, "UFR · French +11"));
            l.Add(V("UFR Pâtisserie pack", "UF", "FR EN ES", "DiffSinger", "39 voices", "https://utaufrance.com/en/diffsinger-ufr-pack-download/", false, false, "Lyse, Remy, ALYS, Fanny, Canary +34"));
            l.Add(V("OpenCPop", "OC", "ZH", "DiffSinger", "", "https://github.com/openvpi/DiffSinger", false, false, "OpenVPI · Mandarin"));
            l.Add(V("PJS", "PJ", "ZH JA", "DiffSinger", "", "https://github.com/UtaUtaUtau/pjs-manual-labels", false, false, "Official repo · ZH JA"));
            l.Add(V("DiffSinger voicebank index", "DI", "all", "DiffSinger", "directory", "https://diffsinger.miraheze.org/wiki/Category:DiffSinger_voicebanks", false, false, "Community wiki · browse all"));

            // NNSVS / ENUNU
            l.Add(V("NNSVS models", "NN", "JA EN ZH", "NNSVS", "directory", "https://nnsvs.github.io/", false, false, "r9y9 · open models"));
            l.Add(V("ENUNU model database", "EN", "JA EN PL ES", "NNSVS", "directory", "https://nnsvs.carrd.co/", false, false, "Community list of free models"));
            l.Add(V("Namine Ritsu ENUNU", "RE", "JA", "NNSVS", "", "https://www.canon-voice.com/", false, false, "Official · 5 singing styles"));

            // UTAU / OpenUtau
            l.Add(V("KASAI OG01", "KO", "JA EN", "UTAU", "", "https://studio-ogien.com/kasai-og01", false, false, "Studio Ogien · JA EN"));
            l.Add(V("KASAI IGNITE α", "KI", "EN", "UTAU", "", "https://mega.nz/folder/xkgCwTqY", false, false, "ARPAsing English"));
            l.Add(V("Kasane Teto", "KT", "JA EN", "UTAU", "", "https://kasaneteto.jp/utau", false, false, "Official · standard, whisper, sakebi"));
            l.Add(V("Defoko (Utane Uta)", "DF", "JA", "UTAU", "bundled with UTAU", "https://utau.us/vb.html", false, false, "Default UTAU voicebank"));
            l.Add(V("Ami Koharune", "AK", "JA EN", "UTAU", "17 pitches", "https://amitaro.net/utau/en_ongen-list.html", false, false, "Official · CV / VCV / CVVC"));
            l.Add(V("Anna Nyui", "AY", "JA EN", "UTAU", "", "https://aoka45.wixsite.com/annanyui/utau?lang=en", false, false, "Official · many appends"));
            l.Add(V("Momone Momo", "MM", "JA EN", "UTAU", "", "https://momonemomo.com/", false, false, "Official · classic UTAU"));
            l.Add(V("Yamine Renri", "YR", "JA", "UTAU", "", "https://renrivoice.wixsite.com/renri-voice", false, false, "Official · VCV + appends"));
            l.Add(V("Haruka Nana", "HN", "JA", "UTAU", "", "https://nanahira.jp/haruka_nana/index.html", false, false, "Official · CVVC"));
            l.Add(V("26 (Iron)", "26", "EN", "UTAU", "F2-B4", "https://drive.google.com/file/d/1xGi2cP_2kfOodrfykoLBSiBtB9bi_HLj/view", false, false, "ARPAsing · masculine"));
            l.Add(V("Aki Toka", "AT", "EN", "UTAU", "F3-C5", "https://bowlroll.net/file/139560", false, false, "ARPAsing · feminine"));
            l.Add(V("Hailey Denali", "HD", "EN", "UTAU", "B2-A6", "https://dl.orangedox.com/hailey-english-vb", false, false, "ARPAsing · feminine"));
            l.Add(V("Resonance Harmony", "RH", "EN", "UTAU", "3 pitches", "http://www.mediafire.com/file/k7bmq82kz9fdncn/Resonance+Harmony+ARPAsing+1.0.rar", false, false, "ARPAsing · power, whisper, growl"));
            l.Add(V("Aino Erufu", "AE", "EN", "UTAU", "high", "http://www.mediafire.com/file/9yy87wv7wyassxw/Aino+Erufu+ARPAsing+ENG.rar", false, false, "ARPAsing · feminine"));
            l.Add(V("Aero-gen Shinsu", "AS", "EN", "UTAU", "tripitch", "http://www.mediafire.com/file/acdogemoybc9byq/Aero-gen+Shinsu+%28ARPAsing%29.zip", false, false, "ARPAsing · adolescent"));
            l.Add(V("Kayaka Konagi", "KK", "EN", "UTAU", "", "https://drive.google.com/open?id=1hYa4gO51Tvb1Otj3ZsgToAnrDi-bxySt", false, false, "ARPAsing · feminine"));
            l.Add(V("ARPAsing prototype", "AP", "EN", "UTAU", "", "https://klad.tubs.wtf/download?f=wem3-arpasing-prototype", false, false, "Reference English bank"));
            l.Add(V("Ame Silver", "SB", "EN", "UTAU", "", "https://mega.nz/#!VptxQYba!50iHBnVBMuV3oY-qTZyU5XGMVH04oTu7vLHoHUMQ_Ks", false, false, "ARPAsing · natural English"));
            l.Add(V("ARPAsing directory", "AR", "EN", "UTAU", "directory", "https://arpasing.tubs.wtf/en/directories/voicebanks", false, false, "Every released English bank"));

            // Synthesizer V Lite (free, non-commercial)
            l.Add(V("Eleanor Forte Lite", "EF", "EN", "SynthV Lite", "97 MB", "https://resource.dreamtonics.com/download/English/Voice%20Databases/Lite%20Voice%20Databases/Eleanor%20Forte/", false, false, "Free Lite · EN"));
            l.Add(V("AiKO Lite", "AI", "ZH", "SynthV Lite", "132 MB", "https://resource.dreamtonics.com/download/English/Voice%20Databases/Lite%20Voice%20Databases/AiKO/", false, false, "Free Lite · Mandarin"));
            l.Add(V("Chiyu Lite", "CY", "ZH", "SynthV Lite", "98 MB", "https://resource.dreamtonics.com/download/English/Voice%20Databases/Lite%20Voice%20Databases/Chiyu/", false, false, "Free Lite · Mandarin"));
            l.Add(V("GENBU Lite", "GB", "JA", "SynthV Lite", "101 MB", "https://resource.dreamtonics.com/download/English/Voice%20Databases/Lite%20Voice%20Databases/Genbu/", false, false, "Free Lite · Japanese"));
            l.Add(V("Kotonoha Akane & Aoi Lite", "KC", "JA", "SynthV Lite", "131 MB", "https://resource.dreamtonics.com/download/English/Voice%20Databases/Lite%20Voice%20Databases/Kotonoha%20Akane%20%26%20Aoi/", false, false, "Free Lite · 2 singers"));
            l.Add(V("Hanakuma Chifuyu Lite", "CF", "JA", "SynthV Lite", "22 MB", "https://resource.dreamtonics.com/download/English/Voice%20Databases/Lite%20Voice%20Databases/Hanakuma%20Chifuyu/", false, false, "Free Lite · Japanese"));
            l.Add(V("ANRI Lite", "AR", "EN", "SynthV Lite", "34 MB", "https://resource.dreamtonics.com/download/English/Voice%20Databases/Lite%20Voice%20Databases/ANRI/", false, false, "Free Lite · EN"));
            l.Add(V("ASTERIAN Lite", "ST", "EN", "SynthV Lite", "34 MB", "https://resource.dreamtonics.com/download/English/Voice%20Databases/Lite%20Voice%20Databases/ASTERIAN/", false, false, "Free Lite · masculine EN"));
            l.Add(V("SynthV Lite collection", "SV", "EN JA ZH", "SynthV Lite", "directory", "https://resource.dreamtonics.com/download/English/Voice%20Databases/Lite%20Voice%20Databases/", false, false, "All free Lite downloads"));

            // Direct file links (skip page scraping)
            SetDirect(l, "Kasane Teto", "https://kasaneteto.jp/assets/download/utau/TETO-sakebi-120930.zip");

            return l;
        }

        static void SetDirect(List<VoiceModel> l, string name, string url)
        {
            VoiceModel v = l.FirstOrDefault(x => x.Name == name);
            if (v != null) v.DirectUrl = url;
        }

        public static int FreeCount()
        {
            return Voices.Count(v => !v.Bundled);
        }

        public static VoiceModel Find(string name)
        {
            return Voices.FirstOrDefault(v => v.Name == name);
        }

        public static List<VoiceModel> Installed()
        {
            return Voices.Where(v => v.Installed).ToList();
        }
    }
}
