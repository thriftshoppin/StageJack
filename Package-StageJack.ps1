param(
    [string]$OutputDirectory = (Join-Path $PSScriptRoot 'release'),
    [string]$CertificateThumbprint = '',
    [string]$ArtifactSigningMetadataPath = '',
    [string]$ArtifactSigningDlibPath = '',
    [string]$TimestampUrl = ''
)

$ErrorActionPreference = 'Stop'
$root = $PSScriptRoot
$version = '0.6.0'
$build = Join-Path $root 'Build-StageJack.cmd'
$setupScript = Join-Path $root 'installer\StageJack.iss'
$exe = Join-Path $root 'dist\StageJack.exe'
$setupName = "StageJack-Setup-$version.exe"
$setupExe = Join-Path $OutputDirectory $setupName

if ($CertificateThumbprint -and $ArtifactSigningMetadataPath) {
    throw 'Choose either -CertificateThumbprint or -ArtifactSigningMetadataPath, not both.'
}

$useArtifactSigning = [bool]$ArtifactSigningMetadataPath
$isSigning = [bool]($CertificateThumbprint -or $ArtifactSigningMetadataPath)
if (-not $TimestampUrl) {
    if ($useArtifactSigning) {
        $TimestampUrl = 'http://timestamp.acs.microsoft.com'
    }
    else {
        $TimestampUrl = 'http://timestamp.digicert.com'
    }
}

if ($useArtifactSigning) {
    if (-not (Test-Path -LiteralPath $ArtifactSigningMetadataPath -PathType Leaf)) {
        throw "Artifact Signing metadata file was not found: $ArtifactSigningMetadataPath"
    }

    $metadata = Get-Content -LiteralPath $ArtifactSigningMetadataPath -Raw | ConvertFrom-Json
    foreach ($field in @('Endpoint', 'CodeSigningAccountName', 'CertificateProfileName')) {
        $value = [string]$metadata.$field
        if (-not $value -or $value -match '^<.*>$') {
            throw "Artifact Signing metadata must contain a real '$field' value. Copy signing\artifact-signing.metadata.example.json and fill in your account/profile values."
        }
    }

    if (-not $ArtifactSigningDlibPath) {
        $ArtifactSigningDlibPath = Join-Path $env:LOCALAPPDATA 'Microsoft\MicrosoftArtifactSigningClientTools\Azure.CodeSigning.Dlib.dll'
    }
    if (-not (Test-Path -LiteralPath $ArtifactSigningDlibPath -PathType Leaf)) {
        throw "Artifact Signing dlib was not found: $ArtifactSigningDlibPath. Install Microsoft's Artifact Signing Client Tools or provide -ArtifactSigningDlibPath."
    }
}

$signTool = ''
if ($isSigning) {
    $sdkBin = 'C:\Program Files (x86)\Windows Kits\10\bin'
    $signTool = Get-ChildItem $sdkBin -Directory -ErrorAction SilentlyContinue |
        Where-Object { $_.Name -match '^\d+(\.\d+)+$' } |
        Sort-Object { [version]$_.Name } -Descending |
        ForEach-Object { Join-Path $_.FullName 'x64\signtool.exe' } |
        Where-Object { Test-Path -LiteralPath $_ } |
        Select-Object -First 1
    if (-not $signTool) {
        $signTool = Join-Path $sdkBin 'x64\signtool.exe'
    }
    if (-not $signTool) {
        throw 'SignTool was not found. Install a current Windows SDK or build without signing parameters.'
    }
}

function Sign-ReleaseFile {
    param([string]$Path)

    if ($useArtifactSigning) {
        Write-Host "Signing $([IO.Path]::GetFileName($Path)) with Microsoft Artifact Signing..."
        & $signTool sign /v /fd SHA256 /tr $TimestampUrl /td SHA256 /dlib $ArtifactSigningDlibPath /dmdf $ArtifactSigningMetadataPath $Path
    }
    else {
        Write-Host "Signing $([IO.Path]::GetFileName($Path)) with the current-user certificate..."
        & $signTool sign /sha1 $CertificateThumbprint /fd SHA256 /tr $TimestampUrl /td SHA256 $Path
    }

    if ($LASTEXITCODE -ne 0) {
        throw "Signing $Path failed with exit code $LASTEXITCODE."
    }
}

New-Item -ItemType Directory -Force -Path $OutputDirectory | Out-Null

Write-Host 'Building StageJack...'
& $build
if ($LASTEXITCODE -ne 0) {
    throw "StageJack build failed with exit code $LASTEXITCODE."
}

if ($isSigning) {
    Sign-ReleaseFile $exe
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

if ($isSigning) {
    Sign-ReleaseFile $setupExe
}

Write-Host "Installer ready: $setupExe"
