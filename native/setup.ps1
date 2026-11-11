param(
  [string]$Version = "",   # blank reads the version stamped in the app
  [string]$OutDir = ""     # blank writes to <repo>\dist
)
$ErrorActionPreference = 'Stop'
$here = Split-Path -Parent $MyInvocation.MyCommand.Path
$root = Split-Path -Parent $here

# Loaded so the referenced-assembly list below can name these types, as build.ps1 does.
Add-Type -AssemblyName PresentationFramework, PresentationCore, WindowsBase, System.Xaml

if (-not $Version) {
  $src = Get-Content (Join-Path $here 'MainWindow.cs') -Raw
  $m = [regex]::Match($src, 'VOICEBOX\s+([0-9]+(?:\.[0-9]+)+)')
  if (-not $m.Success) { throw 'could not read the version stamped in MainWindow.cs' }
  $Version = $m.Groups[1].Value
}

$exe = Join-Path $here 'VoiceBox.exe'
if (-not (Test-Path $exe)) { throw ('build first: ' + $exe + ' is missing (close Voice Box if it is running, it locks the exe)') }

$dist = $OutDir
if (-not $dist) { $dist = Join-Path $root 'dist' }
New-Item -ItemType Directory -Force -Path $dist | Out-Null

$stub = Join-Path $dist 'setup-stub.exe'
if (Test-Path $stub) { Remove-Item $stub -Force }

$refs = @(
  [System.Windows.Window].Assembly.Location,           # PresentationFramework
  [System.Windows.Media.Brush].Assembly.Location,      # PresentationCore
  [System.Windows.DependencyObject].Assembly.Location, # WindowsBase
  [System.Uri].Assembly.Location,                      # System
  [System.Linq.Enumerable].Assembly.Location,          # System.Core
  [System.Xaml.XamlReader].Assembly.Location,          # System.Xaml, needed by the WPF window
  [Microsoft.Win32.Registry].Assembly.Location         # mscorlib in .NET Framework, but be explicit
)

Add-Type -Path (Join-Path $here 'installer\Installer.cs') -ReferencedAssemblies $refs -OutputAssembly $stub -OutputType WindowsApplication

$setup = Join-Path $dist ('VoiceBoxSetup-' + $Version + '.exe')
if (Test-Path $setup) { Remove-Item $setup -Force }

# Append the payload and the trailer the setup reads back at run time. Trailing bytes after a
# .NET executable are ignored by the loader, which is what keeps this a single file.
$stubBytes = [System.IO.File]::ReadAllBytes($stub)
$payload = [System.IO.File]::ReadAllBytes($exe)
$out = [System.IO.File]::Open($setup, 'Create', 'Write')
try {
  $out.Write($stubBytes, 0, $stubBytes.Length)
  $offset = [int64]$stubBytes.Length
  $out.Write($payload, 0, $payload.Length)

  $trailer = New-Object byte[] 40
  [System.Text.Encoding]::ASCII.GetBytes('VBSETUP1').CopyTo($trailer, 0)
  [BitConverter]::GetBytes($offset).CopyTo($trailer, 8)
  [BitConverter]::GetBytes([int64]$payload.Length).CopyTo($trailer, 16)
  $parts = $Version.Split('.')
  [BitConverter]::GetBytes([int32]$parts[0]).CopyTo($trailer, 24)
  [BitConverter]::GetBytes([int32]$parts[1]).CopyTo($trailer, 28)
  [BitConverter]::GetBytes([int32]$parts[2]).CopyTo($trailer, 32)
  $out.Write($trailer, 0, 40)
}
finally { $out.Close() }
Remove-Item $stub -Force

$hash = (Get-FileHash -Algorithm SHA256 -Path $setup).Hash
$info = Get-Item $setup
Write-Output ('built ' + $info.FullName)
Write-Output ('size ' + $info.Length + ' bytes (stub ' + $stubBytes.Length + ' + payload ' + $payload.Length + ' + trailer 40)')
Write-Output ('sha256 ' + $hash)
