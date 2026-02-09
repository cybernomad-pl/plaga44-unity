# Meta XR SDK Setup -- PLAGA '44

Projekt: PLAGA '44 VR survival, post-apo Polska 2144
Unity: 6000.3.7f1 (Unity 6.3 LTS)
Target: Meta Quest 2 / Quest 3
Data dokumentu: 2026-02-09

---

## Dlaczego Meta XR SDK a nie czysty OpenXR

### Kontekst decyzji

Projekt PLAGA '44 celuje WYLACZNIE w Meta Quest 2/3. Nie planujemy portowania na SteamVR, PSVR2 ani inne platformy VR. W tym kontekscie Meta XR SDK daje przewage nad czystym OpenXR.

### Dowody z shipped gier

1. **Beat Saber** -- JEDYNA potwierdzona migracja duzej gry na OpenXR. Migracja dotyczy TYLKO wersji PC (kwiecien 2023). Quest nadal nie zostal zmigrowany. Zespol uzyl "brute force trial and error" do testowania, co sugeruje ze migracja nie jest trywialna.
   - Zrodlo: Khronos Blog, "Keeping the Beat: Porting Beat Saber to OpenXR", 6 pazdziernik 2023
   - URL: https://www.khronos.org/blog/keeping-the-beat-porting-beat-saber-to-openxr-for-an-improved-developer-experience

2. **Walking Dead: Saints & Sinners** -- uzywaja Unreal Engine 4.26.2 (oryginalna gra) / 4.27.2 (Chapter 2). Gra dziala na ekosystemie Oculus/Meta, nie na czystym OpenXR.
   - Zrodlo: GitHub SDK modow (UE4.27.2 shell project)
   - URL: https://github.com/substatica/TWD-CH2-SDK
   - Zrodlo: Unreal Engine interview
   - URL: https://www.unrealengine.com/en-US/developer-interviews/the-walking-dead-saints-sinners-2-continues-to-raise-the-vr-bar
   - UWAGA: Gra jest na Unreal Engine, nie Unity. Pokazuje jednak ze duze studia uzywaja natywnych SDK danej platformy dla Quest.

3. **Asgard's Wrath 2** -- Unreal Engine 4, wyprodukowane przez Sanzaru Games (nabyte przez Meta/Oculus Studios w 2020). Uzywa Meta SDKs (m.in. Meta XR Audio SDK, Haptics SDK).
   - Zrodlo: Meta Developers Blog, "Asgard's Wrath 2: A Deep Dive into the Haptic Design Journey"
   - URL: https://developers.meta.com/horizon/blog/asgards-wrath2-and-haptics-studio/
   - Zrodlo: Wikipedia
   - URL: https://en.wikipedia.org/wiki/Asgard%27s_Wrath_2
   - UWAGA: Gra jest na Unreal Engine, nie Unity. Ale potwierdza uzycie Meta-specific SDKs.

### Zalety Meta XR SDK dla Quest-only projektu

- **Building Blocks**: gotowe bloki do drag-and-drop (Camera Rig, Passthrough, Controller Tracking, Hand Tracking) -- bez pisania kodu
  - Zrodlo: Meta Developers, "Explore Meta Quest Features with Building Blocks"
  - URL: https://developers.meta.com/horizon/documentation/unity/bb-overview/

- **OVRCameraRig**: dedykowany prefab kamery z pelnym wsparciem Quest (tracking, guardian, passthrough)
  - Zrodlo: Meta Developers, "Configure Meta XR camera settings"
  - URL: https://developers.meta.com/horizon/documentation/unity/unity-ovrcamerarig/

- **Body/Hand/Eye Tracking**: natywne wsparcie bez dodatkowych warstw abstrakcji
  - Zrodlo: Meta Developers, "Getting started with the Meta XR Movement SDK"
  - URL: https://developers.meta.com/horizon/documentation/unity/move-unity-getting-started/

- **Passthrough API**: kluczowe dla mixed reality na Quest 3
  - Zrodlo: Meta Developers, "Passthrough basic tutorial"
  - URL: https://developers.meta.com/horizon/documentation/unity/unity-passthrough-tutorial/

### Kiedy OpenXR ma sens (nie nasz przypadek)

OpenXR jest lepszy gdy projekt celuje w wiele platform jednoczesnie (Quest + SteamVR + PSVR2). Beat Saber zrobil to wlasnie dlatego -- mieli osobne implementacje Steam VR i Oculus VR na PC i chcieli je zlaczyc.

Nasz projekt celuje WYLACZNIE w Quest 2/3. Cross-platform nie jest wymagany.

---

## Architektura pakietow -- co instalujemy i dlaczego

