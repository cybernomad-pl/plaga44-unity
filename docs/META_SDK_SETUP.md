# PLAGA '44

Projekt: PLAGA '44 VR survival, post-apo Polska 2144
Unity: 6000.3.7f1 (Unity 6.3 LTS)
Target: Meta Quest 2 / Quest 3
Data dokumentu: 2026-02-09

---

## Scoped Registry -- konfiguracja manifest.json

Aby Unity Package Manager mogl rozwiazac pakiety Meta, potrzebny jest scoped registry:

```json
"scopedRegistries": [
  {
    "name": "Meta XR",
    "url": "https://npm.developer.oculus.com",
    "scopes": [
      "com.meta.xr"
    ]
  }
]
```

Dodajemy TYLKO scoped registry do manifest.json. Pakietow Meta NIE dodajemy do dependencies -- jest aktywny bug z licencja v83 na Unity 6.3 (patrz sekcja na koncu). Pakiety Unity (openxr, meta-openxr) instaluja sie automatycznie po wybraniu Meta Quest build platform.

- Zrodlo: Meta Developers, "Meta XR UPM Packages": https://developers.meta.com/horizon/documentation/unity/unity-package-manager/
- Meta npm registry: https://npm.developer.oculus.com/

---

## Architektura pakietow

### Warstwa 1: Unity OpenXR

Od Meta XR SDK v74+, Meta wymaga Unity OpenXR Plugin zamiast starego Oculus XR Plugin.

| Pakiet | Wersja | Cel |
|--------|--------|-----|
| `com.unity.xr.openxr` | >= 1.14.0 | Bazowy plugin OpenXR |
| `com.unity.xr.meta-openxr` | 2.4.0 | Meta-specific OpenXR extensions |

- https://docs.unity3d.com/6000.3/Documentation/Manual/xr-meta-quest-packages.html
- https://docs.unity3d.com/6000.3/Documentation/Manual/com.unity.xr.meta-openxr.html
- https://docs.unity3d.com/Packages/com.unity.xr.meta-openxr@2.1/manual/install.html

### Warstwa 2: Meta XR SDK

| Pakiet | Cel |
|--------|-----|
| `com.meta.xr.sdk.core` | OVRManager, OVRCameraRig, Building Blocks, Guardian, Passthrough |
| `com.meta.xr.sdk.interaction` | Interaction SDK: rece, kontrolery, grab, poke |
| `com.meta.xr.sdk.audio` | Spatializacja dzwieku 3D |

- https://developers.meta.com/horizon/documentation/unity/unity-package-manager/
- https://developers.meta.com/horizon/documentation/unity/unity-core-sdk/

### Linki do kluczowych funkcji Meta XR SDK

- Building Blocks: https://developers.meta.com/horizon/documentation/unity/bb-overview/
- OVRCameraRig: https://developers.meta.com/horizon/documentation/unity/unity-ovrcamerarig/
- Body/Hand/Eye Tracking: https://developers.meta.com/horizon/documentation/unity/move-unity-getting-started/
- Passthrough API: https://developers.meta.com/horizon/documentation/unity/unity-passthrough-tutorial/

---

## Setup krok po kroku (reczny w Unity Editor)

### Krok 1: Otworz projekt w Unity 6.3.7f1

### Krok 2: Wybierz Meta Quest build platform
1. File > Build Profiles
2. Wybierz "Meta Quest" z listy platform
3. Kliknij "Switch Platform"
4. Unity automatycznie zainstaluje OpenXR Plugin i com.unity.xr.meta-openxr

Zrodlo: Unity 6000.3 Manual, "Meta Quest build platform and build profile"
URL: https://docs.unity3d.com/6000.3/Documentation/Manual/xr-meta-quest-build-profile.html

### Krok 3: Sprawdz domyslne ustawienia (Unity 6.3 ustawia automatycznie)
Unity 6.3 po Switch Platform ustawia:
- Graphics API: Vulkan
- Minimum Android API: Level 29 (Android 10.0)
- Target API: Level 32 (Android 12L)
- Scripting Backend: IL2CPP
- Architecture: ARM64
- Stereo Rendering: Instancing
- Anisotropic Filtering: Forced On

Zrodlo: Unity 6000.3 Manual, "Meta Quest build platform and build profile"
URL: https://docs.unity3d.com/6000.3/Documentation/Manual/xr-meta-quest-build-profile.html

