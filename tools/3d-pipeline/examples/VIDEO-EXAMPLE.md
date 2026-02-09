# Przykładowe Wideo Turntable - BRAK W REPO

## ❌ Problem: Brak przykładowego wideo

Tutorial obecnie zawiera tylko **ZDJĘCIA**:
- `turntable_example/` - 36 zdjęć JPG
- `castle_example/` - 11 zdjęć JPG

**Pipeline działa z tymi zdjęciami** używając `photo_pipeline.py`, ale **NIE MA przykładowego VIDEO** do `turntable_pipeline.py`.

---

## ✅ Rozwiązanie: Użyj własnego wideo

### Opcja 1: Nagraj własne wideo (5 minut)

**Potrzebujesz:**
- Telefon/kamera
- Mały obiekt (zabawka, kubek, butelka)
- Obrotowa podstawka (lub ręcznie obracaj obiekt)
- Dobre światło (2 lampy z boków)

**Jak nagrać:**
1. Postaw obiekt na środku
2. Nagrywaj wideo Full HD, 30 FPS
3. Pełny obrót 360° (wolno, 10-15 sekund)
4. Zapisz jako `turntable_video.mp4`
5. Przenieś do `cbrnmd.3D/assets/input/`

**Przetwórz:**
```bash
cd cbrnmd-content\cbrnmd.3D
python pipeline\turntable_pipeline.py -i assets\input\turntable_video.mp4 -o assets\output\my_model
```

---

### Opcja 2: Pobierz przykładowe wideo z internetu

**Free turntable videos:**

1. **Pexels** (darmowe, bez rejestracji):
   - https://www.pexels.com/search/videos/turntable/
   - https://www.pexels.com/search/videos/rotating%20object/

2. **Pixabay** (darmowe):
   - https://pixabay.com/videos/search/turntable/

3. **YouTube** (download z YouTube-DL):
   - Szukaj: "turntable product video"
   - Download: `yt-dlp "URL" -f mp4`

**Po pobraniu:**
```bash
# Przenieś wideo
copy turntable_video.mp4 cbrnmd-content\cbrnmd.3D\assets\input\

# Uruchom pipeline
cd cbrnmd-content\cbrnmd.3D
python pipeline\turntable_pipeline.py -i assets\input\turntable_video.mp4 -o output\my_model
```

---

### Opcja 3: Wygeneruj wideo z istniejących zdjęć (ffmpeg)

**Jeśli masz ffmpeg:**
```bash
cd cbrnmd-content\cbrnmd.3D\examples\turntable_example\input
ffmpeg -framerate 6 -pattern_type glob -i "*.jpg" -c:v libx264 -pix_fmt yuv420p -movflags +faststart ../turntable_video.mp4
```

**Przetwórz wygenerowane wideo:**
```bash
cd cbrnmd-content\cbrnmd.3D
python pipeline\turntable_pipeline.py -i examples\turntable_example\turntable_video.mp4 -o output\from_video
```

---

## 📋 Alternatywa: Tutorial działa z ZDJĘCIAMI

**Tutorial już MA przykładowe zdjęcia!**

```powershell
# Backend API
cd cbrnmd-content\cbrnmd.3D
python backend\api.py
```

```powershell
# HTTP Server (w drugim terminalu)
cd cbrnmd-content\cbrnmd.3D
python -m http.server 8000
```

**Otwórz:** `http://localhost:8000/tutorial.html`

**Wybierz dataset:**
- **Castle** - 11 prawdziwych zdjęć zamku Sceaux
- **Turntable** - 36 syntetycznych zdjęć obracającego się obiektu

Backend przetworzy **ZDJĘCIA** (nie wideo) przez COLMAP → 3D model.

---

## 🎯 Najszybsze rozwiązanie: Użyj istniejących zdjęć

Tutorial **JUŻ DZIAŁA** z przykładowymi zdjęciami - nie potrzebujesz wideo do testów!

1. Uruchom backend + http server (oneliners powyżej)
2. Otwórz `http://localhost:8000/tutorial.html`
3. Wybierz **Castle** lub **Turntable**
4. Kliknij "URUCHOM" dla każdego kroku
5. Zobacz wyniki w 3D viewerze

**Wideo turntable potrzebne tylko jeśli chcesz testować pipeline ekstrakcji klatek z video.**
