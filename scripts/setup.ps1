<#
.SYNOPSIS
  Prepares this machine for ChessCoach: creates the analysis virtual environment and checks the prerequisites.
.EXAMPLE
  powershell -ExecutionPolicy Bypass -File scripts\setup.ps1
#>
[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
$repo = Split-Path -Parent $PSScriptRoot
$settings = Get-Content (Join-Path $repo 'src\ChessCoach.Desktop\appsettings.json') -Raw | ConvertFrom-Json
$dataDir = $settings.ChessCoach.DataDirectory

function Show([string]$name, [bool]$ok, [string]$detail) {
    $mark = if ($ok) { '[ok]  ' } else { '[MISSING]' }
    Write-Host ("{0} {1}: {2}" -f $mark, $name, $detail)
}

# --- Python virtual environment for the analysis scripts
$venv = Join-Path $repo 'analysis\.venv'
if (-not (Test-Path (Join-Path $venv 'Scripts\python.exe'))) {
    Write-Host 'Creating analysis\.venv ...'
    python -m venv $venv
}
& (Join-Path $venv 'Scripts\python.exe') -m pip install --quiet --disable-pip-version-check -r (Join-Path $repo 'analysis\requirements.txt')
$chessVersion = & (Join-Path $venv 'Scripts\python.exe') -c "import chess; print(chess.__version__)"
Show 'Python venv' $true "analysis\.venv (python-chess $chessVersion)"

# --- Prerequisites
$sdk = (& dotnet --list-sdks) -join ', '
Show '.NET SDK' ($sdk -match '10\.0') $sdk
Show 'Data folder' (Test-Path $dataDir) $dataDir
Show 'Database' (Test-Path (Join-Path $dataDir 'xkirke_games.db')) (Join-Path $dataDir 'xkirke_games.db')
Show 'Stockfish' (Test-Path (Join-Path $dataDir 'engine\stockfish\stockfish-windows-x86-64-universal.exe')) 'engine\stockfish'
Show 'lc0' (Test-Path (Join-Path $dataDir 'engine\lc0\lc0.exe')) 'engine\lc0'
Show 'Maia 1100 weights' (Test-Path (Join-Path $dataDir 'engine\maia\maia-1100.pb.gz')) 'engine\maia'
$wv = Get-ItemProperty 'HKLM:\SOFTWARE\WOW6432Node\Microsoft\EdgeUpdate\Clients\{F3017226-FE2A-4295-8BDF-00C3A9A7E4C5}' -ErrorAction SilentlyContinue
Show 'WebView2 runtime' ($null -ne $wv) ($(if ($wv) { $wv.pv } else { 'install from https://developer.microsoft.com/microsoft-edge/webview2/' }))
