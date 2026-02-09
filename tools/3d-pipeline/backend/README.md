# cbrnmd.3D Backend API

Flask backend server dla interaktywnego tutorialu fotogrametrii.

## 🚀 Szybki Start

### Linux/macOS
```bash
./start_server.sh
```

### Windows PowerShell
```powershell
.\start_server.ps1
```

### Windows CMD (Batch)
```cmd
start_server.bat
```

## 🛑 Zatrzymywanie Serwera

### Linux/macOS
```bash
# Ctrl+C w terminalu, LUB:
./stop_server.sh
```

### Windows PowerShell
```powershell
# Ctrl+C w terminalu, LUB:
.\stop_server.ps1

# ONE-LINER:
Get-NetTCPConnection -LocalPort 5000 -ErrorAction SilentlyContinue | Select -ExpandProperty OwningProcess -Unique | % { Stop-Process -Id $_ -Force }
```

### Windows CMD (Batch)
```cmd
stop_server.bat
```

## 💡 PowerShell ONE-LINERS

### Kill Port 5000
```powershell
Get-NetTCPConnection -LocalPort 5000 -ErrorAction SilentlyContinue | Select -ExpandProperty OwningProcess -Unique | ForEach-Object { Stop-Process -Id $_ -Force }
```

### Sprawdź Co Używa Portu 5000
```powershell
Get-NetTCPConnection -LocalPort 5000 | Select LocalAddress, LocalPort, State, OwningProcess | Format-Table
```

### Sprawdź Proces Po PID
```powershell
Get-Process -Id (Get-NetTCPConnection -LocalPort 5000).OwningProcess | Format-List
```

### Kill Wszystkie Python Procesy (OSTROŻNIE!)
```powershell
Get-Process python* | Stop-Process -Force
```

## 📋 API Endpoints

### Health Check
```bash
GET http://localhost:5000/api/health
```

### Uruchom Krok Pipeline'u
```bash
POST http://localhost:5000/api/pipeline/step/1
Content-Type: application/json

{
  "dataset": "turntable"
}
```

Kroki:
- `1` - Feature Extraction
- `2` - Feature Matching
- `3` - Sparse Reconstruction
- `4` - Dense Reconstruction
- `5` - Meshing & Export

### Reset Pipeline
```bash
POST http://localhost:5000/api/pipeline/reset
```

## 🔧 Wymagania

- Python 3.8+
- Flask
- flask-cors
- trimesh
- COLMAP (w PATH)
- xvfb (Linux tylko)

## 📁 Pliki

- `api.py` - Główny Flask server
- `requirements.txt` - Python dependencies
- `start_server.sh` - Linux/macOS launcher
- `start_server.ps1` - Windows PowerShell launcher
- `stop_server.sh` - Linux/macOS stop script
- `stop_server.ps1` - Windows PowerShell stop script

## 🐛 Troubleshooting

### Port 5000 Already in Use

**Linux/macOS:**
```bash
lsof -i :5000
kill -9 $(lsof -ti:5000)
```

**Windows:**
```powershell
Get-NetTCPConnection -LocalPort 5000 | Format-Table
Get-NetTCPConnection -LocalPort 5000 | Select -ExpandProperty OwningProcess -Unique | % { Stop-Process -Id $_ -Force }
```

### COLMAP Not Found

Upewnij się że COLMAP jest w PATH:

**Linux/macOS:**
```bash
which colmap
export PATH="/path/to/colmap:$PATH"
```

**Windows:**
```powershell
Get-Command colmap
$env:Path += ";C:\path\to\colmap"
```

### Import Error: trimesh

```bash
pip install trimesh
```

## 📝 Licencja

Część projektu CYBERNOMAD - cbrnmd.3D module.
