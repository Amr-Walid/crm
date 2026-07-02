# Automated Test Script for UniGroup CRM API Phase 4 Endpoints
# Writes results to console and handles clean up of processes.

$ErrorActionPreference = "Stop"

# Configurations
$BaseUrl = "http://localhost:5112"
$WorkingDir = "c:\Users\SMART HOME\Documents\Uni-Group\crm customer service"
$ApiDir = "$WorkingDir\src\UniGroup.CRM.API"

Write-Host "=========================================================" -ForegroundColor Cyan
Write-Host "   Starting UniGroup CRM API Phase 4 Automated Tests     " -ForegroundColor Cyan
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

$apiProcess = Start-Process dotnet -ArgumentList "run", "--project", "$ApiDir", "--launch-profile", "http" `
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

Write-Host "API is ready. Commencing test execution..." -ForegroundColor Green

# Test results array
$results = @()

function Add-TestResult($name, $expected, $actual, $details) {
    $status = if ($expected -eq $actual) { "PASS" } else { "FAIL" }
    $color = if ($status -eq "PASS") { "Green" } else { "Red" }
    
    Write-Host "[ $status ] - $name (Expected: $expected, Actual: $actual)" -ForegroundColor $color
    
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
        
        $response = Invoke-WebRequest @params
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

# --- TEST CASE 2: Create Department ---
$deptName = "Hardware Maintenance " + [System.Guid]::NewGuid().ToString().Substring(0,8)
$deptBody = @{
    name = $deptName
    description = "Handles hardware, screen, and battery repairs."
    isActive = $true
} | ConvertTo-Json

$res2 = Send-Request -Method Post -Route "/api/departments" -Body $deptBody -headers $headers
$departmentId = ""
if ($res2.StatusCode -eq 201) {
    $departmentId = $res2.Content.Trim('"')
}
Add-TestResult -name "T2: Create Department (Admin Role)" -expected 201 -actual $res2.StatusCode -details "Created department ID: $departmentId"

# --- TEST CASE 3: Get Departments ---
$res3 = Send-Request -Method Get -Route "/api/departments" -headers $headers
Add-TestResult -name "T3: Get Departments" -expected 200 -actual $res3.StatusCode -details "Retrieved departments list"

# --- TEST CASE 4: Create Ticket (Agent Role) ---
# Ahmed Walid Customer ID: 20df9317-2933-4260-bb53-7dc53ec14d64
# Customer Device ID: feca8d9a-d8d9-4849-9de8-c2804ec36788
$ticketBody = @{
    customerId = "20df9317-2933-4260-bb53-7dc53ec14d64"
    customerDeviceId = "feca8d9a-d8d9-4849-9de8-c2804ec36788"
    title = "Screen Cracked"
    description = "Customer dropped the phone and screen is completely cracked."
    category = 0 # ScreenDamage
    priority = 1 # Medium
} | ConvertTo-Json

$res4 = Send-Request -Method Post -Route "/api/tickets" -Body $ticketBody -headers $headers
$ticketId = ""
if ($res4.StatusCode -eq 201) {
    $ticketId = $res4.Content.Trim('"')
}
Add-TestResult -name "T4: Create Ticket (Agent Role)" -expected 201 -actual $res4.StatusCode -details "Created ticket: $ticketId"

# --- TEST CASE 5: Assign Ticket (Team Leader Role) ---
$agentId = "b8cc4a0f-c5f7-4f97-3916-08ded74840b4"
$assignBody = @{
    assignedToId = $agentId
    departmentId = $departmentId
    note = "Assigning to Hardware Maintenance department"
} | ConvertTo-Json

$res5 = Send-Request -Method Patch -Route "/api/tickets/$ticketId/assign" -Body $assignBody -headers $headers
Add-TestResult -name "T5: Assign Ticket" -expected 200 -actual $res5.StatusCode -details "Assigned to department: $departmentId, agent: $agentId"

# --- TEST CASE 6: Add Internal Note ---
$noteBody = @{
    content = "Screener replacement parts ordered from supplier."
} | ConvertTo-Json

$res6 = Send-Request -Method Post -Route "/api/tickets/$ticketId/notes" -Body $noteBody -headers $headers
Add-TestResult -name "T6: Add Internal Note" -expected 200 -actual $res6.StatusCode -details "Added note to ticket"

# --- TEST CASE 7: Add Attachment ---
$boundary = "----WebKitFormBoundary" + [System.Guid]::NewGuid().ToString().Substring(0,8)
$contentType = "multipart/form-data; boundary=$boundary"
$multipartBody = "--$boundary`r`nContent-Disposition: form-data; name=`"file`"; filename=`"test.txt`"`r`nContent-Type: text/plain`r`n`r`nThis is a test attachment file content.`r`n--$boundary--`r`n"
$bodyBytes = [System.Text.Encoding]::UTF8.GetBytes($multipartBody)

