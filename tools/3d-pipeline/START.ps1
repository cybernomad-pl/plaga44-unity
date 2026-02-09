# cbrnmd.3D - QUICK START (no tests, just launch)
# Kills ports, sets COLMAP_PATH, starts backend + frontend

Write-Host "=== CBRNMD.3D QUICK START ===" -ForegroundColor Cyan

# Kill existing processes
Write-Host "`nKilling ports 5000, 8000..." -ForegroundColor Yellow
Get-NetTCPConnection -LocalPort 5000,8000 -ErrorAction SilentlyContinue |
    Select -ExpandProperty OwningProcess -Unique |
    ForEach-Object { Stop-Process -Id $_ -Force -ErrorAction SilentlyContinue }

# Pull latest changes
Write-Host "Pulling latest changes..." -ForegroundColor Yellow
git pull

# Set COLMAP path
$colmapExe = 'C:\COLMAP\COLMAP-3.9.1-windows-no-cuda\bin\colmap.exe'

if (-not (Test-Path $colmapExe)) {
    Write-Host "`n✗ COLMAP not found at: $colmapExe" -ForegroundColor Red
    Write-Host "Update path in START.ps1 line 15" -ForegroundColor Red
    Read-Host "Press Enter to exit"
    exit 1
}

Write-Host "✓ COLMAP found" -ForegroundColor Green
$env:COLMAP_PATH = $colmapExe

# Start Backend
Write-Host "`nStarting Backend (port 5000)..." -ForegroundColor Green
Start-Sleep -Seconds 2
Start-Process powershell -ArgumentList '-NoExit', '-Command', "`$env:COLMAP_PATH='$colmapExe'; cd '$PSScriptRoot'; python backend/api.py"

# Start HTTP Server
Write-Host "Starting HTTP Server (port 8000)..." -ForegroundColor Green
Start-Sleep -Seconds 2
Start-Process powershell -ArgumentList '-NoExit', '-Command', "cd '$PSScriptRoot'; python -m http.server 8000"

# Open browser
Write-Host "Opening browser..." -ForegroundColor Magenta
Start-Sleep -Seconds 2
Start-Process 'http://localhost:8000/tutorial-v3.html'

Write-Host "`n=== DONE! ===" -ForegroundColor Green
Write-Host "Backend: http://localhost:5000" -ForegroundColor Cyan
Write-Host "Tutorial: http://localhost:8000/tutorial-v3.html" -ForegroundColor Cyan
Write-Host ""
