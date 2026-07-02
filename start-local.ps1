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

Write-Host "Starte JudoTournamentManagement API lokal..." -ForegroundColor Green
Write-Host "LAN Zugriff ueber Host-IP auf Port 5080 moeglich." -ForegroundColor Green

if ([string]::IsNullOrWhiteSpace($env:Security__AuthTokenHmacSecret)) {
    $bytes = New-Object byte[] 32
    [System.Security.Cryptography.RandomNumberGenerator]::Fill($bytes)
    $env:Security__AuthTokenHmacSecret = [Convert]::ToBase64String($bytes)
    Write-Host "Security__AuthTokenHmacSecret wurde fuer diese Sitzung zufaellig erzeugt." -ForegroundColor Yellow
}

& $dotnetExecutable run --project (Join-Path $projectRoot "JudoTournamentManagement.Api\JudoTournamentManagement.Api.csproj") --urls "http://0.0.0.0:5080"
