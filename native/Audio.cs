using System;
using System.Collections.Generic;
using System.IO;

namespace VoiceBox
{
    // A vocal sample reduced to reusable style traits (never its melody).
    public class StyleProfile
    {
        public bool Valid;
        public string Source = "";
        public double MedianNote = 60;
        public double LowNote = 60;
        public double HighNote = 60;
        public double VibratoRate = 5.4;
        public double VibratoDepth = 0.35;   // semitones, half peak-to-peak
        public double Brightness = 0.5;      // 0..1 spectral proxy
        public double Breath = 0.35;         // 0..1 airiness

        public double VibScale { get { return Clamp(VibratoDepth / 0.35, 0.4, 2.4); } }
        public double FormantMul { get { return Math.Pow(2.0, (MedianNote - 60.0) / 30.0); } }
        public double Tilt { get { return Clamp(1.05 - Brightness * 0.45, 0.62, 1.2); } }
        public double BreathBias { get { return Clamp(Breath, 0, 1); } }

        static double Clamp(double v, double a, double b) { return v < a ? a : (v > b ? b : v); }

        public static StyleProfile Analyze(float[] samples, int sampleRate, string source)
        {
            StyleProfile p = new StyleProfile();
            p.Source = source;
            PitchTrack t = PitchTracker.Track(samples, sampleRate);
            if (t.Frames < 20) return p;

            List<double> notes = new List<double>();
            for (int i = 0; i < t.Frames; i++) if (t.Voiced[i]) notes.Add(t.Midi[i]);
            if (notes.Count < 20) return p;

            notes.Sort();
            p.MedianNote = notes[notes.Count / 2];
            p.LowNote = notes[(int)(notes.Count * 0.1)];
            p.HighNote = notes[(int)(notes.Count * 0.9)];

            int bestStart = 0, bestLen = 0, curStart = 0, curLen = 0;
            for (int i = 0; i < t.Frames; i++)
            {
                if (t.Voiced[i])
                {
                    if (curLen == 0) curStart = i;
                    curLen++;
                }
                else
                {
                    if (curLen > bestLen) { bestLen = curLen; bestStart = curStart; }
                    curLen = 0;
                }
            }
            if (curLen > bestLen) { bestLen = curLen; bestStart = curStart; }

            if (bestLen > 25)
            {
                int win = Math.Max(2, (int)(0.12 * t.Rate / t.Hop));
                double sumAbs = 0;
                int crossings = 0;
                int count = 0;
                double prev = 0;
                for (int i = bestStart; i < bestStart + bestLen; i++)
                {
                    double mean = 0;
                    int n = 0;
                    for (int k = Math.Max(bestStart, i - win); k <= Math.Min(bestStart + bestLen - 1, i + win); k++)
                    {
                        mean += t.Midi[k];
                        n++;
                    }
                    mean /= Math.Max(1, n);
                    double d = t.Midi[i] - mean;
                    sumAbs += Math.Abs(d);
                    if (count > 0 && ((d >= 0) != (prev >= 0))) crossings++;
                    prev = d;
                    count++;
                }
                double durSec = bestLen * t.Hop / (double)t.Rate;
                p.VibratoRate = Math.Max(3.0, Math.Min(9.0, crossings / 2.0 / Math.Max(0.2, durSec)));
                p.VibratoDepth = Math.Max(0.05, Math.Min(1.2, (sumAbs / Math.Max(1, count)) * 1.6));
            }

            int zc = 0;
            for (int i = 1; i < samples.Length; i++)
                if ((samples[i - 1] >= 0) != (samples[i] >= 0)) zc++;
            double zcr = zc / (samples.Length / (double)sampleRate);
            p.Brightness = Math.Max(0, Math.Min(1, (zcr - 300) / 2400.0));

            p.Breath = Math.Max(0, Math.Min(1, 1.15 - t.Strength));
            p.Valid = true;
            return p;
        }
    }

    // Three-band EQ applied to the rendered take.
    public class EqSettings
    {
        public bool Bypass;
        public double LowDb;
        public double MidDb;
        public double HighDb;

        public bool Active { get { return !Bypass && (LowDb != 0 || MidDb != 0 || HighDb != 0); } }

        public float[] Process(float[] x, int sr)
        {
            if (!Active) return x;
            float[] y = new float[x.Length];
            Array.Copy(x, y, x.Length);
            Shelf(y, sr, 180, LowDb, true);
            Peak(y, sr, 1200, MidDb, 0.9);
            Shelf(y, sr, 6000, HighDb, false);
            return y;
        }

