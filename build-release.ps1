# Build and Release Script for Universal Timeline Manager
param(
    [string]$Version = "1.1.0"
)

Write-Host "Building Universal Timeline Manager Plugin v$Version..." -ForegroundColor Green

# Clean previous builds
if (Test-Path "release") {
    Remove-Item -Recurse -Force "release"
}
New-Item -ItemType Directory -Path "release" | Out-Null

# Build the plugin
Write-Host "Building plugin..." -ForegroundColor Yellow
dotnet build "Jellyfin.Plugin.TimelineManager/Jellyfin.Plugin.TimelineManager.csproj" --configuration Release

if ($LASTEXITCODE -ne 0) {
    Write-Host "Build failed!" -ForegroundColor Red
    exit 1
}

# Copy DLL to release directory
$dllPath = "Jellyfin.Plugin.TimelineManager/bin/Release/net9.0/Jellyfin.Plugin.TimelineManager.dll"
Copy-Item $dllPath "release/Jellyfin.Plugin.TimelineManager.dll"

# Calculate checksum
$hash = Get-FileHash "release/Jellyfin.Plugin.TimelineManager.dll" -Algorithm SHA256
Write-Host "SHA256 Checksum: $($hash.Hash)" -ForegroundColor Cyan

# Copy to root for easy access
Copy-Item "release/Jellyfin.Plugin.TimelineManager.dll" "Jellyfin.Plugin.TimelineManager.dll"

Write-Host "Release build complete!" -ForegroundColor Green
Write-Host "Files ready for GitHub release:" -ForegroundColor Yellow
Write-Host "  - release/Jellyfin.Plugin.TimelineManager.dll" -ForegroundColor White
Write-Host "  - Checksum: $($hash.Hash)" -ForegroundColor White