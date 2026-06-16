# Fragaria — полная сборка: приложение + установщик Setup.exe
$ErrorActionPreference = "Stop"
Set-Location $PSScriptRoot

Write-Host "`n========================================" -ForegroundColor Magenta
Write-Host "  Fragaria — сборка установщика" -ForegroundColor Magenta
Write-Host "========================================`n" -ForegroundColor Magenta

# --- Шаг 1: dotnet publish ---
& "$PSScriptRoot\build.ps1"
if ($LASTEXITCODE -ne 0) { exit 1 }

# --- Шаг 2: Inno Setup ---
$isccPaths = @(
    "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe",
    "$env:ProgramFiles\Inno Setup 6\ISCC.exe",
    "${env:LocalAppData}\Programs\Inno Setup 6\ISCC.exe"
)

$iscc = $isccPaths | Where-Object { Test-Path $_ } | Select-Object -First 1

if (-not $iscc) {
    Write-Host "`nInno Setup 6 не найден." -ForegroundColor Yellow
    Write-Host "Установите: https://jrsoftware.org/isdl.php" -ForegroundColor Yellow
    Write-Host "Или через winget: winget install JRSoftware.InnoSetup`n" -ForegroundColor Yellow

    if (Get-Command winget -ErrorAction SilentlyContinue) {
        Write-Host "Пробую установить Inno Setup через winget..." -ForegroundColor Cyan
        winget install --id JRSoftware.InnoSetup -e --accept-package-agreements --accept-source-agreements
        $iscc = $isccPaths | Where-Object { Test-Path $_ } | Select-Object -First 1
    }
}

if (-not $iscc) {
    Write-Host "[ОШИБКА] Inno Setup не установлен. Без него Setup.exe не собрать." -ForegroundColor Red
    Write-Host "Portable-версия уже готова: dist\Fragaria\Fragaria.exe`n" -ForegroundColor Gray
    exit 1
}

Write-Host "`nКомпиляция установщика (Inno Setup)..." -ForegroundColor Cyan
& $iscc "$PSScriptRoot\installer\Fragaria.iss"

$setup = Join-Path $PSScriptRoot "dist\FragariaSetup.exe"
if (Test-Path $setup) {
    $sizeMb = [math]::Round((Get-Item $setup).Length / 1MB, 1)
    Write-Host "`n========================================" -ForegroundColor Green
    Write-Host "  ГОТОВО!" -ForegroundColor Green
    Write-Host "========================================" -ForegroundColor Green
    Write-Host "  Установщик: $setup" -ForegroundColor White
    Write-Host "  Размер:     $sizeMb MB" -ForegroundColor White
    Write-Host "`n  Скопируй FragariaSetup.exe на Windows и запусти.`n" -ForegroundColor Gray
} else {
    Write-Host "[ОШИБКА] FragariaSetup.exe не создан." -ForegroundColor Red
    exit 1
}