        static void Shelf(float[] buf, int sr, double f0, double db, bool low)
        {
            if (Math.Abs(db) < 0.01) return;
            double b0, b1, b2, a1, a2;
            ShelfCoeffs(sr, f0, db, low, out b0, out b1, out b2, out a1, out a2);
            Run(buf, b0, b1, b2, a1, a2);
        }

        // Normalised shelf coefficients, shared by filtering and the curve.
        static void ShelfCoeffs(int sr, double f0, double db, bool low,
            out double b0, out double b1, out double b2, out double a1, out double a2)
        {
            double A = Math.Pow(10, db / 40.0);
            double w0 = 2 * Math.PI * f0 / sr;
            double cw = Math.Cos(w0);
            double sw = Math.Sin(w0);
            double alpha = sw / 2 * Math.Sqrt(2.0);
            double twoSqrtAalpha = 2 * Math.Sqrt(A) * alpha;
            double nb0, nb1, nb2, a0, na1, na2;

            if (low)
            {
                nb0 = A * ((A + 1) - (A - 1) * cw + twoSqrtAalpha);
                nb1 = 2 * A * ((A - 1) - (A + 1) * cw);
                nb2 = A * ((A + 1) - (A - 1) * cw - twoSqrtAalpha);
                a0 = (A + 1) + (A - 1) * cw + twoSqrtAalpha;
                na1 = -2 * ((A - 1) + (A + 1) * cw);
                na2 = (A + 1) + (A - 1) * cw - twoSqrtAalpha;
            }
            else
            {
                nb0 = A * ((A + 1) + (A - 1) * cw + twoSqrtAalpha);
                nb1 = -2 * A * ((A - 1) + (A + 1) * cw);
                nb2 = A * ((A + 1) + (A - 1) * cw - twoSqrtAalpha);
                a0 = (A + 1) - (A - 1) * cw + twoSqrtAalpha;
                na1 = 2 * ((A - 1) - (A + 1) * cw);
                na2 = (A + 1) - (A - 1) * cw - twoSqrtAalpha;
            }

            b0 = nb0 / a0;
            b1 = nb1 / a0;
            b2 = nb2 / a0;
            a1 = na1 / a0;
            a2 = na2 / a0;
        }

        static void Peak(float[] buf, int sr, double f0, double db, double q)
        {
            if (Math.Abs(db) < 0.01) return;
            double b0, b1, b2, a1, a2;
            PeakCoeffs(sr, f0, db, q, out b0, out b1, out b2, out a1, out a2);
            Run(buf, b0, b1, b2, a1, a2);
        }

        static void PeakCoeffs(int sr, double f0, double db, double q,
            out double b0, out double b1, out double b2, out double a1, out double a2)
        {
            double A = Math.Pow(10, db / 40.0);
            double w0 = 2 * Math.PI * f0 / sr;
            double cw = Math.Cos(w0);
            double alpha = Math.Sin(w0) / (2 * q);
            double a0 = 1 + alpha / A;
            b0 = (1 + alpha * A) / a0;
            b1 = (-2 * cw) / a0;
            b2 = (1 - alpha * A) / a0;
            a1 = (-2 * cw) / a0;
            a2 = (1 - alpha / A) / a0;
        }

        static double SectionMag(double hz, int sr, double b0, double b1, double b2, double a1, double a2)
        {
            double w = 2 * Math.PI * hz / sr;
            double cw = Math.Cos(w);
            double sw = Math.Sin(w);
            double c2 = Math.Cos(2 * w);
            double s2 = Math.Sin(2 * w);
            double nr = b0 + b1 * cw + b2 * c2;
            double ni = -(b1 * sw + b2 * s2);
            double dr = 1 + a1 * cw + a2 * c2;
            double di = -(a1 * sw + a2 * s2);
            return Math.Sqrt((nr * nr + ni * ni) / Math.Max(1e-12, dr * dr + di * di));
        }

