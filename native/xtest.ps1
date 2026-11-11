param(
  [switch]$Check,      # assert the WAV format and that the stems sum back to the mix
  [int]$Tolerance = 4  # LSB allowed between the summed stems and the mix, from 16-bit rounding
)
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName PresentationFramework

$asm = [Reflection.Assembly]::LoadFrom((Join-Path $PSScriptRoot 'VoiceBox.exe'))

# The same reader the check uses, compiled in memory: parsing the WAVs and summing millions of
# samples in PowerShell takes minutes, in C# it is a fraction of a second.
Add-Type -TypeDefinition @'
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

public static class XCheck
{
    public class Wav
    {
        public string Path;
        public int Format, Channels, Rate, Bits, BlockAlign, ByteRate;
        public long Frames;
        public short[] Samples;
    }

    public static Wav Read(string path)
    {
        byte[] b = File.ReadAllBytes(path);
        Wav w = new Wav();
        w.Path = path;
        if (b.Length < 44) throw new Exception("too short to be a WAV: " + path);
        if (Encoding.ASCII.GetString(b, 0, 4) != "RIFF" || Encoding.ASCII.GetString(b, 8, 4) != "WAVE")
            throw new Exception("not a RIFF/WAVE file: " + path);

        int pos = 12, dataOff = -1, dataLen = 0;
        while (pos + 8 <= b.Length)
        {
            string id = Encoding.ASCII.GetString(b, pos, 4);
            int size = BitConverter.ToInt32(b, pos + 4);
            if (id == "fmt ")
            {
                w.Format = BitConverter.ToInt16(b, pos + 8);
                w.Channels = BitConverter.ToInt16(b, pos + 10);
                w.Rate = BitConverter.ToInt32(b, pos + 12);
                w.ByteRate = BitConverter.ToInt32(b, pos + 16);
                w.BlockAlign = BitConverter.ToInt16(b, pos + 20);
                w.Bits = BitConverter.ToInt16(b, pos + 22);
            }
            else if (id == "data")
            {
                dataOff = pos + 8;
                dataLen = size;
            }
            if (size < 0) break;
            pos += 8 + size + (size % 2);
        }
        if (dataOff < 0) throw new Exception("no data chunk: " + path);
        if (dataOff + dataLen > b.Length) dataLen = b.Length - dataOff;

        int frameBytes = w.BlockAlign > 0 ? w.BlockAlign : 4;
        w.Frames = dataLen / frameBytes;
        int chans = w.Channels > 0 ? w.Channels : 2;
        w.Samples = new short[w.Frames * chans];
        Buffer.BlockCopy(b, dataOff, w.Samples, 0, (int)Math.Min(dataLen, (long)w.Samples.Length * 2));
        return w;
    }

