# cbrnmd.3D - SAFE LAUNCH INSTRUCTIONS

## Quick Start (Recommended)

**Fastest way - RUN.ps1:**
```powershell
cd C:\Users\boris\IdeaProjects\cbrnmd-content\cbrnmd.3D; .\RUN.ps1
```

**Safe way with tests - LAUNCH.ps1:**
```powershell
cd C:\Users\boris\IdeaProjects\cbrnmd-content\cbrnmd.3D
.\LAUNCH.ps1
```

**RUN.ps1** - Fast oneliner as script:
1. ✓ Kills old processes
2. ✓ Pulls latest changes
3. ✓ Sets COLMAP environment
4. ✓ Starts backend + HTTP server
5. ✓ Opens browser

**LAUNCH.ps1** - Safe with COLMAP test:
1. ✓ Checks COLMAP installation
2. ✓ Tests COLMAP configuration
3. ✓ Sets environment variables
4. ✓ Kills old processes
5. ✓ Starts backend + HTTP server
6. ✓ Opens browser
7. **If COLMAP test fails, script will STOP and show error.**

---

## Manual Testing

If you want to test COLMAP separately:

```powershell
# Set environment
$env:COLMAP_PATH = "C:\COLMAP\COLMAP-3.9.1-windows-no-cuda\bin\colmap.exe"
$env:QT_QPA_PLATFORM = "windows"

# Run test
python test-colmap.py
```

Expected output:
```
============================================================
COLMAP CONFIGURATION TEST
============================================================

✓ Platform: Windows
✓ COLMAP_PATH: C:\COLMAP\...\bin\colmap.exe
✓ QT_QPA_PLATFORM: windows
✓ Testing COLMAP executable: C:\COLMAP\...\bin\colmap.exe
✓ COLMAP is accessible and responding
✓ COLMAP help output (first 200 chars):
...

============================================================
✓ COLMAP TEST PASSED - Ready to run tutorial!
============================================================
```

---

## Manual Launch (Old Way)

If LAUNCH.ps1 doesn't work, use the oneliner:

```powershell
cd C:\Users\boris\IdeaProjects\cbrnmd-content\cbrnmd.3D; Get-NetTCPConnection -LocalPort 5000,8000 -ErrorAction SilentlyContinue | Select -ExpandProperty OwningProcess -Unique | % { Stop-Process -Id $_ -Force -ErrorAction SilentlyContinue }; git pull; $colmapExe = 'C:\COLMAP\COLMAP-3.9.1-windows-no-cuda\bin\colmap.exe'; if (Test-Path $colmapExe) { $env:COLMAP_PATH = $colmapExe; $env:QT_QPA_PLATFORM = 'windows'; Start-Sleep -Seconds 2; Start-Process powershell -ArgumentList '-NoExit', '-Command', "`$env:COLMAP_PATH='$colmapExe'; `$env:QT_QPA_PLATFORM='windows'; cd '$PWD'; python backend/api.py"; Start-Sleep -Seconds 2; Start-Process powershell -ArgumentList '-NoExit', '-Command', "cd '$PWD'; python -m http.server 8000"; Start-Sleep -Seconds 2; Start-Process 'http://localhost:8000/tutorial-v3.html' }
```

---

## Troubleshooting

### COLMAP Test Fails

**Error: "COLMAP executable not found"**
- Check COLMAP_PATH in LAUNCH.ps1 (line 12)
- Update path to your COLMAP installation
- Make sure path points to `colmap.exe` (not directory)

**Error: Return code 3221225781**
- DLL not found
- Make sure COLMAP bin directory is in PATH
- LAUNCH.ps1 sets this automatically via COLMAP_PATH

**Error: Return code 3221226505**
- Qt plugin error - wrong QT_QPA_PLATFORM value
- Make sure QT_QPA_PLATFORM is set to 'windows' (not 'offscreen')
- This COLMAP build only has 'windows' Qt plugin
- LAUNCH.ps1 and START.ps1 set this automatically

### Backend Errors

**"Backend not reachable"** in browser console:
- Check if backend terminal shows errors
- Try restarting: kill Python processes and run LAUNCH.ps1 again

**"Module not found"** errors:
```powershell
pip install flask flask-cors
```

---

## Files

- **LAUNCH.ps1** - Safe launcher with COLMAP test (RECOMMENDED)
- **test-colmap.py** - Standalone COLMAP test script
- **RESTART-DEBUG.ps1** - Old launcher (no COLMAP test)
- **backend/api.py** - Flask backend API
- **tutorial-v3.html** - Frontend interface

---

**Always use LAUNCH.ps1 to prevent COLMAP errors!**
