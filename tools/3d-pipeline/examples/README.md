# Examples

Przykładowe projekty i dane testowe dla cbrnmd.3D.

## 📁 Struktura

```
examples/
├── turntable_example/     # ✅ Syntetyczny turntable (36 zdjęć)
│   ├── input/            # 36 JPG files
│   ├── output/           # demo_model.glb (wygenerowany)
│   └── README.md         # Instrukcje
└── castle_example/        # ✅ Zamek Sceaux (11 zdjęć)
    ├── input/            # 11 JPG files
    └── README.md         # Instrukcje
```

## 🎯 Dostępne przykłady

### 1. Turntable Example (Syntetyczny)

**Dataset:** 36 syntetycznych zdjęć obracającego się obiektu
**Wygenerowany model:** `output/demo_model.glb` ✅ (gotowy do podglądu)

```bash
# Zobacz gotowy model w viewer
python3 -m http.server 8000
# Otwórz: http://localhost:8000/viewer.html
# Przeciągnij: examples/turntable_example/output/demo_model.glb
```

**Lub przetworz ponownie:**
```bash
cd cbrnmd.3D
python pipeline/photo_pipeline.py \
    --input examples/turntable_example/input \
    --output examples/turntable_example/output_new
```

### 2. Castle Example (Prawdziwe zdjęcia)

**Dataset:** 11 zdjęć zamku Sceaux (openMVG dataset)
**Status:** Gotowy do przetworzenia

```bash
cd cbrnmd.3D
python pipeline/photo_pipeline.py \
    --input examples/castle_example/input \
    --output examples/castle_example/output
```

## 📥 Pobieranie przykładowych danych

Przykładowe dane nie są included w repo (za duże).

### Opcja 1: Użyj własnych danych
- Nagraj wideo turntable (zobacz [TURNTABLE-GUIDE.md](../docs/TURNTABLE-GUIDE.md))
- Zrób serię zdjęć

### Opcja 2: Publiczne datasety

**Sketchfab Downloads** (free models z photo scans):
- https://sketchfab.com/search?features=downloadable&type=models

**ETH3D Dataset** (academic):
- https://www.eth3d.net/datasets

**Tanks and Temples** (benchmark dataset):
- https://www.tanksandtemples.org/download/

## 📝 Tworzenie własnych przykładów

1. Nagraj/zrób zdjęcia obiektu
2. Umieść w `input/`
3. Uruchom pipeline
4. Sprawdź wyniki w `output/`
5. Dokumentuj proces w README.md

## 🎮 Przykłady dla Unity

Po wygenerowaniu modeli:

```bash
# Skopiuj do projektu Unity
cp output/final/model.glb ~/UnityProjects/CYBERNOMAD/Assets/Models/

# Lub wszystkie LODy
cp output/final/*.glb ~/UnityProjects/CYBERNOMAD/Assets/Models/Generated/
```

## ⚠️ Uwagi

- Przykładowe dane mogą być duże (100+ MB)
- Processing może zająć 15-60 minut na przykład
- Wymagane: co najmniej 16 GB RAM dla większych modeli

## 📚 Dodatkowe zasoby

Jeśli potrzebujesz przykładowych obiektów do skanowania:
- Małe figurki/zabawki
- Kamienie/skały
- Butelki
- Narzędzia
- Rośliny (bez ruchu!)

**Best objects:**
- Matowe powierzchnie
- Teksturowane (nie jednolite kolory)
- Stabilne (nie ruszają się)
- 5-50 cm wysokości

**Avoid:**
- Lustrzane/przezroczyste
- Za małe (<2 cm)
- Za duże (>1 m) - trudniejsze
- Poruszające się (rośliny na wietrze)
