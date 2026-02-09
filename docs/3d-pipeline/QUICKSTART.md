# Quick Start Guide

Najszybszy sposób na rozpoczęcie pracy z cbrnmd.3D.

## ⚡ 5-Minute Setup

### 1. Instalacja (Linux/macOS)

```bash
cd cbrnmd.3D

# Zainstaluj wszystko automatycznie
./tools/install_dependencies.sh

# Aktywuj virtual environment
source venv/bin/activate
```

### 2. Weryfikacja

```bash
python tools/verify_installation.py
```

Powinno pokazać `✓ All required dependencies installed!`

### 3. Twój pierwszy model

#### Opcja A: Z wideo turntable

```bash
# Skopiuj swoje wideo do input/
cp ~/my_video.mp4 assets/input/

# Uruchom pipeline
python pipeline/turntable_pipeline.py \
    --input assets/input/my_video.mp4 \
    --output assets/output/my_first_model
```

#### Opcja B: Z serii zdjęć

```bash
# Skopiuj zdjęcia do folderu
mkdir assets/input/my_photos
cp ~/photos/*.jpg assets/input/my_photos/

# Uruchom pipeline
python pipeline/photo_pipeline.py \
    --input assets/input/my_photos \
    --output assets/output/my_first_model
```

### 4. Sprawdź wyniki

```bash
ls assets/output/my_first_model/final/

# Powinno być:
# - model.ply (główny model)
# - report.json (raport z processingu)
```

## 🎬 Nagrywanie wideo turntable

**Szybkie tips:**
1. **Statyw** - stabilna kamera (lub telefon)
2. **Turntable** - obiekt na obrotowym stole (ręczny ~30 PLN)
3. **Światło** - 2 lampy z obu stron, równomierne
4. **Tło** - jednokolorowe (szare, zielone, niebieskie)
5. **Nagranie** - pełne 360°, 8-10 sekund, Full HD
6. **FPS** - 30 fps minimum (60 fps lepsze)

**Więcej szczegółów:** [docs/TURNTABLE-GUIDE.md](docs/TURNTABLE-GUIDE.md)

## 📱 Skanowanie telefonem

**Najlepsze darmowe apps:**

### iPhone (z LiDAR)
- **Scaniverse** - najlepsze, całkowicie darmowe!
  1. Pobierz z App Store
  2. Scan object
  3. Export → GLB
  4. AirDrop do komputera
  5. `cp scan.glb cbrnmd.3D/assets/input/`

### Android/iOS
- **Polycam** - 50 exports/miesiąc free
- **Kiri Engine** - 10 scans/tydzień free

**Więcej:** [docs/PHONE-SCANNING.md](docs/PHONE-SCANNING.md)

## ⚙️ Konfiguracja

### Podstawowa (domyślna)
Pipeline używa `pipeline/config.yaml` - dobry dla większości przypadków.

### Custom config
```bash
# Skopiuj domyślną konfigurację
cp pipeline/config.yaml my_config.yaml

# Edytuj (np. zmień target_triangles)
nano my_config.yaml

# Użyj custom config
python pipeline/turntable_pipeline.py \
    --input video.mp4 \
    --output output/ \
    --config my_config.yaml
```

### Najważniejsze ustawienia

```yaml
# W config.yaml

frame_extraction:
  target_frames: 120  # Więcej = lepiej, ale wolniej

optimization:
  target_triangles: 10000  # Dla game assets
  generate_lods: true  # LOD levels dla Unity

export:
  formats:
    - GLB  # Unity
    - OBJ  # Universal
```

## 🎮 Import do Unity

```bash
# Po wygenerowaniu modelu
cp assets/output/my_first_model/final/model.glb \
   ~/UnityProjects/CYBERNOMAD/Assets/Models/

# W Unity:
# 1. Model pojawi się automatycznie
# 2. Select model → Inspector
# 3. Set Scale Factor (jeśli potrzeba)
# 4. Generate Colliders (jeśli potrzeba)
# 5. Przeciągnij do sceny
```

## 🔧 Troubleshooting

### "colmap: command not found"
```bash
# Ubuntu
sudo apt install colmap

# macOS
brew install colmap

# Windows: Download from https://github.com/colmap/colmap/releases
```

### "ffmpeg: command not found"
```bash
# Ubuntu
sudo apt install ffmpeg

# macOS
brew install ffmpeg
```

### Pipeline fails at "Feature extraction"
- Sprawdź czy masz wystarczająco RAM (16+ GB)
- Zmniejsz `target_frames` w config.yaml
- Spróbuj z `gpu: false` jeśli masz problemy z GPU

### "Out of memory"
```yaml
# W config.yaml zmniejsz:
frame_extraction:
  target_frames: 60  # Zamiast 120

optimization:
  target_triangles: 5000  # Zamiast 10000
```

### Słaba jakość modelu
1. Więcej zdjęć/klatek (120-200)
2. Lepsze oświetlenie (równomierne, bez cieni)
3. Wolniejszy obrót (więcej overlap)
4. Wyższa rozdzielczość wideo (4K)

## 📚 Next Steps

### Dla początkujących:
1. [TURNTABLE-GUIDE.md](docs/TURNTABLE-GUIDE.md) - Jak nagrywać
2. [PHONE-SCANNING.md](docs/PHONE-SCANNING.md) - Skanowanie telefonem
3. [TOOLS-COMPARISON.md](docs/TOOLS-COMPARISON.md) - Różne narzędzia

### Dla zaawansowanych:
1. [SETUP.md](docs/SETUP.md) - Szczegółowa instalacja
2. `pipeline/config.yaml` - Wszystkie opcje
3. `scripts/` - Helper scripts do customizacji

## 💡 Pro Tips

1. **Batch processing:**
   ```bash
   for video in assets/input/*.mp4; do
       python pipeline/turntable_pipeline.py -i "$video" -o "output/$(basename $video .mp4)"
   done
   ```

2. **Quick test (low quality, fast):**
   ```bash
   # Edytuj config.yaml:
   # target_frames: 30
   # target_triangles: 2000
   ```

3. **GPU acceleration:**
   - Jeśli masz NVIDIA GPU, COLMAP automatycznie go użyje
   - Przyspiesza 3-5x

4. **Multiple heights:**
   Nagraj 3 wideo tego samego obiektu:
   - Kamera 30° powyżej
   - Kamera poziomo (0°)
   - Kamera 30° poniżej

   Połącz zdjęcia i użyj `photo_pipeline.py`

## ❓ Help

- **Problemy:** Zobacz [SETUP.md](docs/SETUP.md) → Troubleshooting
- **Pytania:** Open issue w głównym repo projektu
- **Community:** r/photogrammetry (Reddit)

---

**Ready to create 3D magic?** 🚀

Start with:
```bash
python pipeline/turntable_pipeline.py --help
```