### Krok 4: Zainstaluj Meta XR Core SDK
1. Window > Package Manager
2. Upewnij sie, ze scoped registry Meta jest widoczny (powinien byc po zmianach w manifest.json)
3. Szukaj "Meta XR Core SDK"
4. Zainstaluj -- JESLI wystapi license error, sprobuj:
   a. Unity Hub > Preferences > Licenses > Refresh
   b. Jesli nie pomoze: zainstaluj wersje 81.0 zamiast 83.0 (uzyj version dropdown)
   c. Jesli dalej nie dziala: zainstaluj przez Asset Store (alternatywna sciezka)

Meta XR Core SDK Asset Store: https://assetstore.unity.com/packages/tools/integration/meta-xr-core-sdk-269169
Meta XR All-in-One SDK Asset Store: https://assetstore.unity.com/packages/tools/integration/meta-xr-all-in-one-sdk-269657

### Krok 5: Konfiguracja XR Plugin Management
1. Edit > Project Settings > XR Plug-in Management
2. Zakladka Android: wlacz "OpenXR"
3. Pod OpenXR: dodaj "Meta Quest Feature Group"
4. Interaction Profile: "Meta Quest Touch Pro Controller Profile" lub "Oculus Touch Controller Profile"

Zrodlo: Meta Developers, "XR Plugin Management for Meta Quest"
URL: https://developers.meta.com/horizon/documentation/unity/unity-xr-plugin/

### Krok 6: Dodaj scene z OVRCameraRig
1. Usun domyslna Main Camera ze sceny
2. Meta > Tools > Building Blocks
3. Kliknij (+) na "Camera Rig"
4. Opcjonalnie dodaj: "Controller Tracking", "Hand Tracking"

Alternatywnie (recznie):
1. Usun Main Camera
2. Dodaj pusty GameObject, dodaj komponent OVRManager
3. Uzyj prefab OVRCameraRig z Meta XR Core SDK

Zrodlo: Meta Developers, "Configure Meta XR camera settings"
URL: https://developers.meta.com/horizon/documentation/unity/unity-ovrcamerarig/
Zrodlo: Meta Developers, "Explore Meta Quest Features with Building Blocks"
URL: https://developers.meta.com/horizon/documentation/unity/bb-overview/

### Krok 7: Player Settings (dodatkowe)
1. Edit > Project Settings > Player > Android
2. Color Space: Linear (wymagane dla VR)
3. Company Name: ustawic na nazwe studia
4. Product Name: "PLAGA 44"
5. Package Name: com.cybernomad.plaga44 (lub odpowiedni)
6. Minimum API Level: 29 (powinno byc juz ustawione)

### Krok 8: Quality Settings
1. Edit > Project Settings > Quality
2. Wylacz Vsync (Meta runtime zarzadza framerate)
3. Anti-Aliasing: 4x MSAA (minimum dla VR)
4. Texture Quality: Full Res
5. Anisotropic Textures: Force On (powinno byc juz ustawione)

---

## Performance -- budzety z shipped gier Quest

### Triangle/Polygon budgets

| Metric | Quest 2 | Quest 3 | Zrodlo |
|--------|---------|---------|--------|
| Total triangles/frame | 750K - 1M | 1M - 1.5M (estymacja) | github.com/authorTom/notes-on-VR-performance |
| Character model | 5K - 15K tri | 10K - 20K tri | VRChat guidelines + industry practice |
| Draw calls (busy scene) | 80 - 200 | 100 - 300 (estymacja) | github.com/authorTom/notes-on-VR-performance |
| Draw calls (light scene) | 400 - 600 | 500 - 800 (estymacja) | github.com/authorTom/notes-on-VR-performance |
| Target FPS | 72 Hz minimum, 90 Hz recommended | 72/90/120 Hz | Meta documentation |

Zrodla:
- Performance notes: https://github.com/authorTom/notes-on-VR-performance
- Meta testing docs: https://developers.meta.com/horizon/documentation/unity/unity-perf/
- VRChat Quest optimization: https://creators.vrchat.com/platforms/android/quest-content-optimization/

### Tekstury

| Ustawienie | Wartosc | Dlaczego |
|------------|---------|----------|
| Kompresja | ASTC (6x6 lub 8x8) | Standard na ARM/Quest, najlepszy stosunek jakosc/rozmiar |
| Max rozmiar tekstury | 1024x1024 (postacie), 2048x2048 (environment) | Ograniczona pamiec Quest |
| Mipmapping | Wlaczone | Obowiazkowe -- redukuje bandwidth |
| Anisotropic Filtering | Forced On | Juz ustawione przez Unity 6.3 Meta Quest profile |

