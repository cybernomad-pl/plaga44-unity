# 3D Model Viewer Guide

Interaktywny viewer HTML do oglądania modeli 3D wygenerowanych przez cbrnmd.3D pipeline.

## 🚀 Szybki start

### Otwórz viewer

```bash
# W przeglądarce otwórz:
file:///path/to/cbrnmd.3D/viewer.html

# Lub użyj lokalnego serwera:
cd cbrnmd.3D
python3 -m http.server 8000

# Następnie otwórz: http://localhost:8000/viewer.html
```

### Załaduj swój model

**Metoda 1: Drag & Drop**
- Przeciągnij plik `.glb` lub `.gltf` bezpośrednio na viewer

**Metoda 2: File picker**
- Kliknij "Choose GLB/GLTF File"
- Wybierz swój model z `assets/output/your_model/final/model.glb`

**Metoda 3: Example models**
- Kliknij jeden z przykładowych modeli na dole strony

## ✨ Funkcje

### 🎬 Kontrola kamery
- **Obracanie**: Kliknij i przeciągnij myszką
- **Zoom**: Scroll lub pinch (mobile)
- **Pan**: Prawy przycisk myszy + przeciągnij (desktop)
- **Auto-rotate**: Automatyczny obrót modelu
- **Reset Camera**: Powrót do widoku domyślnego

### 💡 Oświetlenie
- **Toggle Shadow**: Włącz/wyłącz cienie
- **Change Environment**: Przełączaj między różnymi środowiskami oświetleniowymi
  - Neutral (domyślne)
  - Legacy (cieplejsze)
  - Warehouse (studio)

### 📸 Export
- **Screenshot**: Zrób zrzut ekranu modelu
- **Model Info**: Wyświetl informacje o modelu (format, rozmiar, etc.)

## 🎮 Skróty klawiszowe

W viewer (standardowe model-viewer controls):
- **Lewy przycisk myszy**: Obracanie
- **Scroll**: Zoom in/out
- **Prawy przycisk myszy**: Panning
- **Spacja**: Pause/resume auto-rotate

## 🔧 Integracja z pipeline'm

### Po wygenerowaniu modelu

```bash
# Uruchom pipeline
python pipeline/turntable_pipeline.py \
    --input assets/input/video.mp4 \
    --output assets/output/my_model

# Znajdź model
ls assets/output/my_model/final/model.glb

# Otwórz viewer
open viewer.html  # macOS
xdg-open viewer.html  # Linux
start viewer.html  # Windows

# Lub użyj Python serwera
python3 -m http.server 8000
# Następnie: http://localhost:8000/viewer.html
```

### Załaduj model w viewer

1. W viewer kliknij "Choose GLB/GLTF File"
2. Nawiguj do `assets/output/my_model/final/model.glb`
3. Model się automatycznie załaduje

**Lub drag & drop:**
- Znajdź `model.glb` w file explorer
- Przeciągnij na okno viewera
- Gotowe! 🎉

## 🌐 Hosting online

### GitHub Pages (darmowy)

```bash
# 1. Skopiuj viewer.html do docs/
mkdir docs
cp viewer.html docs/index.html

# 2. Skopiuj modele
cp assets/output/*/final/*.glb docs/models/

# 3. Commit i push
git add docs/
git commit -m "Add 3D viewer"
git push

# 4. W GitHub repo settings:
#    - Settings → Pages
#    - Source: docs folder
#    - Save

# 5. Twój viewer będzie dostępny pod:
# https://username.github.io/repo-name/
```

### Netlify (darmowy)

```bash
# 1. Stwórz folder z viewerem
mkdir viewer-deploy
cp viewer.html viewer-deploy/index.html
cp -r assets/output/*/final/*.glb viewer-deploy/models/

# 2. Drag & drop folder na netlify.com
# Lub użyj Netlify CLI:
npm install -g netlify-cli
netlify deploy --dir=viewer-deploy
```

## 📱 Mobile support

Viewer jest w pełni responsywny i działa na urządzeniach mobilnych:
- ✅ Touch controls (pinch to zoom, rotate)
- ✅ Gyroscope support (optional)
- ✅ Optimized for smaller screens
- ✅ Works on iOS Safari, Chrome Android

### AR Mode (iOS/Android)

Model-viewer wspiera AR (Augmented Reality):
- Na iOS Safari: Przycisk AR automatycznie się pojawi
- Na Android Chrome: Quick Look AR
- Model pojawi się w rzeczywistym świecie przez kamerę!

*Note: Wymaga kompatybilnego urządzenia (iOS 12+, Android 8+)*

## 🎨 Customizacja

### Zmiana koloru tła

W pliku `viewer.html` znajdź:

```html
<model-viewer
    background-color="#0f0f23"
    ...
>
```

Zmień `#0f0f23` na inny kolor hex.

### Zmiana auto-rotate speed

```html
<model-viewer
    auto-rotate
    rotation-per-second="30deg"
    ...
>
```

### Wyłączenie shadowów domyślnie

```html
<model-viewer
    shadow-intensity="0"
    ...
>
```

### Custom environment map

```html
<model-viewer
    environment-image="path/to/your/hdr.hdr"
    ...
>
```

