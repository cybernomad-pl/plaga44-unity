# cbrnmd.3D - SAFE LAUNCHER WITH COLMAP TEST
# Tests COLMAP configuration before starting, then launches tutorial

Write-Host "=== CBRNMD.3D SAFE LAUNCHER ===" -ForegroundColor Cyan
Write-Host ""

# Check COLMAP configuration
Write-Host "[1/7] Checking COLMAP configuration..." -ForegroundColor Yellow

$colmapExe = 'C:\COLMAP\COLMAP-3.9.1-windows-no-cuda\bin\colmap.exe'

if (Test-Path $colmapExe) {
    Write-Host "  ✓ COLMAP found: $colmapExe" -ForegroundColor Green
    $env:COLMAP_PATH = $colmapExe
} else {
    Write-Host "  ✗ COLMAP not found at: $colmapExe" -ForegroundColor Red
    Write-Host "  Please install COLMAP or update path in script" -ForegroundColor Red
    exit 1
}

# Run COLMAP test
Write-Host "`n[2/7] Testing COLMAP functionality..." -ForegroundColor Yellow
python test-colmap.py
if ($LASTEXITCODE -ne 0) {
    Write-Host "`n✗ COLMAP test FAILED! Fix configuration before continuing." -ForegroundColor Red
    Read-Host "Press Enter to exit"
    exit 1
}

Write-Host "`n✓ COLMAP test PASSED!" -ForegroundColor Green
Start-Sleep -Seconds 1

# Kill existing processes
Write-Host "`n[3/7] Killing existing backend (port 5000)..." -ForegroundColor Yellow
Get-NetTCPConnection -LocalPort 5000 -ErrorAction SilentlyContinue | Select -ExpandProperty OwningProcess -Unique | ForEach-Object {
    Write-Host "  Killing PID: $_" -ForegroundColor Red
    Stop-Process -Id $_ -Force -ErrorAction SilentlyContinue
}

Write-Host "`n[4/7] Killing existing HTTP server (port 8000)..." -ForegroundColor Yellow
Get-NetTCPConnection -LocalPort 8000 -ErrorAction SilentlyContinue | Select -ExpandProperty OwningProcess -Unique | ForEach-Object {
    Write-Host "  Killing PID: $_" -ForegroundColor Red
    Stop-Process -Id $_ -Force -ErrorAction SilentlyContinue
}

Start-Sleep -Seconds 1

# Start Backend with COLMAP environment
Write-Host "`n[5/7] Starting Backend API (port 5000)..." -ForegroundColor Green
Start-Process powershell -ArgumentList "-NoExit", "-Command", "`$env:COLMAP_PATH='$colmapExe'; cd '$PSScriptRoot'; python backend/api.py"

Start-Sleep -Seconds 2

# Start HTTP Server
Write-Host "`n[6/7] Starting HTTP Server (port 8000)..." -ForegroundColor Green
Start-Process powershell -ArgumentList "-NoExit", "-Command", "cd '$PSScriptRoot'; python -m http.server 8000"

Start-Sleep -Seconds 2

# Open browser
Write-Host "`n[7/7] Opening tutorial in browser..." -ForegroundColor Magenta
Start-Process "http://localhost:8000/tutorial-v3.html"

Write-Host "`n=== DONE! ===" -ForegroundColor Green
Write-Host ""
Write-Host "✓ Backend: http://localhost:5000" -ForegroundColor Cyan
Write-Host "✓ Tutorial: http://localhost:8000/tutorial-v3.html" -ForegroundColor Cyan
Write-Host ""
Write-Host "Open Developer Console (F12) to see debug logs!" -ForegroundColor Yellow
Write-Host ""
