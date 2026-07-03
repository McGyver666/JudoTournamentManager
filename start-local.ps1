param(
    [switch]$SkipFrontendBuild
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$projectRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
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
    throw "Keine .NET SDK-Installation gefunden. Erwartet wurde '.dotnet/dotnet(.exe)' oder 'dotnet' im PATH."
}

$frontendRoot = Join-Path $projectRoot "frontend"
if (-not $SkipFrontendBuild) {
    if (-not (Test-Path (Join-Path $frontendRoot "package.json"))) {
        throw "Frontend-Ordner nicht gefunden: '$frontendRoot'. Start mit -SkipFrontendBuild ausfuehren, wenn nur das API gestartet werden soll."
    }

    $npmCommand = Get-Command npm -ErrorAction SilentlyContinue
    if ($null -eq $npmCommand) {
        throw "npm wurde nicht gefunden. Node.js installieren oder mit -SkipFrontendBuild nur das API starten."
    }

    Write-Host "Baue Frontend (Angular) nach wwwroot ..." -ForegroundColor Cyan
    Push-Location $frontendRoot
    try {
        & $npmCommand.Source run build
    }
    finally {
        Pop-Location
    }
}
else {
    Write-Host "Frontend-Build uebersprungen (-SkipFrontendBuild)." -ForegroundColor Yellow
}

Write-Host "Starte JudoTournamentManagement API lokal..." -ForegroundColor Green
Write-Host "LAN Zugriff ueber Host-IP auf Port 5080 moeglich." -ForegroundColor Green

if ([string]::IsNullOrWhiteSpace($env:Security__AuthTokenHmacSecret)) {
    $bytes = New-Object byte[] 32
    $rng = [System.Security.Cryptography.RandomNumberGenerator]::Create()
    try {
        $rng.GetBytes($bytes)
    }
    finally {
        $rng.Dispose()
    }
    $env:Security__AuthTokenHmacSecret = [Convert]::ToBase64String($bytes)
    Write-Host "Security__AuthTokenHmacSecret wurde fuer diese Sitzung zufaellig erzeugt." -ForegroundColor Yellow
}

& $dotnetExecutable run --project (Join-Path $projectRoot "JudoTournamentManagement.Api\JudoTournamentManagement.Api.csproj") --urls "http://0.0.0.0:5080"
