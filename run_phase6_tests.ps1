# Automated Test Script for UniGroup CRM API Phase 6 Endpoints
# (Chatwoot Webhooks HMAC + Idempotency, CSAT Loop, Audit Trail, Notification Logs)

$ErrorActionPreference = "Stop"

# Configurations
$BaseUrl = "http://localhost:5112"
$WorkingDir = $PSScriptRoot
$ApiDir = Join-Path $WorkingDir "src/UniGroup.CRM.API"
$WebhookSecret = "unigroup-chatwoot-webhook-secret-2026"

# Cross-platform helpers (Windows PowerShell 5.1 / pwsh 7 on Linux & macOS)
$script:OnWindows = ($PSVersionTable.PSVersion.Major -lt 6) -or $IsWindows

function Stop-PortProcesses($port) {
    if ($script:OnWindows) {
        $conns = Get-NetTCPConnection -LocalPort $port -ErrorAction SilentlyContinue
        foreach ($conn in $conns) {
            if ($conn.OwningProcess) {
                Stop-Process -Id $conn.OwningProcess -Force -ErrorAction SilentlyContinue
            }
        }
        return [bool]$conns
    } else {
        $procIds = & lsof -t -i ":$port" 2>$null
        foreach ($procId in $procIds) {
            if ($procId) { Stop-Process -Id $procId -Force -ErrorAction SilentlyContinue }
        }
        return [bool]$procIds
    }
}

function Stop-ApiProcessTree($process) {
    if (-not $process) { return }
    if ($script:OnWindows) {
        taskkill /F /T /PID $process.Id > $null 2>&1
    } else {
        & pkill -9 -P $process.Id 2>$null
        Stop-Process -Id $process.Id -Force -ErrorAction SilentlyContinue
    }
}

function Get-HmacSignature($body, $secret) {
    $hmac = [System.Security.Cryptography.HMACSHA256]::new([System.Text.Encoding]::UTF8.GetBytes($secret))
    try {
        return [Convert]::ToBase64String($hmac.ComputeHash([System.Text.Encoding]::UTF8.GetBytes($body)))
    } finally {
        $hmac.Dispose()
    }
}

Write-Host "=========================================================" -ForegroundColor Cyan
Write-Host "   Starting UniGroup CRM API Phase 6 Automated Tests     " -ForegroundColor Cyan
Write-Host "=========================================================" -ForegroundColor Cyan

# Step 0: Ensure no process is already listening on port 5112
Write-Host "Checking for existing processes on port 5112..." -ForegroundColor Yellow
if (Stop-PortProcesses 5112) {
    Write-Host "Found existing process on port 5112. Terminated." -ForegroundColor Yellow
    Start-Sleep -Seconds 2
}

# Step 1: Start the API server in Development environment
Write-Host "Starting API server in background..." -ForegroundColor Yellow
$stdoutLog = Join-Path $WorkingDir "api_stdout.log"
$stderrLog = Join-Path $WorkingDir "api_stderr.log"

if (Test-Path $stdoutLog) { Remove-Item $stdoutLog -Force }
if (Test-Path $stderrLog) { Remove-Item $stderrLog -Force }

