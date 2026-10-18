# Builds dist\ChessCoach-Setup.exe: the app, an embedded Python with the analysis packages, and the analysis scripts.
# Run from anywhere:  powershell -ExecutionPolicy Bypass -File tools\build-installer.ps1
# Needs the .NET 10 SDK and Inno Setup 6 (winget install JRSoftware.InnoSetup).
$ErrorActionPreference = 'Stop'

$root = Split-Path -Parent $PSScriptRoot
$build = Join-Path $root 'build'
$app = Join-Path $build 'app'
$python = Join-Path $app 'python'
$analysisOut = Join-Path $app 'analysis'
$dist = Join-Path $root 'dist'

# Pinned: the embeddable Python from python.org and its SHA-256.
$pyUrl = 'https://www.python.org/ftp/python/3.12.10/python-3.12.10-embed-amd64.zip'
$pySha256 = '4ACBED6DD1C744B0376E3B1CF57CE906F9DC9E95E68824584C8099A63025A3C3'

if (Test-Path $build) { Remove-Item $build -Recurse -Force }
New-Item -ItemType Directory -Path $app | Out-Null

# 1. The app itself (self-contained, so the player needs no .NET install).
Write-Host 'Publishing the app...'
dotnet publish (Join-Path $root 'src\ChessCoach.Desktop') -c Release -r win-x64 --self-contained true -p:PublishSingleFile=false -o $app
if ($LASTEXITCODE -ne 0) { throw 'dotnet publish failed.' }
# A developer's appsettings.local.json holds their own paths: never ship it.
Remove-Item (Join-Path $app 'appsettings.local.json') -ErrorAction SilentlyContinue

# 2. Embedded Python, checked against its pinned hash.
Write-Host 'Adding Python...'
$pyZip = Join-Path $build 'python-embed.zip'
Invoke-WebRequest -Uri $pyUrl -OutFile $pyZip -UseBasicParsing
if ((Get-FileHash $pyZip -Algorithm SHA256).Hash -ne $pySha256) { throw 'The Python download did not match its checksum.' }
Expand-Archive -Path $pyZip -DestinationPath $python

# The embeddable build ignores site-packages until its ._pth file says otherwise.
$pth = Get-ChildItem $python -Filter 'python*._pth' | Select-Object -First 1
$lines = Get-Content $pth.FullName | ForEach-Object { if ($_ -eq '#import site') { 'import site' } else { $_ } }
$lines = @($lines) + 'Lib\site-packages'
Set-Content -Path $pth.FullName -Value $lines -Encoding ascii

$pyExe = Join-Path $python 'python.exe'
$getPip = Join-Path $build 'get-pip.py'
Invoke-WebRequest -Uri 'https://bootstrap.pypa.io/get-pip.py' -OutFile $getPip -UseBasicParsing
& $pyExe $getPip --no-warn-script-location
if ($LASTEXITCODE -ne 0) { throw 'Installing pip failed.' }

# 3. The analysis packages, pinned in analysis\requirements.txt. The embedded Python has no build tools,
#    and some packages install from source, so the build tools go in first.
& $pyExe -m pip install --no-warn-script-location --disable-pip-version-check setuptools wheel
if ($LASTEXITCODE -ne 0) { throw 'Installing the build tools failed.' }
$requirements = Join-Path $root 'analysis\requirements.txt'
& $pyExe -m pip install --no-warn-script-location --disable-pip-version-check -r $requirements
if ($LASTEXITCODE -ne 0) { throw 'Installing the analysis packages failed.' }

# 4. The analysis scripts (top level only: no tests, no .venv).
New-Item -ItemType Directory -Path $analysisOut | Out-Null
Copy-Item (Join-Path $root 'analysis\*.py') $analysisOut
Copy-Item $requirements $analysisOut

# 5. The installer.
$iscc = Join-Path $env:LOCALAPPDATA 'Programs\Inno Setup 6\ISCC.exe'
if (-not (Test-Path $iscc)) { $iscc = 'C:\Program Files (x86)\Inno Setup 6\ISCC.exe' }
if (-not (Test-Path $iscc)) { throw 'Inno Setup 6 was not found. Install it with: winget install JRSoftware.InnoSetup' }
New-Item -ItemType Directory -Path $dist -Force | Out-Null
& $iscc (Join-Path $root 'installer\ChessCoach.iss')
if ($LASTEXITCODE -ne 0) { throw 'Building the installer failed.' }

Write-Host ''
Write-Host "Done: $(Join-Path $dist 'ChessCoach-Setup.exe')"
