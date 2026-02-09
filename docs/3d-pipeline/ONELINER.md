# 🔥 ULTIMATE POWERSHELL ONELINERS

## 🚀 GIT FETCH + PULL + KILL + START (ALL-IN-ONE!)

### PowerShell ONE-LINER - Odśwież Repo i Restart Serwer:
```powershell
cd cbrnmd.3D; git fetch; git pull; Get-NetTCPConnection -LocalPort 5000 -ErrorAction SilentlyContinue | Select -ExpandProperty OwningProcess -Unique | % { Stop-Process -Id $_ -Force }; Start-Sleep -Seconds 1; .\backend\start_server.ps1
```

### Bash ONE-LINER (Linux/macOS) - Odśwież Repo i Restart Serwer:
```bash
cd cbrnmd.3D && git fetch && git pull && kill -9 $(lsof -ti:5000) 2>/dev/null; sleep 1; ./backend/start_server.sh
```

---

## 📋 SKŁADOWE ONELINERA

### PowerShell - Rozpisane:
```powershell
# Przejdź do katalogu
cd cbrnmd.3D

# Fetch + Pull
git fetch
git pull

# Kill port 5000
Get-NetTCPConnection -LocalPort 5000 -ErrorAction SilentlyContinue | Select -ExpandProperty OwningProcess -Unique | % { Stop-Process -Id $_ -Force }

# Czekaj sekundę
Start-Sleep -Seconds 1

# Start serwer
.\backend\start_server.ps1
```

### Bash - Rozpisane:
```bash
# Przejdź do katalogu
cd cbrnmd.3D

# Fetch + Pull
git fetch && git pull

# Kill port 5000
kill -9 $(lsof -ti:5000) 2>/dev/null

# Czekaj sekundę
sleep 1

# Start serwer
./backend/start_server.sh
```

---

## 🎯 INNE PRZYDATNE ONELINERS

### Kill Port 5000 (TYLKO KILL!)
```powershell
Get-NetTCPConnection -LocalPort 5000 -ErrorAction SilentlyContinue | Select -ExpandProperty OwningProcess -Unique | % { Stop-Process -Id $_ -Force }
```

### Fetch + Pull + Kill + Start (BEZ CD)
```powershell
git fetch; git pull; Get-NetTCPConnection -LocalPort 5000 -ErrorAction SilentlyContinue | Select -ExpandProperty OwningProcess -Unique | % { Stop-Process -Id $_ -Force }; Start-Sleep -Seconds 1; .\backend\start_server.ps1
```

### Kill All Python Processes + Start
```powershell
Get-Process python* | Stop-Process -Force; Start-Sleep -Seconds 1; .\backend\start_server.ps1
```

### Sprawdź Port + Kill + Start
```powershell
Get-NetTCPConnection -LocalPort 5000 | Format-Table; Get-NetTCPConnection -LocalPort 5000 -ErrorAction SilentlyContinue | Select -ExpandProperty OwningProcess -Unique | % { Stop-Process -Id $_ -Force }; .\backend\start_server.ps1
```

---

## 🔧 ULTRA COMPACT WERSJE

### PowerShell ULTRA COMPACT:
```powershell
cd cbrnmd.3D;git fetch;git pull;Get-NetTCPConnection -LocalPort 5000 -EA Silent|Select -Exp OwningProcess -U|%{Stop-Process -Id $_ -F};sleep 1;.\backend\start_server.ps1
```

### Bash ULTRA COMPACT:
```bash
cd cbrnmd.3D&&git fetch&&git pull&&kill -9 $(lsof -ti:5000);sleep 1;./backend/start_server.sh
```

---

## 📝 ALIASY (DODAJ DO PROFILU!)

