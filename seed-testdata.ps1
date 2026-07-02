#Requires -Version 7

param(
    [string]$BaseUrl = "http://localhost:5080"
)

$apiBaseUrl = "$BaseUrl/api"
$headers = @{ "Content-Type" = "application/json" }

if ($env:ASPNETCORE_ENVIRONMENT -eq "Production") {
    Write-Error "Dieses Skript darf nicht in Production ausgefuehrt werden."
    exit 1
}

$adminPassword = if ([string]::IsNullOrWhiteSpace($env:JUDO_TEST_PASSWORD)) {
    [Guid]::NewGuid().ToString("N") + "!A1"
} else {
    $env:JUDO_TEST_PASSWORD
}

function Invoke-Api {
    param(
        [Parameter(Mandatory = $true)]
        [ValidateSet("GET", "POST", "PUT", "DELETE")]
        [string]$Method,
        [Parameter(Mandatory = $true)]
        [string]$Url,
        [hashtable]$Body
    )

    $request = @{
        Method = $Method
        Uri = $Url
        Headers = $script:headers
        ErrorAction = "Stop"
    }

    if ($PSBoundParameters.ContainsKey("Body")) {
        $request.Body = $Body | ConvertTo-Json -Depth 10
    }

    try {
        return Invoke-RestMethod @request
    }
    catch {
        Write-Error "Request failed: $Method $Url`n$($_.Exception.Message)"
        exit 1
    }
}

function Get-RandomItem {
    param(
        [Parameter(Mandatory = $true)]
        [object[]]$Items
    )

    return $Items[(Get-Random -Minimum 0 -Maximum $Items.Count)]
}

Write-Host "=== Seeding Judo Tournament Management test data ===" -ForegroundColor Cyan
Write-Host "Base URL: $BaseUrl" -ForegroundColor DarkGray
Write-Host "Admin-Passwort Quelle: $(if ([string]::IsNullOrWhiteSpace($env:JUDO_TEST_PASSWORD)) { 'zufaellig generiert' } else { 'JUDO_TEST_PASSWORD' })" -ForegroundColor DarkGray

Write-Host "`n[0/6] Bootstrapping admin user..." -ForegroundColor Yellow
try {
    Invoke-RestMethod -Method POST -Uri "$apiBaseUrl/auth/bootstrap-admin" -Headers $headers -Body (@{
        userName = "admin"
        password = $adminPassword
    } | ConvertTo-Json) -ErrorAction Stop | Out-Null
    Write-Host "Created initial admin user 'admin'." -ForegroundColor Green
}
catch {
    $statusCode = $_.Exception.Response.StatusCode.value__
    if ($statusCode -eq 409) {
        if ([string]::IsNullOrWhiteSpace($env:JUDO_TEST_PASSWORD)) {
            Write-Error "Admin user exists. Bitte JUDO_TEST_PASSWORD setzen, damit der Login erfolgen kann."
            exit 1
        }

        Write-Host "Admin user already exists. Continuing seed..." -ForegroundColor DarkYellow
    }
    else {
        Write-Error "Admin bootstrap failed: $($_.Exception.Message)"
        exit 1
    }
}

Write-Host "`nLogging in as admin..." -ForegroundColor Yellow
try {
    $loginResponse = Invoke-RestMethod -Method POST -Uri "$apiBaseUrl/auth/login" -Headers $headers -Body (@{
        userName = "admin"
        password = $adminPassword
    } | ConvertTo-Json) -ErrorAction Stop
    $bearerToken = $loginResponse.accessToken
    $headers.Authorization = "Bearer $bearerToken"
    Write-Host "Logged in successfully. Token acquired." -ForegroundColor Green
}
catch {
    Write-Error "Login failed: $($_.Exception.Message)"
    exit 1
}

Write-Host "`n[1/6] Creating tournament..." -ForegroundColor Yellow
$tournament = Invoke-Api -Method POST -Url "$apiBaseUrl/tournaments" -Body @{
    name = "UI Testturnier 2026"
    date = "2026-09-20"
    venue = "Sporthalle Musterstadt"
    organizer = "JC Musterstadt"
}

$tournamentId = $tournament.id
Write-Host "Created tournament '$($tournament.name)' ($tournamentId)" -ForegroundColor Green

Write-Host "`n[2/6] Creating tatamis..." -ForegroundColor Yellow
$tatamis = @(
    @{ name = "Matte 1"; displayOrder = 0 }
    @{ name = "Matte 2"; displayOrder = 1 }
)

