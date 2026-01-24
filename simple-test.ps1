# Simple API Test Script
# Edit these variables:
$Server = "http://localhost:8096"
$Username = "admin"
$Password = "admin1234"
$ConfigFile = "configurations\mcu.json"

Write-Host "=== Timeline Manager API Test ===" -ForegroundColor Cyan
Write-Host ""

# Test 1: API Connection
Write-Host "[1/4] Testing API..." -ForegroundColor Cyan
$testResult = Invoke-RestMethod -Uri "$Server/Timeline/Test" -Method Get
Write-Host "SUCCESS: API is working!" -ForegroundColor Green
Write-Host ""

# Test 2: Login
Write-Host "[2/4] Logging in..." -ForegroundColor Cyan

$ErrorActionPreference = "Stop"

# Jellyfin requires an Authorization header with client info
$authHeader = 'MediaBrowser Client="Timeline Manager", Device="PowerShell Script", DeviceId="timeline-test-001", Version="0.3.0"'

$loginJson = @"
{
  "Username": "$Username",
  "Pw": "$Password"
}
"@

Write-Host "Login payload: $loginJson" -ForegroundColor Gray
Write-Host "Auth header: $authHeader" -ForegroundColor Gray

try {
    $loginHeaders = @{
        "Authorization" = $authHeader
    }
    
    $response = Invoke-WebRequest -Uri "$Server/Users/authenticatebyname" -Method Post -Body $loginJson -ContentType "application/json; charset=utf-8" -Headers $loginHeaders -UseBasicParsing
    $session = $response.Content | ConvertFrom-Json
    
    $headers = @{
        "X-Emby-Token" = $session.AccessToken
        "Authorization" = $authHeader
    }
    Write-Host "SUCCESS: Logged in as $Username" -ForegroundColor Green
    Write-Host "Token: $($session.AccessToken.Substring(0,20))..." -ForegroundColor Gray
    Write-Host ""
} catch {
    Write-Host "FAILED: Login failed" -ForegroundColor Red
    Write-Host "Status Code: $($_.Exception.Response.StatusCode.value__)" -ForegroundColor Red
    Write-Host "Error: $($_.Exception.Message)" -ForegroundColor Red
    
    # Try to get response body
    if ($_.Exception.Response) {
        $reader = New-Object System.IO.StreamReader($_.Exception.Response.GetResponseStream())
        $responseBody = $reader.ReadToEnd()
        Write-Host "Response: $responseBody" -ForegroundColor Yellow
    }
    
    Write-Host ""
    Write-Host "Troubleshooting:" -ForegroundColor Yellow
    Write-Host "  1. Verify username/password in Jellyfin web UI" -ForegroundColor Yellow
    Write-Host "  2. Check if user has admin privileges" -ForegroundColor Yellow
    Write-Host "  3. Try logging in via browser first" -ForegroundColor Yellow
    exit 1
}

# Test 3: Get Config
Write-Host "[3/4] Getting current config..." -ForegroundColor Cyan
try {
    $currentConfig = Invoke-RestMethod -Uri "$Server/Timeline/Config" -Method Get -Headers $headers
    Write-Host "SUCCESS: Config retrieved" -ForegroundColor Green
} catch {
    Write-Host "WARNING: Could not get config (might not exist yet)" -ForegroundColor Yellow
}
Write-Host ""

# Test 4: Validate
Write-Host "[4/4] Validating $ConfigFile..." -ForegroundColor Cyan

if (-not (Test-Path $ConfigFile)) {
    Write-Host "ERROR: Config file not found!" -ForegroundColor Red
    exit 1
}

try {
    $jsonContent = Get-Content $ConfigFile -Raw -Encoding UTF8
    
    Write-Host "  File size: $($jsonContent.Length) characters" -ForegroundColor Gray
    
    # Escape the JSON content properly
    $escapedJson = $jsonContent -replace '\\', '\\' -replace '"', '\"' -replace "`r`n", '\n' -replace "`n", '\n' -replace "`t", '\t'
    
    # Build the request body
    $requestBody = "{`"jsonContent`":`"$escapedJson`"}"
    
    Write-Host "  Request body size: $($requestBody.Length) characters" -ForegroundColor Gray
    Write-Host "  Sending validation request (timeout: 120 seconds)..." -ForegroundColor Gray
    
    $validateResult = Invoke-RestMethod -Uri "$Server/Timeline/Validate" -Method Post -Body $requestBody -ContentType "application/json; charset=utf-8" -Headers $headers -TimeoutSec 120
    
    Write-Host ""
    if ($validateResult.isValid) {
        Write-Host "SUCCESS: VALIDATION PASSED!" -ForegroundColor Green
        Write-Host $validateResult.message -ForegroundColor Green
        Write-Host ""
        Write-Host "Items Found in Library:" -ForegroundColor Green
        foreach ($item in $validateResult.foundItems) {
            Write-Host "  $item" -ForegroundColor Green
        }
    } else {
        Write-Host "FAILED: VALIDATION FAILED" -ForegroundColor Red
        Write-Host $validateResult.message -ForegroundColor Yellow
        Write-Host ""
        
        if ($validateResult.foundItems -and $validateResult.foundItems.Count -gt 0) {
            Write-Host "Items Found in Library ($($validateResult.foundItems.Count)):" -ForegroundColor Green
            foreach ($item in $validateResult.foundItems) {
                $cleanItem = $item -replace '^\[FOUND\]\s*', ''
                Write-Host "  $cleanItem" -ForegroundColor Green
            }
            Write-Host ""
        }
        
        if ($validateResult.errors -and $validateResult.errors.Count -gt 0) {
            Write-Host "Items Missing from Library ($($validateResult.errors.Count)):" -ForegroundColor Red
            foreach ($error in $validateResult.errors) {
                $cleanError = $error -replace '^\[MISSING\]\s*', ''
                Write-Host "  $cleanError" -ForegroundColor Red
            }
        }
    }
} catch {
    Write-Host "ERROR: Validation failed" -ForegroundColor Red
    Write-Host "Error: $($_.Exception.Message)" -ForegroundColor Red
    if ($_.Exception.Response) {
        Write-Host "Status: $($_.Exception.Response.StatusCode)" -ForegroundColor Yellow
        
        # Try to read error details
        try {
            $reader = New-Object System.IO.StreamReader($_.Exception.Response.GetResponseStream())
            $responseBody = $reader.ReadToEnd()
            Write-Host "Response: $responseBody" -ForegroundColor Yellow
        } catch {}
    }
}

Write-Host ""
Write-Host "=== Test Complete ===" -ForegroundColor Cyan