### Warstwa 1: Unity OpenXR (pakiety Unity)

Od Meta XR SDK v74+, Meta wymaga uzycia Unity OpenXR Plugin zamiast starego Oculus XR Plugin.

| Pakiet | Wersja | Zrodlo | Cel |
|--------|--------|--------|-----|
| `com.unity.xr.openxr` | >= 1.14.0 | Unity Package Manager (wbudowany) | Bazowy plugin OpenXR -- komunikacja z runtime |
| `com.unity.xr.meta-openxr` | 2.4.0 (dla Unity 6.3) | Unity Package Manager (wbudowany) | Meta-specific OpenXR extensions (Quest features) |

- Zrodlo: Unity 6000.3 Manual, "Packages and templates for Meta Quest development"
- URL: https://docs.unity3d.com/6000.3/Documentation/Manual/xr-meta-quest-packages.html
- Zrodlo: Unity 6000.3 Manual, "Unity OpenXR Meta"
- URL: https://docs.unity3d.com/6000.3/Documentation/Manual/com.unity.xr.meta-openxr.html

**WAZNE**: W Unity 6.3 po wybraniu Meta Quest jako build platform, Unity AUTOMATYCZNIE instaluje com.unity.xr.meta-openxr.
- Zrodlo: Unity Docs, "Install Unity OpenXR: Meta"
- URL: https://docs.unity3d.com/Packages/com.unity.xr.meta-openxr@2.1/manual/install.html

### Warstwa 2: Meta XR SDK (pakiety Meta)

Te pakiety pochodza z Meta scoped registry (npm.developer.oculus.com) i dodaja Quest-specyficzne funkcjonalnosci.

| Pakiet | Cel |
|--------|-----|
| `com.meta.xr.sdk.core` | Rdzen: OVRManager, OVRCameraRig, Building Blocks, Guardian, Passthrough |
| `com.meta.xr.sdk.interaction` | Interaction SDK: rece, kontrolery, grab, poke |
| `com.meta.xr.sdk.audio` | Spatializacja dzwieku 3D dla Quest |

- Zrodlo: Meta Developers, "Meta XR UPM Packages"
- URL: https://developers.meta.com/horizon/documentation/unity/unity-package-manager/
- Zrodlo: Meta Developers, "Meta XR Core SDK Overview"
- URL: https://developers.meta.com/horizon/documentation/unity/unity-core-sdk/

### KRYTYCZNY PROBLEM: License error z Meta XR SDK v83 + Unity 6.3

Na dzien pisania tego dokumentu (2026-02-09) istnieje ZNANY BUG:

**Meta XR Core SDK v83.0 + Unity 6.3 LTS = license error**

Blad: "The following packages were not registered because your license doesn't allow it: com.meta.xr.sdk.core@83.0"

- Problem dotyczy v83 (z v81 dziala)
- Blad wystepuje na roznych typach licencji (Personal, Pro)
- Workaround: Unity Hub > Preferences > Licenses > Refresh (nie zawsze dziala)
- Alternatywa: uzyc wersji v81 zamiast v83

Zrodla:
- Meta Community Forums: https://communityforums.atmeta.com/discussions/Questions_Discussions/unity-6-3---meta-xr-core-license-error/1357387
- Unity Discussions: https://discussions.unity.com/t/unity-6-3-lts-meta-xr-core-license-error/1699953
- Unity Discussions (V83): https://discussions.unity.com/t/meta-xr-all-in-one-v83-error-license/1700402

**DECYZJA**: W manifest.json dodajemy scoped registry Meta, ale NIE pinujemy konkretnej wersji pakietow. Instalacja Meta XR SDK bedzie robiona reczenie w Unity Editor (przez Package Manager lub Platform Browser) po sprawdzeniu ktora wersja dziala z Unity 6.3.7f1.

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

Scope `com.meta.xr` obejmuje wszystkie pakiety Meta XR (sdk.core, sdk.interaction, sdk.audio, sdk.platform, simulator, itd.).

- Zrodlo: Meta Developers, "Meta XR UPM Packages"
- URL: https://developers.meta.com/horizon/documentation/unity/unity-package-manager/
- Zrodlo: Meta npm registry
- URL: https://npm.developer.oculus.com/

### Co dodajemy do manifest.json (minimalne)

Dodajemy TYLKO scoped registry. Nie dodajemy konkretnych pakietow Meta do dependencies, bo:
1. Jest aktywny bug z licencja v83 na Unity 6.3
2. Pakiety Unity (com.unity.xr.openxr, com.unity.xr.meta-openxr) sa instalowane automatycznie przez Unity po wybraniu Meta Quest build platform
3. Pakiety Meta (com.meta.xr.sdk.core) lepiej zainstalowac recznie w edytorze, gdzie mozna wybrac dzialajaca wersje

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

