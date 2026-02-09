# Castle Example - Sceaux Castle Dataset

Przykładowy dataset: zamek Sceaux (openMVG test dataset)

## 📁 Zawartość

- **input/** - 11 zdjęć zamku Sceaux
- **Readme.txt** - Oryginalna dokumentacja datasetu

## 📸 O datasecie

Dataset z oficjalnego repozytorium openMVG:
- **Źródło:** https://github.com/openMVG/ImageDataset_SceauxCastle
- **Zdjęcia:** 11 obrazów
- **Obiekt:** Fragment zamku
- **Licencja:** Public domain / openMVG

## 🚀 Uruchomienie pipeline

```bash
cd cbrnmd.3D

# Uruchom pipeline na tym datasecie
python pipeline/photo_pipeline.py \
    --input examples/castle_example/input \
    --output examples/castle_example/output
```

## ⚙️ Oczekiwane wyniki

Ten dataset powinien wygenerować:
- Sparse 3D reconstruction zamku
- Point cloud z teksturami
- Mesh fragment architektury

**Uwaga:** To nie jest turntable - to zestaw zdjęć z różnych kątów wokół obiektu.

## 🎯 Zastosowanie

Ten przykład pokazuje:
- Photo-based photogrammetry (nie turntable)
- Rekonstrukcję architektoniczną
- Multi-view stereo z różnych pozycji kamery

## 📚 Więcej przykładów

Więcej datasetów openMVG:
- https://github.com/openMVG/openMVG/wiki/Samples
