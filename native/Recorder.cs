using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;

namespace VoiceBox
{
    public static class WavIO
    {
        // Loads PCM or float WAV files for style matching. Returns null on success.
        public static string Read(string path, out float[] samples, out int sampleRate)
        {
            samples = new float[0];
            sampleRate = 44100;
            try
            {
                byte[] all = File.ReadAllBytes(path);
                if (all.Length < 44) return "That file is too short to be a WAV.";

                int pos = 12;
                int fmtCode = 1;
                int channels = 1;
                int bits = 16;
                int dataPos = -1;
                int dataLen = 0;

                while (pos + 8 <= all.Length)
                {
                    string id = "" + (char)all[pos] + (char)all[pos + 1] + (char)all[pos + 2] + (char)all[pos + 3];
                    int size = BitConverter.ToInt32(all, pos + 4);
                    int body = pos + 8;
                    if (size < 0) size = 0;

                    if (id == "fmt ")
                    {
                        fmtCode = BitConverter.ToInt16(all, body);
                        channels = BitConverter.ToInt16(all, body + 2);
                        sampleRate = BitConverter.ToInt32(all, body + 4);
                        bits = BitConverter.ToInt16(all, body + 14);
                    }
                    else if (id == "data")
                    {
                        dataPos = body;
                        dataLen = Math.Min(size, all.Length - body);
                        break;
                    }
                    pos = body + size + (size % 2);
                }

                if (dataPos < 0) return "No audio data was found in that WAV.";
                if (channels < 1) channels = 1;
                int bytesPerSample = Math.Max(1, bits / 8);
                int frames = dataLen / (bytesPerSample * channels);
                if (frames < 1) return "That WAV has no samples.";

                float[] mono = new float[frames];
                for (int i = 0; i < frames; i++)
                {
                    double acc = 0;
                    for (int c = 0; c < channels; c++)
                    {
                        int o = dataPos + (i * channels + c) * bytesPerSample;
                        if (o + bytesPerSample > all.Length) break;
                        if (fmtCode == 3 && bits == 32) acc += BitConverter.ToSingle(all, o);
                        else if (bits == 16) acc += BitConverter.ToInt16(all, o) / 32768.0;
                        else if (bits == 24) acc += (((all[o] | (all[o + 1] << 8) | (all[o + 2] << 16)) << 8) >> 8) / 8388608.0;
                        else acc += (all[o] - 128) / 128.0;
                    }
                    mono[i] = (float)(acc / channels);
                }

                samples = mono;
                return null;
            }
            catch (Exception ex)
            {
                return "Could not read that WAV (" + ex.Message + ")";
            }
        }

        public static void Write16(string path, float[] samples, int sampleRate)
        {
            using (FileStream fs = new FileStream(path, FileMode.Create, FileAccess.Write))
            using (BinaryWriter w = new BinaryWriter(fs))
            {
                int dataBytes = samples.Length * 2;
                w.Write(new char[] { 'R', 'I', 'F', 'F' });
                w.Write(36 + dataBytes);
                w.Write(new char[] { 'W', 'A', 'V', 'E' });
                w.Write(new char[] { 'f', 'm', 't', ' ' });
                w.Write(16);
                w.Write((short)1);
                w.Write((short)1);
                w.Write(sampleRate);
                w.Write(sampleRate * 2);
                w.Write((short)2);
                w.Write((short)16);
                w.Write(new char[] { 'd', 'a', 't', 'a' });
                w.Write(dataBytes);
                for (int i = 0; i < samples.Length; i++)
                {
                    float v = samples[i];
                    if (v > 1f) v = 1f;
                    if (v < -1f) v = -1f;
                    w.Write((short)(v * 32000));
                }
            }
        }
    }

    // Microphone capture through winmm waveIn. Buffers are queued and the
    // driver thread appends every completed block to a sample list.
    public class Recorder
    {
        public const int SampleRate = 44100;
        const int BufferCount = 8;
        const int BufferSamples = 4410;   // 100 ms per block

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        struct WAVEFORMATEX
        {
            public ushort wFormatTag;
            public ushort nChannels;
            public uint nSamplesPerSec;
            public uint nAvgBytesPerSec;
            public ushort nBlockAlign;
            public ushort wBitsPerSample;
            public ushort cbSize;
        }

