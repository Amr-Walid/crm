# Automated Test Script for UniGroup CRM API Phase 7 (Knowledge Base & Call Flow Guidance)
# Cross-platform: Windows PowerShell 5.1 / pwsh 7 on Linux & macOS.
# On Linux, export Database__Provider=Sqlite before running (SQL Server unavailable).

$ErrorActionPreference = "Stop"

# Configurations
$BaseUrl = "http://localhost:5112"
$WorkingDir = $PSScriptRoot
$ApiDir = Join-Path $WorkingDir "src/UniGroup.CRM.API"

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

Write-Host "=========================================================" -ForegroundColor Cyan
Write-Host "   Starting UniGroup CRM API Phase 7 Automated Tests     " -ForegroundColor Cyan
Write-Host "   (Knowledge Base & Call Flow Guidance)                 " -ForegroundColor Cyan
Write-Host "=========================================================" -ForegroundColor Cyan

# Step 0: Ensure no process is already listening on port 5112
Write-Host "Checking for existing processes on port 5112..." -ForegroundColor Yellow
if (Stop-PortProcesses 5112) {
    Write-Host "Found existing process on port 5112. Terminated." -ForegroundColor Yellow
    Start-Sleep -Seconds 2
}

# Step 1: Seed the database in a separate short-lived process
$stdoutLog = Join-Path $WorkingDir "api_stdout.log"
$stderrLog = Join-Path $WorkingDir "api_stderr.log"

if (Test-Path $stdoutLog) { Remove-Item $stdoutLog -Force }
if (Test-Path $stderrLog) { Remove-Item $stderrLog -Force }

