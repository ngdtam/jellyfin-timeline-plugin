# Timeline Manager API Test Script
# This script tests your configuration file with the Jellyfin API

param(
    [string]$Server = "http://localhost:8096",
    [string]$Username = "",
    [string]$Password = "",
    [string]$ConfigFile = ""
)

Write-Host "=== Timeline Manager API Test ===" -ForegroundColor Cyan
Write-Host ""

# Get credentials if not provided
if ([string]::IsNullOrEmpty($Username)) {
    $Username = Read-Host "Enter your Jellyfin admin username"
}

if ([string]::IsNullOrEmpty($Password)) {
    $SecurePassword = Read-Host "Enter your Jellyfin admin password" -AsSecureString
    $BSTR = [System.Runtime.InteropServices.Marshal]::SecureStringToBSTR($SecurePassword)
    $Password = [System.Runtime.InteropServices.Marshal]::PtrToStringAuto($BSTR)
}

# Get config file path if not provided
if ([string]::IsNullOrEmpty($ConfigFile)) {
    Write-Host ""
    Write-Host "Where is your config file?" -ForegroundColor Yellow
    Write-Host "1. Docker: Use default Docker path"
    Write-Host "2. Windows: Use default Windows path"
    Write-Host "3. Custom: Enter custom path"
    $choice = Read-Host "Choose (1/2/3)"
    
    switch ($choice) {
        "1" { 
            $containerName = Read-Host "Enter Docker container name (default: jellyfin-win)"
            if ([string]::IsNullOrEmpty($containerName)) { $containerName = "jellyfin-win" }
            Write-Host "Copying config from Docker container..." -ForegroundColor Yellow
            docker cp "${containerName}:/config/timeline_manager_config.json" "./temp_config.json"
            $ConfigFile = "./temp_config.json"
        }
        "2" { 
            $ConfigFile = "$env:ProgramData\Jellyfin\Server\config\timeline_manager_config.json"
        }
        "3" { 
            $ConfigFile = Read-Host "Enter full path to config file"
        }
    }
}

# Check if config file exists
if (-not (Test-Path $ConfigFile)) {
    Write-Host "ERROR: Config file not found at: $ConfigFile" -ForegroundColor Red
    exit 1
}

Write-Host ""
Write-Host "Using config file: $ConfigFile" -ForegroundColor Green
Write-Host ""

# Step 1: Test API connection (no auth needed)
Write-Host "[1/4] Testing API connection..." -ForegroundColor Cyan
try {
    $testResult = Invoke-RestMethod -Uri "$Server/Timeline/Test" -Method Get
    Write-Host "✓ API is working!" -ForegroundColor Green
    Write-Host "  Status: $($testResult.status)" -ForegroundColor Gray
    Write-Host ""
} catch {
    Write-Host "✗ API test failed: $($_.Exception.Message)" -ForegroundColor Red
    Write-Host "  Make sure the plugin is installed and Jellyfin is running." -ForegroundColor Yellow
    exit 1
}

# Step 2: Login to Jellyfin
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
    Write-Host "✓ Logged in successfully as $Username" -ForegroundColor Green
    Write-Host ""
} catch {
    Write-Host "✗ Login failed: $($_.Exception.Message)" -ForegroundColor Red
    Write-Host "  Check your username and password." -ForegroundColor Yellow
    exit 1
}

# Step 3: Read current config from API
Write-Host "[3/4] Reading current configuration from Jellyfin..." -ForegroundColor Cyan
try {
    $currentConfig = Invoke-RestMethod -Uri "$Server/Timeline/Config" -Method Get -Headers $headers
    if ($currentConfig.universes.Count -eq 0) {
        Write-Host "  No configuration found in Jellyfin yet." -ForegroundColor Yellow
    } else {
        Write-Host "✓ Found $($currentConfig.universes.Count) universe(s) configured" -ForegroundColor Green
    }
    Write-Host ""
} catch {
    Write-Host "⚠ Could not read config (this is OK if no config exists yet)" -ForegroundColor Yellow
    Write-Host "  Error: $($_.Exception.Message)" -ForegroundColor Gray
    Write-Host ""
}

# Step 4: Validate the JSON file
Write-Host "[4/4] Validating your configuration file..." -ForegroundColor Cyan
try {
    # Read the JSON file
    $jsonContent = Get-Content $ConfigFile -Raw
    
    # Parse to verify it's valid JSON
    $null = $jsonContent | ConvertFrom-Json
    
    # Create the request body
    $validateBody = @{
        jsonContent = $jsonContent
    } | ConvertTo-Json -Depth 10
    
    Write-Host "  Sending validation request..." -ForegroundColor Gray
    
    # Call the validate endpoint
    $validateResult = Invoke-RestMethod -Uri "$Server/Timeline/Validate" -Method Post -Body $validateBody -ContentType "application/json; charset=utf-8" -Headers $headers
    
    Write-Host ""
    if ($validateResult.isValid) {
        Write-Host "✓ VALIDATION PASSED!" -ForegroundColor Green
        Write-Host "  $($validateResult.message)" -ForegroundColor Green
    } else {
        Write-Host "✗ VALIDATION FAILED" -ForegroundColor Red
        Write-Host "  $($validateResult.message)" -ForegroundColor Yellow
        Write-Host ""
        Write-Host "Errors found:" -ForegroundColor Red
        foreach ($error in $validateResult.errors) {
            Write-Host "  - $error" -ForegroundColor Red
        }
    }
} catch {
    Write-Host "✗ Validation failed: $($_.Exception.Message)" -ForegroundColor Red
    if ($_.ErrorDetails.Message) {
        try {
            $errorObj = $_.ErrorDetails.Message | ConvertFrom-Json
            Write-Host "  Details: $($errorObj.message)" -ForegroundColor Yellow
        } catch {
            Write-Host "  Details: $($_.ErrorDetails.Message)" -ForegroundColor Yellow
        }
    }
    Write-Host ""
    Write-Host "Debug info:" -ForegroundColor Gray
    Write-Host "  Server: $Server" -ForegroundColor Gray
    Write-Host "  Endpoint: $Server/Timeline/Validate" -ForegroundColor Gray
    Write-Host "  Auth token present: $($headers.'X-Emby-Token' -ne $null)" -ForegroundColor Gray
}

Write-Host ""
Write-Host "=== Test Complete ===" -ForegroundColor Cyan

# Cleanup temp file if created
if ($ConfigFile -eq "./temp_config.json" -and (Test-Path "./temp_config.json")) {
    Remove-Item "./temp_config.json" -Force
}
