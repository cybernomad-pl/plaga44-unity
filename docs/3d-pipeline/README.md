# cbrnmd.3D

**Moduł fotogrametrii i skanowania 3D dla projektu CYBERNOMAD**

Kompletny pipeline do tworzenia modeli 3D z wykorzystaniem fotogrametrii - od zdjęć/wideo z turntable po gotowy model 3D.

## 🔥 ULTIMATE ONELINER - Fetch + Pull + Kill + Start

**PowerShell (Windows):**
```powershell
cd cbrnmd.3D; git fetch; git pull; Get-NetTCPConnection -LocalPort 5000 -ErrorAction SilentlyContinue | Select -ExpandProperty OwningProcess -Unique | % { Stop-Process -Id $_ -Force }; Start-Sleep -Seconds 1; .\backend\start_server.ps1
```

**Bash (Linux/macOS):**
```bash
cd cbrnmd.3D && git fetch && git pull && kill -9 $(lsof -ti:5000) 2>/dev/null; sleep 1; ./backend/start_server.sh
```

**Więcej wersji → [ONELINER.md](ONELINER.md)**

---

## 🎯 Możliwości

- **Przetwarzanie wideo z turntable** - automatyczna ekstrakcja klatek i generowanie modeli 3D
- **Fotogrametria ze zdjęć** - tworzenie modeli z serii fotografii
- **Automatyczna konwersja formatów** - PLY → GLB, OBJ, FBX (trimesh)
- **Optymalizacja mesh** - redukcja poligonów, generowanie LOD levels
- **Integracja z aplikacjami mobilnymi** - importowanie skanów z telefonu
- **Darmowe narzędzia open-source** - pełny pipeline bez kosztów licencyjnych
- **Automatyzacja** - skrypty Python do streamingu całego procesu

## 📁 Struktura

```
cbrnmd.3D/
├── README.md                 # Ten plik
├── QUICKSTART.md            # Szybki start (5 minut)
├── VIEWER-GUIDE.md          # Przewodnik po 3D viewerze
├── viewer.html              # 🎮 Interaktywny 3D viewer (HTML)
├── docs/                     # Dokumentacja
│   ├── SETUP.md             # Instrukcja instalacji
│   ├── TURNTABLE-GUIDE.md   # Przewodnik kręcenia wideo turntable
│   ├── PHONE-SCANNING.md    # Integracja z aplikacjami mobilnymi
│   └── TOOLS-COMPARISON.md  # Porównanie narzędzi fotogrametrii
├── pipeline/                 # Główne skrypty pipeline'u
│   ├── turntable_pipeline.py    # Pipeline dla wideo turntable
│   ├── photo_pipeline.py        # Pipeline dla serii zdjęć
│   └── config.yaml              # Konfiguracja pipeline'u
├── scripts/                  # Pomocnicze skrypty
│   ├── extract_frames.py    # Ekstrakcja klatek z wideo
│   ├── prepare_images.py    # Przygotowanie zdjęć
│   └── optimize_mesh.py     # Optymalizacja mesh'a + LODs
├── tools/                    # Instalacja i weryfikacja
│   ├── install_dependencies.sh  # Auto-instalacja
│   ├── verify_installation.py   # Weryfikacja setupu
│   └── requirements.txt         # Python dependencies
├── examples/                 # Przykładowe projekty
│   ├── turntable_example/
│   └── photo_set_example/
└── assets/                   # Katalogi robocze
    ├── input/               # Wejściowe pliki
    └── output/              # Wygenerowane modele
```

## 🚀 Szybki start

### 1. Instalacja narzędzi

```bash
cd cbrnmd.3D/tools
./install_dependencies.sh
```

### 2. Przetwarzanie wideo z turntable

```bash
cd cbrnmd.3D/pipeline
python turntable_pipeline.py --input ../assets/input/turntable_video.mp4 --output ../assets/output/model_01
```

### 3. Przetwarzanie serii zdjęć

```bash
python photo_pipeline.py --input ../assets/input/photos/ --output ../assets/output/model_02
```

### 4. 🎮 Przeglądanie modelu w przeglądarce

```bash
# Otwórz interaktywny 3D viewer
python3 -m http.server 8000

# Następnie otwórz w przeglądarce:
# http://localhost:8000/viewer.html

# Drag & drop swojego model.glb lub użyj przykładowych modeli!
```

Zobacz [VIEWER-GUIDE.md](VIEWER-GUIDE.md) dla szczegółów.

## 🛠️ Narzędzia

### COLMAP (zalecane)
- **Open-source**, darmowe
- Doskonałe dla akademickich i komercyjnych projektów
- Świetna jakość rekonstrukcji 3D
- Działa na Linux, Windows, macOS

### Meshroom (AliceVision)
- **Open-source**, darmowe
- Graficzny interfejs użytkownika
- Pipeline node-based
- Wymaga GPU NVIDIA (CUDA)