Zrodlo: Meta Developers Blog, "Tech Note: Unity Settings for Mobile VR"
URL: https://developers.meta.com/horizon/blog/tech-note-unity-settings-for-mobile-vr/
Zrodlo: Meta Community Forums, "What texture format is best"
URL: https://communityforums.atmeta.com/t5/Quest-Development/What-texture-format-is-best/td-p/747756

### Skinning

| Ustawienie | Wartosc | Dlaczego |
|------------|---------|----------|
| Blend Weights | 2 bones max | Wiecej = wiekszy koszt GPU na mobile |
| Bone count per character | < 75 | Ograniczenie Quest GPU |

Zrodlo: Meta Developers, performance guidelines
URL: https://developers.meta.com/horizon/documentation/unity/unity-perf/

### Rendering

| Ustawienie | Wartosc |
|------------|---------|
| Stereo Rendering | Single Pass Instanced |
| Graphics API | Vulkan (nie GLES) |
| Anti-Aliasing | 4x MSAA |
| Foveated Rendering | Wlaczone (5 poziomow, zero overhead) |
| VSync | Wylaczony (Meta runtime zarzadza) |

Zrodlo: Unity 6000.3 Meta Quest build profile defaults
URL: https://docs.unity3d.com/6000.3/Documentation/Manual/xr-meta-quest-build-profile.html

---

## Podsumowanie decyzji

1. **Uzywamy Meta XR SDK** (com.meta.xr.sdk.core) + Unity OpenXR (com.unity.xr.meta-openxr)
2. **Scoped registry** dodany do manifest.json (npm.developer.oculus.com)
3. **Pakiety Meta NIE sa dodane do manifest.json dependencies** -- instalacja reczna w edytorze z powodu aktywnego buga licencji v83
4. **Pakiety Unity** (openxr, meta-openxr) sa instalowane automatycznie po wybraniu Meta Quest build platform
5. **Setup sceny** musi byc reczny w Unity Editor (Building Blocks, OVRCameraRig)
6. **Editor script** dolaczony do automatyzacji co sie da (Player Settings, Quality Settings)

---

## Znane problemy i migracja

### License error: Meta XR Core SDK v83 + Unity 6.3

**Meta XR Core SDK v83.0 + Unity 6.3 LTS = license error** (aktywny bug na 2026-02-09).

Blad: "The following packages were not registered because your license doesn't allow it: com.meta.xr.sdk.core@83.0"

Workaroundy:
- Unity Hub > Preferences > Licenses > Refresh
- Uzyc wersji v81 zamiast v83
- Zainstalowac przez Asset Store zamiast UPM
- Uzyc TYLKO com.unity.xr.meta-openxr (bez Meta XR SDK) -- mniej funkcji, ale dziala
- Downgrade do Unity 6.0 LTS (dziala z Meta XR SDK)
- Czekac na fix od Meta/Unity

Zrodla:
- https://communityforums.atmeta.com/discussions/Questions_Discussions/unity-6-3---meta-xr-core-license-error/1357387
- https://discussions.unity.com/t/unity-6-3-lts-meta-xr-core-license-error/1699953
- https://discussions.unity.com/t/meta-xr-all-in-one-v83-error-license/1700402

### Ryzyka i plan migracji na czysty OpenXR

Meta oficjalnie przeszlo na OpenXR od v31. Od v74 (2024) wymagaja Unity 6 + OpenXR Plugin. Meta XR SDK to de facto wrapper nad OpenXR z Meta-specific extensions. Trend: Meta idzie w kierunku OpenXR.

W 2024-2025 ujawniono ze Meta OVRPlugin blokuje non-Meta headsety na PCVR przez OpenXR. Dla Quest-only projektu to nie problem, ale przy porcie na SteamVR trzeba wyciagnac OVRPlugin.

- https://developers.meta.com/horizon/blog/oculus-all-in-on-openxr-deprecates-proprietary-apis/
- https://www.uploadvr.com/metas-unity-unreal-openxr-sdks-block-other-pc-vr-headsets/

**Plan migracji (gdyby byl potrzebny):**

1. Zamien OVRCameraRig na XR Origin (com.unity.xr.interaction.toolkit)
2. Zamien OVRInput na Unity Input System + XR Controller
3. Zamien OVRManager na ustawienia w XR Plug-in Management
4. Zamien Building Blocks na XR Interaction Toolkit components
5. Przetestuj na Quest przez OpenXR runtime
6. Estymowany czas: 2-4 tygodnie dla sredniej wielkosci projektu
