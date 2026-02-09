# Turntable Example - Synthetic Object

Przykładowy dataset: syntetyczny obiekt obracający się 360°

## 📁 Zawartość

- **input/** - 36 zdjęć syntetycznego obiektu (co 10°)
- **output/** - Wygenerowany model 3D (demo_model.glb)

## 🚀 Uruchomienie pipeline

```bash
cd cbrnmd.3D

# Uruchom pipeline na tym datasecie
python pipeline/photo_pipeline.py \
    --input examples/turntable_example/input \
    --output examples/turntable_example/output_test
```

## 📊 Wyniki

Dataset został przetworzony przez COLMAP:
- **Zdjęcia:** 36
- **Feature points:** 18-32 per image
- **Rekonstrukcja:** Sparse point cloud (30 punktów 3D)
- **Format wyjściowy:** GLB (1.4 KB)

## 👁️ Podgląd modelu

```bash
# Otwórz viewer
python3 -m http.server 8000

# W przeglądarce: http://localhost:8000/viewer.html
# Przeciągnij: examples/turntable_example/output/demo_model.glb
```

## 📝 Uwagi

To jest **syntetyczny dataset** stworzony dla demonstracji pipeline'u.
Prawdziwe obiekty z turntable wygenerują:
- Więcej feature points (100-1000+ per image)
- Gęstszy point cloud (10,000-100,000+ punktów)
- Pełny textured mesh

## 🎯 Zastosowanie

Użyj tego przykładu żeby:
- Przetestować czy pipeline działa
- Zrozumieć workflow
- Sprawdzić instalację narzędzi
