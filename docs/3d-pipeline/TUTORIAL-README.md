# cbrnmd.3D - Interaktywny Tutorial Fotogrametrii

## 🔥 ULTIMATE POWERSHELL ONELINER

### Git Fetch + Pull + Kill + Start (ALL-IN-ONE!)

**PowerShell:**
```powershell
cd cbrnmd.3D; git fetch; git pull; Get-NetTCPConnection -LocalPort 5000 -ErrorAction SilentlyContinue | Select -ExpandProperty OwningProcess -Unique | % { Stop-Process -Id $_ -Force }; Start-Sleep -Seconds 1; .\backend\start_server.ps1
```

**Bash (Linux/macOS):**
```bash
cd cbrnmd.3D && git fetch && git pull && kill -9 $(lsof -ti:5000) 2>/dev/null; sleep 1; ./backend/start_server.sh
```

**Zobacz [ONELINER.md](ONELINER.md) dla więcej wersji!**

---

## 🎯 Szybki Start

### 1. Uruchom Backend API

**Linux/macOS:**
```bash
cd cbrnmd.3D
./backend/start_server.sh
```

**Windows (PowerShell):**
```powershell
cd cbrnmd.3D
.\backend\start_server.ps1
```

**Windows (PowerShell ONE-LINER do kill port 5000):**
```powershell
Get-NetTCPConnection -LocalPort 5000 -ErrorAction SilentlyContinue | Select -ExpandProperty OwningProcess -Unique | ForEach-Object { Stop-Process -Id $_ -Force }
```

Backend startuje na `http://localhost:5000`

**Zatrzymywanie serwera:**
```bash
# Linux/macOS
# Ctrl+C w terminalu gdzie działa, LUB:
./backend/stop_server.sh

# Windows PowerShell
# Ctrl+C w terminalu gdzie działa, LUB:
.\backend\stop_server.ps1

# Windows PowerShell ONE-LINER:
Get-NetTCPConnection -LocalPort 5000 -ErrorAction SilentlyContinue | Select -ExpandProperty OwningProcess -Unique | % { Stop-Process -Id $_ -Force }
```

### 2. Otwórz Tutorial w Przeglądarce

Otwórz plik `tutorial.html` w przeglądarce:

```bash
# Linux/macOS
open tutorial.html

# Lub użyj prostego serwera HTTP
python3 -m http.server 8000
# Następnie otwórz: http://localhost:8000/tutorial.html
```

### 3. Korzystaj z Tutorialu!

1. **Welcome Screen** - Kliknij "ROZPOCZNIJ TUTORIAL"
2. **Wybierz Dataset** - Kliknij na jeden z przykładowych datasetów
   - **Turntable Example**: 36 syntetycznych zdjęć, szybkie przetwarzanie (~2 min)
   - **Castle Example**: 11 prawdziwych zdjęć zamku, dłuższe przetwarzanie (~5 min)
3. **Zobacz Zdjęcia** - Przewiń karuzelę, zobacz wszystkie zdjęcia z datasetu
4. **Kliknij "ROZPOCZNIJ PRZETWARZANIE"**
5. **Wykonaj Każdy Krok** - Klikaj przyciski "URUCHOM" dla każdego kroku:
   - Krok 1: Ekstrakcja Cech (Feature Extraction)
   - Krok 2: Dopasowanie Cech (Feature Matching)
   - Krok 3: Rekonstrukcja Rzadka (Sparse Reconstruction)
   - Krok 4: Rekonstrukcja Gęsta (Dense Reconstruction)
   - Krok 5: Generowanie Siatki (Meshing)
6. **Zobacz Wyniki** - Obejrzyj gotowy model 3D w interaktywnym viewerze!

## 🔧 Wymagania

### System
- **Linux / macOS / Windows**
- Python 3.8+
- COLMAP
- FFmpeg (do przetwarzania wideo)
- xvfb (Linux - dla COLMAP bez GUI)

### Python
Backend instaluje automatycznie:
- Flask (API server)
- flask-cors (CORS support)
- trimesh (konwersja 3D)

### Instalacja COLMAP

**Ubuntu/Debian:**
```bash
sudo apt install colmap ffmpeg xvfb
```

**macOS:**
```bash
brew install colmap ffmpeg
```

**Windows:**
1. Pobierz COLMAP: https://demuc.de/colmap/
2. Rozpakuj i dodaj do PATH
3. Zainstaluj FFmpeg: `winget install ffmpeg` lub https://ffmpeg.org/download.html

**Automatyczna instalacja (Linux/macOS):**
```bash
cd cbrnmd.3D
./tools/install_dependencies.sh
```

## 📸 Przykładowe Datasety

### Turntable Example
- **Lokalizacja**: `examples/turntable_example/`
- **Zdjęcia**: 36 syntetycznych obrazów (img_000.jpg - img_035.jpg)
- **Model**: `demo_model.glb` (wstępnie wygenerowany)
- **Czas**: ~2 minuty
- **Idealny do**: Szybkiego testu pipeline'u

### Castle Example
- **Lokalizacja**: `examples/castle_example/`
- **Zdjęcia**: 11 prawdziwych zdjęć zamku Sceaux
- **Źródło**: openMVG dataset
- **Czas**: ~5 minut
- **Idealny do**: Realnego przykładu fotogrametrii

## 🎓 Co Dzieje Się w Każdym Kroku?

