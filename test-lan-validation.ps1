#Requires -Version 7

param(
    [string]$BaseUrl = "http://localhost:5080",
    [string]$AdminUser = "admin",
    [SecureString]$AdminPassword,
    [switch]$SkipCertificateCheck,
    [int]$LatencyThresholdMs = 2000,
    [int]$PollIntervalMs = 100,
    [int]$TimeoutMs = 10000
)

$apiBaseUrl = "$BaseUrl/api"
$defaultHeaders = @{ "Content-Type" = "application/json" }

if ($env:ASPNETCORE_ENVIRONMENT -eq "Production") {
    Write-Error "Dieses Skript darf nicht in Production ausgefuehrt werden."
    exit 1
}

function ConvertTo-PlainText {
    param([SecureString]$Value)

    if ($null -eq $Value) {
        return ""
    }

    $bstr = [Runtime.InteropServices.Marshal]::SecureStringToBSTR($Value)
    try {
        return [Runtime.InteropServices.Marshal]::PtrToStringBSTR($bstr)
    }
    finally {
        [Runtime.InteropServices.Marshal]::ZeroFreeBSTR($bstr)
    }
}

function Resolve-AdminPassword {
    param([SecureString]$Provided)

    $providedPlain = ConvertTo-PlainText -Value $Provided

    if (-not [string]::IsNullOrWhiteSpace($providedPlain)) {
        return [pscustomobject]@{ Password = $providedPlain; Source = "Parameter"; Generated = $false }
    }

    if (-not [string]::IsNullOrWhiteSpace($env:JUDO_TEST_PASSWORD)) {
        return [pscustomobject]@{ Password = $env:JUDO_TEST_PASSWORD; Source = "JUDO_TEST_PASSWORD"; Generated = $false }
    }

    return [pscustomobject]@{ Password = ([Guid]::NewGuid().ToString("N") + "!A1"); Source = "Generated"; Generated = $true }
}

function New-AuthHeaders {
    param([string]$Token)

    return @{
        "Content-Type" = "application/json"
        "Authorization" = "Bearer $Token"
    }
}

function Invoke-Api {
    param(
        [Parameter(Mandatory = $true)]
        [ValidateSet("GET", "POST", "PUT", "PATCH", "DELETE")]
        [string]$Method,
        [Parameter(Mandatory = $true)]
        [string]$Url,
        [hashtable]$Headers,
        [object]$Body,
        [switch]$AllowError
    )

    $request = @{
        Method = $Method
        Uri = $Url
        Headers = if ($Headers) { $Headers } else { $script:defaultHeaders }
        ErrorAction = "Stop"
    }

    if ($SkipCertificateCheck) {
        $request.SkipCertificateCheck = $true
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
            ErrorText = ""
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
            ErrorText = $content
        }
    }
}