        // Response of the current settings at hz — this is what the curve draws.
        public double ResponseDb(double hz, int sr)
        {
            if (Bypass) return 0;
            double mag = 1;
            double b0, b1, b2, a1, a2;

            if (Math.Abs(LowDb) > 0.01)
            {
                ShelfCoeffs(sr, 180, LowDb, true, out b0, out b1, out b2, out a1, out a2);
                mag *= SectionMag(hz, sr, b0, b1, b2, a1, a2);
            }
            if (Math.Abs(MidDb) > 0.01)
            {
                PeakCoeffs(sr, 1200, MidDb, 0.9, out b0, out b1, out b2, out a1, out a2);
                mag *= SectionMag(hz, sr, b0, b1, b2, a1, a2);
            }
            if (Math.Abs(HighDb) > 0.01)
            {
                ShelfCoeffs(sr, 6000, HighDb, false, out b0, out b1, out b2, out a1, out a2);
                mag *= SectionMag(hz, sr, b0, b1, b2, a1, a2);
            }
            return 20 * Math.Log10(Math.Max(1e-6, mag));
        }

        static void Run(float[] buf, double b0, double b1, double b2, double a1, double a2)
        {
            double x1 = 0, x2 = 0, y1 = 0, y2 = 0;
            for (int i = 0; i < buf.Length; i++)
            {
                double x0 = buf[i];
                double y0 = b0 * x0 + b1 * x1 + b2 * x2 - a1 * y1 - a2 * y2;
                x2 = x1;
                x1 = x0;
                y2 = y1;
                y1 = y0;
                buf[i] = (float)y0;
            }
        }
    }

    // Active style + EQ shared with the renderer.
    public static class RenderStyle
    {
        public static StyleProfile Current;
        public static readonly EqSettings Eq = new EqSettings();
    }

    // Lightweight formant/wavetable singing synth. Writes a 16-bit mono WAV
    // that the player streams while the UI draws the playhead.
    public static class VocalSynth
    {
        public const int Sr = 48000;
        const int TableSize = 2048;

        static readonly double[,] Formants =
        {
            { 800, 1150, 2900 },  // a
            { 500, 1800, 2500 },  // e
            { 300, 2300, 3000 },  // i
            { 500,  900, 2400 },  // o
            { 320,  800, 2300 }   // u
        };

        public static int VowelIndex(string v)
        {
            switch (v)
            {
                case "ae": case "ay": case "ao": case "aa": return 0;
                case "eh": case "ey": case "ax": return 1;
                case "iy": case "ih": return 2;
                case "ow": return 3;
                case "uw": return 4;
                default: return 1;
            }
        }

        static bool IsVowelPhon(string p)
        {
            switch (p)
            {
                case "ae": case "eh": case "iy": case "ih": case "ow": case "uw": case "ax":
                case "ay": case "ey": case "ao": case "aa": case "er": case "oy": case "aw":
                    return true;
                default: return false;
            }
        }

        // The consonant of a syllable, wherever the writer put it. Seeds carry both shapes:
        // "dh ax" puts the onset first, "ae t" leaves only a coda.
        static string ConsonantOf(string phon)
        {
            if (string.IsNullOrEmpty(phon)) return "";
            string[] parts = phon.Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (string p in parts)
            {
                string c = p.Trim().ToLowerInvariant();
                if (c.Length > 0 && !IsVowelPhon(c)) return c;
            }
            return "";
        }

        // 1 sibilant, 2 plosive, 3 fricative, 4 nasal or liquid, 0 nothing recognisable.
        static int OnsetClass(string c)
        {
            switch (c)
            {
                case "s": case "sh": case "z": case "zh": case "ch": case "jh": return 1;
                case "p": case "t": case "k": case "b": case "d": case "g": return 2;
                case "f": case "v": case "th": case "dh": case "h": return 3;
                case "m": case "n": case "ng": case "l": case "r": case "w": case "y": return 4;
                default: return 0;
            }
        }

        static double Weight(double f, int vowel, double genderMul)
        {
            double s = 0.16;
            for (int i = 0; i < 3; i++)
            {
                double fc = Formants[vowel, i] * genderMul;
                double bw = fc * 0.2;
                double d = f - fc;
                s += Math.Exp(-(d * d) / (2 * bw * bw));
            }
            return s;
        }

        static float[] MakeTable(double f0, int vowel, double genderMul, double tension, double tilt)
        {
            float[] table = new float[TableSize];
            int maxH = (int)Math.Floor(5200.0 / f0);
            if (maxH > 28) maxH = 28;
            if (maxH < 1) maxH = 1;

            double[] amp = new double[maxH + 1];
            double sum = 0;
            for (int h = 1; h <= maxH; h++)
            {
                double f = h * f0;
                double a = Weight(f, vowel, genderMul) / Math.Pow(h, tilt);
                if (h > 8) a *= 1.0 + tension * 0.35;   // brighter upper harmonics
                amp[h] = a;
                sum += a;
            }
            if (sum <= 0.0001) sum = 1;

            for (int i = 0; i < TableSize; i++)
            {
                double ph = 2 * Math.PI * i / TableSize;
                double s = 0;
                for (int h = 1; h <= maxH; h++) s += amp[h] * Math.Sin(ph * h);
                table[i] = (float)(s / sum);
            }
            return table;
        }