        [StructLayout(LayoutKind.Sequential)]
        struct WAVEHDR
        {
            public IntPtr lpData;
            public uint dwBufferLength;
            public uint dwBytesRecorded;
            public IntPtr dwUser;
            public uint dwFlags;
            public uint dwLoops;
            public IntPtr lpNext;
            public IntPtr reserved;
        }

        [DllImport("winmm.dll")]
        static extern int waveInGetNumDevs();
        [DllImport("winmm.dll")]
        static extern int waveInOpen(out IntPtr hwi, uint device, ref WAVEFORMATEX fmt, IntPtr callback, IntPtr instance, uint flags);
        [DllImport("winmm.dll")]
        static extern int waveInPrepareHeader(IntPtr hwi, IntPtr hdr, int size);
        [DllImport("winmm.dll")]
        static extern int waveInUnprepareHeader(IntPtr hwi, IntPtr hdr, int size);
        [DllImport("winmm.dll")]
        static extern int waveInAddBuffer(IntPtr hwi, IntPtr hdr, int size);
        [DllImport("winmm.dll")]
        static extern int waveInStart(IntPtr hwi);
        [DllImport("winmm.dll")]
        static extern int waveInStop(IntPtr hwi);
        [DllImport("winmm.dll")]
        static extern int waveInReset(IntPtr hwi);
        [DllImport("winmm.dll")]
        static extern int waveInClose(IntPtr hwi);

        readonly List<float> _samples = new List<float>();
        IntPtr _hwi = IntPtr.Zero;
        IntPtr[] _headers;
        IntPtr[] _buffers;
        bool _active;

        public string Error;
        public float LastRms;
        public long ElapsedSamples;
        public readonly string FilePath;

        public Recorder()
        {
            FilePath = Path.Combine(Path.GetTempPath(), "voicebox_input.wav");
        }

        public static int InputDevices
        {
            get
            {
                try { return waveInGetNumDevs(); }
                catch { return 0; }
            }
        }

        public bool IsRecording { get { return _active; } }

        public double Seconds
        {
            get { return ElapsedSamples / (double)SampleRate; }
        }

        public float[] Snapshot()
        {
            lock (_samples) return _samples.ToArray();
        }

        public bool Start()
        {
            if (_active) return true;
            lock (_samples) _samples.Clear();
            ElapsedSamples = 0;
            LastRms = 0;
            Error = null;

            if (InputDevices <= 0)
            {
                Error = "No microphone was found (0 input devices).";
                return false;
            }

            WAVEFORMATEX fmt = new WAVEFORMATEX();
            fmt.wFormatTag = 1;
            fmt.nChannels = 1;
            fmt.nSamplesPerSec = SampleRate;
            fmt.wBitsPerSample = 16;
            fmt.nBlockAlign = 2;
            fmt.nAvgBytesPerSec = SampleRate * 2;
            fmt.cbSize = 0;

            int rc = waveInOpen(out _hwi, 0xFFFFFFFFu, ref fmt, IntPtr.Zero, IntPtr.Zero, 0);
            if (rc != 0)
            {
                Error = "Could not open the input device (winmm " + rc + ").";
                _hwi = IntPtr.Zero;
                return false;
            }

            int size = Marshal.SizeOf(typeof(WAVEHDR));
            _headers = new IntPtr[BufferCount];
            _buffers = new IntPtr[BufferCount];
            for (int i = 0; i < BufferCount; i++)
            {
                _buffers[i] = Marshal.AllocHGlobal(BufferSamples * 2);
                _headers[i] = Marshal.AllocHGlobal(size);
                WAVEHDR h = new WAVEHDR();
                h.lpData = _buffers[i];
                h.dwBufferLength = BufferSamples * 2;
                h.dwBytesRecorded = 0;
                h.dwUser = IntPtr.Zero;
                h.dwFlags = 0;
                h.dwLoops = 0;
                h.lpNext = IntPtr.Zero;
                h.reserved = IntPtr.Zero;
                Marshal.StructureToPtr(h, _headers[i], false);
                waveInPrepareHeader(_hwi, _headers[i], size);
                waveInAddBuffer(_hwi, _headers[i], size);
            }

            rc = waveInStart(_hwi);
            if (rc != 0)
            {
                Error = "Could not start recording (winmm " + rc + ").";
                Cleanup();
                return false;
            }
            _active = true;
            return true;
        }

