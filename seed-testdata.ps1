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

function Read-PlainTextPassword {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Prompt
    )

    $securePassword = Read-Host -Prompt $Prompt -AsSecureString
    $credential = [System.Net.NetworkCredential]::new("", $securePassword)
    return $credential.Password
}

function Get-RandomWeightKg {
    param(
        [Parameter(Mandatory = $true)]
        [decimal]$Minimum,
        [Parameter(Mandatory = $true)]
        [decimal]$Maximum
    )

    $minimumTenths = [int]($Minimum * 10)
    $maximumTenthsExclusive = [int]($Maximum * 10) + 1
    return [math]::Round((Get-Random -Minimum $minimumTenths -Maximum $maximumTenthsExclusive) / 10.0, 1)
}

Write-Host "=== Seeding Judo Tournament Management test data ===" -ForegroundColor Cyan
Write-Host "Base URL: $BaseUrl" -ForegroundColor DarkGray
Write-Host "Admin-Passwort Quelle: $(if ([string]::IsNullOrWhiteSpace($env:JUDO_TEST_PASSWORD)) { 'zufaellig generiert' } else { 'JUDO_TEST_PASSWORD' })" -ForegroundColor DarkGray

Write-Host "`n[0/4] Bootstrapping admin user..." -ForegroundColor Yellow
$adminPasswordWasPrompted = $false
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
            Write-Host "Admin user already exists and no JUDO_TEST_PASSWORD is set." -ForegroundColor DarkYellow
            $adminPassword = Read-PlainTextPassword -Prompt "Bitte Admin-Passwort fuer Login eingeben"
            if ([string]::IsNullOrWhiteSpace($adminPassword)) {
                Write-Error "Kein Admin-Passwort eingegeben. Seed abgebrochen."
                exit 1
            }

            $adminPasswordWasPrompted = $true
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
    if (-not $adminPasswordWasPrompted -and [string]::IsNullOrWhiteSpace($env:JUDO_TEST_PASSWORD)) {
        Write-Host "Login with bootstrapped password failed. Please enter the existing admin password." -ForegroundColor DarkYellow
        $adminPassword = Read-PlainTextPassword -Prompt "Bitte Admin-Passwort fuer Login eingeben"

        try {
            $loginResponse = Invoke-RestMethod -Method POST -Uri "$apiBaseUrl/auth/login" -Headers $headers -Body (@{
                userName = "admin"
                password = $adminPassword
            } | ConvertTo-Json) -ErrorAction Stop
            $bearerToken = $loginResponse.accessToken
            $headers.Authorization = "Bearer $bearerToken"
            $adminPasswordWasPrompted = $true
            Write-Host "Logged in successfully. Token acquired." -ForegroundColor Green
        }
        catch {
            Write-Error "Login failed: $($_.Exception.Message)"
            exit 1
        }
    }
    else {
        Write-Error "Login failed: $($_.Exception.Message)"
        exit 1
    }
}

Write-Host "`n[1/4] Creating tournament..." -ForegroundColor Yellow
$tournament = Invoke-Api -Method POST -Url "$apiBaseUrl/tournaments" -Body @{
    name = "UI Testturnier 2026"
    date = "2026-09-20"
    venue = "Sporthalle Musterstadt"
    organizer = "JC Musterstadt"
}

$tournamentId = $tournament.id
Write-Host "Created tournament '$($tournament.name)' ($tournamentId)" -ForegroundColor Green

Write-Host "`n[2/4] Creating tatamis..." -ForegroundColor Yellow
$tatamis = @(
    @{ name = "Matte 1"; displayOrder = 0 }
    @{ name = "Matte 2"; displayOrder = 1 }
)

foreach ($tatamiData in $tatamis) {
    $tatami = Invoke-Api -Method POST -Url "$apiBaseUrl/tournaments/$tournamentId/tatamis" -Body $tatamiData
    Write-Host "Created tatami '$($tatami.name)'" -ForegroundColor Green
}

Write-Host "`n[3/4] Creating clubs..." -ForegroundColor Yellow
$clubs = @(
    "JC Musterhausen",
    "Judo-Team Beispielstadt",
    "PSV Testdorf",
    "Judo Akademie Neustadt"
)

$createdClubs = @()
foreach ($clubName in $clubs) {
    $club = Invoke-Api -Method POST -Url "$apiBaseUrl/tournaments/$tournamentId/clubs" -Body @{
        name = $clubName
        contactName = "Kontakt $clubName"
        contactEmail = ("kontakt@{0}.example" -f ($clubName.ToLowerInvariant() -replace '[^a-z0-9]+', '-').Trim('-'))
        contactPhone = "+49 555 0100"
    }
    $createdClubs += $club
    Write-Host "Created club '$($club.name)'" -ForegroundColor Green
}

