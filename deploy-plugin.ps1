# Jellyfin Universal Timeline Manager Plugin Deployment Script
# This script builds and deploys the plugin to a Jellyfin server

param(
    [Parameter(Mandatory=$false)]
    [string]$JellyfinPluginsPath = "",
    
    [Parameter(Mandatory=$false)]
    [switch]$BuildOnly = $false,
    
    [Parameter(Mandatory=$false)]
    [switch]$Clean = $false
)

Write-Host "Jellyfin Universal Timeline Manager Plugin Deployment" -ForegroundColor Green
Write-Host "=================================================" -ForegroundColor Green

# Set default Jellyfin plugins path if not provided
if ([string]::IsNullOrEmpty($JellyfinPluginsPath)) {
    if ($IsWindows -or $env:OS -eq "Windows_NT") {
        $JellyfinPluginsPath = "$env:ProgramData\Jellyfin\Server\plugins"
    } elseif ($IsLinux) {
        $JellyfinPluginsPath = "/var/lib/jellyfin/plugins"
    } else {
        Write-Error "Unable to determine default Jellyfin plugins path. Please specify -JellyfinPluginsPath parameter."
        exit 1
    }
}

Write-Host "Target Jellyfin plugins directory: $JellyfinPluginsPath" -ForegroundColor Yellow

# Clean previous builds if requested
if ($Clean) {
    Write-Host "Cleaning previous builds..." -ForegroundColor Yellow
    dotnet clean --configuration Release --verbosity minimal
    if ($LASTEXITCODE -ne 0) {
        Write-Error "Clean failed"
        exit 1
    }
}

# Build the plugin
Write-Host "Building Universal Timeline Manager plugin..." -ForegroundColor Yellow
dotnet build --configuration Release --verbosity minimal

if ($LASTEXITCODE -ne 0) {
    Write-Error "Build failed"
    exit 1
}

Write-Host "Build completed successfully!" -ForegroundColor Green

# Exit if build-only mode
if ($BuildOnly) {
    Write-Host "Build-only mode: Plugin built but not deployed" -ForegroundColor Yellow
    Write-Host "Plugin DLL location: Jellyfin.Plugin.TimelineManager\bin\Release\net9.0\Jellyfin.Plugin.TimelineManager.dll" -ForegroundColor Cyan
    exit 0
}

# Check if Jellyfin plugins directory exists
if (-not (Test-Path $JellyfinPluginsPath)) {
    Write-Warning "Jellyfin plugins directory not found: $JellyfinPluginsPath"
    Write-Host "Creating plugins directory..." -ForegroundColor Yellow
    try {
        New-Item -ItemType Directory -Path $JellyfinPluginsPath -Force | Out-Null
    } catch {
        Write-Error "Failed to create plugins directory: $_"
        exit 1
    }
}

# Create plugin subdirectory
$pluginDir = Join-Path $JellyfinPluginsPath "Universal Timeline Manager"
if (-not (Test-Path $pluginDir)) {
    Write-Host "Creating plugin directory: $pluginDir" -ForegroundColor Yellow
    New-Item -ItemType Directory -Path $pluginDir -Force | Out-Null
}

# Copy plugin files
$sourceDll = "Jellyfin.Plugin.TimelineManager\bin\Release\net9.0\Jellyfin.Plugin.TimelineManager.dll"
$targetDll = Join-Path $pluginDir "Jellyfin.Plugin.TimelineManager.dll"

Write-Host "Deploying plugin files..." -ForegroundColor Yellow

try {
    Copy-Item $sourceDll $targetDll -Force
    Write-Host "✓ Copied: Jellyfin.Plugin.TimelineManager.dll" -ForegroundColor Green
    
    # Copy metadata file if it exists
    $sourceMeta = "Jellyfin.Plugin.TimelineManager\meta.json"
    if (Test-Path $sourceMeta) {
        $targetMeta = Join-Path $pluginDir "meta.json"
        Copy-Item $sourceMeta $targetMeta -Force
        Write-Host "✓ Copied: meta.json" -ForegroundColor Green
    }
    
    # Copy README if it exists
    $sourceReadme = "Jellyfin.Plugin.TimelineManager\README.md"
    if (Test-Path $sourceReadme) {
        $targetReadme = Join-Path $pluginDir "README.md"
        Copy-Item $sourceReadme $targetReadme -Force
        Write-Host "✓ Copied: README.md" -ForegroundColor Green
    }
    
} catch {
    Write-Error "Failed to copy plugin files: $_"
    exit 1
}

Write-Host ""
Write-Host "Plugin deployed successfully!" -ForegroundColor Green
Write-Host "Plugin location: $pluginDir" -ForegroundColor Cyan
Write-Host ""
Write-Host "Next steps:" -ForegroundColor Yellow
Write-Host "1. Restart your Jellyfin server" -ForegroundColor White
Write-Host "2. Create configuration file at: /config/timeline_manager_config.json" -ForegroundColor White
Write-Host "3. Go to Admin Dashboard → Scheduled Tasks" -ForegroundColor White
Write-Host "4. Run 'Universal Timeline Manager' task" -ForegroundColor White
Write-Host ""
Write-Host "For configuration help, see README.md in the plugin directory" -ForegroundColor Cyan