$apiProcess = Start-Process dotnet -ArgumentList "run --project `"$ApiDir`" --launch-profile http -- --seed" `
    -PassThru -WorkingDirectory $WorkingDir `
    -RedirectStandardOutput $stdoutLog -RedirectStandardError $stderrLog

Write-Host "API server process started with PID: $($apiProcess.Id)" -ForegroundColor Green

# Wait for server to start up and listen
$maxRetries = 20
$ready = $false
Write-Host "Waiting for API to become ready..." -ForegroundColor Yellow
for ($i = 1; $i -le $maxRetries; $i++) {
    try {
        $response = Invoke-WebRequest -Uri "$BaseUrl/api/auth/login" -Method Post -Body '{"email":"testuser@unigroup.com","password":"Password123!"}' -ContentType "application/json" -ErrorAction SilentlyContinue
        if ($response.StatusCode -eq 200) {
            $ready = $true
            break
        }
    } catch {
        # Server not ready yet
    }
    Start-Sleep -Seconds 1
}

if (-not $ready) {
    Write-Host "Error: API server did not become ready in time. Logs:" -ForegroundColor Red
    if (Test-Path $stdoutLog) { Get-Content $stdoutLog -Tail 20 | Write-Host -ForegroundColor Gray }
    if (Test-Path $stderrLog) { Get-Content $stderrLog -Tail 20 | Write-Host -ForegroundColor Red }
    Stop-ApiProcessTree $apiProcess
    Stop-PortProcesses 5112 | Out-Null
    exit 1
}

Write-Host "API is ready. Commencing Phase 6 test execution..." -ForegroundColor Green

# Test results array
$results = @()

function Add-TestResult($name, $expected, $actual, $details) {
    $status = if ("$expected" -eq "$actual") { "PASS" } else { "FAIL" }
    $color = if ($status -eq "PASS") { "Green" } else { "Red" }

    Write-Host "[ $status ] - $name (Expected: $expected, Actual: $actual)" -ForegroundColor $color
    if ($status -eq "FAIL" -and $global:LastResponseContent) {
        Write-Host "         Response Content: $global:LastResponseContent" -ForegroundColor Magenta
    }

    $global:results += [PSCustomObject]@{
        TestName     = $name
        ExpectedCode = "$expected"
        ActualCode   = "$actual"
        Status       = $status
        Details      = $details
    }
}

function Send-Request($method, $route, $body = $null, $headers = @{}, $contentType = "application/json") {
    $uri = "$BaseUrl$route"
    $statusCode = 0
    $content = ""

    try {
        $params = @{
            Uri = $uri
            Method = $method
            Headers = $headers
        }
        if ($body) {
            $params.Add("Body", $body)
        }
        if ($contentType) {
            $params.Add("ContentType", $contentType)
        }

        $response = Invoke-WebRequest @params -UseBasicParsing -TimeoutSec 60
        $statusCode = $response.StatusCode
        $content = $response.Content
    } catch {
        if ($_.Exception.Response) {
            $statusCode = [int]$_.Exception.Response.StatusCode
            if ($_.ErrorDetails -and $_.ErrorDetails.Message) {
                $content = $_.ErrorDetails.Message
            } elseif ($_.Exception.Response.PSObject.Methods['GetResponseStream']) {
                $reader = New-Object System.IO.StreamReader($_.Exception.Response.GetResponseStream())
                $content = $reader.ReadToEnd()
                $reader.Close()
            }
        } else {
            $statusCode = 500
            $content = $_.Exception.Message
        }
    }

    $global:LastResponseContent = $content
    return [PSCustomObject]@{
        StatusCode = $statusCode
        Content = $content
    }
}

# --- TEST CASE 1: Login ---
$loginBody = @{
    email = "testuser@unigroup.com"
    password = "Password123!"
} | ConvertTo-Json

$res1 = Send-Request -Method Post -Route "/api/auth/login" -Body $loginBody
$token = ""
if ($res1.StatusCode -eq 200) {
    $authObj = $res1.Content | ConvertFrom-Json
    $token = $authObj.token
}
Add-TestResult -name "T1: Login Authenticated" -expected 200 -actual $res1.StatusCode -details "Obtained JWT token"

$headers = @{
    "Authorization" = "Bearer $token"
}

# --- Prepare a unique Chatwoot webhook payload (raw JSON string, signed as-is) ---
$eventId = Get-Random -Minimum 700000 -Maximum 999999
$convId = Get-Random -Minimum 40000 -Maximum 99999
$webhookBody = "{`"event`":`"message_created`",`"id`":$eventId,`"message_type`":`"incoming`",`"content`":`"Hello, my phone screen is cracked!`",`"conversation`":{`"id`":$convId},`"sender`":{`"name`":`"Webhook Test Customer`",`"phone_number`":`"+201155566777`",`"email`":`"webhook.customer@example.com`"}}"
$validSignature = Get-HmacSignature $webhookBody $WebhookSecret

# Baseline: count tickets before webhook ingestion
$resBase = Send-Request -Method Get -Route "/api/tickets?page=1&pageSize=1" -headers $headers
$ticketCountBefore = -1
if ($resBase.StatusCode -eq 200) {
    $ticketCountBefore = ($resBase.Content | ConvertFrom-Json).totalCount
}

# --- TEST CASE 2: Webhook with VALID HMAC signature -> 202 Accepted ---
$whHeaders = @{ "X-Chatwoot-Signature" = $validSignature }
$res2 = Send-Request -Method Post -Route "/api/webhooks/chatwoot" -Body $webhookBody -headers $whHeaders
Add-TestResult -name "T2: Webhook valid HMAC signature" -expected 202 -actual $res2.StatusCode -details "Payload accepted and enqueued"