Write-Host "`n[4/4] Creating athletes..." -ForegroundColor Yellow
$maleFirstNames = @(
    "Ben", "Elias", "Finn", "Jonas", "Leon", "Luca", "Mats", "Noah", "Nico", "Paul",
    "Anton", "David", "Emil", "Felix", "Jan", "Karl", "Luis", "Milan", "Oskar", "Timo",
    "Aron", "Bennet", "Hannes", "Jannis", "Levi", "Linus", "Mika", "Moritz", "Theo", "Yusuf"
)
$femaleFirstNames = @(
    "Anna", "Clara", "Ella", "Emma", "Frieda", "Hannah", "Ida", "Lea", "Lena", "Lina",
    "Maja", "Mia", "Nele", "Paula", "Sofia", "Zoe", "Amelie", "Greta", "Juna", "Mila",
    "Charlotte", "Elif", "Helena", "Johanna", "Luisa", "Marie", "Nora", "Sara", "Thea", "Yara"
)
$lastNames = @(
    "Becker", "Bergmann", "Fischer", "Franke", "Hoffmann", "Kaiser", "Klein", "Koch", "Krause", "Krueger",
    "Lehmann", "Mayer", "Neumann", "Richter", "Schmidt", "Schneider", "Scholz", "Schubert", "Vogel", "Wagner",
    "Baumann", "Brandt", "Engel", "Friedrich", "Hartmann", "Jung", "Keller", "Koenig", "Lange", "Lorenz",
    "Peters", "Roth", "Schreiber", "Simon", "Weber", "Weiss", "Werner", "Winkler", "Wolf", "Zimmermann"
)

$ageGroups = @(
    @{
        Name = "U11"
        Count = 30
        BirthYears = @(2016, 2017)
        MinimumWeightKg = 22.0
        MaximumWeightKg = 40.0
    },
    @{
        Name = "U13"
        Count = 68
        BirthYears = @(2014, 2015)
        MinimumWeightKg = 28.0
        MaximumWeightKg = 48.0
    },
    @{
        Name = "U15"
        Count = 52
        BirthYears = @(2012, 2013)
        MinimumWeightKg = 35.0
        MaximumWeightKg = 60.0
    }
)

$genders = @()
for ($i = 0; $i -lt 53; $i++) {
    $genders += "Female"
}
for ($i = 0; $i -lt 97; $i++) {
    $genders += "Male"
}
$genders = $genders | Sort-Object { Get-Random }

$athleteBodies = @()
$athleteNumber = 0

foreach ($ageGroup in $ageGroups) {
    for ($i = 0; $i -lt $ageGroup.Count; $i++) {
        $club = $createdClubs[$athleteNumber % $createdClubs.Count]
        $gender = $genders[$athleteNumber]
        $firstName = if ($gender -eq "Female") {
            Get-RandomItem -Items $femaleFirstNames
        } else {
            Get-RandomItem -Items $maleFirstNames
        }
        $lastName = Get-RandomItem -Items $lastNames
        $birthYear = Get-RandomItem -Items $ageGroup.BirthYears
        $grade = Get-Random -Minimum 2 -Maximum 9
        $licenseId = "LIZ-{0}-{1:D3}" -f (Get-Date -Format "yyyyMMddHHmm"), ($athleteNumber + 1)

        $athleteBody = @{
            clubId = $club.id
            firstName = $firstName
            lastName = $lastName
            birthYear = $birthYear
            gender = $gender
            licenseId = $licenseId
            weightKg = Get-RandomWeightKg -Minimum $ageGroup.MinimumWeightKg -Maximum $ageGroup.MaximumWeightKg
            grade = $grade
        }

        $athleteBodies += $athleteBody
        $athleteNumber++
    }
}

$athleteBodies = $athleteBodies | Sort-Object { Get-Random }

$importedAthletes = Invoke-Api -Method POST -Url "$apiBaseUrl/tournaments/$tournamentId/athletes/import?allowDuplicate=true" -Body @{
    athletes = $athleteBodies
}

Write-Host "Imported $($importedAthletes.Count) athletes in one batch." -ForegroundColor Green
Write-Host "Distribution: 30 U11, 68 U13, 52 U15; 53 female, 97 male; 4 clubs." -ForegroundColor DarkGray

Write-Host "`nSeed complete." -ForegroundColor Cyan
Write-Host "Tournament ID: $tournamentId" -ForegroundColor DarkGray
Write-Host "Open the UI and select 'UI Testturnier 2026'." -ForegroundColor DarkGray

Write-Host "`n━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━" -ForegroundColor Yellow
Write-Host "Admin Credentials:" -ForegroundColor Yellow
Write-Host "  Username: admin" -ForegroundColor White
if ($adminPasswordWasPrompted) {
    Write-Host "  Password: existing password entered interactively" -ForegroundColor White
} else {
    Write-Host "  Password: $adminPassword" -ForegroundColor White
}
Write-Host "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━" -ForegroundColor Yellow