### OpenMVG + OpenMVS
- Kompletnie **open-source**
- Modularny pipeline
- Dobre dla zaawansowanych użytkowników

## 📱 Aplikacje mobilne (Android/iOS)

### Darmowe aplikacje do skanowania 3D:

1. **Polycam** (Darmowa wersja)
   - iOS i Android
   - Export do OBJ, GLB
   - Dobra jakość skanów

2. **Scaniverse** (Darmowy)
   - iOS (LiDAR)
   - Export bez ograniczeń

3. **Metascan** (Open-source)
   - Android
   - Integracja z OpenCV

4. **Kiri Engine** (Darmowa wersja)
   - iOS i Android
   - Processing w chmurze

Zobacz `docs/PHONE-SCANNING.md` dla szczegółów integracji.

## 📸 Best practices dla turntable

1. **Oświetlenie**: Równomierne, bez ostrych cieni
2. **Tło**: Jednorodne, matowe (najlepiej zielony/niebieski)
3. **FPS**: 30 fps, Full HD (1920x1080)
4. **Obrót**: Pełne 360°, 2-3 sekund na obrót
5. **Klatki**: 60-120 klatek (co 3-6 stopni)
6. **Stabilność**: Stabilna kamera, ruchomy obiekt

Zobacz `docs/TURNTABLE-GUIDE.md` dla pełnego przewodnika.

## 🎮 Integracja z Unity (CYBERNOMAD)

Pipeline automatycznie generuje modele w formatach:
- **GLB/GLTF** - najlepszy dla Unity (z trimesh)
- **OBJ** - uniwersalny
- **PLY** - raw mesh z COLMAP

**Automatyczna optymalizacja (konfiguruj w config.yaml):**
- ✅ Redukcja poligonów (quadric decimation)
- ✅ Generowanie LOD levels (LOD0, LOD1, LOD2)
- ✅ Konwersja formatów (PLY → GLB/OBJ)
- ⏳ Bakowanie tekstur PBR (wkrótce)

## 🎮 3D Viewer

Moduł zawiera **interaktywny HTML viewer** (`viewer.html`) do przeglądania wygenerowanych modeli 3D:

**Funkcje:**
- 🎬 Interaktywna kontrola kamery (obracanie, zoom, pan)
- 💡 Różne środowiska oświetleniowe
- 📸 Screenshot export
- 📱 Mobile & AR support
- 🎨 Drag & drop GLB/GLTF files
- 🚀 Przykładowe modele do testowania

**Jak używać:**
```bash
# Uruchom lokalny serwer
python3 -m http.server 8000

# Otwórz w przeglądarce
open http://localhost:8000/viewer.html

# Przeciągnij swój model.glb na viewer!
```

Zobacz [VIEWER-GUIDE.md](VIEWER-GUIDE.md) dla pełnej dokumentacji.

## 📚 Dokumentacja

- [QUICKSTART.md](QUICKSTART.md) - Szybki start w 5 minut
- [VIEWER-GUIDE.md](VIEWER-GUIDE.md) - Interaktywny 3D viewer
- [SETUP.md](docs/SETUP.md) - Szczegółowa instalacja wszystkich narzędzi
- [TURNTABLE-GUIDE.md](docs/TURNTABLE-GUIDE.md) - Jak prawidłowo kręcić wideo turntable
- [PHONE-SCANNING.md](docs/PHONE-SCANNING.md) - Integracja z aplikacjami mobilnymi
- [TOOLS-COMPARISON.md](docs/TOOLS-COMPARISON.md) - Porównanie narzędzi fotogrametrii

## 🔧 Wymagania systemowe

### Minimalne:
- CPU: 4 rdzenie (8 wątków)
- RAM: 16 GB
- GPU: Opcjonalnie (przyspiesza processing)
- Dysk: 50 GB wolnego miejsca

### Zalecane:
- CPU: 8+ rdzeni (16+ wątków)
- RAM: 32+ GB
- GPU: NVIDIA RTX (8+ GB VRAM) z CUDA
- Dysk: SSD 100+ GB

## 📄 Licencje

Wszystkie użyte narzędzia są open-source lub darmowe:
- COLMAP: BSD License
- Meshroom: MPLv2 License
- OpenMVG/OpenMVS: MPLv2 License
- FFmpeg: LGPL/GPL
- Model Viewer: Apache License 2.0

## 🤝 Contribution

Moduł jest częścią projektu CYBERNOMAD. Dla zmian:
1. Testuj na przykładowych danych w `examples/`
2. Dokumentuj zmiany w odpowiednich plikach docs
3. Commit z opisem zmian

## 📞 Support

Dla pytań i problemów otwórz issue w repozytorium głównym projektu.

---

**Status:** ✅ Ready for production
**Wersja:** 1.0.0
**Ostatnia aktualizacja:** 2025-11-18