        static double Envelope(double t, double dur)
        {
            if (t < 0.045) return t / 0.045;
            if (t <= dur) return 1.0;
            double rel = 1.0 - (t - dur) / 0.20;
            return rel < 0 ? 0 : rel;
        }

        public static string Render(string path, double bpm, double tension, double breath, double gender, bool metronome, List<Note> notes)
        {
            return Render(path, bpm, tension, breath, gender, metronome, notes, true);
        }

        // normalize=false leaves the synth's own level alone, so several takes keep their
        // relative balance when they are mixed outside the app.
        public static string Render(string path, double bpm, double tension, double breath, double gender, bool metronome, List<Note> notes, bool normalize)
        {
            return Render(path, bpm, tension, breath, gender, metronome, notes, normalize, 0);
        }

        // trimDb is a per-export gain, applied after the take's own level so a take can be
        // balanced against others before it leaves the app.
        public static string Render(string path, double bpm, double tension, double breath, double gender, bool metronome, List<Note> notes, bool normalize, double trimDb)
        {
            double spb = 60.0 / bpm;
            int beatsToRender = Project.TotalBeats;
            int total = (int)(beatsToRender * spb * Sr) + Sr;
            // Two channels: per-track pan needs somewhere to put the sides, and level is
            // folded into the same gain so stems and the full mix share one path.
            float[] left = new float[total];
            float[] right = new float[total];
            ClearTrackStats();

            StyleProfile st = RenderStyle.Current;
            bool styled = st != null && st.Valid;
            double genderMul = (1.0 + (gender / 100.0) * 0.45) * (styled ? st.FormantMul : 1.0);
            double breathAmt = Math.Max(0, Math.Min(1, breath / 100.0 + (styled ? st.BreathBias * 0.35 : 0)));
            double tensionAmt = Math.Max(0, Math.Min(1, tension / 100.0));
            double tilt = styled ? st.Tilt : 0.95;
            bool anySolo = AnySolo();

            foreach (Note note in notes)
            {
                double gainDb = 0, pan = 0;
                Track owner = null;
                foreach (Track tr in Project.Tracks)
                    if (tr.Id == note.Track) { owner = tr; gainDb = tr.GainDb; pan = tr.Pan; break; }
                // Mute silences a track outright; a solo silences every track that is not soloed.
                if (!TrackAudible(owner, anySolo)) continue;

                double trackGain = Math.Pow(10, gainDb / 20.0);
                double panAngle = (pan + 1) * Math.PI / 4.0;   // constant-power law
                double gl = Math.Cos(panAngle) * trackGain;
                double gr = Math.Sin(panAngle) * trackGain;

                if (note.Track == "p1")
                {
                    AddPiano(note, (int)(note.Start * spb * Sr), (int)(note.Dur * spb * Sr), total, left, right, gl, gr);
                    continue;
                }

                // Noise is seeded per note: a note breathes the same way whichever other
                // notes share the render, so per-track stems add back up to the full mix.
                Random rnd = new Random(unchecked(note.Id * 397 + 7));

                double f0 = 440.0 * Math.Pow(2.0, (note.Midi - 69) / 12.0);
                int vi = VowelIndex(note.Vowel);
                float[] table = MakeTable(f0, vi, genderMul, tensionAmt, tilt);

                int start = (int)(note.Start * spb * Sr);
                int durSamples = (int)(note.Dur * spb * Sr);
                int len = durSamples + (int)(0.22 * Sr);
                double durSec = durSamples / (double)Sr;

                // Velocity shapes the sung note: level, onset, breath and a touch of tone.
                // Every factor is exactly 1 at full velocity, so existing takes render unchanged.
                double vel = Math.Max(0.0, Math.Min(1.0, note.Velocity));
                double velGain = 0.30 + 0.70 * vel;
                double velAttack = 0.030 * (1 - vel);
                double velTone = 0.60 * (1 - vel);
                double velBreath = 0.7 + 0.3 * vel;
                double velCons = 0.55 + 0.45 * vel;

                double nSum = 0;
                float nPeak = 0;
                int nCount = 0;

                double phase = 0;
                double lp = 0;
                double lpv = 0;
                for (int k = 0; k < len; k++)
                {
                    int idx = start + k;
                    if (idx < 0 || idx >= total) continue;

                    double t = k / (double)Sr;
                    double prog = k / (double)Math.Max(1, durSamples);
                    double vibOn = prog < 0.3 ? 0 : Math.Min(1, (prog - 0.3) / 0.5);
                    double vibRate = styled ? st.VibratoRate : note.Rate;
                    double vib = Math.Sin(2 * Math.PI * vibRate * t) * note.Vib * (styled ? st.VibScale : 1.0) * 55 * vibOn;
                    double scoop = prog < 0.1 ? -30 * (1 - prog / 0.1) : 0;
                    double f = f0 * Math.Pow(2.0, (vib + scoop + note.Drift) / 1200.0);

                    phase += f / Sr;
                    if (phase >= 1) phase -= 1;

                    double idxf = phase * TableSize;
                    int i0 = (int)idxf & (TableSize - 1);
                    int i1 = (i0 + 1) & (TableSize - 1);
                    double frac = idxf - Math.Floor(idxf);
                    double s = table[i0] * (1 - frac) + table[i1] * frac;

                    if (breathAmt > 0.05)
                    {
                        double wn = rnd.NextDouble() * 2 - 1;
                        lp = lp * 0.88 + wn * 0.12;
                        s += lp * breathAmt * 0.28 * velBreath;
                    }

                    double shaped = s;
                    if (velTone > 0.001)
                    {
                        lpv = lpv * 0.72 + s * 0.28;
                        shaped = s * (1 - velTone) + lpv * velTone;
                    }

                    double env = Envelope(t, durSec);
                    // A little shimmer, because a held note sung by a person is never perfectly
                    // still; without it a sustained vowel sounds like a synth oscillator.
                    env *= 1.0 + 0.035 * Math.Sin(2 * Math.PI * 5.3 * t + note.Id * 0.7);
                    if (velAttack > 0.0005) env *= Math.Min(1.0, t / velAttack);
                    float ns = (float)(shaped * env * 0.26 * velGain);
                    float lv = (float)(ns * gl), rv = (float)(ns * gr);
                    left[idx] += lv;
                    right[idx] += rv;
                    nSum += (double)lv * lv + (double)rv * rv;
                    float ap = Math.Max(Math.Abs(lv), Math.Abs(rv));
                    if (ap > nPeak) nPeak = ap;
                    nCount++;
                }

                // The syllable's own consonant shapes the onset: a sibilant hisses, a plosive
                // clicks, a fricative breathes, a nasal hums. Every syllable used to start with
                // the same generic click, so a sung line read as an instrument playing notes
                // rather than a voice saying words.
                string onset = ConsonantOf(note.Phon);
                int consLen = (int)(0.050 * Sr);
                double consLevel = 0.16, consDecay = 70, consHp = 0.15;
                switch (OnsetClass(onset))
                {
                    case 1: consLen = (int)(0.085 * Sr); consLevel = 0.30; consDecay = 22; consHp = 0.90; break;
                    case 2: consLen = (int)(0.020 * Sr); consLevel = 0.38; consDecay = 130; consHp = 0.55; break;
                    case 3: consLen = (int)(0.060 * Sr); consLevel = 0.16; consDecay = 38; consHp = 0.25; break;
                    case 4: consLen = (int)(0.055 * Sr); consLevel = 0.11; consDecay = 30; consHp = 0.05; break;
                }
                double prevNoise = 0;
                for (int k = 0; k < consLen; k++)
                {
                    int idx = start + k;
                    if (idx < 0 || idx >= total) continue;
                    double t = k / (double)Sr;
                    double wn = rnd.NextDouble() * 2 - 1;
                    // ConsHp tilts the same noise from a hiss down to a hum without needing
                    // a filter per phoneme, and stays deterministic for the stem sum.
                    double shapedNoise = wn - prevNoise * consHp;
                    prevNoise = wn;
                    double rise = Math.Min(1.0, t / 0.012);
                    double cenv = Math.Exp(-t * consDecay) * rise * consLevel * velGain * velCons;
                    float cs = (float)(shapedNoise * cenv);
                    float cl = (float)(cs * gl), cr = (float)(cs * gr);
                    left[idx] += cl;
                    right[idx] += cr;
                    nSum += (double)cl * cl + (double)cr * cr;
                    float cap = Math.Max(Math.Abs(cl), Math.Abs(cr));
                    if (cap > nPeak) nPeak = cap;
                    nCount++;
                }

                AddTrackStat(note.Track, nSum, nPeak, nCount);
            }

            if (metronome)
            {
                int beatCount = (int)(beatsToRender / 1);
                for (int b = 0; b <= beatCount; b++)
                {
                    int start = (int)(b * spb * Sr);
                    bool accent = b % 4 == 0;
                    double freq = accent ? 1580 : 1050;
                    int len = (int)(0.05 * Sr);
                    for (int k = 0; k < len; k++)
                    {
                        int idx = start + k;
                        if (idx < 0 || idx >= total) continue;
                        double t = k / (double)Sr;
                        double env = Math.Exp(-t * 90) * (accent ? 0.10 : 0.05);
                        float ms = (float)(Math.Sin(2 * Math.PI * freq * t) * env);
                        left[idx] += ms * 0.7071f;
                        right[idx] += ms * 0.7071f;
                    }
                }
            }

            left = RenderStyle.Eq.Process(left, Sr);
            right = RenderStyle.Eq.Process(right, Sr);

            float peak = 0.0001f;
            for (int i = 0; i < total; i++)
            {
                float a = Math.Abs(left[i]); if (a > peak) peak = a;
                a = Math.Abs(right[i]); if (a > peak) peak = a;
            }
            float gain = (normalize ? 0.88f / peak : 1f) * (float)Math.Pow(10, trimDb / 20.0);

            using (FileStream fs = new FileStream(path, FileMode.Create, FileAccess.Write))
            using (BinaryWriter w = new BinaryWriter(fs))
            {
                int dataBytes = total * 4;
                w.Write(new char[] { 'R', 'I', 'F', 'F' });
                w.Write(36 + dataBytes);
                w.Write(new char[] { 'W', 'A', 'V', 'E' });
                w.Write(new char[] { 'f', 'm', 't', ' ' });
                w.Write(16);
                w.Write((short)1);
                w.Write((short)2);
                w.Write(Sr);
                w.Write(Sr * 4);
                w.Write((short)4);
                w.Write((short)16);
                w.Write(new char[] { 'd', 'a', 't', 'a' });
                w.Write(dataBytes);
                for (int i = 0; i < total; i++)
                {
                    float l = left[i] * gain, r = right[i] * gain;
                    if (l > 1f) l = 1f; if (l < -1f) l = -1f;
                    if (r > 1f) r = 1f; if (r < -1f) r = -1f;
                    w.Write((short)(l * 32000));
                    w.Write((short)(r * 32000));
                }
            }
            return path;
        }