function Wait-ForCondition {
    param(
        [Parameter(Mandatory = $true)]
        [scriptblock]$Probe,
        [Parameter(Mandatory = $true)]
        [scriptblock]$Predicate,
        [Parameter(Mandatory = $true)]
        [int]$TimeoutMilliseconds,
        [Parameter(Mandatory = $true)]
        [int]$IntervalMilliseconds
    )

    $sw = [System.Diagnostics.Stopwatch]::StartNew()
    while ($sw.ElapsedMilliseconds -lt $TimeoutMilliseconds) {
        $value = & $Probe
        if (& $Predicate $value) {
            return [pscustomobject]@{ Matched = $true; Value = $value; ElapsedMs = $sw.ElapsedMilliseconds }
        }

        Start-Sleep -Milliseconds $IntervalMilliseconds
    }

    return [pscustomobject]@{ Matched = $false; Value = $null; ElapsedMs = $sw.ElapsedMilliseconds }
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

function Get-AccessToken {
    param(
        [Parameter(Mandatory = $true)]
        [string]$UserName,
        [Parameter(Mandatory = $true)]
        [SecureString]$Password
    )

    $plainPassword = ConvertTo-PlainText -Value $Password

    $login = Invoke-Api -Method POST -Url "$apiBaseUrl/auth/login" -Body @{
        userName = $UserName
        password = $plainPassword
    }

    $token = $login.Json.accessToken
    Assert-True -Condition (-not [string]::IsNullOrWhiteSpace($token)) -Message "Login for $UserName returned no accessToken."
    return $token
}

$passwordInfo = Resolve-AdminPassword -Provided $AdminPassword
$resolvedAdminPassword = $passwordInfo.Password

Write-Host "=== LAN validation (A-02) ===" -ForegroundColor Cyan
Write-Host "Base URL: $BaseUrl" -ForegroundColor DarkGray
Write-Host "Admin password source: $($passwordInfo.Source)" -ForegroundColor DarkGray

$stamp = [DateTimeOffset]::UtcNow.ToString("yyyyMMddHHmmss")
$sharedUserPassword = "LanVal!${stamp}"

Write-Host "[1/9] Bootstrap admin (idempotent)..." -ForegroundColor Yellow
$bootstrap = Invoke-Api -Method POST -Url "$apiBaseUrl/auth/bootstrap-admin" -Body @{
    userName = $AdminUser
    password = $resolvedAdminPassword
} -AllowError

if ($bootstrap.Ok) {
    Write-Host "Admin user created." -ForegroundColor Green
}
elseif ($bootstrap.StatusCode -eq 409) {
    if ($passwordInfo.Generated) {
        throw "Admin user already exists. Set -AdminPassword or JUDO_TEST_PASSWORD to continue."
    }
    Write-Host "Admin user already exists." -ForegroundColor DarkYellow
}
else {
    throw "Bootstrap failed with status $($bootstrap.StatusCode). $($bootstrap.ErrorText)"
}

Write-Host "[2/9] Login admin and create LAN test users..." -ForegroundColor Yellow
$adminToken = Get-AccessToken -UserName $AdminUser -Password (ConvertTo-SecureString $resolvedAdminPassword -AsPlainText -Force)
$adminHeaders = New-AuthHeaders -Token $adminToken

$operatorUser = "op-lan-$stamp"
$displayUser = "disp-lan-$stamp"

Invoke-Api -Method POST -Url "$apiBaseUrl/auth/users" -Headers $adminHeaders -Body @{
    userName = $operatorUser
    role = "Operator"
    password = $sharedUserPassword
} | Out-Null
Invoke-Api -Method POST -Url "$apiBaseUrl/auth/users" -Headers $adminHeaders -Body @{
    userName = $displayUser
    role = "Display"
    password = $sharedUserPassword
} | Out-Null

$operatorToken = Get-AccessToken -UserName $operatorUser -Password (ConvertTo-SecureString $sharedUserPassword -AsPlainText -Force)
$displayToken = Get-AccessToken -UserName $displayUser -Password (ConvertTo-SecureString $sharedUserPassword -AsPlainText -Force)
$operatorHeaders = New-AuthHeaders -Token $operatorToken
$displayHeaders = New-AuthHeaders -Token $displayToken

Write-Host "[3/9] Setup minimal tournament flow data..." -ForegroundColor Yellow
$tournament = Invoke-Api -Method POST -Url "$apiBaseUrl/tournaments" -Headers $adminHeaders -Body @{
    name = "LAN Validation $stamp"
    date = "2026-09-20"
    venue = "LAN Hall"
    organizer = "LAN Club"
}
$tournamentId = $tournament.Json.id

$tatami = Invoke-Api -Method POST -Url "$apiBaseUrl/tournaments/$tournamentId/tatamis" -Headers $adminHeaders -Body @{
    name = "Tatami LAN"
    displayOrder = 1
}
$tatamiId = $tatami.Json.id

$category = Invoke-Api -Method POST -Url "$apiBaseUrl/tournaments/$tournamentId/categories" -Headers $adminHeaders -Body @{
    name = "U13 LAN -40"
    ageGroup = "U13"
    gender = "Male"
    weightClassKg = 40
    matchDurationSeconds = 120
    goldenScoreEnabled = $false
}
$categoryId = $category.Json.id

$club = Invoke-Api -Method POST -Url "$apiBaseUrl/tournaments/$tournamentId/clubs" -Headers $adminHeaders -Body @{ name = "LAN Club $stamp" }
$clubId = $club.Json.id

$athleteA = Invoke-Api -Method POST -Url "$apiBaseUrl/tournaments/$tournamentId/athletes" -Headers $adminHeaders -Body @{
    clubId = $clubId
    firstName = "Lan"
    lastName = "Alpha-$stamp"
    birthYear = 2014
    gender = "Male"
    grade = 5
    weightKg = 30.2
}
$athleteB = Invoke-Api -Method POST -Url "$apiBaseUrl/tournaments/$tournamentId/athletes" -Headers $adminHeaders -Body @{
    clubId = $clubId
    firstName = "Lan"
    lastName = "Bravo-$stamp"
    birthYear = 2014
    gender = "Male"
    grade = 5
    weightKg = 31.3
}

$regA = Invoke-Api -Method POST -Url "$apiBaseUrl/tournaments/$tournamentId/registrations" -Headers $adminHeaders -Body @{
    athleteId = $athleteA.Json.id
    weightKg = 30.2
    licenseConfirmed = $false
}
$regB = Invoke-Api -Method POST -Url "$apiBaseUrl/tournaments/$tournamentId/registrations" -Headers $adminHeaders -Body @{
    athleteId = $athleteB.Json.id
    weightKg = 31.3
    licenseConfirmed = $false
}

Invoke-Api -Method POST -Url "$apiBaseUrl/tournaments/$tournamentId/registrations/$($regA.Json.id)/category" -Headers $adminHeaders -Body @{ categoryId = $categoryId } | Out-Null
Invoke-Api -Method POST -Url "$apiBaseUrl/tournaments/$tournamentId/registrations/$($regB.Json.id)/category" -Headers $adminHeaders -Body @{ categoryId = $categoryId } | Out-Null
Invoke-Api -Method POST -Url "$apiBaseUrl/tournaments/$tournamentId/categories/$categoryId/draw" -Headers $adminHeaders -Body @{ format = "SingleElimination" } | Out-Null

$fightsResponse = Invoke-Api -Method GET -Url "$apiBaseUrl/tournaments/$tournamentId/categories/$categoryId/fights" -Headers $adminHeaders
$fight = @($fightsResponse.Json | Where-Object { -not $_.isBye -and $_.status -eq "Pending" -and $_.whiteAthleteId -and $_.blueAthleteId }) | Select-Object -First 1
Assert-True -Condition ($null -ne $fight) -Message "No startable fight found for LAN validation."
$fightId = $fight.id

Invoke-Api -Method POST -Url "$apiBaseUrl/tournaments/$tournamentId/fights/$fightId/assign-tatami" -Headers $adminHeaders -Body @{ tatamiId = $tatamiId } | Out-Null

Write-Host "[4/9] Warm up display client polling..." -ForegroundColor Yellow
$initialQueue = Invoke-Api -Method GET -Url "$apiBaseUrl/tournaments/$tournamentId/tatamis/$tatamiId/queue" -Headers $displayHeaders
Assert-True -Condition $initialQueue.Ok -Message "Display client queue warm-up failed."

Write-Host "[5/9] Measure propagation: start fight (operator -> display queue)..." -ForegroundColor Yellow
$startTrigger = [System.Diagnostics.Stopwatch]::StartNew()
$startFightResponse = Invoke-Api -Method POST -Url "$apiBaseUrl/tournaments/$tournamentId/fights/$fightId/start" -Headers $operatorHeaders -AllowError
$startTrigger.Stop()
Assert-True -Condition ($startFightResponse.StatusCode -eq 204) -Message "Start fight failed with status $($startFightResponse.StatusCode)."

$startPropagation = Wait-ForCondition -TimeoutMilliseconds $TimeoutMs -IntervalMilliseconds $PollIntervalMs -Probe {
    Invoke-Api -Method GET -Url "$apiBaseUrl/tournaments/$tournamentId/tatamis/$tatamiId/queue" -Headers $displayHeaders
} -Predicate {
    param($result)
    if (-not $result.Ok -or $null -eq $result.Json.current) {
        return $false
    }

    return $result.Json.current.id -eq $fightId -and $result.Json.current.status -eq "InProgress"
}

Assert-True -Condition $startPropagation.Matched -Message "Display client did not observe started fight within timeout."

Write-Host "[6/9] Measure propagation: score adjust (operator -> display fights)..." -ForegroundColor Yellow
$scoreAdjust = Invoke-Api -Method POST -Url "$apiBaseUrl/tournaments/$tournamentId/fights/$fightId/score/adjust" -Headers $operatorHeaders -Body @{
    side = "white"
    scoreType = "Yuko"
    delta = 1
} -AllowError
Assert-True -Condition ($scoreAdjust.StatusCode -eq 204) -Message "Score adjust failed with status $($scoreAdjust.StatusCode)."

$scorePropagation = Wait-ForCondition -TimeoutMilliseconds $TimeoutMs -IntervalMilliseconds $PollIntervalMs -Probe {
    Invoke-Api -Method GET -Url "$apiBaseUrl/tournaments/$tournamentId/categories/$categoryId/fights" -Headers $displayHeaders
} -Predicate {
    param($result)
    if (-not $result.Ok) {
        return $false
    }

    $target = @($result.Json | Where-Object { $_.id -eq $fightId }) | Select-Object -First 1
    if ($null -eq $target) {
        return $false
    }

    return [int]$target.whiteScore -ge 1 -or [int]$target.whiteYukoCount -ge 1
}

Assert-True -Condition $scorePropagation.Matched -Message "Display client did not observe score update within timeout."

Write-Host "[7/9] Evaluate thresholds..." -ForegroundColor Yellow
$startLatencyMs = [int]$startPropagation.ElapsedMs
$scoreLatencyMs = [int]$scorePropagation.ElapsedMs
$maxLatencyMs = [Math]::Max($startLatencyMs, $scoreLatencyMs)
$thresholdPassed = $maxLatencyMs -le $LatencyThresholdMs

Write-Host "Start propagation latency: $startLatencyMs ms" -ForegroundColor DarkGray
Write-Host "Score propagation latency: $scoreLatencyMs ms" -ForegroundColor DarkGray
Write-Host "Max latency: $maxLatencyMs ms (threshold: $LatencyThresholdMs ms)" -ForegroundColor DarkGray

Write-Host "[8/9] Write report..." -ForegroundColor Yellow
$report = [ordered]@{
    timestampUtc = [DateTimeOffset]::UtcNow.ToString("o")
    baseUrl = $BaseUrl
    tournamentId = $tournamentId
    clients = @{
        admin = $AdminUser
        operator = $operatorUser
        display = $displayUser
    }
    thresholds = @{
        latencyThresholdMs = $LatencyThresholdMs
        pollIntervalMs = $PollIntervalMs
        timeoutMs = $TimeoutMs
    }
    metrics = @{
        startFightPropagationMs = $startLatencyMs
        scorePropagationMs = $scoreLatencyMs
        maxPropagationMs = $maxLatencyMs
    }
    passed = $thresholdPassed
}

$reportFile = Join-Path $PSScriptRoot ("lan-validation-report-{0}.json" -f $stamp)
$report | ConvertTo-Json -Depth 10 | Set-Content -Path $reportFile -Encoding UTF8

Write-Host "[9/9] Completed." -ForegroundColor Green
Write-Host "Report: $reportFile" -ForegroundColor DarkGray

if (-not $thresholdPassed) {
    Write-Error "LAN validation exceeded threshold ($LatencyThresholdMs ms)."
    exit 1
}

Write-Host "LAN validation PASSED." -ForegroundColor Green
