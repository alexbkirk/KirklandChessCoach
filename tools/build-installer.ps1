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

# 3. pip, setuptools (it builds chess from source, and builds wheels itself) and the analysis package. Every file is downloaded from PyPI, checked against its pinned
#    SHA-256, and installed offline (--no-index), so nothing unverified runs during the build.
$wheels = Join-Path $build 'packages'
New-Item -ItemType Directory -Path $wheels | Out-Null
function Get-Pinned([string]$url, [string]$sha256) {
    $file = Join-Path $wheels (Split-Path $url -Leaf)
    Invoke-WebRequest -Uri $url -OutFile $file -UseBasicParsing
    if ((Get-FileHash $file -Algorithm SHA256).Hash -ne $sha256.ToUpperInvariant()) { throw "$(Split-Path $url -Leaf) did not match its checksum." }
    return $file
}
$pipWheel = Get-Pinned 'https://files.pythonhosted.org/packages/f3/6e/1736e5b4ae2b778ef2f81c47d797de9f891d4d8acb047a24ca37a60294dd/pip-26.2.1-py3-none-any.whl' '71138adf1f4ca900cdb7d289c21b7494329f2332b6d85f0e1c42108c0384ed3e'
$setuptoolsWheel = Get-Pinned 'https://files.pythonhosted.org/packages/95/9c/c510029fc6ef33a6275cd2c5d3cecd6613dfd6aa401d57c54f1c18852ccf/setuptools-84.0.0-py3-none-any.whl' '51a52592b3b99e102b609654876bd65f19f999935166d1352678931132b0c670'
$chessSdist = Get-Pinned 'https://files.pythonhosted.org/packages/93/09/7d04d7581ae3bb8b598017941781bceb7959dd1b13e3ebf7b6a2cd843bc9/chess-1.11.2.tar.gz' 'a8b43e5678fdb3000695bdaa573117ad683761e5ca38e591c4826eba6d25bb39'

# The pinned chess version must be the one analysis\requirements.txt asks for.
$requirements = Join-Path $root 'analysis\requirements.txt'
if (-not (Select-String -Path $requirements -Pattern '^chess==1\.11\.2$' -Quiet)) { throw 'analysis\requirements.txt no longer asks for chess==1.11.2: update the pinned download here too.' }

# A wheel is a zip: unpacking pip's into site-packages installs it, then pip installs the rest offline.
$sitePackages = Join-Path $python 'Lib\site-packages'
$pipZip = [System.IO.Path]::ChangeExtension($pipWheel, '.zip')
Copy-Item $pipWheel $pipZip
Expand-Archive -Path $pipZip -DestinationPath $sitePackages
& $pyExe -m pip --version
if ($LASTEXITCODE -ne 0) { throw 'Installing pip failed.' }
& $pyExe -m pip install --no-index --no-warn-script-location --disable-pip-version-check $setuptoolsWheel
if ($LASTEXITCODE -ne 0) { throw 'Installing the build tools failed.' }
& $pyExe -m pip install --no-index --no-build-isolation --no-warn-script-location --disable-pip-version-check $chessSdist
if ($LASTEXITCODE -ne 0) { throw 'Installing the analysis package failed.' }

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
