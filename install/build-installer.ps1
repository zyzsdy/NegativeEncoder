[CmdletBinding()]
param(
    [string]$Version,
    [string]$PackageDirectory,
    [string]$OutputDirectory,
    [string]$Python = 'python',
    [string]$SevenZip = "$env:ProgramFiles\7-Zip\7z.exe",
    [string]$MakeNsis = "${env:ProgramFiles(x86)}\NSIS\makensis.exe",
    [switch]$ReuseDependencies,
    [switch]$NoRestore
)

$ErrorActionPreference = 'Stop'
function Get-Sha256([string]$Path) {
    $stream = [IO.File]::OpenRead($Path)
    $algorithm = [Security.Cryptography.SHA256]::Create()
    try {
        return [BitConverter]::ToString($algorithm.ComputeHash($stream)).Replace('-', '').ToLowerInvariant()
    } finally {
        $algorithm.Dispose()
        $stream.Dispose()
    }
}
$repository = Split-Path -Parent $PSScriptRoot
if (!$PackageDirectory) { $PackageDirectory = Join-Path $repository 'artifacts/package' }
if (!$OutputDirectory) { $OutputDirectory = Join-Path $repository 'artifacts/installer' }
$PackageDirectory = [IO.Path]::GetFullPath($PackageDirectory)
$OutputDirectory = [IO.Path]::GetFullPath($OutputDirectory)
if (!(Test-Path -LiteralPath $MakeNsis -PathType Leaf)) { throw "NSIS compiler not found: $MakeNsis" }

if (!$Version) {
    [xml]$project = Get-Content -LiteralPath (Join-Path $repository 'NegativeEncoder/NegativeEncoder.csproj')
    $Version = [string]$project.Project.PropertyGroup.Version
}
$Version = $Version.TrimStart('v')
if ($Version -notmatch '^\d+\.\d+\.\d+(\.\d+)?$') { throw "Expected a numeric release version, got: $Version" }
if ($Version.Split('.').Count -eq 3) { $Version += '.0' }
foreach ($part in $Version.Split('.')) {
    if ([int]$part -gt 65535) { throw 'Windows file version components must be <= 65535' }
}

Push-Location $repository
try {
    if ($ReuseDependencies) {
        $expectedLock = Get-Sha256 (Join-Path $PSScriptRoot 'dependencies.lock.json')
        $stagedLock = Get-Sha256 (Join-Path $PackageDirectory 'dependencies.lock.json')
        if ($expectedLock -ne $stagedLock) { throw 'Staged dependencies use a different lock file; collect into an empty directory' }
        & $Python (Join-Path $PSScriptRoot 'collect_dependencies.py') --output $PackageDirectory --verify-only
    } else {
        & $Python (Join-Path $PSScriptRoot 'collect_dependencies.py') --output $PackageDirectory --seven-zip $SevenZip
    }
    if ($LASTEXITCODE -ne 0) { throw 'Dependency collection/verification failed' }
    $setupDirectory = Join-Path $PackageDirectory 'Setup'
    New-Item -ItemType Directory -Path $setupDirectory -Force | Out-Null
    Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'apple-runtime.lock.json') -Destination $setupDirectory

    $restoreArguments = @()
    if ($NoRestore) { $restoreArguments += '--no-restore' }
    & dotnet publish (Join-Path $repository 'NegativeEncoder/NegativeEncoder.csproj') @restoreArguments `
        --configuration Release --runtime win-x64 --self-contained true `
        "-p:Version=$Version" --output (Join-Path $PackageDirectory 'NEbin')
    if ($LASTEXITCODE -ne 0) { throw 'dotnet publish failed' }

    New-Item -ItemType Directory -Path $OutputDirectory -Force | Out-Null
    $installer = Join-Path $OutputDirectory "NegativeEncoder-$Version-win-x64-setup.exe"
    & $MakeNsis /INPUTCHARSET UTF8 "/DPRODUCT_VERSION=$Version" "/DPACKAGE_DIR=$PackageDirectory" `
        "/DOUTPUT_FILE=$installer" (Join-Path $PSScriptRoot 'NegativeEncoder.nsi')
    if ($LASTEXITCODE -ne 0) { throw 'NSIS compilation failed' }
    if (!(Test-Path -LiteralPath $installer -PathType Leaf)) { throw 'NSIS produced no installer' }

    Copy-Item -LiteralPath (Join-Path $PackageDirectory 'dependencies.lock.json') -Destination $OutputDirectory
    Copy-Item -LiteralPath (Join-Path $PackageDirectory 'THIRD-PARTY-NOTICES.md') -Destination $OutputDirectory
    $hash = Get-Sha256 $installer
    "$hash  $([IO.Path]::GetFileName($installer))" |
        Set-Content -LiteralPath (Join-Path $OutputDirectory 'SHA256SUMS.txt') -Encoding ascii
    Write-Host "Installer: $installer"
} finally {
    Pop-Location
}
