$ErrorActionPreference = 'Stop'
$here = Split-Path -Parent $MyInvocation.MyCommand.Path
$out  = Join-Path $here 'VoiceBox.exe'

Add-Type -AssemblyName PresentationFramework, PresentationCore, WindowsBase, System.Xaml
Add-Type -AssemblyName System.IO.Compression.FileSystem

$refs = @(
  [System.Windows.Window].Assembly.Location,          # PresentationFramework
  [System.Windows.Media.Brush].Assembly.Location,     # PresentationCore
  [System.Windows.DependencyObject].Assembly.Location,# WindowsBase
  [System.Xaml.XamlReader].Assembly.Location,         # System.Xaml
  [System.Uri].Assembly.Location,                     # System
  [System.Linq.Enumerable].Assembly.Location,         # System.Core
  [System.IO.Compression.ZipFile].Assembly.Location   # zip unpacking
)

$files = Get-ChildItem -Path (Join-Path $here '*.cs') | Select-Object -ExpandProperty FullName

if (Test-Path $out) { Remove-Item $out -Force }

Add-Type -Path $files -ReferencedAssemblies $refs -OutputAssembly $out -OutputType WindowsApplication

Write-Host "Built $out"