Write-Host "Seeding database..." -ForegroundColor Yellow
$seedProcess = Start-Process dotnet -ArgumentList "run --project `"$ApiDir`" --launch-profile http -- --seed" -Wait -NoNewWindow -PassThru
Write-Host "Seeding completed." -ForegroundColor Green

# Step 2: Start the API server in the background
Write-Host "Starting API server in background..." -ForegroundColor Yellow
$apiProcess = Start-Process dotnet -ArgumentList "run --project `"$ApiDir`" --launch-profile http" `
    -PassThru -WorkingDirectory $WorkingDir `
    -RedirectStandardOutput $stdoutLog -RedirectStandardError $stderrLog

Write-Host "API server process started with PID: $($apiProcess.Id)" -ForegroundColor Green

# Wait for server to start up and listen
$maxRetries = 45
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

Write-Host "API is ready. Commencing Phase 7 test execution..." -ForegroundColor Green

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
        ExpectedCode = $expected
        ActualCode   = $actual
        Status       = $status
        Details      = $details
    }
}

function Send-Request($method, $route, $body = $null, $headers = @{}, $contentType = "application/json") {
    $uri = "$BaseUrl$route"
    $statusCode = 0
    $content = ""
    try {
        $params = @{ Uri = $uri; Method = $method; Headers = $headers; UseBasicParsing = $true }
        if ($body) { $params.Add("Body", $body) }
        if ($contentType) { $params.Add("ContentType", $contentType) }
        $response = Invoke-WebRequest @params
        $statusCode = [int]$response.StatusCode
        $content = $response.Content
    } catch {
        if ($_.Exception.Response) {
            $statusCode = [int]$_.Exception.Response.StatusCode
            if ($_.ErrorDetails -and $_.ErrorDetails.Message) {
                $content = $_.ErrorDetails.Message
            } else {
                try {
                    $reader = New-Object System.IO.StreamReader($_.Exception.Response.GetResponseStream())
                    $content = $reader.ReadToEnd()
                    $reader.Close()
                } catch { $content = "" }
            }
        } else {
            $statusCode = 500
            $content = $_.Exception.Message
        }
    }
    $global:LastResponseContent = $content
    return [PSCustomObject]@{ StatusCode = $statusCode; Content = $content }
}

# ---------------------------------------------------------------------------
# Authentication: the seeded test user carries all roles including Admin.
# ---------------------------------------------------------------------------
$loginBody = @{ email = "testuser@unigroup.com"; password = "Password123!" } | ConvertTo-Json
$resLogin = Send-Request -method Post -route "/api/auth/login" -body $loginBody
if ($resLogin.StatusCode -ne 200) {
    Write-Host "Fatal: login failed ($($resLogin.StatusCode)). Cannot continue." -ForegroundColor Red
    Stop-ApiProcessTree $apiProcess
    Stop-PortProcesses 5112 | Out-Null
    exit 1
}
$token = ($resLogin.Content | ConvertFrom-Json).token
$authHeaders = @{ "Authorization" = "Bearer $token" }

# ---------------------------------------------------------------------------
# Test 1: Get seeded ACTIVE article by category (ScreenDamage = 0).
#         Exercises the EF Core 9 compiled query hot path.
# ---------------------------------------------------------------------------
$res1 = Send-Request -method Get -route "/api/knowledge-base/category/0" -headers $authHeaders
$t1Details = "Compiled-query category lookup"
if ($res1.StatusCode -eq 200) {
    $article1 = $res1.Content | ConvertFrom-Json
    $t1Details = "Got article: $($article1.title) [format=$($article1.contentFormat)]"
}
Add-TestResult -name "T1: Get Article by Category (ScreenDamage, compiled query)" -expected 200 -actual $res1.StatusCode -details $t1Details

# ---------------------------------------------------------------------------
# Test 2: Create a new article as Admin (SoftwareIssue = 3), Markdown content.
# ---------------------------------------------------------------------------
$newArticleBody = @{
    category             = 3 # SoftwareIssue
    title                = "Bootloop Troubleshooting Guidelines"
    questionsToAsk       = "- Does the phone reach the **home screen**?`n- Does it vibrate when bootlooping?"
    diagnosisSteps       = "1. Force reboot (power + volume down 10s).`n2. Hold volume keys to access the **recovery menu**."
    suggestedAnswers     = "> We will attempt to **reflash the software**. Any unsaved data may be lost."
    escalationConditions = "- Escalate if the bootloader is **locked** and the device does not boot."
    keywords             = "bootloop, restart, software, flash, recovery"
} | ConvertTo-Json
$res2 = Send-Request -method Post -route "/api/knowledge-base" -body $newArticleBody -headers $authHeaders
$newArticleId = ""
if ($res2.StatusCode -eq 201) { $newArticleId = $res2.Content.Trim('"') }
Add-TestResult -name "T2: Create Knowledge Base Article (Admin role)" -expected 201 -actual $res2.StatusCode -details "Created article: $newArticleId"

# ---------------------------------------------------------------------------
# Test 3: Create without JWT -> 401 (route admission blocks anonymous writes).
# ---------------------------------------------------------------------------
$res3 = Send-Request -method Post -route "/api/knowledge-base" -body $newArticleBody
Add-TestResult -name "T3: Create Article without JWT" -expected 401 -actual $res3.StatusCode -details "Anonymous write blocked"

# ---------------------------------------------------------------------------
# Test 3b: Validation guard — empty/whitespace content must be rejected (400).
# ---------------------------------------------------------------------------
$badArticleBody = @{
    category             = 5 # CameraIssue
    title                = "   "
    questionsToAsk       = ""
    diagnosisSteps       = "Only field provided"
    suggestedAnswers     = ""
    escalationConditions = ""
} | ConvertTo-Json
$res3b = Send-Request -method Post -route "/api/knowledge-base" -body $badArticleBody -headers $authHeaders
Add-TestResult -name "T3b: Reject empty/malformed article (validation guard)" -expected 400 -actual $res3b.StatusCode -details "Guard rejected whitespace-only content"