        // Mute is absolute; solo silences everything that is not soloed. Shared with stem
        // export so a skipped stem matches exactly what the mix leaves out.
        // Only a solo on a track that can actually be heard counts. Pressing S and then M on the
        // same track used to leave solo engaged on a silenced track, which silenced every other
        // track as well: a mix of nothing, with the metronome as the only audible part.
        public static bool AnySolo()
        {
            foreach (Track t in Project.Tracks) if (t.Solo && !t.Muted) return true;
            return false;
        }

        public static bool TrackAudible(Track t, bool anySolo)
        {
            if (t == null) return true;
            return !t.Muted && (!anySolo || t.Solo);
        }

        // Per-track level of the last render, so the track headers can show a real meter
        // without exporting anything. Values are the track's own contribution after its
        // level and pan, before the master EQ.
        public static readonly Dictionary<string, double[]> TrackStats = new Dictionary<string, double[]>();

        public static void ClearTrackStats()
        {
            lock (TrackStats) TrackStats.Clear();
        }

        static void AddTrackStat(string id, double sumSq, float peak, int count)
        {
            if (string.IsNullOrEmpty(id)) return;
            lock (TrackStats)
            {
                double[] s;
                if (!TrackStats.TryGetValue(id, out s)) { s = new double[] { 0, 0, 0 }; TrackStats[id] = s; }
                s[0] += sumSq;
                if (peak > s[1]) s[1] = peak;
                s[2] += count;
            }
        }