## Ryzyka i migracja

### Ryzyko 1: Meta zmienia SDK

Meta oficjalnie przeszlo na OpenXR od v31 (blog z 2021, deprecja starych API sierpien 2022). Od v74 (2024) wymagaja Unity 6 + OpenXR Plugin.

Trend jest JASNY: Meta idzie w kierunku OpenXR. Meta XR SDK (com.meta.xr.sdk.core) to de facto wrapper nad OpenXR z Meta-specific extensions.

Jesli Meta zdeprecionuje Meta XR SDK:
- com.unity.xr.meta-openxr juz teraz daje duzo funkcji bez Meta SDK
- Migracja = zamiana OVRCameraRig na XR Origin + XR Interaction Toolkit
- Wiekszose logiki gry (AI, crafting, survival) nie zalezy od VR SDK

Zrodlo: Meta Developers Blog, "Oculus All In on OpenXR: Deprecates Proprietary APIs"
URL: https://developers.meta.com/horizon/blog/oculus-all-in-on-openxr-deprecates-proprietary-apis/

### Ryzyko 2: License error z Unity 6.3

Aktywny problem na dzien 2026-02-09. Jesli nie da sie zainstalowac Meta XR SDK:
- Opcja A: downgrade do Unity 6.0 LTS (dziala z Meta XR SDK)
- Opcja B: uzyc TYLKO com.unity.xr.meta-openxr (bez Meta XR SDK) -- mniej funkcji, ale dziala
- Opcja C: zainstalowac przez Asset Store zamiast UPM scoped registry
- Opcja D: czekac na fix od Meta/Unity

### Ryzyko 3: Meta blokuje inne headsety przez OVRPlugin

W 2024-2025 ujawniono ze Meta OVRPlugin blokuje non-Meta headsety na PCVR nawet przez OpenXR.

Dla naszego projektu (Quest-only) to NIE jest problem. Ale gdybysmy chcieli port na SteamVR -- musielibysmy wyciagnac OVRPlugin.

Zrodlo: UploadVR, "Meta's Unity & Unreal OpenXR Integrations Block Other PC VR Headsets"
URL: https://www.uploadvr.com/metas-unity-unreal-openxr-sdks-block-other-pc-vr-headsets/

### Plan migracji na czysty OpenXR (gdyby byl potrzebny)

1. Zamien OVRCameraRig na XR Origin (z com.unity.xr.interaction.toolkit)
2. Zamien OVRInput na Unity Input System + XR Controller
3. Zamien OVRManager na ustawienia w XR Plug-in Management
4. Zamien Building Blocks na XR Interaction Toolkit components
5. Przetestuj na Quest przez OpenXR runtime
6. Estymowany czas: 2-4 tygodnie dla sredniej wielkosci projektu

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

### Referencje z konkretnych gier Quest

| Gra | Engine | Detale |
|-----|--------|--------|
| Beat Saber | Unity | PC zmigrowane na OpenXR (kwiecien 2023), Quest nadal na starym SDK |
| Walking Dead: S&S | UE4.26.2 / 4.27.2 | Quest 2 baseline, Quest 3 update ze zwiekszona liczba walkerow |
| Asgard's Wrath 2 | UE4 | 700+ efektow haptycznych, Quest 3 enhanced rendering, 90Hz toggle |
| Batman: Arkham Shadow | NIEPOTWIERDZONE | Quest 3 exclusive (2024) |
| Alien: Rogue Incursion | NIEPOTWIERDZONE | Quest 3 (2024) |

---

## Podsumowanie decyzji

1. **Uzywamy Meta XR SDK** (com.meta.xr.sdk.core) + Unity OpenXR (com.unity.xr.meta-openxr)
2. **Scoped registry** dodany do manifest.json (npm.developer.oculus.com)
3. **Pakiety Meta NIE sa dodane do manifest.json dependencies** -- instalacja reczna w edytorze z powodu aktywnego buga licencji v83
4. **Pakiety Unity** (openxr, meta-openxr) sa instalowane automatycznie po wybraniu Meta Quest build platform
5. **Setup sceny** musi byc reczny w Unity Editor (Building Blocks, OVRCameraRig)
6. **Editor script** dolaczony do automatyzacji co sie da (Player Settings, Quality Settings)

---

## Zrodla -- kompletna lista

