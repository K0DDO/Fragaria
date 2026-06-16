@# Fragaria Virtual Audio Driver setup
@# Requires VB-Audio Virtual Cable or future native driver

Write-Host "Fragaria Virtual Audio Setup" -ForegroundColor Magenta

$cable = Get-WmiObject Win32_SoundDevice | Where-Object { $_.Name -like "*CABLE*" }
if ($cable) {
    Write-Host "[OK] Virtual cable detected: $($cable.Name)" -ForegroundColor Green
} else {
    Write-Host "[!] Virtual cable not found." -ForegroundColor Yellow
    Write-Host "    Install VB-Audio Virtual Cable from https://vb-audio.com/Cable/"
    Write-Host "    Or wait for native Fragaria driver (coming soon)."
}

Write-Host ""
Write-Host "Target device names (when native driver ships):"
Write-Host "  - Fragaria Input"
Write-Host "  - Fragaria Output A  (headphones bus)"
Write-Host "  - Fragaria Output B  (stream bus)"