        public static double TrackPeak(string id)
        {
            lock (TrackStats)
            {
                double[] s;
                if (!TrackStats.TryGetValue(id, out s)) return 0;
                return s[1];
            }
        }

        public static double TrackRms(string id)
        {
            lock (TrackStats)
            {
                double[] s;
                if (!TrackStats.TryGetValue(id, out s) || s[2] <= 0) return 0;
                return Math.Sqrt(s[0] / s[2]);
            }
        }

        static readonly Dictionary<int, float[]> _pianoTables = new Dictionary<int, float[]>();
        static readonly object _pianoLock = new object();
        static volatile bool _pianoWarm;

        // False until the startup warm-up has finished, so the UI can say plainly whether the
        // first play is going to wait on table building or not.
        public static bool PianoTablesWarm
        {
            get { return _pianoWarm; }
        }

        public static int PianoTableCount
        {
            get { lock (_pianoLock) return _pianoTables.Count; }
        }

        // Tables are half a megabyte each and take real time to synthesise, so they are mirrored
        // to disk and read back on the next launch. The file name carries a format version and
        // the sample rate, and the payload carries a checksum, so a changed synth, a different
        // rate or a half-written file misses the cache instead of loading stale or broken tone.
        const int PianoCacheMagic = 0x50424E31;   // "PBN1"
        const int PianoCacheVersion = 1;
        static readonly object _pianoFileLock = new object();

