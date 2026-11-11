# Voice Box

A native Windows singing-synthesis workstation: draw notes in a piano roll, type a lyric
line, and the app renders sung vocals in a blue frosted-glass interface modelled on
Synthesizer V Studio.

It is a desktop application — WPF, C#, no browser, no web runtime, no Node or Python.

## Features

- **Piano roll** with drag-to-move, drag-the-edge-to-resize, per-note **velocity** (a lane
  under the roll, notes shaded by dynamics, and a small 0–100 badge printed inside notes
  wide enough to hold one).
- **Lyrics** — type a line, and syllables are distributed across the notes on the selected
  track with vowel phonemes and consonant onsets.
- **Style match** — listens to a vocal sample (or a WAV) and copies its range, vibrato,
  brightness and air onto the synthetic voice. It never copies the melody.
- **3-band EQ** with a draggable response curve, plus live audition: edits are re-rendered
  in the background and handed over through an adjustable equal-power crossfade (0–300 ms).
- **Exports** — normalised WAV, raw-level WAV, and one stem per sung track plus the piano,
  with an `EXPORT TRIM` and a `session.txt` describing the mix.
- **Voice library** of 42 singers (DiffSinger, UTAU/OpenUtau, NNSVS, Synthesizer V Lite),
  every row linking to an official page, with in-app downloads.
- **Recording** from the default microphone, with `CONVERT TO MIDI` to turn a take into notes.
- **Mix presets**, per-track level/pan/mute/solo with live meters, and a shortcut legend (**F1**).

`native/README.md` documents the implementation of each of these, including what is
synthesised versus modelled.

## Requirements

- Windows 10 or 11.
- Windows PowerShell (the `powershell.exe` that ships with Windows).
- That is all: the build uses `Add-Type` with the C# compiler in the .NET Framework, so it
  needs no Visual Studio, no .NET SDK, no NuGet and no internet access.

## Build

From the repository root:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\native\build.ps1
```

The script compiles every `.cs` file in `native/` and writes `native\VoiceBox.exe`. It
removes any previous build first, so a failed build leaves no stale executable behind. The
project compiles with warnings treated as errors, so a warning is a build failure.

## Run

```powershell
.\native\VoiceBox.exe
```

`VoiceBox.exe` is intentionally git-ignored — build it locally as above.

On first launch the app shows a one-time hint pointing at the shortcut legend. Settings,
style presets, mix presets and the piano table cache are written under
`%LOCALAPPDATA%\VoiceBox\`.

## Repository layout

```
native/            C# sources, build script, detailed README
  build.ps1        compiles native\VoiceBox.exe
  README.md        full description of behaviour and design
  ptest.ps1        checks the pitch transcriber without a microphone
```

To check the MIDI transcription without singing into a microphone:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\native\ptest.ps1
```

## Model choice

The singing voice is a wavetable/formant synthesis engine written for this project — real
signal processing, not a trained neural network — so it runs on a plain Windows machine with
no GPU and no model download. Pitch tracking for `CONVERT TO MIDI` is monophonic.
