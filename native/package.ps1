param(
  [string]$Version = "",   # blank reads the version stamped in the app
  [string]$OutDir = ""     # blank writes to <repo>\dist
)
$ErrorActionPreference = 'Stop'
$here = Split-Path -Parent $MyInvocation.MyCommand.Path
$root = Split-Path -Parent $here

# The status bar is stamped with the app's version, so the package cannot claim another one.
if (-not $Version) {
  $src = Get-Content (Join-Path $here 'MainWindow.cs') -Raw
  $m = [regex]::Match($src, 'VOICEBOX\s+([0-9]+(?:\.[0-9]+)+)')
  if (-not $m.Success) { throw 'could not read the version stamped in MainWindow.cs' }
  $Version = $m.Groups[1].Value
}

$exe = Join-Path $here 'VoiceBox.exe'
if (-not (Test-Path $exe)) { throw ('build first: ' + $exe + ' is missing') }

$dist = $OutDir
if (-not $dist) { $dist = Join-Path $root 'dist' }
New-Item -ItemType Directory -Force -Path $dist | Out-Null

$stage = Join-Path $env:TEMP ('vb_pkg_' + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Force -Path $stage | Out-Null
Copy-Item $exe (Join-Path $stage 'VoiceBox.exe')
Copy-Item (Join-Path $root 'LICENSE') (Join-Path $stage 'LICENSE')

$notes = @"
Voice Box $Version - portable Windows build

This is a single executable. There is nothing to install: double-click
VoiceBox.exe, or run it from a terminal:

    .\VoiceBox.exe

Requirements: Windows 10 or 11. The .NET Framework that ships with Windows is
all it needs - no SDK, no runtime download, no administrator rights.

Where your data goes: settings, style presets, mix presets, the piano table
cache and any downloaded voice models live under

    %LOCALAPPDATA%\VoiceBox

Deleting VoiceBox.exe leaves all of that in place. Delete that folder to remove
it.

To rebuild from source, see README.md in the repository: it is one PowerShell
script and needs no extra tooling.

Licensed under the MIT license - see LICENSE.
"@
Set-Content -Path (Join-Path $stage 'PORTABLE.txt') -Value $notes -Encoding ASCII

$zip = Join-Path $dist ('VoiceBox-' + $Version + '-windows.zip')
if (Test-Path $zip) { Remove-Item $zip -Force }

Add-Type -AssemblyName System.IO.Compression.FileSystem
[System.IO.Compression.ZipFile]::CreateFromDirectory($stage, $zip, [System.IO.Compression.CompressionLevel]::Optimal, $false)
Remove-Item -Recurse -Force $stage -ErrorAction SilentlyContinue

$hash = (Get-FileHash -Algorithm SHA256 -Path $zip).Hash
$info = Get-Item $zip
Write-Output ('packaged ' + $info.FullName)
Write-Output ('size ' + $info.Length + ' bytes')
Write-Output ('sha256 ' + $hash)