# --- TEST CASE 3: Webhook with INVALID HMAC signature -> 401 Unauthorized ---
$badHeaders = @{ "X-Chatwoot-Signature" = "aW52YWxpZHNpZ25hdHVyZQ==" }
$res3 = Send-Request -Method Post -Route "/api/webhooks/chatwoot" -Body $webhookBody -headers $badHeaders
Add-TestResult -name "T3: Webhook invalid HMAC signature" -expected 401 -actual $res3.StatusCode -details "Tampered signature rejected"

# --- TEST CASE 4: Webhook with MISSING signature header -> 400 Bad Request ---
$res4 = Send-Request -Method Post -Route "/api/webhooks/chatwoot" -Body $webhookBody
Add-TestResult -name "T4: Webhook missing signature header" -expected 400 -actual $res4.StatusCode -details "Requests without signature header rejected"

# Allow background channel consumer to process the first (valid) webhook
Write-Host "Waiting 5s for background webhook processing..." -ForegroundColor Yellow
Start-Sleep -Seconds 5

# --- TEST CASE 5: Webhook idempotency - duplicate payload creates exactly ONE ticket ---
$resMid = Send-Request -Method Get -Route "/api/tickets?page=1&pageSize=1" -headers $headers
$ticketCountAfterFirst = ($resMid.Content | ConvertFrom-Json).totalCount

# Re-send the exact same signed payload (same event id)
$resDup = Send-Request -Method Post -Route "/api/webhooks/chatwoot" -Body $webhookBody -headers $whHeaders
Start-Sleep -Seconds 5
$resAfter = Send-Request -Method Get -Route "/api/tickets?page=1&pageSize=1" -headers $headers
$ticketCountAfterDup = ($resAfter.Content | ConvertFrom-Json).totalCount

$idempotencyOk = ($ticketCountAfterFirst -eq ($ticketCountBefore + 1)) -and ($ticketCountAfterDup -eq $ticketCountAfterFirst) -and ($resDup.StatusCode -eq 202)
$global:LastResponseContent = "before=$ticketCountBefore afterFirst=$ticketCountAfterFirst afterDuplicate=$ticketCountAfterDup dupStatus=$($resDup.StatusCode)"
Add-TestResult -name "T5: Webhook idempotency (duplicate event id ignored)" -expected $true -actual $idempotencyOk -details "before=$ticketCountBefore afterFirst=$ticketCountAfterFirst afterDuplicate=$ticketCountAfterDup"

# --- TEST CASE 6: Full lifecycle -> Closed => CSAT survey auto-created ---
$createBody = @{
    customerId = "20df9317-2933-4260-bb53-7dc53ec14d64"
    customerDeviceId = "feca8d9a-d8d9-4849-9de8-c2804ec36788"
    title = "Phase 6 CSAT lifecycle test"
    description = "Ticket used to validate the automated CSAT feedback loop."
    category = 3
    priority = 1
} | ConvertTo-Json

$resCreate = Send-Request -Method Post -Route "/api/tickets" -Body $createBody -headers $headers
$csatTicketId = ""
if ($resCreate.StatusCode -eq 201) {
    # The endpoint returns the ticket id as a plain/JSON string, e.g. T-2026-00001 or "T-2026-00001"
    $csatTicketId = "$($resCreate.Content)".Trim().Trim('"')
}

# Walk the ticket through: New -> Open -> InProgress -> Resolved -> Closed
foreach ($status in @(1, 2, 6, 7)) {
    $transBody = @{ newStatus = $status; note = "Phase 6 automated transition" } | ConvertTo-Json
    $resTrans = Send-Request -Method Patch -Route "/api/tickets/$csatTicketId/status" -Body $transBody -headers $headers
    if ($resTrans.StatusCode -ne 200) {
        Write-Host "  Warning: transition to $status returned $($resTrans.StatusCode)" -ForegroundColor Yellow
    }
}

# Give the TicketClosedEventHandler a moment to persist the survey
Start-Sleep -Seconds 2

$resSurvey = Send-Request -Method Get -Route "/api/surveys/ticket/$csatTicketId" -headers $headers
$surveyToken = ""
$surveyCreated = $false
if ($resSurvey.StatusCode -eq 200) {
    $surveyObj = $resSurvey.Content | ConvertFrom-Json
    $surveyToken = $surveyObj.surveyToken
    $surveyCreated = -not [string]::IsNullOrEmpty($surveyToken)
}
Add-TestResult -name "T6: CSAT survey auto-created on ticket closure" -expected $true -actual $surveyCreated -details "Ticket $csatTicketId closed; survey token retrieved"

