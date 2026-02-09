# Phone Scanning Integration

Przewodnik po aplikacjach mobilnych do skanowania 3D i integracji z pipeline cbrnmd.3D.

## 📱 Najlepsze darmowe aplikacje

### 1. Polycam (iOS & Android)

**Darmowa wersja:**
- ✅ Unlimited scans
- ✅ Export do OBJ, PLY
- ✅ Photo mode + LiDAR mode (iOS)
- ✅ 50 export/miesiąc (darmowe konto)
- ❌ Watermark na eksportach

**Pro: $12.99/miesiąc** (opcjonalnie)

**Jak używać:**
1. Pobierz Polycam z App Store/Play Store
2. Utwórz konto (darmowe)
3. Tryb "Photo Mode" dla fotogrametrii
4. Obchodź obiekt dookoła, rób zdjęcia
5. App automatycznie przetworzy (w chmurze)
6. Export → OBJ/PLY

**Import do cbrnmd.3D:**
```bash
# Przenieś exported model do input
cp polycam_export.obj cbrnmd.3D/assets/input/

# Konwersja i optymalizacja
cd cbrnmd.3D/scripts
python optimize_mesh.py --input ../assets/input/polycam_export.obj
```

### 2. Scaniverse (iOS - LiDAR)

**Całkowicie darmowe!**
- ✅ Nieograniczone skany
- ✅ Export OBJ, GLTF, GLB, FBX, STL, PLY, USDZ
- ✅ Bez watermark
- ✅ Świetna jakość (używa LiDAR)
- ✅ Offline processing
- ❌ Tylko iOS (wymaga LiDAR: iPhone 12 Pro+, iPad Pro 2020+)

**Najlepsza opcja dla posiadaczy iPhone!**

**Jak używać:**
1. Pobierz Scaniverse (iOS)
2. Scan object (app prowadzi przez proces)
3. Export → GLB/OBJ
4. AirDrop/transfer do komputera

**Import:**
```bash
cp scaniverse_export.glb cbrnmd.3D/assets/input/
```

### 3. Kiri Engine (iOS & Android)

**Darmowa wersja:**
- ✅ 10 scans/tydzień
- ✅ Photo-based fotogrametria
- ✅ Cloud processing
- ✅ Export OBJ, GLTF
- ❌ Limit 50 zdjęć/scan (Free tier)

**Pro: $9.99/miesiąc** (opcjonalnie)

**Jak używać:**
1. Pobierz Kiri Engine
2. Utwórz konto
3. Take Photos mode
4. 30-50 zdjęć dookoła obiektu
5. Upload → cloud processing (2-15 min)
6. Export model

### 4. Display.land (iOS & Android)

**Całkowicie darmowe!**
- ✅ Unlimited scans
- ✅ Photo + LiDAR
- ✅ Export OBJ, GLB
- ✅ Social sharing (opcjonalne)

**Jak używać:**
1. Pobierz Display.land
2. Scan obiekt (app guide)
3. Download → OBJ lub GLB

### 5. Metascan (Android - Open Source)

**Open source, darmowe**
- ✅ Lokalne processing (no cloud)
- ✅ OpenCV backend
- ✅ Export OBJ, PLY
- ❌ Wymaga mocnego telefonu
- ❌ Wolniejsze niż cloud solutions

**Dla privacy-conscious users**

**GitHub:** https://github.com/andreluizbvs/Metascan

### 6. Trnio (iOS & Android)

**Free tier:**
- ✅ 10 exports/miesiąc
- ✅ Photo-based
- ✅ OBJ export

**Jak używać:**
1. Scan w app
2. Processing w chmurze
3. Export OBJ

## 🎯 Porównanie funkcji

| App | Platform | Free Tier | Export Formats | LiDAR | Offline |
|-----|----------|-----------|----------------|-------|---------|
| **Polycam** | iOS, Android | 50/mo | OBJ, PLY | ✅ (iOS) | ❌ |
| **Scaniverse** | iOS only | Unlimited | OBJ, GLB, FBX | ✅ | ✅ |
| **Kiri Engine** | iOS, Android | 10/week | OBJ, GLTF | ❌ | ❌ |
| **Display.land** | iOS, Android | Unlimited | OBJ, GLB | ✅ (iOS) | ❌ |
| **Metascan** | Android | Unlimited | OBJ, PLY | ❌ | ✅ |
| **Trnio** | iOS, Android | 10/mo | OBJ | ❌ | ❌ |

