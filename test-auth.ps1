#Requires -Version 7

$apiBaseUrl = "http://localhost:5080/api"
$headers = @{ "Content-Type" = "application/json" }

if ($env:ASPNETCORE_ENVIRONMENT -eq "Production") {
    Write-Error "Dieses Skript darf nicht in Production ausgefuehrt werden."
    exit 1
}

$password = if ([string]::IsNullOrWhiteSpace($env:JUDO_TEST_PASSWORD)) {
    [Guid]::NewGuid().ToString("N") + "!A1"
} else {
    $env:JUDO_TEST_PASSWORD
}

Write-Host "Verwende Testpasswort aus $(if ([string]::IsNullOrWhiteSpace($env:JUDO_TEST_PASSWORD)) { 'zufaelliger Generierung' } else { 'JUDO_TEST_PASSWORD' })." -ForegroundColor DarkGray

Write-Host "Step 1: Login" -ForegroundColor Yellow
$loginBody = @{
    userName = "admin"
    password = $password
} | ConvertTo-Json
$loginResponse = Invoke-RestMethod -Method POST -Uri "$apiBaseUrl/auth/login" -Headers $headers -Body $loginBody -ErrorAction Stop
$token = if ($null -ne $loginResponse.accessToken) { $loginResponse.accessToken } else { $loginResponse.token }
if ([string]::IsNullOrWhiteSpace($token)) {
    Write-Error "Login response enthaelt kein Token (accessToken/token)."
    exit 1
}
Write-Host "Token acquired: $($token.Substring(0, 20))..." -ForegroundColor Green

Write-Host "`nStep 2: Create tournament with Bearer token" -ForegroundColor Yellow
$authHeaders = @{ 
    "Content-Type" = "application/json"
    "Authorization" = "Bearer $token"
}

$body = @{
    name = "Test Tournament"
    date = "2026-09-20"
    venue = "Test Venue"
    organizer = "Test Org"
} | ConvertTo-Json

Write-Host "Posting to: $apiBaseUrl/tournaments" -ForegroundColor DarkGray
Write-Host "Headers: $($authHeaders.Keys -join ', ')" -ForegroundColor DarkGray

try {
    $result = Invoke-RestMethod -Method POST -Uri "$apiBaseUrl/tournaments" -Headers $authHeaders -Body $body -ErrorAction Stop
    Write-Host "Success! Tournament created: $($result.id)" -ForegroundColor Green
    Write-Host $result | ConvertTo-Json
}
catch {
    Write-Host "Error: $($_.Exception.Message)" -ForegroundColor Red
    Write-Host $_.Exception.Response.StatusCode
}