# --- TEST CASE 7: CSAT submit with VALID token -> 200 ---
$submitBody = @{ token = $surveyToken; rating = 5; feedback = "Excellent and fast service!" } | ConvertTo-Json
$res7 = Send-Request -Method Post -Route "/api/surveys/submit" -Body $submitBody
Add-TestResult -name "T7: CSAT submission with valid token" -expected 200 -actual $res7.StatusCode -details "Survey response recorded"

# --- TEST CASE 8: CSAT RE-submit same token -> 400 (single submission) ---
$res8 = Send-Request -Method Post -Route "/api/surveys/submit" -Body $submitBody
Add-TestResult -name "T8: CSAT resubmission rejected" -expected 400 -actual $res8.StatusCode -details "Second submission with same token blocked"

# --- TEST CASE 9: CSAT submit with EXPIRED token (seeded, sent 10 days ago) -> 400 ---
$expiredBody = @{ token = "expiredtoken000000000000000000000000000000000000000000000000fixed"; rating = 4; feedback = "too late" } | ConvertTo-Json
$res9 = Send-Request -Method Post -Route "/api/surveys/submit" -Body $expiredBody
$expiredOk = ($res9.StatusCode -eq 400) -and ($res9.Content -match "expired")
$global:LastResponseContent = $res9.Content
Add-TestResult -name "T9: CSAT expired token (7-day window) rejected" -expected $true -actual $expiredOk -details "Status=$($res9.StatusCode); message mentions expiration"

# --- TEST CASE 10: Audit trail automatically recorded ticket activity ---
Write-Host "Waiting 4s for audit log background flush..." -ForegroundColor Yellow
Start-Sleep -Seconds 4
$res10 = Send-Request -Method Get -Route "/api/audit-logs?tableName=Tickets&pageSize=5" -headers $headers
$auditOk = $false
if ($res10.StatusCode -eq 200) {
    $auditObj = $res10.Content | ConvertFrom-Json
    $auditOk = $auditObj.totalCount -gt 0
}
Add-TestResult -name "T10: Audit logs auto-created for ticket changes" -expected $true -actual $auditOk -details "GET /api/audit-logs?tableName=Tickets totalCount > 0"

# --- TEST CASE 11: Audit logs WITHOUT JWT -> 401 ---
$res11 = Send-Request -Method Get -Route "/api/audit-logs"
Add-TestResult -name "T11: Audit logs without JWT rejected" -expected 401 -actual $res11.StatusCode -details "Anonymous access to audit trail denied"

# --- TEST CASE 12: Notification log recorded the CSAT dispatch ---
$res12 = Send-Request -Method Get -Route "/api/notifications/logs?templateType=CsatSurvey" -headers $headers
$notifOk = $false
if ($res12.StatusCode -eq 200) {
    $notifObj = $res12.Content | ConvertFrom-Json
    $notifOk = $notifObj.totalCount -gt 0
}
Add-TestResult -name "T12: Notification log contains CSAT dispatch" -expected $true -actual $notifOk -details "GET /api/notifications/logs?templateType=CsatSurvey totalCount > 0"

# Step 4: Cleanup
Write-Host "Cleaning up API server process..." -ForegroundColor Yellow
if ($apiProcess) {
    Stop-ApiProcessTree $apiProcess
    Write-Host "API server process PID $($apiProcess.Id) terminated." -ForegroundColor Green
}
Stop-PortProcesses 5112 | Out-Null

# Write summary
Write-Host "=========================================================" -ForegroundColor Cyan
Write-Host "                     TEST SUMMARY                        " -ForegroundColor Cyan
Write-Host "=========================================================" -ForegroundColor Cyan
$failedCount = ($results | Where-Object { $_.Status -eq "FAIL" }).Count
$passedCount = ($results | Where-Object { $_.Status -eq "PASS" }).Count

Write-Host "Passed: $passedCount / $($results.Count)" -ForegroundColor Green
if ($failedCount -gt 0) {
    Write-Host "Failed: $failedCount / $($results.Count)" -ForegroundColor Red
} else {
    Write-Host "All tests passed successfully!" -ForegroundColor Green
}
Write-Host "=========================================================" -ForegroundColor Cyan

$results | Format-Table -AutoSize

$resultsJson = $results | ConvertTo-Json
Set-Content -Path (Join-Path $WorkingDir "phase6_test_results.json") -Value $resultsJson
