param(
    [string]$Configuration = "Release",
    [string]$OutputDirectory = ".\artifacts\transfer",
    [string]$Runtime,
    [switch]$SelfContained,
    [switch]$SkipFrontendBuild,
    [switch]$IncludeDatabase,
    [switch]$IncludeDevCertificate
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$projectRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$apiProject = Join-Path $projectRoot "JudoTournamentManagement.Api\JudoTournamentManagement.Api.csproj"

if (-not (Test-Path $apiProject)) {
    throw "API project not found: '$apiProject'."
}

$dotnetWindowsPath = Join-Path $projectRoot ".dotnet\dotnet.exe"
$dotnetUnixPath = Join-Path $projectRoot ".dotnet/dotnet"
$dotnetExecutable = $null

if (Test-Path $dotnetWindowsPath) {
    $dotnetExecutable = $dotnetWindowsPath
}
elseif (Test-Path $dotnetUnixPath) {
    $dotnetExecutable = $dotnetUnixPath
}
else {
    $dotnetCommand = Get-Command dotnet -ErrorAction SilentlyContinue
    if ($null -ne $dotnetCommand) {
        $dotnetExecutable = $dotnetCommand.Source
    }
}

if ($null -eq $dotnetExecutable) {
    throw "No .NET SDK installation found. Expected '.dotnet/dotnet(.exe)' or 'dotnet' in PATH."
}

$frontendRoot = Join-Path $projectRoot "frontend"
if (-not $SkipFrontendBuild) {
    if (-not (Test-Path (Join-Path $frontendRoot "package.json"))) {
        throw "Frontend folder not found: '$frontendRoot'. Use -SkipFrontendBuild for API-only packaging."
    }

    $npmCommand = Get-Command npm -ErrorAction SilentlyContinue
    if ($null -eq $npmCommand) {
        throw "npm not found. Install Node.js or use -SkipFrontendBuild for API-only packaging."
    }

    Write-Host "Building frontend (Angular) before publish..." -ForegroundColor Cyan
    Push-Location $frontendRoot
    try {
        & $npmCommand.Source run build
    }
    finally {
        Pop-Location
    }
}
else {
    Write-Host "Frontend build skipped (-SkipFrontendBuild)." -ForegroundColor Yellow
}

$resolvedOutputDirectory = if ([System.IO.Path]::IsPathRooted($OutputDirectory)) {
    $OutputDirectory
}
else {
    Join-Path $projectRoot $OutputDirectory
}

$timestamp = Get-Date -Format "yyyyMMdd-HHmmss"
$bundleName = "judo-tournament-transfer-$timestamp"
$bundleRoot = Join-Path $resolvedOutputDirectory $bundleName
$publishDirectory = Join-Path $bundleRoot "app"
$zipPath = "$bundleRoot.zip"

if (Test-Path $bundleRoot) {
    Remove-Item -Path $bundleRoot -Recurse -Force
}

if (Test-Path $zipPath) {
    Remove-Item -Path $zipPath -Force
}

New-Item -ItemType Directory -Path $publishDirectory -Force | Out-Null

$publishArgs = @(
    "publish",
    $apiProject,
    "-c", $Configuration,
    "-o", $publishDirectory,
    "-p:UseAppHost=true",
    "-p:DebugType=None",
    "-p:DebugSymbols=false"
)

if (-not [string]::IsNullOrWhiteSpace($Runtime)) {
    $publishArgs += @("-r", $Runtime)
}

if ($SelfContained) {
    $publishArgs += "--self-contained"
}
elseif (-not [string]::IsNullOrWhiteSpace($Runtime)) {
    $publishArgs += @("--self-contained", "false")
}

Write-Host "Publishing API for transfer bundle..." -ForegroundColor Cyan
& $dotnetExecutable @publishArgs

# Remove build metadata files that are not needed at runtime.
Get-ChildItem -Path $publishDirectory -Filter "*.pdb" -File -ErrorAction SilentlyContinue | Remove-Item -Force
Get-ChildItem -Path $publishDirectory -Filter "*.xml" -File -ErrorAction SilentlyContinue | Remove-Item -Force
Get-ChildItem -Path $publishDirectory -Filter "appsettings.Development.json" -File -ErrorAction SilentlyContinue | Remove-Item -Force

if ($IncludeDatabase) {
    $sourceDataDirectory = Join-Path $projectRoot "JudoTournamentManagement.Api\App_Data"
    if (Test-Path $sourceDataDirectory) {
        Write-Host "Including App_Data in bundle..." -ForegroundColor Yellow
        Copy-Item -Path $sourceDataDirectory -Destination (Join-Path $publishDirectory "App_Data") -Recurse -Force
    }
}

if ($IncludeDevCertificate) {
    $sourceCertificateDirectory = Join-Path $projectRoot "JudoTournamentManagement.Api\App_Data\certs"
    if (Test-Path $sourceCertificateDirectory) {
        Write-Host "Including development TLS certificate folder in bundle..." -ForegroundColor Yellow
        New-Item -ItemType Directory -Path (Join-Path $publishDirectory "App_Data") -Force | Out-Null
        Copy-Item -Path $sourceCertificateDirectory -Destination (Join-Path $publishDirectory "App_Data\certs") -Recurse -Force
    }
}

$startPs1Path = Join-Path $bundleRoot "start-package.ps1"
$startShPath = Join-Path $bundleRoot "start-package.sh"
$readmePath = Join-Path $bundleRoot "README.txt"

@'
param(
    [string]$Urls = "http://0.0.0.0:5080"
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$bundleRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$appRoot = Join-Path $bundleRoot "app"

$exe = Get-ChildItem -Path $appRoot -Filter "JudoTournamentManagement.Api*.exe" -File -ErrorAction SilentlyContinue |
    Sort-Object Length -Descending |
    Select-Object -First 1

if ($null -ne $exe) {
    & $exe.FullName --urls $Urls
    exit $LASTEXITCODE
}

$dotnet = Get-Command dotnet -ErrorAction SilentlyContinue
if ($null -eq $dotnet) {
    throw "dotnet runtime not found in PATH and no self-contained executable found."
}

$dllPath = Join-Path $appRoot "JudoTournamentManagement.Api.dll"
if (-not (Test-Path $dllPath)) {
    throw "Could not find JudoTournamentManagement.Api.dll in '$appRoot'."
}

& $dotnet.Source $dllPath --urls $Urls
exit $LASTEXITCODE
'@ | Set-Content -Path $startPs1Path -Encoding UTF8

@'
#!/usr/bin/env bash
set -euo pipefail

URLS="${1:-http://0.0.0.0:5080}"
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
APP_DIR="$SCRIPT_DIR/app"

if [[ -x "$APP_DIR/JudoTournamentManagement.Api" ]]; then
  "$APP_DIR/JudoTournamentManagement.Api" --urls "$URLS"
  exit $?
fi

if command -v dotnet >/dev/null 2>&1; then
  exec dotnet "$APP_DIR/JudoTournamentManagement.Api.dll" --urls "$URLS"
fi

echo "dotnet runtime not found and no self-contained executable present." >&2
exit 1
'@ | Set-Content -Path $startShPath -Encoding UTF8

@'
Judo Tournament Management - Transfer Bundle

Contents
- app/: published runtime files
- start-package.ps1: start script for Windows PowerShell
- start-package.sh: start script for Linux/macOS bash

Start
Windows:
  .\start-package.ps1

Linux/macOS:
  chmod +x ./start-package.sh
  ./start-package.sh

Optional URL override
Windows:
  .\start-package.ps1 -Urls "http://0.0.0.0:5080"
Linux/macOS:
  ./start-package.sh "http://0.0.0.0:5080"

Notes
- Framework-dependent package requires dotnet runtime on target machine.
- Self-contained package does not require dotnet runtime but is larger.
- If data should be migrated, create package with -IncludeDatabase.
'@ | Set-Content -Path $readmePath -Encoding UTF8

Write-Host "Creating ZIP archive..." -ForegroundColor Cyan
Compress-Archive -Path (Join-Path $bundleRoot "*") -DestinationPath $zipPath -CompressionLevel Optimal

Write-Host "Transfer bundle created:" -ForegroundColor Green
Write-Host "  Folder: $bundleRoot" -ForegroundColor Green
Write-Host "  Zip:    $zipPath" -ForegroundColor Green
