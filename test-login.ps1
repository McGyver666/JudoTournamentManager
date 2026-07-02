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

Write-Host "Testing login endpoint..." -ForegroundColor Yellow

try {
    $body = @{
        userName = "admin"
        password = $password
    } | ConvertTo-Json
    $response = Invoke-RestMethod -Method POST -Uri "$apiBaseUrl/auth/login" -Headers $headers -Body $body -ErrorAction Stop
    Write-Host "Response:" -ForegroundColor Green
    Write-Host ($response | ConvertTo-Json)
    Write-Host "`nResponse type: $($response.GetType())" -ForegroundColor DarkGray
    Write-Host "Response keys: $($response.Keys -join ', ')" -ForegroundColor DarkGray
}
catch {
    Write-Host "Error: $($_.Exception.Message)" -ForegroundColor Red
}
