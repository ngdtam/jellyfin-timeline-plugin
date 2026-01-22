@echo off
echo Jellyfin Universal Timeline Manager Plugin Deployment
echo =================================================

echo Building plugin...
dotnet build --configuration Release --verbosity minimal

if %ERRORLEVEL% neq 0 (
    echo Build failed!
    pause
    exit /b 1
)

echo.
echo Build completed successfully!
echo.
echo Plugin DLL location: Jellyfin.Plugin.TimelineManager\bin\Release\net9.0\Jellyfin.Plugin.TimelineManager.dll
echo.
echo To deploy the plugin:
echo 1. Copy the DLL file to your Jellyfin plugins directory
echo 2. Default location: %ProgramData%\Jellyfin\Server\plugins\
echo 3. Restart your Jellyfin server
echo 4. Create configuration file at: /config/timeline_manager_config.json
echo.
echo For detailed deployment instructions, run: powershell -ExecutionPolicy Bypass -File deploy-plugin.ps1
echo.
pause