## 🔗 Przykładowe modele

Viewer domyślnie ładuje przykładowe modele z KhronosGroup:
- **Damaged Helmet** - PBR showcase model
- **Avocado** - Photogrammetry-style
- **Lantern** - Detailed prop

Te modele pochodzą z oficjalnego repozytorium glTF samples:
https://github.com/KhronosGroup/glTF-Sample-Models

### Dodaj własne przykłady

W `viewer.html` znajdź sekcję "Example Models" i dodaj:

```html
<div class="model-card" onclick="loadModelFromURL('URL_TO_YOUR_MODEL.glb')">
    <h4>🎮 Your Model Name</h4>
    <p>Description of your model</p>
</div>
```

## 🛠️ Troubleshooting

### Model nie ładuje się

**Problem**: "Error loading model"

**Rozwiązania:**
1. Sprawdź czy plik ma rozszerzenie `.glb` lub `.gltf`
2. Sprawdź czy model nie jest zbyt duży (>100MB może być wolny)
3. Spróbuj otworzyć viewer przez `localhost` zamiast `file://`
4. Sprawdź console przeglądarki (F12) dla błędów

### Model jest niewidoczny/czarny

**Rozwiązania:**
1. Kliknij "Change Environment" kilka razy
2. Włącz shadow: kliknij "Toggle Shadow"
3. Reset camera: kliknij "Reset Camera"
4. Sprawdź czy model ma tekstury/materiały

### Model jest za duży/mały

**Rozwiązania:**
1. Use scroll/pinch to zoom
2. Model może być w złej skali - sprawdź podczas generowania w pipeline
3. W Unity ustaw Scale Factor podczas importu

### Viewer jest wolny

**Rozwiązania:**
1. Zmniejsz target_triangles w config.yaml podczas generowania
2. Użyj LOD versions (model_LOD1.glb, model_LOD2.glb)
3. Wyłącz auto-rotate: kliknij "Toggle Auto-Rotate"
4. Zmniejsz shadow-intensity do 0

### CORS errors (localhost)

Jeśli masz błędy CORS przy ładowaniu lokalnych plików:

```bash
# Użyj Python serwera:
python3 -m http.server 8000

# Lub Node.js:
npx http-server -p 8000

# Lub PHP:
php -S localhost:8000
```

Następnie otwórz: `http://localhost:8000/viewer.html`

## 📚 Dodatkowe zasoby

### Model Viewer docs
https://modelviewer.dev/docs/

### glTF format
https://www.khronos.org/gltf/

### Three.js (używane wewnętrznie)
https://threejs.org/

### Optimization tips
- https://modelviewer.dev/examples/loading/
- https://developers.google.com/speed/webp

## 🎓 Przykład workflow

```bash
# 1. Nagraj wideo turntable
# (Zobacz TURNTABLE-GUIDE.md)

# 2. Wygeneruj model
cd cbrnmd.3D
python pipeline/turntable_pipeline.py \
    --input assets/input/my_object.mp4 \
    --output assets/output/my_object

# 3. Znajdź wygenerowany model
ls assets/output/my_object/final/
# Output: model.glb, model_LOD1.glb, model_LOD2.glb

# 4. Otwórz viewer
python3 -m http.server 8000 &
open http://localhost:8000/viewer.html

# 5. Załaduj model (drag & drop model.glb)

# 6. Ciesz się! 🎉
# - Obracaj model
# - Zmień oświetlenie
# - Zrób screenshot
# - Wyeksportuj do Unity
```

## 💡 Pro Tips

1. **Najlepsza jakość**: Użyj model.glb (LOD0) dla najlepszej jakości
2. **Performance**: Użyj model_LOD2.glb dla szybszego ładowania
3. **Mobile**: Użyj LOD1 lub LOD2 dla urządzeń mobilnych
4. **AR**: Modele <10MB działają najlepiej w AR
5. **Screenshot**: Dla najlepszych screenshotów:
   - Wyłącz auto-rotate
   - Ustaw dobre oświetlenie (Change Environment)
   - Wykadruj idealnie
   - Kliknij Screenshot

## 🚀 Next Level

### Embed w swojej stronie

```html
<!DOCTYPE html>
<html>
<head>
    <script type="module" src="https://ajax.googleapis.com/ajax/libs/model-viewer/3.3.0/model-viewer.min.js"></script>
</head>
<body>
    <model-viewer
        src="path/to/your/model.glb"
        alt="3D Model"
        camera-controls
        auto-rotate
        ar
        style="width: 100%; height: 600px;"
    ></model-viewer>
</body>
</html>
```

### React/Vue/Angular integration

```jsx
// React example
import '@google/model-viewer';

function ModelViewer({ src }) {
    return (
        <model-viewer
            src={src}
            camera-controls
            auto-rotate
            style={{ width: '100%', height: '600px' }}
        />
    );
}
```

### Unity WebGL + Model Viewer

Możesz użyć obu razem:
- Model-viewer dla preview
- Unity WebGL dla pełnej interaktywności/gameplay

---

**Miłej zabawy z 3D models! 🎮✨**

*cbrnmd.3D - CYBERNOMAD Project*