1. Khronos Blog -- Beat Saber OpenXR (2023-10-06): https://www.khronos.org/blog/keeping-the-beat-porting-beat-saber-to-openxr-for-an-improved-developer-experience
2. Meta Developers -- Building Blocks: https://developers.meta.com/horizon/documentation/unity/bb-overview/
3. Meta Developers -- OVRCameraRig: https://developers.meta.com/horizon/documentation/unity/unity-ovrcamerarig/
4. Meta Developers -- XR Plugin Management: https://developers.meta.com/horizon/documentation/unity/unity-xr-plugin/
5. Meta Developers -- UPM Packages: https://developers.meta.com/horizon/documentation/unity/unity-package-manager/
6. Meta Developers -- Core SDK Overview: https://developers.meta.com/horizon/documentation/unity/unity-core-sdk/
7. Meta Developers -- Project Setup: https://developers.meta.com/horizon/documentation/unity/unity-project-setup/
8. Meta Developers -- Testing and Performance: https://developers.meta.com/horizon/documentation/unity/unity-perf/
9. Meta Developers -- OpenXR Deprecation Blog: https://developers.meta.com/horizon/blog/oculus-all-in-on-openxr-deprecates-proprietary-apis/
10. Meta Developers -- Asgard's Wrath 2 Haptics: https://developers.meta.com/horizon/blog/asgards-wrath2-and-haptics-studio/
11. Meta Developers -- Passthrough Tutorial: https://developers.meta.com/horizon/documentation/unity/unity-passthrough-tutorial/
12. Meta Developers -- Movement SDK: https://developers.meta.com/horizon/documentation/unity/move-unity-getting-started/
13. Meta Developers -- OpenXR + Meta blog: https://developers.meta.com/horizon/blog/openxr-standard-quest-horizonos-unity-unreal-godot-developer-success/
14. Meta Developers -- Tech Note Unity Settings: https://developers.meta.com/horizon/blog/tech-note-unity-settings-for-mobile-vr/
15. Unity 6000.3 Manual -- Meta Quest packages: https://docs.unity3d.com/6000.3/Documentation/Manual/xr-meta-quest-packages.html
16. Unity 6000.3 Manual -- Meta Quest build profile: https://docs.unity3d.com/6000.3/Documentation/Manual/xr-meta-quest-build-profile.html
17. Unity 6000.3 Manual -- XR packages: https://docs.unity3d.com/6000.3/Documentation/Manual/xr-support-packages.html
18. Unity 6000.3 Manual -- OpenXR Meta: https://docs.unity3d.com/6000.3/Documentation/Manual/com.unity.xr.meta-openxr.html
19. Unity Docs -- OpenXR Meta Install: https://docs.unity3d.com/Packages/com.unity.xr.meta-openxr@2.1/manual/install.html
20. Unity Discussions -- Meta Core SDK workaround: https://discussions.unity.com/t/unity-6-meta-core-sdk-workaround/1538217
21. Unity Discussions -- License error 6.3: https://discussions.unity.com/t/unity-6-3-lts-meta-xr-core-license-error/1699953
22. Meta Community Forums -- License error: https://communityforums.atmeta.com/discussions/Questions_Discussions/unity-6-3---meta-xr-core-license-error/1357387
23. Meta Asset Store -- Core SDK: https://assetstore.unity.com/packages/tools/integration/meta-xr-core-sdk-269169
24. Meta Asset Store -- All-in-One: https://assetstore.unity.com/packages/tools/integration/meta-xr-all-in-one-sdk-269657
25. UploadVR -- Meta OpenXR blocking: https://www.uploadvr.com/metas-unity-unreal-openxr-sdks-block-other-pc-vr-headsets/
26. GitHub -- TWD:S&S Chapter 2 SDK (UE4.27.2): https://github.com/substatica/TWD-CH2-SDK
27. Unreal Engine -- TWD:S&S interview: https://www.unrealengine.com/en-US/developer-interviews/the-walking-dead-saints-sinners-2-continues-to-raise-the-vr-bar
28. GitHub -- VR Performance Notes: https://github.com/authorTom/notes-on-VR-performance
29. VRChat -- Quest Optimization: https://creators.vrchat.com/platforms/android/quest-content-optimization/
30. Meta NPM Registry: https://npm.developer.oculus.com/
31. Unity Discussions -- OpenXR vs Meta OpenXR: https://discussions.unity.com/t/whats-the-difference-between-unity-open-xr-and-meta-open-xr-can-i-use-both/946470
32. Meta Developers -- OpenXR Compatibility: https://developers.meta.com/horizon/documentation/unity/unity-and-openxr-compatibility/
33. Meta Developers -- Downloads Core SDK: https://developers.meta.com/horizon/downloads/package/meta-xr-core-sdk/
