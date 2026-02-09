# cbrnmd.3D Tutorial Backend Server
# PowerShell script for Windows

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "  cbrnmd.3D Tutorial Backend Server" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

# Check if virtual environment exists
if (-not (Test-Path "venv")) {
    Write-Host "Creating virtual environment..." -ForegroundColor Yellow
    python -m venv venv
}

# Activate virtual environment
Write-Host "Activating virtual environment..." -ForegroundColor Yellow
& "venv\Scripts\Activate.ps1"

# Install dependencies
Write-Host "Installing dependencies..." -ForegroundColor Yellow
pip install -q -r backend\requirements.txt

# Check if COLMAP is installed
if (-not (Get-Command colmap -ErrorAction SilentlyContinue)) {
    Write-Host "ERROR: COLMAP not found!" -ForegroundColor Red
    Write-Host "Please install COLMAP first from:" -ForegroundColor Red
    Write-Host "  https://demuc.de/colmap/" -ForegroundColor Red
    exit 1
}

Write-Host ""
Write-Host "Checking for running instances..." -ForegroundColor Yellow

# PowerShell ONE-LINER to kill port 5000
Get-NetTCPConnection -LocalPort 5000 -ErrorAction SilentlyContinue | Select-Object -ExpandProperty OwningProcess -Unique | ForEach-Object { Stop-Process -Id $_ -Force -ErrorAction SilentlyContinue }

if ($?) {
    Write-Host "✓ Killed existing processes on port 5000" -ForegroundColor Green
    Start-Sleep -Seconds 1
}

Write-Host ""
Write-Host "Starting Flask API server..." -ForegroundColor Green
Write-Host "Server will be available at: http://localhost:5000" -ForegroundColor Cyan
Write-Host "Open tutorial.html in your browser to use the interactive tutorial" -ForegroundColor Cyan
Write-Host ""
Write-Host "Press Ctrl+C to stop the server" -ForegroundColor Yellow
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

# Run the API server
python backend\api.py