# ---------------------------------------------------------------------------
# Test 3c: Single-active-article-per-category invariant — a second ACTIVE
#          article for the same category (SoftwareIssue) must be rejected (400).
# ---------------------------------------------------------------------------
$dupArticleBody = @{
    category             = 3 # SoftwareIssue (already has the T2 active article)
    title                = "Duplicate active software guidance"
    questionsToAsk       = "- duplicate?"
    diagnosisSteps       = "1. duplicate"
    suggestedAnswers     = "> duplicate"
    escalationConditions = "- duplicate"
} | ConvertTo-Json
$res3c = Send-Request -method Post -route "/api/knowledge-base" -body $dupArticleBody -headers $authHeaders
Add-TestResult -name "T3c: Reject second ACTIVE article for same category" -expected 400 -actual $res3c.StatusCode -details "Unique active-per-category constraint enforced"

# ---------------------------------------------------------------------------
# Test 4: Paginated list with case-insensitive tokenized search.
#         "BOOTLOOP" (uppercase) must find the T2 article (stored lowercase).
# ---------------------------------------------------------------------------
$res4 = Send-Request -method Get -route "/api/knowledge-base?page=1&pageSize=5&search=BOOTLOOP" -headers $authHeaders
$t4Details = "Search executed"
$t4Expected = 200
$t4Actual = $res4.StatusCode
if ($res4.StatusCode -eq 200) {
    $listResult = $res4.Content | ConvertFrom-Json
    if ($listResult.totalCount -lt 1) {
        $t4Actual = "200-but-0-results"
        $t4Details = "Case-insensitive search returned no rows"
    } else {
        $t4Details = "Search 'BOOTLOOP' matched $($listResult.totalCount) article(s), totalPages=$($listResult.totalPages)"
    }
}
Add-TestResult -name "T4: Get Articles List (case-insensitive search)" -expected $t4Expected -actual $t4Actual -details $t4Details

# ---------------------------------------------------------------------------
# Test 4b: Update the T2 article (Admin) — change title, keep it active.
# ---------------------------------------------------------------------------
$updateBody = @{
    id                   = $newArticleId
    title                = "Bootloop & Boot Failure Troubleshooting Guidelines"
    questionsToAsk       = "- Does the phone reach the **home screen**?`n- Does it vibrate when bootlooping?"
    diagnosisSteps       = "1. Force reboot (power + volume down 10s).`n2. Hold volume keys to access the **recovery menu**.`n3. Check for pending OS updates in recovery."
    suggestedAnswers     = "> We will attempt to **reflash the software**. Any unsaved data may be lost."
    escalationConditions = "- Escalate if the bootloader is **locked** and the device does not boot."
    keywords             = "bootloop, restart, software, flash, recovery, boot failure"
    isActive             = $true
} | ConvertTo-Json
$res4b = Send-Request -method Put -route "/api/knowledge-base/$newArticleId" -body $updateBody -headers $authHeaders
Add-TestResult -name "T4b: Update Article (Admin)" -expected 200 -actual $res4b.StatusCode -details "Article updated with UpdatedAt stamp"

# ---------------------------------------------------------------------------
# Test 5: Delete the created article (Admin) -> 204.
# ---------------------------------------------------------------------------
$res5 = Send-Request -method Delete -route "/api/knowledge-base/$newArticleId" -headers $authHeaders
Add-TestResult -name "T5: Delete Article (Admin)" -expected 204 -actual $res5.StatusCode -details "Article deleted successfully"

# Cleanup
Write-Host "Stopping API server..." -ForegroundColor Yellow
Stop-ApiProcessTree $apiProcess
Stop-PortProcesses 5112 | Out-Null

# Summary
$failedCount = ($results | Where-Object { $_.Status -eq "FAIL" }).Count
$passedCount = ($results | Where-Object { $_.Status -eq "PASS" }).Count
Write-Host "=========================================================" -ForegroundColor Cyan
Write-Host "Passed: $passedCount / $($results.Count)" -ForegroundColor Green

$results | ConvertTo-Json | Out-File (Join-Path $WorkingDir "phase7_test_results.json")

if ($failedCount -gt 0) {
    Write-Host "Failed: $failedCount / $($results.Count)" -ForegroundColor Red
    exit 1
} else {
    Write-Host "All Phase 7 tests passed successfully!" -ForegroundColor Green
    exit 0
}