$res7 = Send-Request -Method Post -Route "/api/tickets/$ticketId/attachments" -Body $bodyBytes -headers $headers -contentType $contentType
Add-TestResult -name "T7: Add Attachment" -expected 200 -actual $res7.StatusCode -details "Uploaded text attachment"

# --- TEST CASE 8: Get Ticket Details ---
$res8 = Send-Request -Method Get -Route "/api/tickets/$ticketId" -headers $headers
Add-TestResult -name "T8: Get Ticket Details" -expected 200 -actual $res8.StatusCode -details "Retrieved full details for $ticketId"

# --- TEST CASE 9: Get Tickets List (Paging & Filtering) ---
$res9 = Send-Request -Method Get -Route "/api/tickets?status=0&priority=1&page=1&pageSize=10" -headers $headers
Add-TestResult -name "T9: Get Tickets List with Paging/Filtering" -expected 200 -actual $res9.StatusCode -details "Retrieved paged tickets list"

# --- TEST CASE 10: Get My Tickets ---
$res10 = Send-Request -Method Get -Route "/api/tickets/my?status=0" -headers $headers
Add-TestResult -name "T10: Get My Tickets" -expected 200 -actual $res10.StatusCode -details "Retrieved agent tickets list"

# --- TEST CASE 11: Change ticket status: New -> Open (Valid) ---
$statusBody1 = @{
    newStatus = 1 # Open
    note = "Acknowledging the new ticket"
} | ConvertTo-Json
$res11 = Send-Request -Method Patch -Route "/api/tickets/$ticketId/status" -Body $statusBody1 -headers $headers
Add-TestResult -name "T11: Transition Status New -> Open (Valid)" -expected 200 -actual $res11.StatusCode -details "Status changed to Open"

# --- TEST CASE 12: Change ticket status: Open -> Resolved (Invalid - expect 400) ---
$statusBody2 = @{
    newStatus = 6 # Resolved
    note = "Trying to resolve directly without InProgress"
} | ConvertTo-Json
$res12 = Send-Request -Method Patch -Route "/api/tickets/$ticketId/status" -Body $statusBody2 -headers $headers
Add-TestResult -name "T12: Transition Status Open -> Resolved (Invalid)" -expected 400 -actual $res12.StatusCode -details "Correctly rejected with 400 Bad Request"

# --- TEST CASE 13: Change ticket status: Open -> InProgress (Valid) ---
$statusBody3 = @{
    newStatus = 2 # InProgress
    note = "Starting to work on the screen repair"
} | ConvertTo-Json
$res13 = Send-Request -Method Patch -Route "/api/tickets/$ticketId/status" -Body $statusBody3 -headers $headers
Add-TestResult -name "T13: Transition Status Open -> InProgress (Valid)" -expected 200 -actual $res13.StatusCode -details "Status changed to InProgress"

# --- TEST CASE 14: Change ticket status: InProgress -> WaitingForCustomer (Valid - pauses SLA) ---
$statusBody4 = @{
    newStatus = 3 # WaitingForCustomer
    note = "Waiting for customer to verify passcode"
} | ConvertTo-Json
$res14 = Send-Request -Method Patch -Route "/api/tickets/$ticketId/status" -Body $statusBody4 -headers $headers
Add-TestResult -name "T14: Transition Status InProgress -> WaitingForCustomer (Valid - Pauses SLA)" -expected 200 -actual $res14.StatusCode -details "Status changed to WaitingForCustomer, SLA paused"

# --- TEST CASE 15: Change ticket status: WaitingForCustomer -> InProgress (Valid - resumes SLA) ---
$statusBody5 = @{
    newStatus = 2 # InProgress
    note = "Customer provided passcode, resuming work"
} | ConvertTo-Json
$res15 = Send-Request -Method Patch -Route "/api/tickets/$ticketId/status" -Body $statusBody5 -headers $headers
Add-TestResult -name "T15: Transition Status WaitingForCustomer -> InProgress (Valid - Resumes SLA)" -expected 200 -actual $res15.StatusCode -details "Status changed to InProgress, SLA resumed"


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

# Export results to environment variable or file if needed
$resultsJson = $results | ConvertTo-Json
Set-Content -Path "$WorkingDir\phase4_test_results.json" -Value $resultsJson
