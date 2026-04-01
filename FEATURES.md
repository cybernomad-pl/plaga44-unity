# PLAGA44 TESTBED -- FEATURE LIST (2026-04-01)

```
============================================================
  PLAGA44 VR SURVIVAL TESTBED -- Quest 2/3 + PCVR
  Build: quest-testbed-default branch
  Engine: Unity 6000.3.7f1 + Meta XR SDK + URP
============================================================

RENDERING
---------
[x] URP pipeline z XR stereo rendering
[x] Dwa profile: SAFE (Quest standalone) / HI-END (PCVR/Editor)
[x] Hardcoded presety: PresetSafe.cs, PresetHiEnd.cs
[x] Runtime quality menu (B button) -- 125+ suwaków
[x] Preset system: SLOT 1:HI-END, SLOT 2:CUSTOM, SLOT 3:SAFE
[x] Save to Log (ADB logcat extraction)
[x] Foveated rendering (poziomy 0-3)
[x] MSAA (2/4/8)
[x] Render scale (0.5-2.0)

NIEBO
-----
[x] Custom skybox shader (Skybox_Rotating)
[x] Animowana rotacja nieba
[x] Cloud brightness boost
[x] Cloud threshold
[x] Sky tint RGB + exposure
[x] Runtime suwaki

WODA
----
[x] Custom shader FG_PBR_Water
[x] Gerstner waves (4 octave)
[x] Dual normal map scrolling
[x] Fresnel reflections
[x] Depth-based transparency (shallow=transparent, deep=opaque)
[x] Edge foam na brzegach
[x] Kolor/metallic/smoothness/emission/UV density
[x] Wave height/freq/complexity/steepness
[x] Efekt podwodny (zielony tint + vigneta)
[x] Runtime suwaki wszystkiego

TEREN
-----
[x] FloodedGrounds terrain z 3 warstwami
[x] Per-layer: normal scale, tile size, metallic, smoothness
[x] Runtime Perlin noise deformacja
[x] Terrain material tuning
[x] Runtime suwaki

DRZEWA
------
[x] Bark kolor RGB
[x] Bark smoothness + specular
[x] Runtime suwaki

OSWIETLENIE
-----------
[x] Directional light: intensity, kolor, shadow strength
[x] Bounce intensity
[x] Ambient mode (skybox) + intensity + kolor
[x] Fog: density, start/end, kolor
[x] Color grading: exposure, contrast, saturation, hue, filter
[x] Reflection probe (realtime, tylko HI-END)
[x] Runtime suwaki

KONTROLERY / INPUT
------------------
[x] Smooth locomotion (lewy stick)
[x] Snap turn (prawy stick)
[x] Sprint x3 (L3 / lewy stick click)
[x] Menu toggle (B button)
[x] Spawner toggle (X button)
[x] Remove last item (Y button)
[x] Suwaki: prawy/lewy trigger
[x] Sekcje: prawy stick gora/dol
[x] Menu blokuje ruch

SPAWNER
-------
[x] Item spawner (X button)
[x] M249 SAW z prefabem
[x] GazeThrow (korekcja gazeowa przy rzucaniu)
[x] Skalowanie (lewy stick lewo/prawo)

M249 SAW
--------
[x] FBX model (6844 verts)
[x] Rozbity na 5 parts (handguard, receiver, grip, stock, magazine)
[x] M249GripFix (orientacja lufy wzdluz palca)
[x] M249Handler: two-handed grip (lewa reka na carry handle/rail)
[x] M249Handler: bipod deploy przy crouch
[x] M249MaterialSetup: runtime gunmetal material
[x] Suwaki: gun color RGB, metallic, smoothness
[ ] Rozbity model jako osobne GameObjects (OBJ zaimportowane, nie podpiete)
[ ] Bipod visual animation
[ ] Muzzle flash / firing

BODY TRACKING
-------------
[x] BodyTrackingManager (SDK-agnostic via reflection)
[x] OVRBody integration (Quest 3)
[x] Crouch detection
[x] Lean detection
[x] Debug skeleton renderer
[ ] IK foot grounding (placeholder)
[ ] Full body avatar

PARTICLE EFFECTS
----------------
[x] Shoreline spray (fontanna na brzegach)
[x] Shoreline mist (mgla)
[ ] Impact particles
[ ] Weapon effects

PERFORMANCE
-----------
[x] PerformanceBenchmark: auto 5s warmup + 30s test
[x] FPS min/max/avg, frame time, memory
[x] StartupLogger: memory + XR status co sekunde
[x] FPS counter w menu header

BUILD / DEPLOY
--------------
[x] BuildQuest.cs (batch mode Android build)
[x] build-plaga44-apk.sh (CLI skrypt)
[x] ADB install + launch pipeline
[x] ADB screencap pipeline
[x] Safe mode auto-detection (UNITY_EDITOR vs device)

BRAK / TODO
-----------
[ ] PCVR standalone build (SteamVR/OpenXR desktop)
[ ] Multiplayer (Netcode / Photon / Fish-Net)
[ ] Audio (ambient, weapons, footsteps)
[ ] NPC / AI enemies
[ ] Health system
[ ] Inventory
[ ] Crafting
[ ] Save/load game state
[ ] Main menu / UI
[ ] Optimization pass (draw calls, batching, LOD)
```
