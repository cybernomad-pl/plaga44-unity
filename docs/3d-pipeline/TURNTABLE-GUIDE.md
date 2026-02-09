# Turntable Video Guide

Kompletny przewodnik kręcenia wideo turntable dla fotogrametrii.

## 🎬 Czym jest turntable video?

Turntable video to nagranie obiektu obracającego się 360° na stole obrotowym przy nieruchomej kamerze. To jedna z najłatwiejszych i najbardziej efektywnych metod zbierania danych do fotogrametrii.

## 🛠️ Sprzęt

### Minimalne wymagania

1. **Kamera/Smartfon**
   - Rozdzielczość: Full HD (1920x1080) minimum
   - FPS: 30 fps (60 fps lepsze)
   - Stabilizacja: Włączona
   - Focus: Manualny (zalecane) lub auto

2. **Stół obrotowy**
   - Ręczny turntable (najtańsze: 20-50 PLN)
   - Elektryczny turntable (100-500 PLN)
   - DIY: silnik + kontroler (Arduino/ESP32)

3. **Oświetlenie**
   - 2-3 softboxy lub lampy LED
   - Równomierne rozproszone światło
   - Bez ostrych cieni

4. **Tło**
   - Jednokolorowe (szare, zielone, niebieskie)
   - Matowe (bez odbić)
   - Wystarczająco duże aby pokryć kadr

### Zalecany setup (profesjonalny)

- **Kamera:** DSLR/Mirrorless (4K)
- **Turntable:** Elektryczny z kontrolerem prędkości
- **Oświetlenie:** 3x softbox LED (5500K daylight)
- **Tło:** Green screen 2x3m
- **Statyw:** Stabilny, regulowany

### DIY Budget Setup (~200 PLN)

```
Smartfon + statyw = 50 PLN
Ręczny turntable = 30 PLN
2x lampa LED = 60 PLN
Tło karton/tkanina = 30 PLN
Klamerki, taśma = 30 PLN
```

## 📸 Parametry nagrania

### Rozdzielczość i FPS

```yaml
Minimum:
  Resolution: 1920x1080 (Full HD)
  FPS: 30
  Format: MP4, H.264

Zalecane:
  Resolution: 3840x2160 (4K)
  FPS: 60
  Format: MP4, H.265/HEVC

Profesjonalne:
  Resolution: 4K+
  FPS: 60+
  Format: ProRes, RAW
```

### Czas nagrania

```
Wolny obrót: 10-15 sekund na 360°
Normalny obrót: 5-8 sekund na 360°
Szybki obrót: 3-4 sekundy na 360°

Zalecenie: 8 sekund @ 30fps = 240 klatek
Użyjemy co 2-4 klatki = 60-120 zdjęć
```

### Nakładanie się klatek (overlap)

Dla dobrej rekonstrukcji **70-80% overlap** między kolejnymi klatkami:

```
60 zdjęć = co 6° obrotu
120 zdjęć = co 3° obrotu (lepsze)
```

## 🎨 Oświetlenie

### Setup podstawowy (2-light)

```
        [KAMERA]
            |
            |
    [LIGHT] [OBIEKT] [LIGHT]
         45°    ↻    45°

Wysokość świateł: 45° powyżej obiektu
Odległość: 1-2 metry od obiektu
Moc: Równomierna z obu stron
```

### Setup zaawansowany (3-light)

```
        [KAMERA]
            |
            |
    [LIGHT] [OBIEKT] [LIGHT]
         45°    ↻    45°

         [BACKLIGHT]
         (za obiektem)

Key light: 100% moc (lewa)
Fill light: 50-70% moc (prawa)
Back light: 30-50% moc (eliminuje cienie)
```

### Ustawienia światła

- **Temperatura:** 5500K (daylight) - wszystkie światła ta sama!
- **Dyfuzja:** Softboxy lub dyfuzory (miękkie światło)
- **Pozycja:** 45° z boku, 45° z góry
- **Test:** Zrób zdjęcie próbne - brak ostrych cieni

## 📐 Pozycjonowanie kamery

### Odległość od obiektu

```
Małe obiekty (5-20cm): 50-100cm
Średnie obiekty (20-50cm): 100-200cm
Duże obiekty (50cm+): 200-400cm

Zasada: Obiekt zajmuje 60-80% kadru
```