### PowerShell Profile (`$PROFILE`):
```powershell
# Dodaj do: C:\Users\YourName\Documents\PowerShell\Microsoft.PowerShell_profile.ps1

function Restart-Cbrnmd3D {
    cd C:\path\to\cbrnmd-content\cbrnmd.3D
    git fetch
    git pull
    Get-NetTCPConnection -LocalPort 5000 -ErrorAction SilentlyContinue | Select -ExpandProperty OwningProcess -Unique | ForEach-Object { Stop-Process -Id $_ -Force }
    Start-Sleep -Seconds 1
    .\backend\start_server.ps1
}

Set-Alias -Name cbrnmd -Value Restart-Cbrnmd3D
```

**Użycie:**
```powershell
cbrnmd  # Wszystko w jednej komendzie!
```

### Bash Profile (`~/.bashrc` lub `~/.zshrc`):
```bash
# Dodaj do: ~/.bashrc (Linux) lub ~/.zshrc (macOS)

cbrnmd() {
    cd ~/cbrnmd-content/cbrnmd.3D
    git fetch && git pull
    kill -9 $(lsof -ti:5000) 2>/dev/null
    sleep 1
    ./backend/start_server.sh
}
```

**Użycie:**
```bash
cbrnmd  # Wszystko w jednej komendzie!
```

---

## 🎮 PRZYKŁADY UŻYCIA

### Scenariusz 1: Szybki Restart Serwera
```powershell
# Jesteś w katalogu cbrnmd.3D
Get-NetTCPConnection -LocalPort 5000 -EA Silent|Select -Exp OwningProcess -U|%{Stop-Process -Id $_ -F};.\backend\start_server.ps1
```

### Scenariusz 2: Pełny Update z Restartem
```powershell
# Z dowolnego katalogu
cd C:\projects\cbrnmd-content\cbrnmd.3D;git fetch;git pull;Get-NetTCPConnection -LocalPort 5000 -EA Silent|Select -Exp OwningProcess -U|%{Stop-Process -Id $_ -F};sleep 1;.\backend\start_server.ps1
```

### Scenariusz 3: Kill Wszystko i Start od Nowa
```powershell
Get-Process python*|Stop-Process -F;Get-NetTCPConnection -LocalPort 5000 -EA Silent|Select -Exp OwningProcess -U|%{Stop-Process -Id $_ -F};.\backend\start_server.ps1
```

---

## 🔥 ULTIMATE MEGA ONELINER (GIT + KILL + INSTALL + START)

### PowerShell - Wszystko w Jednym!
```powershell
cd cbrnmd.3D; git fetch; git pull; Get-NetTCPConnection -LocalPort 5000 -EA Silent | Select -Exp OwningProcess -U | % { Stop-Process -Id $_ -F }; if(-not(Test-Path venv)){python -m venv venv}; .\venv\Scripts\Activate.ps1; pip install -q -r backend\requirements.txt; Start-Sleep 1; python backend\api.py
```

**Co robi:**
1. `cd cbrnmd.3D` - Przejdź do katalogu
2. `git fetch; git pull` - Aktualizuj repo
3. Kill port 5000
4. Stwórz venv jeśli nie istnieje
5. Aktywuj venv
6. Zainstaluj dependencies
7. Start Flask server

### Bash - Wszystko w Jednym!
```bash
cd cbrnmd.3D && git fetch && git pull && kill -9 $(lsof -ti:5000) 2>/dev/null; [[ ! -d venv ]] && python3 -m venv venv; source venv/bin/activate; pip install -q -r backend/requirements.txt; sleep 1; python3 backend/api.py
```

---

## 💾 KOPIUJ-WKLEJ (GOTOWE DO UŻYCIA)

### Windows PowerShell:
```powershell
cd cbrnmd.3D; git fetch; git pull; Get-NetTCPConnection -LocalPort 5000 -ErrorAction SilentlyContinue | Select -ExpandProperty OwningProcess -Unique | % { Stop-Process -Id $_ -Force }; Start-Sleep -Seconds 1; .\backend\start_server.ps1
```

### Linux/macOS Bash:
```bash
cd cbrnmd.3D && git fetch && git pull && kill -9 $(lsof -ti:5000) 2>/dev/null; sleep 1; ./backend/start_server.sh
```

**SKOPIUJ, WKLEJ, ENTER - GOTOWE! 🚀**
