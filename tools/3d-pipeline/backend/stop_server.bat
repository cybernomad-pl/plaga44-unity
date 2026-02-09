@echo off
REM Stop cbrnmd.3D Backend Server
REM Batch script for Windows CMD

echo ========================================
echo   Stopping cbrnmd.3D Backend Server
echo ========================================
echo.

REM Kill processes on port 5000 using PowerShell
powershell -Command "Get-NetTCPConnection -LocalPort 5000 -ErrorAction SilentlyContinue | Select-Object -ExpandProperty OwningProcess -Unique | ForEach-Object { Stop-Process -Id $_ -Force -ErrorAction SilentlyContinue }"

if not errorlevel 1 (
    echo Server stopped successfully
) else (
    echo No server running on port 5000
)

echo ========================================