### Krok 1: Ekstrakcja Cech
COLMAP wykrywa charakterystyczne punkty na każdym zdjęciu używając SIFT.
- **Input**: Zdjęcia JPEG
- **Output**: Baza danych z cechami (database.db)
- **Czas**: ~30 sekund

### Krok 2: Dopasowanie Cech
COLMAP porównuje cechy między wszystkimi parami zdjęć.
- **Input**: Baza danych z cechami
- **Output**: Dopasowania między zdjęciami
- **Czas**: ~1-2 minuty

### Krok 3: Rekonstrukcja Rzadka
Obliczanie pozycji kamer i tworzenie rzadkiej chmury punktów 3D.
- **Input**: Dopasowania + zdjęcia
- **Output**: Sparse model (cameras.bin, images.bin, points3D.bin)
- **Czas**: ~30 sekund

### Krok 4: Rekonstrukcja Gęsta
Tworzenie bardzo gęstej chmury punktów używając stereo matching.
- **Input**: Sparse model + zdjęcia
- **Output**: Gęsta chmura punktów (fused.ply)
- **Czas**: ~2-3 minuty (najdłuższy krok!)

### Krok 5: Generowanie Siatki
Łączenie punktów w trójkąty i eksport do GLB.
- **Input**: Gęsta chmura punktów
- **Output**: Mesh 3D (model.glb)
- **Czas**: ~30 sekund

## 🎮 Funkcje Tutorialu

### ✓ Bez Wpisywania Ścieżek
Wszystko działa na przyciski - wybierasz dataset, klikasz "Uruchom" dla każdego kroku.

### ✓ Karuzelka Zdjęć
Zobacz wszystkie zdjęcia w datasecie przed przetwarzaniem. Przewijaj strzałkami lub klikaj miniaturki.

### ✓ Progres w Czasie Rzeczywistym
Każdy krok pokazuje:
- Progress bar z animacją
- Logi konsoli z tym co się dzieje
- Statystyki wyniku (liczba punktów, plików, etc.)

### ✓ Interaktywny Viewer 3D
Na końcu zobacz swój model w Google Model Viewer:
- Obracaj myszką
- Zoom scroll'em
- Auto-rotate toggle
- Pobierz model GLB

### ✓ CYBERNOMAD Design
Całość w stylistyce CYBERNOMAD:
- Share Tech Mono font
- Teal (#3fc99a) + Orange (#d4713d)
- Dark cyber aesthetic

## 🐛 Troubleshooting

### Backend nie startuje?

**Linux/macOS:**
```bash
# Sprawdź czy port 5000 jest wolny
lsof -i :5000

# Kill proces na porcie 5000
kill -9 $(lsof -ti:5000)

# Sprawdź czy COLMAP jest zainstalowany
which colmap

# Zainstaluj zależności manualnie
pip install flask flask-cors trimesh
```

**Windows PowerShell:**
```powershell
# Sprawdź co zajmuje port 5000
Get-NetTCPConnection -LocalPort 5000 | Format-Table

# Kill proces na porcie 5000 (ONE-LINER!)
Get-NetTCPConnection -LocalPort 5000 -ErrorAction SilentlyContinue | Select -ExpandProperty OwningProcess -Unique | % { Stop-Process -Id $_ -Force }

# Sprawdź czy COLMAP jest zainstalowany
Get-Command colmap

# Zainstaluj zależności manualnie
pip install flask flask-cors trimesh
```

### Tutorial nie łączy się z backendem?
- Upewnij się że backend działa na `http://localhost:5000`
- Sprawdź konsole przeglądarki (F12) dla błędów CORS
- Otwórz tutorial przez `http://localhost:8000` zamiast `file://`

### Kroki zbyt długo się wykonują?
- Turntable Example powinien zająć ~2 minuty
- Castle Example może zająć ~5 minut
- Dense reconstruction jest najdłuższy - to normalne!

### Model nie wyświetla się na końcu?
- Sprawdź czy plik GLB został utworzony w `assets/output/`
- Fallback używa `examples/turntable_example/demo_model.glb`
- Sprawdź logi backendu dla błędów

## 📚 Dalsze Kroki

Po ukończeniu tutorialu możesz:

1. **Użyć własnych zdjęć**
   - Skopiuj zdjęcia do `examples/my_dataset/`
   - Dodaj dataset do `datasets` w tutorial.html
   - Uruchom pipeline

2. **Przetwarzać wideo z turntable**
   ```bash
   python pipeline/turntable_pipeline.py --input my_video.mp4 --output my_model
   ```

3. **Dostosować parametry**
   - Edytuj `pipeline/config.yaml`
   - Zmień liczbę klatek, jakość, LODs, etc.

4. **Eksportować do Unity**
   - Model GLB jest gotowy do Unity!
   - Przeciągnij do Assets/
   - Zobacz `README.md` dla integracji Unity

## 🚀 API Endpoints

Backend udostępnia:

- `GET /api/health` - Health check
- `POST /api/pipeline/initialize` - Inicjalizuj dataset
- `POST /api/pipeline/step/<step_num>` - Uruchom krok 1-5
- `POST /api/pipeline/reset` - Reset pipeline

Przykład użycia:
```javascript
// Initialize with dataset
await fetch('http://localhost:5000/api/pipeline/step/1', {
    method: 'POST',
    headers: {'Content-Type': 'application/json'},
    body: JSON.stringify({dataset: 'turntable'})
});
```

## 📝 Licencja

Część projektu CYBERNOMAD - cbrnmd.3D module.
