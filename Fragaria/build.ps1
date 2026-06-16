# Fragaria — сборка portable EXE для Windows
$ErrorActionPreference = "Stop"
Set-Location $PSScriptRoot

$out = Join-Path $PSScriptRoot "dist\Fragaria"

Write-Host "`n=== Fragaria: сборка EXE ===" -ForegroundColor Magenta

if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
    Write-Host "Установите .NET 8 SDK: https://dotnet.microsoft.com/download/dotnet/8.0" -ForegroundColor Red
    exit 1
}

if (Test-Path $out) { Remove-Item $out -Recurse -Force }

Write-Host "Публикация..." -ForegroundColor Cyan
dotnet publish Fragaria\Fragaria.csproj `
    -c Release `
    -r win-x64 `
    --self-contained true `
    -p:WindowsAppSDKSelfContained=true `
    -o $out

$exe = Join-Path $out "Fragaria.exe"
if (Test-Path $exe) {
    Write-Host "`nГотово: $exe" -ForegroundColor Green
    Write-Host "Размер: $([math]::Round((Get-Item $exe).Length / 1MB, 1)) MB (папка dist ~полный runtime)`n"
} else {
    Write-Host "EXE не найден — проверьте ошибки выше." -ForegroundColor Red
    exit 1
}
