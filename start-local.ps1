param(
    [switch]$SkipFrontendBuild,
    [switch]$EnableTls,
    [int]$HttpsPort = 7080
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
$frontendOutputRoot = Join-Path $projectRoot "JudoTournamentManagement.Api\wwwroot"
$frontendIndexPath = Join-Path $frontendOutputRoot "index.html"

function Invoke-FrontendBuild {
    param(
        [string]$FrontendRoot,
        [string]$NpmExecutable
    )

    Write-Host "Baue Frontend (Angular) nach wwwroot ..." -ForegroundColor Cyan
    Push-Location $FrontendRoot
    try {
        & $NpmExecutable run build
        if ($LASTEXITCODE -ne 0) {
            throw "Frontend-Build fehlgeschlagen (ExitCode $LASTEXITCODE)."
        }
    }
    finally {
        Pop-Location
    }
}

if (-not $SkipFrontendBuild) {
    if (-not (Test-Path (Join-Path $frontendRoot "package.json"))) {
        throw "Frontend-Ordner nicht gefunden: '$frontendRoot'. Start mit -SkipFrontendBuild ausfuehren, wenn nur das API gestartet werden soll."
    }

    $npmCommand = Get-Command npm -ErrorAction SilentlyContinue
    if ($null -eq $npmCommand) {
        throw "npm wurde nicht gefunden. Node.js installieren oder mit -SkipFrontendBuild nur das API starten."
    }

    Invoke-FrontendBuild -FrontendRoot $frontendRoot -NpmExecutable $npmCommand.Source
}
else {
    if (Test-Path $frontendIndexPath) {
        Write-Host "Frontend-Build uebersprungen (-SkipFrontendBuild). Vorhandene UI-Artefakte werden verwendet." -ForegroundColor Yellow
    }
    else {
        if (-not (Test-Path (Join-Path $frontendRoot "package.json"))) {
            throw "Frontend-Build wurde uebersprungen, aber '$frontendIndexPath' fehlt und der Frontend-Ordner wurde nicht gefunden. Erst das Frontend bauen oder ohne -SkipFrontendBuild starten."
        }

        $npmCommand = Get-Command npm -ErrorAction SilentlyContinue
        if ($null -eq $npmCommand) {
            throw "Frontend-Build wurde uebersprungen, aber '$frontendIndexPath' fehlt. npm wurde nicht gefunden. Frontend zuerst bauen oder ohne -SkipFrontendBuild auf einem Rechner mit Node.js starten."
        }

        Write-Host "Frontend-Build wurde uebersprungen, aber es sind noch keine UI-Artefakte vorhanden. Fuehre einmalig einen Frontend-Build aus ..." -ForegroundColor Yellow
        Invoke-FrontendBuild -FrontendRoot $frontendRoot -NpmExecutable $npmCommand.Source
    }
}

Write-Host "Starte JudoTournamentManagement API lokal..." -ForegroundColor Green

$urls = "http://0.0.0.0:5080"
if ($EnableTls) {
    $certDirectory = Join-Path $projectRoot "JudoTournamentManagement.Api\App_Data\certs"
    $certPath = Join-Path $certDirectory "dev-lan-cert.pfx"
    New-Item -ItemType Directory -Path $certDirectory -Force | Out-Null

    $certPassword = $env:JUDO_DEV_TLS_CERT_PASSWORD
    if ([string]::IsNullOrWhiteSpace($certPassword)) {
        $bytes = New-Object byte[] 24
        $rng = [System.Security.Cryptography.RandomNumberGenerator]::Create()
        try {
            $rng.GetBytes($bytes)
        }
        finally {
            $rng.Dispose()
        }

        $certPassword = [Convert]::ToBase64String($bytes)
        $env:JUDO_DEV_TLS_CERT_PASSWORD = $certPassword
        Write-Host "JUDO_DEV_TLS_CERT_PASSWORD wurde fuer diese Sitzung zufaellig erzeugt." -ForegroundColor Yellow
    }

    & $dotnetExecutable dev-certs https -ep $certPath -p $certPassword | Out-Null

    $env:ASPNETCORE_Kestrel__Certificates__Default__Path = $certPath
    $env:ASPNETCORE_Kestrel__Certificates__Default__Password = $certPassword
    $urls = "http://0.0.0.0:5080;https://0.0.0.0:$HttpsPort"

    Write-Host "TLS aktiviert. HTTPS URL: https://localhost:$HttpsPort" -ForegroundColor Green
}
else {
    Remove-Item Env:ASPNETCORE_Kestrel__Certificates__Default__Path -ErrorAction SilentlyContinue
    Remove-Item Env:ASPNETCORE_Kestrel__Certificates__Default__Password -ErrorAction SilentlyContinue
}

Write-Host "LAN Zugriff ueber Host-IP moeglich. URLs: $urls" -ForegroundColor Green

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

& $dotnetExecutable run --project (Join-Path $projectRoot "JudoTournamentManagement.Api\JudoTournamentManagement.Api.csproj") --urls $urls