    public static string Verify(string mixPath, string[] stemPaths, int toleranceLsb)
    {
        StringBuilder r = new StringBuilder();
        List<string> bad = new List<string>();

        Wav mix = Read(mixPath);
        r.AppendLine("MIX " + Path.GetFileName(mixPath) + " format=" + mix.Format + " channels=" + mix.Channels +
                     " rate=" + mix.Rate + " bits=" + mix.Bits + " blockAlign=" + mix.BlockAlign +
                     " byteRate=" + mix.ByteRate + " frames=" + mix.Frames);
        if (mix.Format != 1) bad.Add("mix format " + mix.Format + ", expected 1 (PCM)");
        if (mix.Channels != 2) bad.Add("mix has " + mix.Channels + " channels, expected 2");
        if (mix.Rate != 48000) bad.Add("mix rate " + mix.Rate + ", expected 48000");
        if (mix.Bits != 16) bad.Add("mix is " + mix.Bits + "-bit, expected 16");
        if (mix.BlockAlign != 4) bad.Add("mix block align " + mix.BlockAlign + ", expected 4");
        if (mix.ByteRate != 48000 * 4) bad.Add("mix byte rate " + mix.ByteRate + ", expected 192000");
        if (mix.Frames <= 0) bad.Add("mix has no samples");

        long n = (long)mix.Frames * mix.Channels;
        int[] sum = new int[n];
        long stemPeak = 0;
        foreach (string p in stemPaths)
        {
            Wav s = Read(p);
            r.AppendLine("STEM " + Path.GetFileName(p) + " format=" + s.Format + " channels=" + s.Channels +
                         " rate=" + s.Rate + " bits=" + s.Bits + " frames=" + s.Frames);
            if (s.Format != mix.Format) bad.Add(Path.GetFileName(p) + " format " + s.Format + " differs from the mix");
            if (s.Channels != mix.Channels) bad.Add(Path.GetFileName(p) + " channels " + s.Channels + " differ from the mix");
            if (s.Rate != mix.Rate) bad.Add(Path.GetFileName(p) + " rate " + s.Rate + " differs from the mix");
            if (s.Bits != mix.Bits) bad.Add(Path.GetFileName(p) + " bits " + s.Bits + " differ from the mix");
            if (s.Frames != mix.Frames) bad.Add(Path.GetFileName(p) + " frames " + s.Frames + " differ from the mix");
            long m = Math.Min(n, s.Samples.Length);
            for (long i = 0; i < m; i++)
            {
                sum[i] += s.Samples[i];
                long a = Math.Abs((long)s.Samples[i]);
                if (a > stemPeak) stemPeak = a;
            }
        }

        long maxDiff = 0, diffAt = -1, mixPeak = 0;
        for (long i = 0; i < n; i++)
        {
            long d = Math.Abs((long)sum[i] - mix.Samples[i]);
            if (d > maxDiff) { maxDiff = d; diffAt = i; }
            long a = Math.Abs((long)mix.Samples[i]);
            if (a > mixPeak) mixPeak = a;
        }

        r.AppendLine("STEMS " + stemPaths.Length + " stemPeak=" + stemPeak + " mixPeak=" + mixPeak);
        r.AppendLine("MAX_DIFF_LSB " + maxDiff + (diffAt >= 0 ? " at sample " + diffAt : "") + " (tolerance " + toleranceLsb + ")");
        if (stemPaths.Length == 0) bad.Add("no stems were written");
        if (mixPeak == 0) bad.Add("the mix is silent");
        if (stemPeak == 0) bad.Add("every stem is silent");
        if (maxDiff > toleranceLsb) bad.Add("summed stems differ from the mix by " + maxDiff + " LSB, tolerance " + toleranceLsb);

        foreach (string b in bad) r.AppendLine("FAIL " + b);
        if (bad.Count == 0) r.AppendLine("VERIFY OK");
        return r.ToString();
    }
}
'@

$bpm = [double][VoiceBox.Project]::Bpm
$anySolo = [VoiceBox.VocalSynth]::AnySolo()

$dir = Join-Path $env:TEMP ('vb_xtest_' + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Force -Path $dir | Out-Null
[Console]::WriteLine("work dir " + $dir)

function Render-One([string]$path, $list) {
  # Raw level, no trim and no metronome: exactly what EXPORT RAW and the stem writer use.
  [VoiceBox.VocalSynth]::Render($path, $bpm, 0.0, 0.0, 0.0, $false, $list, $false, 0.0) | Out-Null
  return $path
}

$clock = [Diagnostics.Stopwatch]::StartNew()
$mixPath = Join-Path $dir 'mix.wav'
Render-One $mixPath ([VoiceBox.Project]::Notes)
[Console]::WriteLine("mix rendered in " + [int]$clock.Elapsed.TotalSeconds + "s")

$stemPaths = New-Object System.Collections.Generic.List[string]
foreach ($t in [VoiceBox.Project]::Tracks) {
  $mine = New-Object 'System.Collections.Generic.List[VoiceBox.Note]'
  foreach ($x in [VoiceBox.Project]::Notes) { if ($x.Track -eq $t.Id) { $mine.Add($x) } }
  if ($mine.Count -eq 0) { continue }
  if (-not [VoiceBox.VocalSynth]::TrackAudible($t, $anySolo)) { continue }
  $p = Join-Path $dir ('stem-' + $t.Id + '.wav')
  Render-One $p $mine
  $stemPaths.Add($p) | Out-Null
}
[Console]::WriteLine("stems rendered in " + [int]$clock.Elapsed.TotalSeconds + "s total")

$report = [XCheck]::Verify($mixPath, $stemPaths.ToArray(), $Tolerance)
[Console]::WriteLine($report.TrimEnd())

if ($Check) {
  if ($report -match 'FAIL ') { exit 1 }
  Write-Output "XTEST OK"
  exit 0
}

Remove-Item -Recurse -Force $dir -ErrorAction SilentlyContinue
