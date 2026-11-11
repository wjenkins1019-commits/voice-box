param(
  [string]$OutDir = ""   # blank writes to <repo>\dist
)
$ErrorActionPreference = 'Stop'
$here = Split-Path -Parent $MyInvocation.MyCommand.Path
$root = Split-Path -Parent $here

$dist = $OutDir
if (-not $dist) { $dist = Join-Path $root 'dist' }

$files = Get-ChildItem -Path $dist -File | Where-Object { $_.Name -ne 'SHA256SUMS' } | Sort-Object Name
if ($files.Count -eq 0) { throw ('nothing to checksum in ' + $dist) }

# The same "<hash>  <name>" layout sha256sum writes, so `sha256sum -c SHA256SUMS` works
# untouched in Git Bash, Linux or macOS, and the names match the release assets exactly.
$lines = @()
foreach ($f in $files) {
  $hash = (Get-FileHash -Algorithm SHA256 -Path $f.FullName).Hash.ToLower()
  $lines += ($hash + '  ' + $f.Name)
}

$out = Join-Path $dist 'SHA256SUMS'
# LF endings on purpose: Set-Content writes CRLF on Windows, and sha256sum then reads the
# carriage return as part of the file name, so every entry fails with "FAILED open or read".
$text = (($lines -join "`n") + "`n")
[System.IO.File]::WriteAllText($out, $text, (New-Object System.Text.ASCIIEncoding))

$lines | ForEach-Object { Write-Output $_ }
Write-Output ('wrote ' + $out + ' for ' + $files.Count + ' file(s)')
