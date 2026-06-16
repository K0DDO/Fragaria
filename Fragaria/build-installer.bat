@echo off
setlocal
cd /d "%~dp0"
echo.
echo ========================================
echo   Fragaria - сборка установщика
echo ========================================
echo.

where dotnet >nul 2>&1
if errorlevel 1 (
    echo [ОШИБКА] .NET 8 SDK не найден.
    echo Скачай: https://dotnet.microsoft.com/download/dotnet/8.0
    pause
    exit /b 1
)

powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0build-installer.ps1"
if errorlevel 1 (
    pause
    exit /b 1
)

pause
