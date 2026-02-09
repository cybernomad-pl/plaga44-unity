# Stop cbrnmd.3D Backend Server
# PowerShell script for Windows

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "  Stopping cbrnmd.3D Backend Server" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

# Find and kill processes on port 5000
$processes = Get-NetTCPConnection -LocalPort 5000 -ErrorAction SilentlyContinue | Select-Object -ExpandProperty OwningProcess -Unique

if ($processes) {
    Write-Host "Killing Flask server on port 5000..." -ForegroundColor Yellow
    foreach ($pid in $processes) {
        Stop-Process -Id $pid -Force -ErrorAction SilentlyContinue
    }
    Write-Host "✓ Server stopped" -ForegroundColor Green
} else {
    Write-Host "No server running on port 5000" -ForegroundColor Gray
}

Write-Host "========================================" -ForegroundColor Cyan
