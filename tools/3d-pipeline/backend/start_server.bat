@echo off
REM cbrnmd.3D Tutorial Backend Server
REM Batch script for Windows CMD

echo ========================================
echo   cbrnmd.3D Tutorial Backend Server
echo ========================================
echo.

REM Check if virtual environment exists
if not exist "venv" (
    echo Creating virtual environment...
    python -m venv venv
)

REM Activate virtual environment
echo Activating virtual environment...
call venv\Scripts\activate.bat

REM Install dependencies
echo Installing dependencies...
pip install -q -r backend\requirements.txt

REM Check if COLMAP is installed
where colmap >nul 2>nul
if errorlevel 1 (
    echo ERROR: COLMAP not found!
    echo Please install COLMAP from: https://demuc.de/colmap/
    exit /b 1
)

echo.
echo Checking for running instances...

REM Kill processes on port 5000 using PowerShell
powershell -Command "Get-NetTCPConnection -LocalPort 5000 -ErrorAction SilentlyContinue | Select-Object -ExpandProperty OwningProcess -Unique | ForEach-Object { Stop-Process -Id $_ -Force -ErrorAction SilentlyContinue }"

if not errorlevel 1 (
    echo Killed existing processes on port 5000
    timeout /t 1 /nobreak >nul
)

echo.
echo Starting Flask API server...
echo Server will be available at: http://localhost:5000
echo Open tutorial.html in your browser to use the interactive tutorial
echo.
echo Press Ctrl+C to stop the server
echo ========================================
echo.

REM Run the API server
python backend\api.py