        static string PianoCacheDir
        {
            get
            {
                string root = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                return System.IO.Path.Combine(System.IO.Path.Combine(root, "VoiceBox"), "Piano Cache");
            }
        }

        static string PianoCachePath(int midi, int vb, int len)
        {
            return System.IO.Path.Combine(PianoCacheDir,
                "piano-v" + PianoCacheVersion + "-" + Sr + "-" + midi + "-" + vb + "-" + len + ".pbt");
        }

        static uint PianoChecksum(byte[] data, int offset, int count)
        {
            uint h = 2166136261;
            for (int i = 0; i < count; i++)
            {
                h ^= data[offset + i];
                h *= 16777619;
            }
            return h;
        }

        static float[] LoadPianoTable(int midi, int vb, int len)
        {
            try
            {
                string path = PianoCachePath(midi, vb, len);
                if (!System.IO.File.Exists(path)) return null;
                byte[] bytes = System.IO.File.ReadAllBytes(path);
                if (bytes.Length != 12 + len * 4) return null;
                if (BitConverter.ToInt32(bytes, 0) != PianoCacheMagic) return null;
                if (BitConverter.ToInt32(bytes, 4) != len) return null;
                if (BitConverter.ToUInt32(bytes, 8) != PianoChecksum(bytes, 12, len * 4)) return null;
                float[] tab = new float[len];
                Buffer.BlockCopy(bytes, 12, tab, 0, len * 4);
                return tab;
            }
            catch { return null; }
        }

        static void SavePianoTable(int midi, int vb, float[] tab)
        {
            try
            {
                byte[] bytes = new byte[12 + tab.Length * 4];
                BitConverter.GetBytes(PianoCacheMagic).CopyTo(bytes, 0);
                BitConverter.GetBytes(tab.Length).CopyTo(bytes, 4);
                Buffer.BlockCopy(tab, 0, bytes, 12, tab.Length * 4);
                BitConverter.GetBytes(PianoChecksum(bytes, 12, tab.Length * 4)).CopyTo(bytes, 8);

                lock (_pianoFileLock)
                {
                    System.IO.Directory.CreateDirectory(PianoCacheDir);
                    string path = PianoCachePath(midi, vb, tab.Length);
                    string tmp = path + ".tmp";
                    System.IO.File.WriteAllBytes(tmp, bytes);
                    if (System.IO.File.Exists(path)) System.IO.File.Delete(path);
                    System.IO.File.Move(tmp, path);
                }
            }
            catch { }   // a full or read-only disk must not stop a render
        }

        // Builds every table the current part needs off the UI thread, so the first render after
        // launch costs the same as later ones instead of paying for the whole cache.
        public static void WarmPianoTables(List<Note> notes)
        {
            if (notes == null) { _pianoWarm = true; return; }
            List<Note> copy;
            try { copy = new List<Note>(notes); }
            catch { _pianoWarm = true; return; }   // notes may be edited meanwhile

            for (int i = 0; i < copy.Count; i++)
            {
                Note n = copy[i];
                if (n == null || n.Track != "p1") continue;
                int vb = (int)Math.Round(Math.Max(0, Math.Min(1, n.Velocity)) * 7);
                try { PianoTable(n.Midi, vb); } catch { }
            }
            _pianoWarm = true;
        }