### Wysokość kamery

```
Standard: Na wysokości środka obiektu
Wariant: 3 wideo z różnych wysokości:
  - Górne: 30° powyżej
  - Środek: 0° (poziomo)
  - Dolne: 30° poniżej
```

###焦距 i ustawienia kamery

```yaml
Lens:
  - 50mm (full frame) - standard
  - 35-85mm zakres dobry
  - Unikaj ultra-wide (<24mm) - dystorsja

Camera settings:
  Aperture: f/8 - f/11 (duża głębia ostrości)
  Shutter: 1/60 - 1/125 (sharp, bez motion blur)
  ISO: Najniższe możliwe (100-400)
  White Balance: Manual 5500K
  Focus: Manual (AF może "pływać")
```

## 🎥 Proces nagrywania

### Przygotowanie

1. **Stabilizuj kamerę**
   - Solidny statyw
   - Wyłącz stabilizację obrazu (może wprowadzać artefakty)
   - Zablokuj wszystkie osie statywu

2. **Ustaw obiekt**
   - Wyśrodkuj na turntable
   - Sprawdź czy jest stabilny
   - Usuń połyskliwe elementy/odbicia

3. **Skonfiguruj oświetlenie**
   - Równomierne z obu stron
   - Test białej kartki - brak hot spots
   - Sprawdź cienie na tle

4. **Test shot**
   - Zrób krótkie 5-sekundowe test video
   - Sprawdź focus, ekspozycję, kadrowanie
   - Popraw jeśli potrzeba

### Nagrywanie

```bash
Krok 1: Naciśnij REC
Krok 2: Poczekaj 2 sekundy (stabilizacja)
Krok 3: Rozpocznij powolny, równomierny obrót
Krok 4: Pełne 360° + dodatkowe 10-20° (overlap)
Krok 5: Zatrzymaj obrót
Krok 6: Poczekaj 2 sekundy
Krok 7: STOP
```

**Ważne:** Obrót musi być **równomierny** (stała prędkość)!

### Warianty nagrania

#### Single-pass (podstawowy)
- 1 wideo, kamera na wysokości środka obiektu
- Wystarczające dla większości obiektów

#### Multi-elevation (lepszy)
- 3 wideo z różnych wysokości:
  1. +30° (góra)
  2. 0° (środek)
  3. -30° (dół)
- Lepsza rekonstrukcja góry i dołu obiektu

#### Multi-pass orbital (profesjonalny)
- 3-5 wideo orbital (kamera się porusza wokół obiektu)
- Najlepsza jakość rekonstrukcji
- Wymaga gimbal/slider

## ❌ Częste błędy

### 1. Niestabilna kamera
**Objaw:** Drżący obraz, artefakty w modelu
**Rozwiązanie:** Solidny statyw, zablokowane osie

### 2. Nierówne oświetlenie
**Objaw:** Ciemne obszary, trudności z matchingiem
**Rozwiązanie:** Dodaj fill light, użyj softboxów

### 3. Za szybki obrót
**Objaw:** Motion blur, za mało overlap
**Rozwiązanie:** Wolniejszy obrót (8-10 sek)

### 4. Refleksyjne powierzchnie
**Objaw:** Highlight shift, trudności z trackingiem
**Rozwiązanie:** Matujący spray, zmieniaj kąt światła

### 5. Autofocus hunting
**Objaw:** Focus "pływa", rozmyte klatki
**Rozwiązanie:** Manualny focus

### 6. Jednorodne tło = obiekt
**Objaw:** Brak kontrastu, słabe feature detection
**Rozwiązanie:** Zmień tło lub dodaj teksturę

## 📊 Checklist przed nagraniem

```
□ Kamera na statywie (stabilna)
□ Manualny focus (ustawiony na obiekt)
□ Manualny white balance (5500K)
□ Aperture f/8-f/11
□ ISO najniższe możliwe
□ Shutter 1/60 - 1/125
□ Format MP4 H.264/H.265
□ Full HD minimum (4K lepsze)
□ 30 fps minimum (60 fps lepsze)
□ Oświetlenie równomierne z obu stron
□ Tło jednorodne, matowe
□ Obiekt wyśrodkowany na turntable
□ Testowe 5-sekundowe wideo zrobione
□ Sprawdzony focus, ekspozycja, kadrowanie
□ Prędkość obrotu ~8 sekund/360°
```