**Rekomendacja:**
- **iPhone z LiDAR:** Scaniverse (najlepsze)
- **Android:** Polycam lub Kiri Engine
- **Privacy-focused:** Metascan

## 📸 Techniki skanowania telefonem

### Podstawowa technika (orbital scan)

```
1. Postaw obiekt na stole
2. Ustaw dobre oświetlenie (równomierne)
3. Otwórz app scanning
4. Zacznij od frontu obiektu
5. Powoli poruszaj się wokół (180-360°)
6. Trzymaj telefon na tej samej wysokości
7. Nachyl telefon: górne 30° → środek → dolne 30°
8. Zakończ scan
```

### Zaawansowana technika (spiral scan)

```
Start: Dół obiektu (45° w dół)
  ↓
Spirala w górę, okrążając obiekt
  ↓
Top: Góra obiektu (45° w górę)

Zapewnia pełne pokrycie 360° + góra/dół
```

### Wskazówki dla najlepszych rezultatów

1. **Światło:**
   - Równomierne, rozproszone
   - Unikaj ostrych cieni
   - Naturalne światło dzienne = najlepsze

2. **Ruch:**
   - Powolny, płynny
   - Bez szarpnięć
   - 70-80% overlap między klatkami

3. **Odległość:**
   - Obiekt zajmuje 60-80% kadru
   - Za blisko = brak głębi
   - Za daleko = brak detalu

4. **Obiekt:**
   - Matowe powierzchnie (lepsze)
   - Teksturowane (najlepsze)
   - Unikaj przezroczystych/lustrzanych

5. **Tło:**
   - Kontrastujące z obiektem
   - Nie za chaotyczne
   - Lub użyj turntable + jednolite tło

## 🔄 Workflow: Phone scan → Unity

### Metoda 1: Direct import (quick & dirty)

```bash
# 1. Export z app (GLB format)
# 2. Przenieś do Unity project
cp phone_scan.glb ~/UnityProjects/CYBERNOMAD/Assets/Models/

# 3. Import w Unity - gotowe!
```

### Metoda 2: Pipeline optimization (production quality)

```bash
# 1. Export z phone app (OBJ/GLB)
cp phone_scan.glb cbrnmd.3D/assets/input/

# 2. Optimize mesh
cd cbrnmd.3D/scripts
python optimize_mesh.py \
  --input ../assets/input/phone_scan.glb \
  --output ../assets/output/optimized_model.glb \
  --target-tris 10000 \
  --generate-lods \
  --bake-textures

# 3. Output: optimized model + LODs + PBR textures
# 4. Import do Unity
```

## 🛠️ Processing scripts

### optimize_mesh.py - Optymalizacja modelu

```python
# Podstawowe użycie
python optimize_mesh.py --input model.obj

# Z redukcją poligonów
python optimize_mesh.py \
  --input model.obj \
  --output optimized.glb \
  --target-tris 5000

# Z LODs
python optimize_mesh.py \
  --input model.obj \
  --generate-lods \
  --lod-levels 3 \
  --lod-reduction 0.5,0.25,0.1
```

### convert_formats.py - Konwersja formatów

```python
# OBJ → GLB
python convert_formats.py --input model.obj --output model.glb

# PLY → FBX
python convert_formats.py --input scan.ply --output scan.fbx
```

## 📊 Jakość: Phone vs Desktop photogrammetry

