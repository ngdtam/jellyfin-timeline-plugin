# Quick API Test - Edit the variables below then run this script

# === EDIT THESE ===
$Server = "http://localhost:8096"
$Username = "admin"  # Change to your admin username
$Password = "admin1234"  # Change to your admin password
$ConfigFile = "D:\Script\jellyfin\tmdb-create-list\mcu.json"  # Path to your config file
# ==================

Write-Host "=== Timeline Manager API Test ===" -ForegroundColor Cyan
Write-Host ""

# Step 1: Test API connection
Write-Host "[1/4] Testing API connection..." -ForegroundColor Cyan
try {
    $testResult = Invoke-RestMethod -Uri "$Server/Timeline/Test" -Method Get
    Write-Host "✓ API is working!" -ForegroundColor Green
    Write-Host "  Status: $($testResult.status)" -ForegroundColor Gray
    Write-Host ""
} catch {
    Write-Host "✗ API test failed: $($_.Exception.Message)" -ForegroundColor Red
    exit 1
}

# Step 2: Login
Write-Host "[2/4] Logging in to Jellyfin..." -ForegroundColor Cyan
try {
    $loginBody = @{
        Username = $Username
        Pw = $Password
    } | ConvertTo-Json

    $session = Invoke-RestMethod -Uri "$Server/Users/authenticatebyname" -Method Post -Body $loginBody -ContentType "application/json"
    $headers = @{
        "X-Emby-Token" = $session.AccessToken
    }
    Write-Host "✓ Logged in as $Username" -ForegroundColor Green
    Write-Host ""
} catch {
    Write-Host "✗ Login failed: $($_.Exception.Message)" -ForegroundColor Red
    exit 1
}

# Step 3: Read current config
Write-Host "[3/4] Reading current configuration..." -ForegroundColor Cyan
try {
    $currentConfig = Invoke-RestMethod -Uri "$Server/Timeline/Config" -Method Get -Headers $headers
    if ($currentConfig.universes.Count -eq 0) {
        Write-Host "  No configuration found yet." -ForegroundColor Yellow
    } else {
        Write-Host "✓ Found $($currentConfig.universes.Count) universe(s)" -ForegroundColor Green
    }
    Write-Host ""
} catch {
    Write-Host "✗ Failed: $($_.Exception.Message)" -ForegroundColor Red
}

# Step 4: Validate config file
Write-Host "[4/4] Validating $ConfigFile..." -ForegroundColor Cyan
try {
    $jsonContent = Get-Content $ConfigFile -Raw
    
    $validateBody = @{
        jsonContent = $jsonContent
    } | ConvertTo-Json
    
    $validateResult = Invoke-RestMethod -Uri "$Server/Timeline/Validate" -Method Post -Body $validateBody -ContentType "application/json" -Headers $headers
    
    Write-Host ""
    if ($validateResult.isValid) {
        Write-Host "✓ VALIDATION PASSED!" -ForegroundColor Green
        Write-Host "  $($validateResult.message)" -ForegroundColor Green
    } else {
        Write-Host "✗ VALIDATION FAILED" -ForegroundColor Red
        Write-Host "  $($validateResult.message)" -ForegroundColor Yellow
        Write-Host ""
        Write-Host "Missing items:" -ForegroundColor Red
        foreach ($error in $validateResult.errors) {
            Write-Host "  $error" -ForegroundColor Red
        }
    }
} catch {
    Write-Host "✗ Validation failed: $($_.Exception.Message)" -ForegroundColor Red
}

Write-Host ""
Write-Host "=== Test Complete ===" -ForegroundColor Cyan
