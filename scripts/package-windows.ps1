[CmdletBinding()]
param(
    [string]$MakeNsis,
    [string]$OutputDirectory = (Join-Path $PSScriptRoot '../artifacts/win-x64')
)

$ErrorActionPreference = 'Stop'
$windowsHost = $env:OS -eq 'Windows_NT'
if (!$MakeNsis) {
    $command = Get-Command makensis -CommandType Application -ErrorAction SilentlyContinue
    if ($command) {
        $MakeNsis = $command.Source
    } elseif ($windowsHost) {
        $MakeNsis = Join-Path ${env:ProgramFiles(x86)} 'NSIS/makensis.exe'
    } else {
        throw 'Install NSIS 3 or pass -MakeNsis with the compiler path.'
    }
}
$compiler = (Get-Command $MakeNsis -CommandType Application).Source
$output = $ExecutionContext.SessionState.Path.GetUnresolvedProviderPathFromPSPath($OutputDirectory)
$repo = Split-Path -Parent $PSScriptRoot

Push-Location $repo
try {
    $project = 'NegativeEncoder.Desktop/NegativeEncoder.Desktop.csproj'
    $properties = & dotnet msbuild $project -nologo -target:GetAssemblyVersion '-getProperty:Version,FileVersion'
    if ($LASTEXITCODE -ne 0) { throw "Could not read project version: $properties" }
    $version = ($properties | Out-String | ConvertFrom-Json).Properties
    $name = "NegativeEncoder-$($version.Version)-win-x64-setup.exe"

    $null = [IO.Directory]::CreateDirectory($output)
    $stage = New-Item -ItemType Directory -Path (Join-Path $output ([guid]::NewGuid().ToString()))
    try {
        $publish = Join-Path $stage.FullName 'publish'
        & dotnet publish $project -c Release -r win-x64 --self-contained true -o $publish `
            -p:Product=NegativeEncoder -p:AssemblyTitle=NegativeEncoder `
            -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:DebugType=none
        if ($LASTEXITCODE -ne 0) { throw "dotnet publish failed ($LASTEXITCODE)." }
        $files = @(Get-ChildItem -LiteralPath $publish -Force)
        if ($files.Count -ne 1 -or $files[0].Name -ne 'NegativeEncoder.Desktop.exe') {
            throw "Expected a single-file Windows publish, found: $($files.Name -join ', ')"
        }

        $prefix = if ($windowsHost) { '/' } else { '-' }
        $installer = Join-Path $stage.FullName $name
        & $compiler "${prefix}INPUTCHARSET" UTF8 "${prefix}DPRODUCT_VERSION=$($version.Version)" `
            "${prefix}DFILE_VERSION=$($version.FileVersion)" "${prefix}DAPP_FILE=$($files[0].FullName)" `
            "${prefix}DOUTPUT_FILE=$installer" (Join-Path $PSScriptRoot 'windows/NegativeEncoder.nsi')
        if ($LASTEXITCODE -ne 0) { throw "NSIS compilation failed ($LASTEXITCODE)." }

        $destination = Join-Path $output $name
        Move-Item -LiteralPath $installer -Destination $destination -Force
        Write-Host "Installer: $destination"
        Write-Host "SHA256: $((Get-FileHash -LiteralPath $destination -Algorithm SHA256).Hash)"
    } finally {
        Remove-Item -LiteralPath $stage.FullName -Recurse -Force
    }
} finally {
    Pop-Location
}