        internal static void Dbg(string msg)
        {
            if (Environment.GetEnvironmentVariable("VOICEBOX_RECDEBUG") != "1") return;
            try
            {
                System.IO.File.AppendAllText(
                    System.IO.Path.Combine(System.IO.Path.GetTempPath(), "voicebox_rec_debug.log"),
                    msg + Environment.NewLine);
            }
            catch { }
        }

        // Drains finished driver buffers. Called from the UI timer, so capture
        // never depends on a winmm callback thread (that path deadlocks on close).
        public void Poll()
        {
            if (!_active || _headers == null) return;
            int size = Marshal.SizeOf(typeof(WAVEHDR));
            for (int i = 0; i < _headers.Length; i++)
            {
                IntPtr hdr = _headers[i];
                if (hdr == IntPtr.Zero) continue;
                WAVEHDR h = (WAVEHDR)Marshal.PtrToStructure(hdr, typeof(WAVEHDR));
                if ((h.dwFlags & 1u) == 0) continue; // WHDR_DONE

                int count = (int)h.dwBytesRecorded / 2;
                if (count > 0)
                {
                    short[] tmp = new short[count];
                    Marshal.Copy(h.lpData, tmp, 0, count);
                    double sum = 0;
                    lock (_samples)
                    {
                        for (int k = 0; k < count; k++)
                        {
                            float v = tmp[k] / 32768f;
                            _samples.Add(v);
                            sum += v * v;
                        }
                    }
                    LastRms = (float)Math.Sqrt(sum / count);
                    ElapsedSamples += count;
                }

                h.dwBytesRecorded = 0;
                h.dwFlags = h.dwFlags & 2u; // keep WHDR_PREPARED, clear WHDR_DONE
                Marshal.StructureToPtr(h, hdr, false);
                waveInAddBuffer(_hwi, hdr, size);
            }
        }

        public void Stop()
        {
            if (!_active) return;
            Poll(); // drain the last completed blocks before tearing down
            _active = false;
            Dbg("reset begin");
            try { waveInReset(_hwi); } catch { }
            Dbg("reset done");
            Cleanup();
            Dbg("cleanup done");
        }

        void Cleanup()
        {
            int size = Marshal.SizeOf(typeof(WAVEHDR));
            if (_headers != null)
            {
                for (int i = 0; i < _headers.Length; i++)
                {
                    if (_headers[i] != IntPtr.Zero)
                    {
                        Dbg("unprepare " + i);
                        try { waveInUnprepareHeader(_hwi, _headers[i], size); } catch { }
                        Marshal.FreeHGlobal(_headers[i]);
                        _headers[i] = IntPtr.Zero;
                    }
                    if (_buffers != null && _buffers[i] != IntPtr.Zero)
                    {
                        Marshal.FreeHGlobal(_buffers[i]);
                        _buffers[i] = IntPtr.Zero;
                    }
                }
            }
            if (_hwi != IntPtr.Zero)
            {
                Dbg("close begin");
                try { waveInClose(_hwi); } catch { }
                Dbg("close done");
                _hwi = IntPtr.Zero;
            }
        }

        public string SaveTake()
        {
            float[] snapshot = Snapshot();
            if (snapshot.Length > 0) WavIO.Write16(FilePath, snapshot, SampleRate);
            return FilePath;
        }
    }

    // Frame-level pitch track used by the style analyser.
    public class PitchTrack
    {
        public double[] Midi = new double[0];
        public bool[] Voiced = new bool[0];
        public int Hop = 1;
        public int Rate = 11025;
        public double Strength;

        public int Frames { get { return Voiced.Length; } }
        public double FrameSec { get { return 0.045; } }
        public double TimeOf(int i) { return (double)i * Hop / Rate; }
    }

