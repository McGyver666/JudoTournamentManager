#Requires -Version 7

param(
    [string]$BaseUrl = "http://localhost:5080",
    [string]$AdminUser = "admin",
    [string]$AdminPassword = "StrongPassword!123"
)

$apiBaseUrl = "$BaseUrl/api"
$headers = @{ "Content-Type" = "application/json" }

function Invoke-Api {
    param(
        [Parameter(Mandatory = $true)]
        [ValidateSet("GET", "POST", "PUT", "PATCH", "DELETE")]
        [string]$Method,
        [Parameter(Mandatory = $true)]
        [string]$Url,
        [object]$Body,
        [switch]$AllowError
    )

    $request = @{
        Method = $Method
        Uri = $Url
        Headers = $script:headers
        ErrorAction = "Stop"
    }

    if ($PSBoundParameters.ContainsKey("Body")) {
        $request.Body = $Body | ConvertTo-Json -Depth 20
    }

    try {
        $response = Invoke-WebRequest @request
        $json = $null
        if ($response.Content) {
            try {
                $json = $response.Content | ConvertFrom-Json
            }
            catch {
                $json = $null
            }
        }

        return [pscustomobject]@{
            Ok = $true
            StatusCode = [int]$response.StatusCode
            Json = $json
            Raw = $response
        }
    }
    catch {
        $statusCode = 0
        $content = ""
        if ($_.Exception.Response) {
            $statusCode = [int]$_.Exception.Response.StatusCode
            try {
                $content = $_.Exception.Response.Content.ReadAsStringAsync().GetAwaiter().GetResult()
            }
            catch {
                $content = ""
            }
        }

        if (-not $AllowError) {
            throw "Request failed ($statusCode): $Method $Url`n$content"
        }

        $json = $null
        if ($content) {
            try {
                $json = $content | ConvertFrom-Json
            }
            catch {
                $json = $null
            }
        }

        return [pscustomobject]@{
            Ok = $false
            StatusCode = $statusCode
            Json = $json
            Raw = $null
        }
    }
}

function Assert-True {
    param(
        [Parameter(Mandatory = $true)]
        [bool]$Condition,
        [Parameter(Mandatory = $true)]
        [string]$Message
    )

    if (-not $Condition) {
        throw "ASSERT FAILED: $Message"
    }
}

Write-Host "=== Draw/Lock smoke flow ===" -ForegroundColor Cyan
Write-Host "Base URL: $BaseUrl" -ForegroundColor DarkGray

Write-Host "[1/10] Bootstrap admin (idempotent)..." -ForegroundColor Yellow
$bootstrap = Invoke-Api -Method POST -Url "$apiBaseUrl/auth/bootstrap-admin" -Body @{
    userName = $AdminUser
    password = $AdminPassword
} -AllowError
if ($bootstrap.Ok) {
    Write-Host "Admin user created." -ForegroundColor Green
}
elseif ($bootstrap.StatusCode -eq 409) {
    Write-Host "Admin user already exists." -ForegroundColor DarkYellow
}
else {
    throw "Bootstrap admin failed with status $($bootstrap.StatusCode)."
}

Write-Host "[2/10] Login..." -ForegroundColor Yellow
$login = Invoke-Api -Method POST -Url "$apiBaseUrl/auth/login" -Body @{
    userName = $AdminUser
    password = $AdminPassword
}
$token = $login.Json.accessToken
Assert-True -Condition (-not [string]::IsNullOrWhiteSpace($token)) -Message "Login did not return accessToken."
$headers.Authorization = "Bearer $token"
Write-Host "Login successful." -ForegroundColor Green

Write-Host "[3/10] Create tournament, clubs, category, and athletes..." -ForegroundColor Yellow
$stamp = [DateTimeOffset]::UtcNow.ToString("yyyyMMddHHmmss")
$tournament = Invoke-Api -Method POST -Url "$apiBaseUrl/tournaments" -Body @{
    name = "Smoke Draw Lock $stamp"
    date = "2026-09-20"
    venue = "Smoke Hall"
    organizer = "Smoke Club"
}
$tournamentId = $tournament.Json.id

$clubA = Invoke-Api -Method POST -Url "$apiBaseUrl/tournaments/$tournamentId/clubs" -Body @{ name = "Club A $stamp" }
$clubB = Invoke-Api -Method POST -Url "$apiBaseUrl/tournaments/$tournamentId/clubs" -Body @{ name = "Club B $stamp" }

$category = Invoke-Api -Method POST -Url "$apiBaseUrl/tournaments/$tournamentId/categories" -Body @{
    name = "U13 Male -40"
    ageGroup = "U13"
    gender = "Male"
    weightClassKg = 40
    matchDurationSeconds = 120
}
$categoryId = $category.Json.id

$athlete1 = Invoke-Api -Method POST -Url "$apiBaseUrl/tournaments/$tournamentId/athletes" -Body @{
    clubId = $clubA.Json.id
    firstName = "Tom"
    lastName = "SmokeA-$stamp"
    birthYear = 2014
    gender = "Male"
    weightKg = 30.1
}
$athlete2 = Invoke-Api -Method POST -Url "$apiBaseUrl/tournaments/$tournamentId/athletes" -Body @{
    clubId = $clubA.Json.id
    firstName = "Max"
    lastName = "SmokeB-$stamp"
    birthYear = 2014
    gender = "Male"
    weightKg = 31.1
}
$athlete3 = Invoke-Api -Method POST -Url "$apiBaseUrl/tournaments/$tournamentId/athletes" -Body @{
    clubId = $clubB.Json.id
    firstName = "Leo"
    lastName = "SmokeC-$stamp"
    birthYear = 2014
    gender = "Male"
    weightKg = 32.1
}

