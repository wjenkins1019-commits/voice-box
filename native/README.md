# Voice Box — native desktop build

A native Windows (WPF) application. No browser, no web runtime, no Node/Python.

`VoiceBox.exe` is a .NET Framework Windows executable compiled from the C# sources in
this folder with the C# compiler that ships with Windows.

## Run

Double-click `VoiceBox.exe`, or:

```powershell
.\VoiceBox.exe
```

## Rebuild

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\build.ps1
```

The build uses `Add-Type -OutputAssembly ... -OutputType WindowsApplication`, so it needs
no SDK, NuGet or internet access.

## Controls

| Action | How |
| --- | --- |
| Play / pause | Space, or the transport button |
| Return to start | Home, or the rewind button |
| Move a note | drag it in the piano roll |
| Change note length | drag the right edge of a note |
| Change pitch | drag a note up or down |
| Seek | click the arrangement ruler or an empty spot in the roll |
| Delete note | select it, press Delete |
| Record vocals | R, or the red button in the transport |
| Convert take to MIDI | **CONVERT TO MIDI** in the Record panel |
| Scrub / pan timeline | drag the slim scrollbars, or zoom with `−` / `+` |

Panels: voice model and language, expression pad (vibrato) with tension/breath/gender
faders, non-destructive AI retakes, and a free voice-model library whose buttons open the
official voicebank pages.

## Recording and MIDI

Capture uses winmm `waveIn` with eight 100 ms buffers that the UI timer drains and
re-queues (no callback thread), writing 16-bit mono 44.1 kHz.

`ptest.ps1` checks the transcriber without a microphone — it feeds synthetic tones
(A3/E4/C4 and a D4 with 5.5 Hz vibrato) through `PitchTracker.ToNotes` and reports the
notes found, plus the input device count and a short live capture:

```
powershell -NoProfile -ExecutionPolicy Bypass -File native/ptest.ps1
```

Two switches make it usable without a microphone: **`-Headless`** skips the live capture
(runners have no input device), and **`-Check`** asserts the expected notes — A3/E4/C4 in
order, nothing from silence, a D4 for the vibrato tone — printing `PTEST OK` or the failed
expectations, and exiting non-zero so a pitch-tracking regression fails a build. CI runs
`ptest.ps1 -Headless -Check` after the build.

The **Record · MIDI** panel captures the default microphone at 44.1 kHz mono through
winmm, starting at the current playhead. While recording, the input level meter moves,
the elapsed time counts, and a red take clip with a live waveform is drawn on the
selected track in the arrangement.

**CONVERT TO MIDI** runs an autocorrelation pitch tracker over the take (45 ms frames,
12 ms hop, 55–1000 Hz), smooths the pitch track, segments it into stable notes, and
inserts them on the selected track starting at the recording position. Notes that already
occupied that time range on that track are replaced; the take itself is kept at
`%TEMP%\voicebox_input.wav`.

Pitch tracking is monophonic, so it follows one sung line — chords, heavy vibrato and
spoken consonants can shift note boundaries.

## Lyrics

The **Lyrics** field takes a typed line and spreads it across the selected track: one
syllable per note in time order, splitting words on vowel groups (`hel|lo`, `beau|ti|ful`).
Each syllable gets a vowel phoneme and an approximate consonant onset, and leftover notes
sustain the previous vowel instead of keeping stale words. Press **SING LYRICS**.

## Style match

**Style Match** listens to a vocal sample and copies its *style*, never its melody:

* **LEARN FROM TAKE** uses the last microphone recording.
* **LOAD WAV…** analyses any 16/24/32-bit PCM or float WAV.

The analyser measures **range** (median low/high note), **vibrato rate and depth**,
**brightness** (zero-crossing density) and **air** (how periodic the pitch track is), then
applies them to synthesis: formant scaling from the register, vibrato rate and depth
scaling, harmonic tilt for brightness and extra breath noise. A learned style shows as
`ACTIVE` with its measured numbers; **STYLE OFF** returns to the default voice.

Learned styles are reusable: type a name, press **SAVE STYLE**, and the profile appears
in the preset list with its range and vibrato rate. **USE** re-applies it (auditioned live
while the take plays), **✕** deletes it. Presets are plain `key=value` text under
`%LOCALAPPDATA%\VoiceBox\Styles\<name>.style`, so they survive restarts.

Each preset row also has **EXP** and **✎**. **EXP** writes the preset to a `.style` file
anywhere you choose, and **IMPORT STYLE FILE…** copies one back into the library, keeping
the file's name and adding ` 2`, ` 3`… if that name is taken. **✎** turns the name box into
a rename: type the new name, press **RENAME ▸**, and the profile moves to the new filename
with its values untouched. Renaming refuses a name that already belongs to another preset.

## Equalizer

The **Equalizer** panel is a real 3-band EQ in the render path — low shelf at 180 Hz,
a peaking bell at 1.2 kHz and a high shelf at 6 kHz, each ±12 dB, with an EQ ON/BYPASS
chip.

The panel draws its **response curve** from the same biquad coefficients that filter the
audio, on a log frequency axis (40 Hz - 16 kHz, ±15 dB shown). The three band handles are
draggable: grab LOW, MID or HIGH and pull vertically in half-decibel steps, and the
curve, the fader and the dB readout follow each other. Dragging a handle goes through the
same live-audition path as a fader move.

EQ and style edits are **heard live while the take plays**: a change is debounced for
about a fifth of a second, the take is re-rendered into a second buffer in the
background, and the player jumps onto it at the current beat. Rapid drags coalesce into
one render instead of queueing a render per pixel, and the old take keeps playing until
the new one is ready.

The handover is an **equal-power crossfade**: the fresh take opens on a second player, is
seeked to the beat the old one has reached, and the two fade across each other with
`sin`/`cos` volume ramps, so the summed level stays flat and the seam has no click. The
old player is only closed once the new one is fully up; pausing, stopping or seeking
cancels a fade in progress, and a render that lands mid-fade is queued for the fade end.

The fade length is adjustable in the Equalizer panel with **LIVE FADE ↗MS**, from `0`
(hand over the instant the new take is ready, a hard cut) up to `300` ms, where a style
change glides instead of snapping. The ramp is driven by a stopwatch rather than a tick
count, so the setting is honoured to within one screen tick (about 16 ms) instead of
doubling. A chip under the slider switches the ramp shape between **FADE EQUAL-POWER**
(sin/cos, summed level flat through the join) and **FADE LINEAR** (a 3 dB dip at the
midpoint, which reads as a quicker handover). The length can also be nudged from the
transport with `[` and `]` — 8 ms a press, or 48 ms with Shift — so the Equalizer panel does
not have to be open. It is a session setting: the app starts at 48 ms on the equal-power
curve.

The status bar shows this work as it happens: while a take is re-rendering, the engine
label reads `RE-RENDERING · LIVE` beside a small sweeping highlight, changes to `FADING IN`
while the crossfade runs, and returns to `SYNTHESIZING` / `ENGINE READY` when the handover
finishes. First renders and exports show the same shimmer as `RENDERING TAKE`, and the
indicator hides itself and hands the label back when nothing is running. Beside LATENCY,
the `FADE` readout records the last real handover as `measured / setting` — for example
`61 / 48 ms` — or `instant` when the fade is switched off, so the configured glide can be
checked against what actually happened.

## Voice library

The library ships with **42 singers**: the two bundled voices plus **40 free downloads**.
Every row links to an official artist page, vendor download folder or community
directory — nothing points at a re-upload:

* **DiffSinger (9)** — Amano Naoki, No.7 (SEVEN), Namine Ritsu, Chiaki (28 languages),
  Murin, the 39-voice UFR Pâtisserie pack, OpenCPop, PJS, plus the wiki index.
* **UTAU / OpenUtau (19)** — KASAI OG01 and IGNITE α, Kasane Teto, Defoko, Ami Koharune,
  Anna Nyui, Momone Momo, Yamine Renri, Haruka Nana, and the ARPAsing English banks
  (26 Iron, Aki Toka, Hailey Denali, Resonance Harmony, Aino Erufu, Aero-gen Shinsu,
  Kayaka Konagi, prototype, Ame Silver) plus the ARPAsing directory.
* **NNSVS / ENUNU (3)** — the NNSVS model set, the ENUNU model database, Ritsu's styles.
* **Synthesizer V Lite (9)** — Eleanor Forte, AiKO, Chiyu, GENBU, Kotonoha Akane & Aoi,
  Hanakuma Chifuyu, ANRI, ASTERIAN and the full Lite download folder.

Engine chips (`ALL / DIFFSINGER / UTAU / NNSVS / SYNTHV LITE / VOICE BOX`) and the search
box filter the list by engine, name, language or size. Size is only shown where it is
known; directory rows say `directory`.

### Downloads happen inside the app

Each row has a **DOWNLOAD** button that fetches the model into
`%LOCALAPPDATA%\VoiceBox\Voices\<model>\` with a live percentage, then unpacks zip
archives in place. **OPEN VOICES FOLDER** shows the results.

Before fetching, the downloader turns the stored link into a real file:

* direct archive links are used as-is (Kasane Teto);
* Google Drive file pages become `drive.usercontent.google.com` downloads (UFR pack);
* MediaFire pages are scraped for the download host (`ARPAsing banks`);
* GitHub release pages are resolved through the releases API (PJS);
* artist pages are scanned for an archive link, skipping MMD models, artwork and logos.

Hosts that need their own client (MEGA folders) cannot be fetched this way, and JS-only
listings such as Dreamtonics' file browser expose no direct link; those rows report the
reason instead of silently failing. Directory rows say so too.

## What is real and what is simulated

- The audio is synthesised locally: a wavetable/formant engine renders the note data to a
  48 kHz WAV in `%TEMP%\voicebox_take.wav`, which the player streams while the playhead
  follows the transport. It is a signal-processing model, not a trained neural network.
- **EXPORT WAV** in the Arrangement header writes that same render to a file you choose, with
  the current EQ curve and style baked in, so a take outlives the temp buffer. Rendering
  happens on a worker thread. This export is peak-normalised — the synth scales every take
  to about −1.3 dBFS — so a bigger EQ boost changes the shape, not the exported level.
- **EXPORT RAW** writes the same audio at the synth's own level instead, with no gain applied.
  Use it when several takes have to keep their relative balance in a mix; the files are
  quieter than the normalised ones but line up with each other. Anything that would exceed
  full scale is clamped at ±1. Both exports are 48 kHz 16-bit stereo, so track pan survives the trip, and the status bar
  reports that rate because it reads it from the engine instead of a hard-coded label.
- **EXPORT TRIM DB** in the AI Retakes panel sets a gain from −24 to +12 dB that is applied
  to every export — both take exports and every stem — so a take can be balanced against
  the others before it leaves the app. It never touches playback, and a trim that pushes
  the signal past full scale is clamped at ±1.
- Export dialogs remember where you last wrote and what that mode's file was called, kept in
  `%LOCALAPPDATA%\VoiceBox\settings.txt` and restored on the next launch, so a repeat export
  is open → Enter.
- **TRACK LEVEL** and **TRACK PAN** in the Expression panel act on the selected track and
  follow the selection when you pick another one. Level runs −24 to +12 dB; pan is a
  constant-power law (−1 hard left, 0 centre, +1 hard right), so centre sits 3 dB down in
  each channel. Both are applied where notes are mixed into the stereo field, which is why
  a stem carries exactly the setting the full mix uses.
- The **M** and **S** chips in the track headers change the audio: mute drops that track from
  the render, solo silences every track that is not soloed, and a muted or solo-silenced
  track gets no stem at all — the toast names what was skipped. Toggling either chip
  re-renders the take so the change is heard straight away.
- Each track header has a small level meter and a dB readout taken from the last render — the
  track's own contribution after its level and pan, before the master EQ. A muted or
  solo-silenced track reads `silent` with a dimmed bar. The meters refresh after every render
  and after any mute/solo change, so balances can be judged without exporting.
- Track levels, pans, mute and solo are written to `settings.txt` as `mix.<id>.gain`,
  `mix.<id>.pan`, `mix.<id>.mute` and `mix.<id>.solo` whenever they change, and restored at
  launch, so the exact mix — including silenced tracks — survives a restart.
- **EXPORT STEMS** writes one raw-level WAV per sung track — `<name> - Lead Vocal.wav`,
  `<name> - Harmony.wav` — into the folder you pick. Stems carry the same EQ and style, no
  metronome click, and they add back up to the full mix sample for sample (checked to
  within 1 LSB), so they can be balanced and processed elsewhere. The Grand Piano track gets a stem
  too: it is a synthesised struck string (stretched partials, hammer attack, damper on key
  up) rather than a recorded sample, and it plays through the same pan and level path.
  Every note carries a velocity (0–1): the seeded part swells and eases across its 32 bars
  (the whole arrangement), the chord root is struck hardest, and each note's velocity drives
  both its level and how bright the strike is, so the accompaniment has dynamics rather than
  one flat volume. A velocity lane sits under the roll — one bar per note, drawn at the note's
  start and height — and dragging a bar up or down redraws that note's dynamics, which marks
  the take stale and re-renders it, so the change is audible rather than cosmetic.
  Notes in the roll are shaded by velocity — bright for loud, darker for soft, with the lyric
  label dimming to match — so the dynamics read from the roll as well as the lane. A note wide
  enough to fit one also prints its velocity as a bare 0–100 number in its trailing corner, so
  an exact value is readable without the lane: the number sits in a small dark badge so it reads
  the same on a bright loud note and on one shaded down to the grid, the lyric label shortens
  to make room for it, and notes too narrow to hold a badge stay bare rather than showing a
  clipped digit.
  Velocity shapes the sung notes as well: a quieter note arrives softer, with a gentler onset,
  a little more breath and a darker tone, so a phrase can swell and recede. Every velocity
  factor is exactly 1 at full velocity, so takes drawn at full stay bit-identical.
  The roll's pitch range drops to G1 so the bass notes are reachable, and the
  piano roll scrolls vertically — the key strip slides with it so labels stay on their rows.
- Renders and exports now cover the full arrangement (128 beats, about 81 s) instead of the
  first 64 beats, so the later sung lines and the extended piano part are all included.
- The piano tone tables are built on a low-priority background thread at startup, so the first
  render after launch costs the same as later ones instead of paying to warm the cache. They
  are also cached on disk in `%LOCALAPPDATA%\VoiceBox\Piano Cache` (one `.pbt` per pitch and
  velocity bucket, about 470 KB each), so after the first run the warm-up is a set of file
  reads. Each file has a versioned name plus a checksum and length in its header, so a changed
  synth, a different sample rate or a truncated file is rebuilt rather than trusted.
  The status bar carries a small chip that reads **WARMING TABLES** while that warm-up runs and
  **TABLES READY** once it finishes, so a first play that would otherwise stall is visible up
  front rather than a surprise.
- Each stem export also writes `<name> - session.txt` beside the WAVs: EQ bands, the style
  profile, fade length and curve, expression settings, export trim and every track's level,
  pan and mute/solo state, plus the stem filenames — enough to rebuild the mix later.
- The first launch after install shows a one-time hint beside the **?** chip pointing at the
  shortcut legend; it fades after a few seconds and never returns (`hint.help=1` in
  `settings.txt`), and opening the legend from it also dismisses it. The legend itself caps its
  height to the window and scrolls its list, shrinking and wrapping its rows on narrow screens,
  so it stays fully readable at the minimum window size and on a 150%-scaled laptop. Below the
  key rows sits a **MOUSE & GESTURES** section: dragging a note to move it (or its right edge to
  resize), dragging a velocity bar, any fader (track level, pan, fade length, export trim), the
  floating fade card by its header, and an EQ node.
- **F1** (or **?**) opens a shortcut legend over the workspace listing the transport, fade and
  editing keys; **Esc**, the CLOSE button or a click on the backdrop dismisses it. A small **?**
  chip beside LOOP and CLICK opens the same list, so it can be found without knowing F1.
  The legend also covers **E**, which bypasses and re-enables the equalizer, and **S**, which
  steps through the saved style presets — the same actions as the EQ ON chip and each preset's
  USE button. Number keys **1** to **9** load a preset directly, in the order the list shows
  them, so a particular style is one key away; a number past the end says there is no such
  preset instead of doing nothing. Both number-row and keypad digits work, and are ignored
  while a text box has focus. Shortcuts also announce themselves where they act: the EQ chip
  carries a small **E** keycap and the Style Match header an **S** keycap, each with a tooltip
  naming what the key does (`T.KeyCap` styles the cap, chips wrap their label so the cap sits
  inside them, and the on/off tinting still reaches the label).
- **Mix Presets** in the sidebar saves the current levels, pans, mute and solo as a named
  `.mix` file under `%LOCALAPPDATA%\VoiceBox\Mix Presets`, with USE to swap balances and ✕
  to delete. Applying one repaints the track headers and expression faders, re-renders the
  take, and is written to `settings.txt` so the app reopens on the mix you were using.
  **RESET** beside the name box returns every track to unity gain and centre, clearing mute
  and solo, and saves that neutral mix.
- The fade length and curve are remembered in `settings.txt` as `fade.ms` and `fade.curve`, so
  the app reopens with the glide you last settled on; the slider writes once on release and
  the bracket keys and curve chip write immediately.
- The fade strip in the Equalizer panel has a `FLOAT` chip that pops it into a small card over
  the workspace, draggable by its `LIVE FADE` header and able to stay visible while other
  panels are open; `DOCK` puts it back. The `–` chip collapses the controls to just the
  header, which is handy while the card is floating.
- Voice-model "install" is local to the session; the download links go to the real,
  free/official voicebank pages.
- This is a standalone application. It does not include actual VST3 / AU / AAX / ARA
  plugin binaries.