## 🎯 Wskazówki profesjonalne

1. **Więcej klatek = lepiej**
   - 120 zdjęć lepsze niż 60
   - Ale 200+ to diminishing returns

2. **Multiple passes**
   - Nagraj 2-3 razy to samo
   - Użyj najlepszego nagrania

3. **Markers na turntable**
   - Naklejki/markery pomagają w trackingu
   - Nie za dużo (5-10 wystarczy)

4. **RAW photos > video**
   - Jeśli masz czas: zrób 120 RAW zdjęć zamiast wideo
   - Lepsza jakość, więcej kontroli

5. **Calibration target**
   - Umieść skalę/ruler obok obiektu (1 klatka)
   - Pomaga w kalibracji rozmiaru

## 📱 Nagrywanie smartfonem

### Najlepsze aplikacje

#### iOS
- **Filmic Pro** ($15) - pełna kontrola manualna
- **ProCam** ($10) - dobry balans cena/możliwości
- **Native Camera** - wystarczający dla podstaw

#### Android
- **Cinema FV-5** ($4) - pełne manualne ustawienia
- **Open Camera** (FREE) - open source, dobre możliwości
- **Native Camera Pro mode** - jeśli telefon ma

### Ustawienia smartfona

```yaml
Resolution: 4K (3840x2160)
FPS: 60 (lub 30 jeśli brakuje miejsca)
Focus: Manual lock
Exposure: Manual lock
White Balance: Daylight (5500K)
Stabilization: On (optical OIS)
HDR: Off
Format: H.265 (mniejsze pliki)
```

### Mocowanie smartfona

- **Statyw:** Mini tripod z adapter (~30 PLN)
- **Gimbal:** DJI OM (jeśli potrzebujesz ruchu)
- **DIY:** Telefon w uchwycie + książki/pudełka

## 🔄 Po nagraniu

### Sprawdzenie jakości

```bash
1. Odtwórz wideo ramka po ramce
2. Sprawdź czy focus jest ostry przez cały obrót
3. Sprawdź czy oświetlenie jest równomierne
4. Sprawdź czy obrót jest płynny
5. Sprawdź czy obiekt nie wychodzi z kadru
```

### Backup

```bash
# Zapisz oryginalne wideo!
cp turntable_video.mp4 turntable_video_ORIGINAL.mp4

# Lub sync do chmury
rsync -avz *.mp4 backup_server:/backups/
```

### Następny krok

Przenieś wideo do `cbrnmd.3D/assets/input/` i uruchom pipeline:

```bash
cd cbrnmd.3D/pipeline
python turntable_pipeline.py --input ../assets/input/your_video.mp4
```

## 📚 Przykładowe setupy

### Budget Setup (~200 PLN)
```
Telefon: Twój smartfon (4K capable)
Statyw: Mini tripod (30 PLN)
Turntable: Ręczny obrotowy (30 PLN)
Światło: 2x LED panel (80 PLN)
Tło: Biały karton A1 (20 PLN)
Taśma/klamerki: (20 PLN)
```

### Mid-range (~1000 PLN)
```
Kamera: używany DSLR (500 PLN)
Statyw: Manfrotto compact (200 PLN)
Turntable: Elektryczny (150 PLN)
Światło: 2x softbox LED (300 PLN)
Tło: Green screen 2x2m (100 PLN)
```

### Pro Setup (~5000+ PLN)
```
Kamera: Sony A7 III / Canon EOS R
Obiektyw: 50mm f/1.8 lub 24-70mm f/2.8
Statyw: Profesjonalny fluid head
Turntable: Elektryczny z kontrolerem
Światło: 3x softbox LED professional
Tło: Cyclorama lub green screen 3x6m
```

## 🎓 Zasoby do nauki

- [COLMAP Tutorial](https://colmap.github.io/tutorial.html)
- [Meshroom Tutorial](https://meshroom-manual.readthedocs.io/)
- YouTube: "photogrammetry turntable setup"
- YouTube: "DIY photogrammetry lighting"

---

**Gotowy do nagrywania?** Sprawdź [SETUP.md](SETUP.md) dla instalacji narzędzi processing.
