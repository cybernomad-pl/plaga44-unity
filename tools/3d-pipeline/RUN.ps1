# cbrnmd.3D - ONELINER AS SCRIPT
# Kliknij i uruchom - robi wszystko automatycznie

cd C:\Users\boris\IdeaProjects\cbrnmd-content\cbrnmd.3D
Get-NetTCPConnection -LocalPort 5000,8000 -ErrorAction SilentlyContinue | Select -ExpandProperty OwningProcess -Unique | % { Stop-Process -Id $_ -Force -ErrorAction SilentlyContinue }
Write-Host "Pulling latest changes..." -ForegroundColor Yellow
git pull
$colmapExe = 'C:\COLMAP\COLMAP-3.9.1-windows-no-cuda\bin\colmap.exe'
if (Test-Path $colmapExe) {
    $env:COLMAP_PATH = $colmapExe
    Start-Sleep -Seconds 2
    Start-Process powershell -ArgumentList '-NoExit', '-Command', "`$env:COLMAP_PATH='$colmapExe'; cd '$PWD'; python backend/api.py"
    Start-Sleep -Seconds 2
    Start-Process powershell -ArgumentList '-NoExit', '-Command', "cd '$PWD'; python -m http.server 8000"
    Start-Sleep -Seconds 2
    Start-Process 'http://localhost:8000/tutorial-v3.html'
}