    // Monophonic pitch tracking (autocorrelation) -> notes in project time.
    public static class PitchTracker
    {
        // Same analysis as ToNotes, exposed as raw frames for style matching.
        public static PitchTrack Track(float[] samples, int sampleRate)
        {
            PitchTrack t = new PitchTrack();
            if (samples == null || samples.Length < sampleRate / 8) return t;

            const int decim = 4;
            int hz = sampleRate / decim;
            int downLen = samples.Length / decim;
            float[] down = new float[downLen];
            for (int i = 0; i < downLen; i++)
            {
                float s = 0;
                for (int k = 0; k < decim; k++) s += samples[i * decim + k];
                down[i] = s / decim;
            }

            int frameLen = (int)(0.045 * hz);
            int hop = (int)(0.012 * hz);
            if (downLen <= frameLen) return t;
            int frames = (downLen - frameLen) / hop + 1;

            double[] midi = new double[frames];
            bool[] voiced = new bool[frames];
            int minLag = Math.Max(2, hz / 1000);
            int maxLag = Math.Min(frameLen - 2, hz / 60);
            double confSum = 0;
            int confCount = 0;

            for (int f = 0; f < frames; f++)
            {
                int off = f * hop;
                double energy = 0;
                for (int i = 0; i < frameLen; i++) energy += down[off + i] * down[off + i];
                double rms = Math.Sqrt(energy / frameLen);
                if (rms < 0.008) continue;

                double[] corr = new double[maxLag + 2];
                double best = 0;
                int bestLag = 0;
                for (int lag = minLag; lag <= maxLag; lag++)
                {
                    double c = CorrNormalized(down, off, frameLen, lag);
                    corr[lag] = c;
                    if (c > best) { best = c; bestLag = lag; }
                }
                if (best < 0.5 || bestLag <= 0) continue;

                int chosen = bestLag;
                for (int lag = minLag + 1; lag < maxLag; lag++)
                {
                    if (corr[lag] >= best * 0.92 && corr[lag] >= 0.55 &&
                        corr[lag] >= corr[lag - 1] && corr[lag] >= corr[lag + 1])
                    {
                        chosen = lag;
                        break;
                    }
                }

                double refined = chosen;
                double denom = corr[chosen - 1] - 2 * corr[chosen] + corr[chosen + 1];
                if (Math.Abs(denom) > 1e-9)
                    refined = chosen + 0.5 * (corr[chosen - 1] - corr[chosen + 1]) / denom;

                double freq = hz / refined;
                if (freq < 55 || freq > 1000) continue;

                midi[f] = 69 + 12 * Math.Log(freq / 440.0, 2);
                voiced[f] = true;
                confSum += corr[chosen];
                confCount++;
            }

            t.Midi = midi;
            t.Voiced = voiced;
            t.Hop = hop;
            t.Rate = hz;
            t.Strength = confCount > 0 ? confSum / confCount : 0;
            return t;
        }

        static double CorrNormalized(float[] x, int off, int len, int lag)
        {
            double sum = 0, e1 = 0, e2 = 0;
            int n = len - lag;
            for (int i = 0; i < n; i++)
            {
                double a = x[off + i];
                double b = x[off + i + lag];
                sum += a * b;
                e1 += a * a;
                e2 += b * b;
            }
            return sum / Math.Sqrt(e1 * e2 + 1e-12);
        }

