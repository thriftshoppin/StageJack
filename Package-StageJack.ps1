param(
    [string]$OutputDirectory = (Join-Path $PSScriptRoot 'release'),
    [string]$CertificateThumbprint = '',
    [string]$TimestampUrl = 'http://timestamp.digicert.com'
)

$ErrorActionPreference = 'Stop'
$root = $PSScriptRoot
$version = '0.6.0'
$build = Join-Path $root 'Build-StageJack.cmd'
$setupScript = Join-Path $root 'installer\StageJack.iss'
$exe = Join-Path $root 'dist\StageJack.exe'
$setupName = "StageJack-Setup-$version.exe"
$setupExe = Join-Path $OutputDirectory $setupName

New-Item -ItemType Directory -Force -Path $OutputDirectory | Out-Null

Write-Host 'Building StageJack...'
& $build
if ($LASTEXITCODE -ne 0) {
    throw "StageJack build failed with exit code $LASTEXITCODE."
}

if ($CertificateThumbprint) {
    $sdkBin = 'C:\Program Files (x86)\Windows Kits\10\bin'
    $signTool = Join-Path $sdkBin 'x64\signtool.exe'
    if (-not (Test-Path -LiteralPath $signTool)) {
        $signTool = Get-ChildItem $sdkBin -Directory -ErrorAction SilentlyContinue |
            Where-Object { $_.Name -match '^\d+(\.\d+)+$' } |
            Sort-Object { [version]$_.Name } -Descending |
            ForEach-Object { Join-Path $_.FullName 'x64\signtool.exe' } |
            Where-Object { Test-Path -LiteralPath $_ } |
            Select-Object -First 1
    }
    if (-not $signTool) {
        throw 'SignTool was not found. Install the Windows SDK or build without -CertificateThumbprint.'
    }

    Write-Host 'Signing StageJack.exe...'
    & $signTool sign /sha1 $CertificateThumbprint /fd SHA256 /tr $TimestampUrl /td SHA256 $exe
    if ($LASTEXITCODE -ne 0) {
        throw "Signing StageJack.exe failed with exit code $LASTEXITCODE."
    }
}

$iscc = Join-Path $env:LOCALAPPDATA 'Programs\Inno Setup 7\ISCC.exe'
if (-not (Test-Path -LiteralPath $iscc)) {
    $found = Get-Command ISCC.exe -ErrorAction SilentlyContinue
    if ($found) { $iscc = $found.Source }
}
if (-not (Test-Path -LiteralPath $iscc)) {
    throw 'Inno Setup 7 compiler was not found. Install it from https://jrsoftware.org/isdl.php.'
}

Write-Host "Compiling $setupName..."
& $iscc "--output-dir=$OutputDirectory" "--output-filename=$([IO.Path]::GetFileNameWithoutExtension($setupName))" $setupScript
if ($LASTEXITCODE -ne 0) {
    throw "Inno Setup compilation failed with exit code $LASTEXITCODE."
}

if ($CertificateThumbprint) {
    Write-Host 'Signing installer...'
    & $signTool sign /sha1 $CertificateThumbprint /fd SHA256 /tr $TimestampUrl /td SHA256 $setupExe
    if ($LASTEXITCODE -ne 0) {
        throw "Signing the installer failed with exit code $LASTEXITCODE."
    }
}

Write-Host "Installer ready: $setupExe"
