# PLAGA '44 -- TESTBED V2 -- Stan projektu i TODO

Repo: cybernomad-pl/plaga44-unity branch: main
Unity: 6000.3.7f1 | URP 17.3.0 | Meta XR SDK 81.0.0

---

## CO JEST (stan obecny)

### Branding
- [x] companyName: Cybernomad
- [x] productName: PLAGA 44
- [x] bundleId: games.cybernomad.plaga44
- [x] bundleVersion: 0.1.0
- [x] AndroidBundleVersionCode: 1

### Rendering
- [x] Graphics API: Vulkan (Android), Auto (Standalone)
- [x] Color Space: Linear
- [x] Scripting Backend: IL2CPP
- [x] Architecture: ARM64
- [x] URP Mobile pipeline: Mobile_RPAsset + Mobile_Renderer
- [x] URP PC pipeline: PC_RPAsset + PC_Renderer

### Quality (Mobile level -- Quest)
- [x] MSAA: x4
- [x] Shadow distance: 20m
- [x] Shadow cascades: 1
- [x] Pixel lights: 2
- [x] LOD bias: 1.0
- [x] VSync: off (VR handles it)
- [x] Realtime reflections: off
- [x] Soft particles: off

### URP Mobile_RPAsset (UWAGA -- wartosci default, do poprawy)
- [ ] HDR: ON (powinno byc OFF na Quest -- kosztowne)
- [ ] Render Scale: 0.8 (ok na start, mozna zwiekszyc do 1.0 na Quest 3)
- [ ] Shadow Distance: 50m (NIESPOJNE z Quality 20m -- URP nadpisuje)
- [ ] Shadow Resolution Main: 1024 (ok)
- [ ] Shadow Resolution Additional: 2048 (za duzo na Quest, dac 512)
- [ ] Soft Shadows: off (ok)
- [ ] MSAA in URP: 1 (NIESPOJNE z Quality x4 -- URP nadpisuje, dac 4)

### Android
- [x] Min SDK: 32 (Android 12L)
- [x] Target SDK: 32
- [x] Orientation: Landscape Left locked

### AndroidManifest.xml
- [x] VR category: com.oculus.intent.category.VR
- [x] Focus aware: true
- [x] Splash background: black
- [x] Supported devices: quest|quest2|questpro|quest3|quest3s
- [x] Head tracking required: true

### Audio
- [x] Spatializer: Meta XR Audio
- [x] Ambisonic decoder: Meta XR Audio
- [ ] Speaker Mode: Stereo (default -- ok dla VR)
- [ ] DSP Buffer: 1024 (default -- moze byc za duzy, 512 to mniej latency)

### Physics
- [x] Gravity: -9.81 (default, ok)
- [ ] Solver iterations: 6 (default -- moze byc za duzo na Quest, 4 wystarczy)
- [ ] Fixed Timestep: 0.02 (50Hz -- Quest to 72/90/120Hz, rozwazyc 0.01111 = 90Hz)

### Oculus Config (OculusProjectConfig.asset)
- [x] Target devices: Quest 1-3, Pro, 3S
- [ ] Hand tracking: DISABLED (0) -- wlaczyc gdy potrzebne
- [ ] Body tracking: DISABLED -- wlaczyc gdy potrzebne
- [ ] Face tracking: DISABLED -- wlaczyc gdy potrzebne
- [ ] Eye tracking: DISABLED -- wlaczyc gdy potrzebne
- [ ] Anchor support: DISABLED -- wlaczyc dla MR
- [ ] Scene support: DISABLED -- wlaczyc dla MR

### XR Plugin Management
- [x] OpenXR Loader enabled (Android)
- [x] XR Simulation enabled (Editor)

### Tags/Layers
- [ ] Tylko defaultowe (Default, TransparentFX, Ignore Raycast, Water, UI)
- [ ] Brak custom layers -- dodac gdy potrzebne (np. Player, Enemy, Interactable, Ground)

### Packages (manifest.json)
- [x] com.unity.xr.openxr 1.14.0
- [x] com.unity.xr.meta-openxr 2.4.0
- [x] com.meta.xr.sdk.core 81.0.0
- [x] com.meta.xr.sdk.interaction 81.0.0
- [x] com.meta.xr.sdk.interaction.ovr 81.0.0
- [x] com.meta.xr.sdk.audio 81.0.0
- [x] com.unity.render-pipelines.universal 17.3.0

### Editor Tools (Assets/Editor/)
- [x] MetaQuestSetup.cs -- auto-setup SDK on editor open
- [x] SDKVersionChecker.cs -- check installed SDK versions

---

## TODO -- PILNE (blokuje development)

### URP Pipeline Fix
- [ ] Mobile_RPAsset: HDR OFF
- [ ] Mobile_RPAsset: MSAA = 4 (nie 1)
- [ ] Mobile_RPAsset: Shadow Distance = 20 (nie 50)
- [ ] Mobile_RPAsset: Additional Shadow Resolution = 512 (nie 2048)

### Branding / Identity
- [ ] Ikona aplikacji (512x512 PNG, kwadratowa)
- [ ] Android splash screen
- [ ] Splash screen logo (Cybernomad + PLAGA '44)
- [ ] m_ShowUnitySplashScreen = 0 (wymaga Pro licencji?)

---

## TODO -- PRZED BUILDEM NA QUESTA

- [ ] Scena TESTBED_V2 z OVRCameraRig (lub OVRPlayerController)
- [ ] Przetestowac build + deploy przez ADB
- [ ] Strip engine code: ON (juz jest)

---

## TODO -- FICZERY (wydzielac z reference-branch jeden po drugim)

- [ ] CameraRig setup (wstawianie OVRCameraRig/OVRPlayerController do sceny)
- [ ] Locomotion (SmoothLocomotion, LocomotionManager, VRCrouch)
- [ ] Hand Grab (HandGrabInteractor, GrabHandPose)
- [ ] Haptic feedback (GripVibration, HapticManager)
- [ ] UI system (VRMenuManager, hamburger menu)
- [ ] NPC system (NPCStateController, NPCSpawner)
- [ ] Combat (HitDetector, ThrowableStone, M249)
- [ ] Body tracking
- [ ] Face tracking
- [ ] Eye tracking
- [ ] Mixed Reality (Passthrough)
- [ ] Networking
- [ ] Audio zones (SpatialAudioManager, AmbientZone)
- [ ] Performance (PerformanceMonitor, QualityScaler, SpaceWarp)
- [ ] Build scripts (BuildAPK, BuildQuest)

---

## TODO -- NICE TO HAVE

- [ ] Custom layers: Player, Enemy, Interactable, Ground, Water, Projectile
- [ ] Custom tags: Enemy, NPC, Weapon, Pickup, Trigger
- [ ] Physics Timestep dostrojony do Quest refresh rate
- [ ] DSP buffer 512 (mniej audio latency)
- [ ] Texture streaming ON (dla duzych scen)
- [ ] Async upload buffer 32-64MB (szybszy loading)

---

## PLIKI REFERENCYJNE

Stara pelna implementacja: branch `reference-branch`
Stary bleeding-edge: branch `bleeding-edge` (na remote)
