@echo off
setlocal
cd /d "%~dp0"
echo.
echo === Fragaria: сборка EXE ===
echo.

where dotnet >nul 2>&1
if errorlevel 1 (
    echo [ОШИБКА] .NET 8 SDK не найден.
    echo Установите: https://dotnet.microsoft.com/download/dotnet/8.0
    pause
    exit /b 1
)

set OUT=%~dp0dist\Fragaria

echo Очистка...
if exist "%OUT%" rmdir /s /q "%OUT%"

echo Публикация (self-contained, win-x64)...
dotnet msbuild Fragaria\Fragaria.csproj ^
    /restore ^
    /t:Publish ^
    /p:Configuration=Release ^
    /p:Platform=x64 ^
    /p:RuntimeIdentifier=win-x64 ^
    /p:SelfContained=true ^
    /p:WindowsAppSDKSelfContained=true ^
    /p:EnableMsixTooling=false ^
    /p:PublishDir=%OUT%\

if errorlevel 1 (
    echo.
    echo [ОШИБКА] Сборка не удалась.
    pause
    exit /b 1
)

echo.
echo ========================================
echo  Готово!
echo  EXE: %OUT%\Fragaria.exe
echo ========================================
echo.
echo Запуск: dist\Fragaria\Fragaria.exe
echo.
pause