foreach ($tatamiData in $tatamis) {
    $tatami = Invoke-Api -Method POST -Url "$apiBaseUrl/tournaments/$tournamentId/tatamis" -Body $tatamiData
    Write-Host "Created tatami '$($tatami.name)'" -ForegroundColor Green
}

Write-Host "`n[3/6] Creating U13 male categories..." -ForegroundColor Yellow
$categories = @(
    @{ Label = "-23"; WeightClassKg = 23 }
    @{ Label = "-25"; WeightClassKg = 25 }
    @{ Label = "-27"; WeightClassKg = 27 }
    @{ Label = "-29"; WeightClassKg = 29 }
    @{ Label = "-31"; WeightClassKg = 31 }
    @{ Label = "-34"; WeightClassKg = 34 }
    @{ Label = "-37"; WeightClassKg = 37 }
    @{ Label = "-40"; WeightClassKg = 40 }
    @{ Label = "-43"; WeightClassKg = 43 }
    @{ Label = "-46"; WeightClassKg = 46 }
    @{ Label = "+46"; WeightClassKg = $null }
)

foreach ($categoryData in $categories) {
    $body = @{
        name = "U13 Maennlich $($categoryData.Label) kg"
        ageGroup = "U13"
        gender = "Male"
        matchDurationSeconds = 120
        goldenScoreEnabled = $true
        goldenScoreDurationSeconds = 30
        rulesetNotes = $null
    }

    if ($null -ne $categoryData.WeightClassKg) {
        $body.weightClassKg = $categoryData.WeightClassKg
    }

    $category = Invoke-Api -Method POST -Url "$apiBaseUrl/tournaments/$tournamentId/categories" -Body $body
    Write-Host "Created category '$($category.name)'" -ForegroundColor Green
}

Write-Host "`n[4/6] Creating clubs..." -ForegroundColor Yellow
$clubs = @(
    "JC Musterhausen",
    "Judo-Team Beispielstadt",
    "PSV Testdorf"
)

$createdClubs = @()
foreach ($clubName in $clubs) {
    $club = Invoke-Api -Method POST -Url "$apiBaseUrl/tournaments/$tournamentId/clubs" -Body @{ name = $clubName }
    $createdClubs += $club
    Write-Host "Created club '$($club.name)'" -ForegroundColor Green
}

Write-Host "`n[5/6] Creating athletes..." -ForegroundColor Yellow
$firstNames = @(
    "Ben", "Elias", "Finn", "Jonas", "Leon", "Luca", "Mats", "Noah", "Nico", "Paul",
    "Anton", "David", "Emil", "Felix", "Jan", "Karl", "Luis", "Milan", "Oskar", "Timo"
)
$lastNames = @(
    "Becker", "Bergmann", "Fischer", "Franke", "Hoffmann", "Kaiser", "Klein", "Koch", "Krause", "Krueger",
    "Lehmann", "Mayer", "Neumann", "Richter", "Schmidt", "Schneider", "Scholz", "Schubert", "Vogel", "Wagner"
)

for ($i = 0; $i -lt 40; $i++) {
    $club = $createdClubs[$i % $createdClubs.Count]
    $firstName = Get-RandomItem -Items $firstNames
    $lastName = Get-RandomItem -Items $lastNames
    $birthYear = Get-Random -Minimum 2014 -Maximum 2018
    $licenseId = if ((Get-Random -Minimum 0 -Maximum 4) -eq 0) { "LIZ-{0:D4}" -f ($i + 1) } else { $null }

    $athleteBody = @{
        clubId = $club.id
        firstName = $firstName
        lastName = "{0}-{1:D2}" -f $lastName, ($i + 1)
        birthYear = $birthYear
        gender = "Male"
        licenseId = $licenseId
    }

    if ($i -lt 20) {
        $weightTenth = Get-Random -Minimum 200 -Maximum 411
        $athleteBody.weightKg = [math]::Round($weightTenth / 10.0, 1)
    }

    $athlete = Invoke-Api -Method POST -Url "$apiBaseUrl/tournaments/$tournamentId/athletes" -Body $athleteBody
    $weightDisplay = if ($null -ne $athlete.weightKg) { "$($athlete.weightKg) kg" } else { "no weight" }
    Write-Host "Created athlete '$($athlete.firstName) $($athlete.lastName)' for '$($club.name)' ($weightDisplay)" -ForegroundColor Green
}

Write-Host "`nSeed complete." -ForegroundColor Cyan
Write-Host "Tournament ID: $tournamentId" -ForegroundColor DarkGray
Write-Host "Open the UI and select 'UI Testturnier 2026'." -ForegroundColor DarkGray
