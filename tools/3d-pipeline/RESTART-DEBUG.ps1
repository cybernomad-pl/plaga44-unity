# cbrnmd.3D - RESTART WITH DEBUG LOGGING
# Kills backend + HTTP server, starts both, opens tutorial-v3.html

Write-Host "=== CBRNMD.3D DEBUG RESTART ===" -ForegroundColor Cyan

# Kill port 5000 (Backend)
Write-Host "`n[1/6] Killing backend (port 5000)..." -ForegroundColor Yellow
Get-NetTCPConnection -LocalPort 5000 -ErrorAction SilentlyContinue | Select -ExpandProperty OwningProcess -Unique | ForEach-Object {
    Write-Host "  Killing PID: $_" -ForegroundColor Red
    Stop-Process -Id $_ -Force -ErrorAction SilentlyContinue
}

# Kill port 8000 (HTTP Server)
Write-Host "`n[2/6] Killing HTTP server (port 8000)..." -ForegroundColor Yellow
Get-NetTCPConnection -LocalPort 8000 -ErrorAction SilentlyContinue | Select -ExpandProperty OwningProcess -Unique | ForEach-Object {
    Write-Host "  Killing PID: $_" -ForegroundColor Red
    Stop-Process -Id $_ -Force -ErrorAction SilentlyContinue
}

Start-Sleep -Seconds 1

# Start Backend in new terminal
Write-Host "`n[3/6] Starting Backend API (port 5000)..." -ForegroundColor Green
Start-Process powershell -ArgumentList "-NoExit", "-Command", "cd '$PSScriptRoot'; python backend/api.py"

Start-Sleep -Seconds 2

# Start HTTP Server in new terminal
Write-Host "`n[4/6] Starting HTTP Server (port 8000)..." -ForegroundColor Green
Start-Process powershell -ArgumentList "-NoExit", "-Command", "cd '$PSScriptRoot'; python -m http.server 8000"

Start-Sleep -Seconds 2

# Open tutorial-v3.html in browser
Write-Host "`n[5/6] Opening tutorial-v3.html in browser..." -ForegroundColor Magenta
Start-Process "http://localhost:8000/tutorial-v3.html"

Write-Host "`n[6/6] DONE! Debug logging enabled." -ForegroundColor Green
Write-Host "`nOtwórz Developer Console (F12) aby zobaczyć debug logs!" -ForegroundColor Yellow
Write-Host "Backend: http://localhost:5000" -ForegroundColor Cyan
Write-Host "Tutorial: http://localhost:8000/tutorial-v3.html" -ForegroundColor Cyan
