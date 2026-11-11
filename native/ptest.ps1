param(
  [switch]$Headless,  # skip the microphone capture: CI runners have no input device
  [switch]$Check      # turn the measurements into pass/fail, non-zero on a regression
)
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName PresentationFramework

$asm = [Reflection.Assembly]::LoadFrom((Join-Path $PSScriptRoot 'VoiceBox.exe'))
$sr = 44100
$n = $sr * 3
$buf = New-Object 'single[]' $n
for ($i = 0; $i -lt $n; $i++) {
  $t = $i / $sr
  if ($t -lt 1.2) { $f = 220.0 }
  elseif ($t -lt 2.4) { $f = 329.63 }
  else { $f = 261.63 }
  $s = [math]::Sin(2 * [math]::PI * $f * $t) + 0.4 * [math]::Sin(4 * [math]::PI * $f * $t) + 0.2 * [math]::Sin(6 * [math]::PI * $f * $t)
  $buf[$i] = [single]($s * 0.22)
}

$pt = $asm.GetType('VoiceBox.PitchTracker')
$notes = $pt.GetMethod('ToNotes').Invoke($null, @([float[]]$buf, 44100, 96.0, 0.0, 'v1'))
Write-Output ("NOTE_COUNT " + $notes.Count)
foreach ($x in $notes) {
  Write-Output ("NOTE midi=" + $x.Midi + " start=" + $x.Start + " dur=" + $x.Dur)
}

$silent = New-Object 'single[]' ($sr * 2)
$none = $pt.GetMethod('ToNotes').Invoke($null, @([float[]]$silent, 44100, 96.0, 0.0, 'v1'))
Write-Output ("SILENCE_NOTES " + $none.Count)

$vb = New-Object 'single[]' ($sr * 2)
$phase = 0.0
for ($i = 0; $i -lt $vb.Length; $i++) {
  $t = $i / $sr
  $f = 293.66 * [math]::Pow(2, (60 * [math]::Sin(2 * [math]::PI * 5.5 * $t)) / 1200.0)
  $phase += 2 * [math]::PI * $f / $sr
  $s = [math]::Sin($phase) + 0.45 * [math]::Sin(2 * $phase) + 0.2 * [math]::Sin(3 * $phase)
  $vb[$i] = [single]($s * 0.2)
}
$vn = $pt.GetMethod('ToNotes').Invoke($null, @([float[]]$vb, 44100, 96.0, 0.0, 'v1'))
Write-Output ("VIBRATO_NOTES " + $vn.Count)
foreach ($x in $vn) { Write-Output ("VIB midi=" + $x.Midi + " start=" + $x.Start + " dur=" + $x.Dur) }

if ($Headless) {
  Write-Output "CAPTURE skipped (headless)"
} else {
  $rec = $asm.GetType('VoiceBox.Recorder')
  Write-Output ("INPUT_DEVICES " + $rec.GetProperty('InputDevices').GetValue($null, $null))

  $r = [Activator]::CreateInstance($rec)
  $ok = $r.Start()
  Start-Sleep -Milliseconds 1200
  $r.Stop()
  $snap = $r.Snapshot()
  $peak = 0.0
  $sum = 0.0
  $count = [math]::Min(6000, $snap.Length)
  for ($i = 0; $i -lt $count; $i++) {
    $a = [math]::Abs([double]$snap[$i])
    if ($a -gt $peak) { $peak = $a }
    $sum += $a * $a
  }
  $rms = 0.0
  if ($count -gt 0) { $rms = [math]::Round([math]::Sqrt($sum / $count), 4) }
  Write-Output ("CAPTURE started=" + $ok + " samples=" + $snap.Length + " secs=" + [math]::Round($r.Seconds, 2) + " rms=" + $rms + " peak=" + [math]::Round($peak, 3) + " error=" + $r.Error)
}

# -Check turns those measurements into pass/fail, so a change that breaks pitch tracking
# fails a build instead of quietly printing different numbers. A3 / E4 / C4 are midi 57 /
# 64 / 60, and the vibrato tone sits on D4 (62) with a 60-cent swing either side.
if ($Check) {
  $problems = New-Object System.Collections.Generic.List[string]

  $wantMidi = @(57, 64, 60)
  $gotMidi = @($notes | ForEach-Object { [int]$_.Midi })
  if ($gotMidi.Count -ne $wantMidi.Count) {
    $problems.Add("tone sequence: expected " + $wantMidi.Count + " notes, got " + $gotMidi.Count + " (" + ($gotMidi -join ',') + ")")
  } else {
    for ($k = 0; $k -lt $wantMidi.Count; $k++) {
      if ($gotMidi[$k] -ne $wantMidi[$k]) {
        $problems.Add("tone sequence note " + ($k + 1) + ": expected midi " + $wantMidi[$k] + ", got " + $gotMidi[$k])
      }
    }
  }

  if ($none.Count -ne 0) { $problems.Add("silence: expected no notes, got " + $none.Count) }

  if ($vn.Count -lt 1) {
    $problems.Add("vibrato tone: expected at least one note, got none")
  } else {
    foreach ($x in $vn) {
      $m = [int]$x.Midi
      if ($m -lt 61 -or $m -gt 63) { $problems.Add("vibrato note midi " + $m + " outside the expected 61-63") }
    }
  }

  if ($problems.Count -gt 0) {
    foreach ($p in $problems) { Write-Output ("FAIL " + $p) }
    exit 1
  }
  Write-Output "PTEST OK"
}