| Aspekt | Phone (LiDAR) | Phone (Photo) | Desktop (COLMAP) |
|--------|---------------|---------------|------------------|
| **Szybkość** | ⭐⭐⭐⭐⭐ (realtime) | ⭐⭐⭐⭐ (5-15 min) | ⭐⭐ (30-120 min) |
| **Jakość** | ⭐⭐⭐⭐ (bardzo dobra) | ⭐⭐⭐ (dobra) | ⭐⭐⭐⭐⭐ (doskonała) |
| **Detail** | ⭐⭐⭐ (dobre) | ⭐⭐⭐ (dobre) | ⭐⭐⭐⭐⭐ (najlepsze) |
| **Texture** | ⭐⭐⭐ (OK) | ⭐⭐⭐⭐ (dobre) | ⭐⭐⭐⭐⭐ (doskonałe) |
| **Setup** | ⭐⭐⭐⭐⭐ (zero) | ⭐⭐⭐⭐⭐ (zero) | ⭐⭐ (turntable, światła) |
| **Cost** | 💰 (masz telefon) | 💰 (masz telefon) | 💰💰💰 (sprzęt) |

**Kiedy używać phone scanning:**
- ✅ Prototyping
- ✅ Quick captures
- ✅ Location/field scanning
- ✅ Large objects (trudne dla turntable)
- ✅ Organic shapes

**Kiedy używać desktop photogrammetry:**
- ✅ Production assets
- ✅ High-detail required
- ✅ Small objects
- ✅ Controlled environment
- ✅ Batch processing wielu objektów

## 🎮 Integracja z Unity (CYBERNOMAD)

### Importing phone scans

```csharp
// W Unity:
// 1. Przeciągnij GLB do Assets/Models/PhoneScans/
// 2. Configure import settings:

Model Import Settings:
- Scale Factor: 1 (lub dopasuj)
- Mesh Compression: Medium
- Read/Write Enabled: OFF (oszczędza RAM)
- Optimize Mesh: ON
- Generate Colliders: Jeśli potrzebne

Materials:
- Extract Materials
- Material Location: Use External Materials
- Naming: By Base Texture Name

// 3. Create prefab
// 4. Add do sceny
```

### LOD Setup

```csharp
// LOD Group component
LOD 0: 0-25% distance → Full quality (10k tris)
LOD 1: 25-50% distance → 50% quality (5k tris)
LOD 2: 50-80% distance → 25% quality (2.5k tris)
LOD 3: 80-100% distance → Billboard/culled
```

### Optimization checklist

```
□ Mesh optimized (<10k tris dla prop)
□ LODs generated
□ Textures power of 2 (1024, 2048)
□ Textures compressed (DXT5/ASTC)
□ Normal maps generated
□ Collider simplified (box/capsule gdy możliwe)
□ Lightmap UVs generated (jeśli static)
□ Materials using Standard/URP shader
```

## 🔧 Troubleshooting

### Problem: Dziury w modelu
**Rozwiązanie:**
- Re-scan z większym overlap
- Użyj MeshLab → Filters → Remeshing → Close Holes

### Problem: Za duży plik
**Rozwiązanie:**
```bash
python optimize_mesh.py --input huge_model.obj --target-tris 5000
```

### Problem: Zła skala
**Rozwiązanie:**
- Umieść reference object (znany rozmiar) przy skanie
- Użyj Unity: Scale Factor w import settings

### Problem: Tekstury low quality
**Rozwiązanie:**
- Phone app: użyj 4K resolution w settings
- Desktop: re-bake tekstury z higher res zdjęć

### Problem: Watermark (Polycam free)
**Rozwiązanie:**
- Upgrade do Pro ($13/mo)
- LUB użyj innej app (Scaniverse, Display.land)
- LUB crop watermark w Blender

## 📚 Dodatkowe zasoby

**Tutorials:**
- [Scaniverse Tutorial](https://scaniverse.com/learn)
- [Polycam Docs](https://poly.cam/docs)
- [Phone Photogrammetry Best Practices](https://www.youtube.com/watch?v=dQw4w9WgXcQ)

**Communities:**
- r/photogrammetry (Reddit)
- Photogrammetry Discord
- Unity Forums - 3D Modeling

**Tools:**
- [MeshLab](https://www.meshlab.net/) - mesh cleaning
- [Blender](https://www.blender.org/) - modeling, retopology
- [Instant Meshes](https://github.com/wjakob/instant-meshes) - retopology

---

**Next steps:**
- Download wybrana app
- Zrób test scan
- Import do cbrnmd.3D
- Optimize i export do Unity!

**Questions?** Sprawdź [TURNTABLE-GUIDE.md](TURNTABLE-GUIDE.md) lub otwórz issue.