Write-Host "[4/10] Register athletes and assign first two to category..." -ForegroundColor Yellow
$reg1 = Invoke-Api -Method POST -Url "$apiBaseUrl/tournaments/$tournamentId/registrations" -Body @{
    athleteId = $athlete1.Json.id
    weightKg = 30.1
    licenseConfirmed = $false
}
$reg2 = Invoke-Api -Method POST -Url "$apiBaseUrl/tournaments/$tournamentId/registrations" -Body @{
    athleteId = $athlete2.Json.id
    weightKg = 31.1
    licenseConfirmed = $false
}
Invoke-Api -Method POST -Url "$apiBaseUrl/tournaments/$tournamentId/registrations/$($reg1.Json.id)/category" -Body @{ categoryId = $categoryId } | Out-Null
Invoke-Api -Method POST -Url "$apiBaseUrl/tournaments/$tournamentId/registrations/$($reg2.Json.id)/category" -Body @{ categoryId = $categoryId } | Out-Null

Write-Host "[5/10] Generate draw and verify category stays unlocked..." -ForegroundColor Yellow
Invoke-Api -Method POST -Url "$apiBaseUrl/tournaments/$tournamentId/categories/$categoryId/draw" -Body @{ format = "SingleElimination" } | Out-Null
$categoriesAfterDraw = Invoke-Api -Method GET -Url "$apiBaseUrl/tournaments/$tournamentId/categories"
$categoryAfterDraw = $categoriesAfterDraw.Json | Where-Object { $_.id -eq $categoryId }
Assert-True -Condition ($null -ne $categoryAfterDraw) -Message "Category not found after draw generation."
Assert-True -Condition (-not $categoryAfterDraw.isLocked) -Message "Category must stay unlocked after draw generation."

$fightsBeforeReassign = Invoke-Api -Method GET -Url "$apiBaseUrl/tournaments/$tournamentId/categories/$categoryId/fights"
$fightCountBefore = @($fightsBeforeReassign.Json).Count

Write-Host "[6/10] Assign third athlete before first fight starts..." -ForegroundColor Yellow
$reg3 = Invoke-Api -Method POST -Url "$apiBaseUrl/tournaments/$tournamentId/registrations" -Body @{
    athleteId = $athlete3.Json.id
    weightKg = 32.1
    licenseConfirmed = $false
}

$assignThird = Invoke-Api -Method POST -Url "$apiBaseUrl/tournaments/$tournamentId/registrations/$($reg3.Json.id)/category" -Body @{ categoryId = $categoryId }
Assert-True -Condition ($assignThird.Ok) -Message "Assigning third athlete before first start should succeed."

$fightsAfterReassign = Invoke-Api -Method GET -Url "$apiBaseUrl/tournaments/$tournamentId/categories/$categoryId/fights"
$fightCountAfter = @($fightsAfterReassign.Json).Count
Assert-True -Condition ($fightCountAfter -gt $fightCountBefore) -Message "Draw was not refreshed after category change before first start."

$containsThirdAthlete = @($fightsAfterReassign.Json | Where-Object {
        $_.whiteAthleteId -eq $athlete3.Json.id -or $_.blueAthleteId -eq $athlete3.Json.id
    }).Count -gt 0
Assert-True -Condition $containsThirdAthlete -Message "Refreshed draw does not contain newly assigned athlete."

Write-Host "[7/10] Start first real fight..." -ForegroundColor Yellow
$firstRealFight = $fightsAfterReassign.Json | Where-Object {
    -not $_.isBye -and $_.status -eq "Pending" -and $_.whiteAthleteId -and $_.blueAthleteId
} | Select-Object -First 1
Assert-True -Condition ($null -ne $firstRealFight) -Message "No startable real fight found."

$startFight = Invoke-Api -Method POST -Url "$apiBaseUrl/tournaments/$tournamentId/fights/$($firstRealFight.id)/start" -AllowError
Assert-True -Condition ($startFight.StatusCode -eq 204) -Message "Starting first real fight failed."

Write-Host "[8/10] Verify category is now locked..." -ForegroundColor Yellow
$categoriesAfterStart = Invoke-Api -Method GET -Url "$apiBaseUrl/tournaments/$tournamentId/categories"
$categoryAfterStart = $categoriesAfterStart.Json | Where-Object { $_.id -eq $categoryId }
Assert-True -Condition ($categoryAfterStart.isLocked) -Message "Category should be locked after first fight start."

Write-Host "[9/10] Verify reassign is blocked with 409 after lock..." -ForegroundColor Yellow
$moveOutAfterLock = Invoke-Api -Method POST -Url "$apiBaseUrl/tournaments/$tournamentId/registrations/$($reg3.Json.id)/category" -Body @{ categoryId = $categoryId } -AllowError
Assert-True -Condition ($moveOutAfterLock.StatusCode -eq 409) -Message "Expected 409 conflict for assignment after category lock."

$conflictTitle = if ($moveOutAfterLock.Json) { $moveOutAfterLock.Json.title } else { "<no-title>" }
Write-Host "Conflict title: $conflictTitle" -ForegroundColor DarkGray

Write-Host "[10/10] Smoke flow completed successfully." -ForegroundColor Green
Write-Host "Tournament ID: $tournamentId" -ForegroundColor DarkGray
