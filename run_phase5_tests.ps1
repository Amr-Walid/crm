# Automated Test Script for UniGroup CRM API Phase 5 Endpoints (Dashboards & Reports)
# Writes results to console and handles clean up of processes.

$ErrorActionPreference = "Stop"

# Configurations
$BaseUrl = "http://localhost:5112"
$WorkingDir = "c:\Users\SMART HOME\Documents\Uni-Group\crm customer service"
$ApiDir = "$WorkingDir\src\UniGroup.CRM.API"

Write-Host "=========================================================" -ForegroundColor Cyan
Write-Host "   Starting UniGroup CRM API Phase 5 Automated Tests     " -ForegroundColor Cyan
Write-Host "=========================================================" -ForegroundColor Cyan

# Step 0: Ensure no process is already listening on port 5112
Write-Host "Checking for existing processes on port 5112..." -ForegroundColor Yellow
$existingConn = Get-NetTCPConnection -LocalPort 5112 -ErrorAction SilentlyContinue
if ($existingConn) {
    Write-Host "Found existing process on port 5112. Terminating..." -ForegroundColor Yellow
    foreach ($conn in $existingConn) {
        if ($conn.OwningProcess) {
            Stop-Process -Id $conn.OwningProcess -Force -ErrorAction SilentlyContinue
        }
    }
    Start-Sleep -Seconds 2
}

# Step 1: Start the API server in Development environment
Write-Host "Starting API server in background..." -ForegroundColor Yellow
$stdoutLog = "$WorkingDir\api_stdout.log"
$stderrLog = "$WorkingDir\api_stderr.log"

# Clean previous logs
if (Test-Path $stdoutLog) { Remove-Item $stdoutLog -Force }
if (Test-Path $stderrLog) { Remove-Item $stderrLog -Force }

$apiProcess = Start-Process dotnet -ArgumentList "run --project `"$ApiDir`" --launch-profile http" `
    -PassThru -WorkingDirectory $WorkingDir `
    -RedirectStandardOutput $stdoutLog -RedirectStandardError $stderrLog

Write-Host "API server process started with PID: $($apiProcess.Id)" -ForegroundColor Green

# Wait for server to start up and listen
$maxRetries = 15
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
    
    # Clean up
    if ($apiProcess) {
        taskkill /F /T /PID $apiProcess.Id > $null 2>&1
    }
    exit 1
}

Write-Host "API is ready. Commencing Phase 5 test execution..." -ForegroundColor Green

# Test results array
$results = @()

function Add-TestResult($name, $expected, $actual, $details) {
    $status = if ($expected -eq $actual) { "PASS" } else { "FAIL" }
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
        
        $response = Invoke-WebRequest @params -UseBasicParsing
        $statusCode = $response.StatusCode
        $content = $response.Content
    } catch {
        if ($_.Exception.Response) {
            $statusCode = [int]$_.Exception.Response.StatusCode
            $reader = New-Object System.IO.StreamReader($_.Exception.Response.GetResponseStream())
            $content = $reader.ReadToEnd()
            $reader.Close()
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

# Setup Authorization headers
$headers = @{
    "Authorization" = "Bearer $token"
}

# --- TEST CASE 2: Get Dashboard Summary ---
$res2 = Send-Request -Method Get -Route "/api/dashboard/summary" -headers $headers
Add-TestResult -name "T2: Get Dashboard Summary (Admin/TL Role)" -expected 200 -actual $res2.StatusCode -details "Fetched dashboard metrics summary successfully"

# --- TEST CASE 3: Get Agent Performance ---
$res3 = Send-Request -Method Get -Route "/api/dashboard/agent-performance" -headers $headers
Add-TestResult -name "T3: Get Agent Performance (Admin/TL Role)" -expected 200 -actual $res3.StatusCode -details "Fetched agent performance list successfully"

# --- TEST CASE 4: Get Device Failure Report ---
$res4 = Send-Request -Method Get -Route "/api/dashboard/device-failures" -headers $headers
Add-TestResult -name "T4: Get Device Failure Report (Admin/TL Role)" -expected 200 -actual $res4.StatusCode -details "Fetched device failure report successfully"

# --- TEST CASE 5: Get Hourly Call Volume ---
$res5 = Send-Request -Method Get -Route "/api/dashboard/call-volume" -headers $headers
Add-TestResult -name "T5: Get Hourly Call Volume (Admin/TL Role)" -expected 200 -actual $res5.StatusCode -details "Fetched hourly call volume successfully"

# --- TEST CASE 6: Get Tickets By Status ---
$res6 = Send-Request -Method Get -Route "/api/dashboard/tickets-by-status" -headers $headers
Add-TestResult -name "T6: Get Tickets By Status (Admin/TL Role)" -expected 200 -actual $res6.StatusCode -details "Fetched tickets by status successfully"

# --- TEST CASE 7: Export Agent Report (Admin Role only) ---
$res7 = Send-Request -Method Get -Route "/api/reports/agents/export" -headers $headers
$isCsv = $false
if ($res7.StatusCode -eq 200 -and $global:LastResponseContent.Contains("AgentId") -and $global:LastResponseContent.Contains("AgentName")) {
    $isCsv = $true
}
Add-TestResult -name "T7: Export Agent Report CSV (Admin Role)" -expected $true -actual $isCsv -details "Fetched agent performance CSV export successfully"

# --- TEST CASE 8: Unauthorized Dashboard access (No token) ---
$res8 = Send-Request -Method Get -Route "/api/dashboard/summary"
Add-TestResult -name "T8: Get Dashboard Summary without JWT" -expected 401 -actual $res8.StatusCode -details "Access denied without authentication token"

# Step 4: Cleanup
Write-Host "Cleaning up API server process..." -ForegroundColor Yellow
if ($apiProcess) {
    taskkill /F /T /PID $apiProcess.Id > $null 2>&1
    Write-Host "API server process PID $($apiProcess.Id) terminated." -ForegroundColor Green
}

# Double check port 5112 is free
$remainingConn = Get-NetTCPConnection -LocalPort 5112 -ErrorAction SilentlyContinue
if ($remainingConn) {
    foreach ($conn in $remainingConn) {
        if ($conn.OwningProcess) {
            Stop-Process -Id $conn.OwningProcess -Force -ErrorAction SilentlyContinue
        }
    }
}

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

# Output results table
$results | Format-Table -AutoSize

# Export results to file
$resultsJson = $results | ConvertTo-Json
Set-Content -Path "$WorkingDir\phase5_test_results.json" -Value $resultsJson