        // A struck string, synthesised rather than sampled: stretched partials, a fast hammer
        // attack, faster decay on the high partials and a damper when the key comes up. One
        // table per pitch is cached, so a stem is bit-identical to that track in the full mix.
        static float[] PianoTable(int midi, int vb)
        {
            int key = midi * 8 + vb;
            float[] tab;
            lock (_pianoLock) { if (_pianoTables.TryGetValue(key, out tab)) return tab; }

            int len = Sr * 2 + Sr / 2;

            // Second look: the table may already be on disk from an earlier session, which turns
            // the startup warm-up into a handful of file reads instead of hours of synthesis.
            tab = LoadPianoTable(midi, vb, len);
            if (tab != null)
            {
                lock (_pianoLock) { _pianoTables[key] = tab; }
                return tab;
            }

            // A harder strike excites more of the string, so the tone brightens with velocity.
            double hardness = 0.45 + 0.55 * (vb / 7.0);

            tab = new float[len];
            double f0 = 440.0 * Math.Pow(2.0, (midi - 69) / 12.0);
            double stretch = 0.00035 * Math.Max(0.35, Math.Min(2.4, 220.0 / Math.Max(40.0, f0)));
            int partials = (int)Math.Max(3, Math.Min(10, 6000.0 / Math.Max(40.0, f0)));
            double sum = 0;
            for (int n = 1; n <= partials; n++) sum += 1.0 / Math.Pow(n, 1.35);

            for (int n = 1; n <= partials; n++)
            {
                double fn = n * f0 * Math.Sqrt(1.0 + stretch * n * n);
                if (fn >= Sr * 0.45) break;
                double amp = (1.0 / Math.Pow(n, 1.35)) / sum * Math.Pow(hardness, n * 0.45);
                double dec = 1.0 + 0.85 * n;
                double w = 2 * Math.PI * fn / Sr;
                double ph = 0;
                for (int k = 0; k < len; k++)
                {
                    tab[k] += (float)(amp * Math.Exp(-dec * k / (double)Sr) * Math.Sin(ph));
                    ph += w;
                    if (ph > Math.PI * 4) ph -= Math.PI * 4;
                }
            }

            float peak = 0.0001f;
            for (int k = 0; k < len; k++) { float a = Math.Abs(tab[k]); if (a > peak) peak = a; }
            for (int k = 0; k < len; k++) tab[k] = tab[k] / peak;
            SavePianoTable(midi, vb, tab);
            // Built outside the lock so a concurrent build is not blocked; whoever lands
            // second just replaces an identical table.
            lock (_pianoLock) { _pianoTables[key] = tab; }
            return tab;
        }

        static void AddPiano(Note note, int start, int durSamples, int total, float[] left, float[] right, double gl, double gr)
        {
            int vb = (int)Math.Round(Math.Max(0, Math.Min(1, note.Velocity)) * 7);
            double vel = vb / 7.0;
            float[] tab = PianoTable(note.Midi, vb);
            double amp = 0.26 * (0.15 + 0.85 * vel);
            double knockAmt = 0.05 * (0.3 + 0.7 * vel);
            Random rnd = new Random(unchecked(note.Id * 7919 + 13));
            double durSec = durSamples / (double)Sr;
            int len = Math.Min(tab.Length, Math.Max(durSamples, 1) + (int)(1.2 * Sr));
            double sum = 0;
            float peak = 0;
            int count = 0;

            for (int k = 0; k < len; k++)
            {
                int idx = start + k;
                if (idx < 0 || idx >= total) continue;
                double t = k / (double)Sr;

                double env = Math.Min(1.0, t / 0.004);
                if (t > durSec) env *= Math.Exp(-(t - durSec) * 7.0);   // key up, damper falls
                double knock = (rnd.NextDouble() * 2 - 1) * Math.Exp(-t * 110) * knockAmt;

                float v = (float)((tab[k] * env + knock) * amp);
                double lv = v * gl, rv = v * gr;
                left[idx] += (float)lv;
                right[idx] += (float)rv;
                sum += lv * lv + rv * rv;
                float ap = (float)Math.Max(Math.Abs(lv), Math.Abs(rv));
                if (ap > peak) peak = ap;
                count++;
            }

            AddTrackStat(note.Track, sum, peak, count);
        }
    }
}