        public static List<Note> ToNotes(float[] samples, int sampleRate, double bpm, double startBeat, string track)
        {
            List<Note> result = new List<Note>();
            if (samples == null || samples.Length < sampleRate / 4) return result;

            const int decim = 4;
            int hz = sampleRate / decim;
            int downLen = samples.Length / decim;
            float[] down = new float[downLen];
            for (int i = 0; i < downLen; i++)
            {
                float s = 0;
                for (int k = 0; k < decim; k++) s += samples[i * decim + k];
                down[i] = s / decim;
            }

            int frameLen = (int)(0.045 * hz);
            int hop = (int)(0.012 * hz);
            if (downLen <= frameLen) return result;
            int frames = (downLen - frameLen) / hop + 1;

            double[] midi = new double[frames];
            bool[] voiced = new bool[frames];
            int minLag = Math.Max(2, hz / 1000);
            int maxLag = Math.Min(frameLen - 2, hz / 60);

            for (int f = 0; f < frames; f++)
            {
                int off = f * hop;
                double energy = 0;
                for (int i = 0; i < frameLen; i++) energy += down[off + i] * down[off + i];
                double rms = Math.Sqrt(energy / frameLen);
                if (rms < 0.008) continue;

                double[] corr = new double[maxLag + 2];
                double best = 0;
                int bestLag = 0;
                for (int lag = minLag; lag <= maxLag; lag++)
                {
                    double c = CorrNormalized(down, off, frameLen, lag);
                    corr[lag] = c;
                    if (c > best) { best = c; bestLag = lag; }
                }
                if (best < 0.5 || bestLag <= 0) continue;

                // Prefer the shortest period that is nearly as strong as the global
                // peak, otherwise periodic signals lock one or two octaves low.
                int chosen = bestLag;
                for (int lag = minLag + 1; lag < maxLag; lag++)
                {
                    if (corr[lag] >= best * 0.92 && corr[lag] >= 0.55 &&
                        corr[lag] >= corr[lag - 1] && corr[lag] >= corr[lag + 1])
                    {
                        chosen = lag;
                        break;
                    }
                }

                double refined = chosen;
                double denom = corr[chosen - 1] - 2 * corr[chosen] + corr[chosen + 1];
                if (Math.Abs(denom) > 1e-9)
                    refined = chosen + 0.5 * (corr[chosen - 1] - corr[chosen + 1]) / denom;
                double freq = hz / refined;
                if (freq < 55 || freq > 1000) continue;

                midi[f] = 69 + 12 * Math.Log(freq / 440.0, 2);
                voiced[f] = true;
            }

            // light median smoothing over voiced frames
            double[] smooth = new double[frames];
            for (int f = 0; f < frames; f++)
            {
                if (!voiced[f]) continue;
                List<double> win = new List<double>();
                for (int k = Math.Max(0, f - 2); k <= Math.Min(frames - 1, f + 2); k++)
                    if (voiced[k]) win.Add(midi[k]);
                win.Sort();
                smooth[f] = win[win.Count / 2];
            }

            int pos = 0;
            while (pos < frames)
            {
                if (!voiced[pos]) { pos++; continue; }
                int j = pos;
                double med = smooth[pos];
                while (j < frames && voiced[j] && Math.Abs(smooth[j] - med) <= 1.2)
                {
                    j++;
                    if (j < frames && voiced[j]) med = med * 0.7 + smooth[j] * 0.3;
                }

                int len = j - pos;
                if (len >= 4)
                {
                    double[] seg = new double[len];
                    Array.Copy(smooth, pos, seg, 0, len);
                    Array.Sort(seg);
                    int midiNote = (int)Math.Round(seg[len / 2]);
                    midiNote = Math.Max(Project.BottomMidi, Math.Min(Project.TopMidi, midiNote));

                    double t0 = (double)pos * hop / hz;
                    double t1 = (double)j * hop / hz + (double)frameLen / hz;
                    double durSec = t1 - t0;
                    if (durSec >= 0.1)
                    {
                        Note n = new Note();
                        n.Id = Project.NewId();
                        n.Track = track;
                        n.Start = Math.Max(0, Math.Round((startBeat + t0 * bpm / 60.0) / 0.125) * 0.125);
                        n.Dur = Math.Max(0.125, Math.Round(durSec * bpm / 60.0 / 0.125) * 0.125);
                        n.Midi = midiNote;
                        n.Word = "la";
                        n.Phon = "l a";
                        n.Vowel = "ax";
                        n.Vib = durSec > 0.7 ? 0.35 : 0.15;
                        n.Rate = 5.2;
                        n.Drift = 0;
                        result.Add(n);
                    }
                }
                pos = j > pos ? j : pos + 1;
            }

            // join neighbouring notes of the same pitch that are nearly touching
            List<Note> merged = new List<Note>();
            foreach (Note n in result)
            {
                if (merged.Count > 0)
                {
                    Note prev = merged[merged.Count - 1];
                    double gap = n.Start - (prev.Start + prev.Dur);
                    if (prev.Midi == n.Midi && gap <= 0.2 && gap >= -0.01)
                    {
                        prev.Dur = Math.Round((n.Start + n.Dur - prev.Start) / 0.125) * 0.125;
                        continue;
                    }
                }
                merged.Add(n);
            }
            return merged;
        }
    }
}
