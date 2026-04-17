# PLAGA '44 -- TESTBED V6 -- TODO

Repo: cybernomad-pl/plaga44-unity | Unity 6000.3.7f1 | URP 17.3.0 | Meta XR SDK 81.0.0+
Ostatnia aktualizacja: 2026-04-16

---

## STAN SYSTEMOW

### Bootstrap (auto-setup sceny)
- [x] Terrain (FloodedGrounds Scene_A)
- [x] Skybox (BGR_Sky1)
- [x] Directional Light (sun)
- [x] Bounce Light (fill, rotation -90 = w gore, no shadows)
- [x] OVRCameraRig (CC + Locomotion + SmoothTurn + PlayerAvatar)
- [x] Haptic system (HapticManager na rig)
- [x] Inventory (PlayerInventory + InventoryLoadout + OVRGrabber L/R)
- [x] HamburgerMenu + SkyRotator (scene singletons)
- [x] ObjectSpawner (Items/Revolver default)
- [ ] AvatarRegistry -- EMPTY, import nie odpalil sie jeszcze

### Refactor
- [x] Wszystkie klasy Editor (26 plikow) -- done
- [x] Wszystkie klasy UI (4 pliki) -- done
- [x] Wszystkie klasy Runtime/Locomotion/Feedback (10 plikow) -- done
- [x] Wszystkie klasy Inventory (4 pliki) -- done
- [ ] Code review z Borysem -- DISCUSSION_TRACKING.md, 0/44 omowione

### Config API
- [x] 26 edytorow konfiguracji (VRPipeline, PCPipeline, Volume, Quality, Physics, Oculus, etc.)
- [x] Quest2Preset -- jeden przycisk
- [x] CYBERNOMAD menu w edytorze

---

## TODO -- BLOKUJE TESTOWANIE

### Fly / Stance system (LocomotionController) -- NOWE
- [x] R thumbstick UP = fly z akceleracja (gravity suspended)
- [x] R thumbstick DOWN short = toggle crouch
- [x] R thumbstick DOWN long = prone
- [x] Stance states: STAND -> CROUCH -> PRONE
- [x] Stary jetpack usuniety
- [ ] Przetestowac w Play Mode

### Item Browser -- NOWY
- [x] ItemBrowser.cs (singleton, auto-boot, Resources/Items/ loader)
- [x] ITEMS section w SettingsRegistry + HamburgerMenu
- [x] Spawn w dloni gracza (R/L hand anchor)
- [x] PlayerPrefs persistence (item + hand selection)
- [ ] Przetestowac w Play Mode

### HamburgerMenu -- ZMIANY
- [x] Transparent background (alpha=0)
- [x] Text shadow (Outline + Shadow na title/footer)
- [x] PRESETS sekcja usunieta
- [x] Auto-save defaults przy kazdej zmianie wartosci
- [x] Flush PlayerPrefs przy GoBack ze Settings
- [ ] Przetestowac czytelnosc na przezroczystym tle

### Avatar system
- [x] Import 5 avatarow (FBX w Assets/PLAGA44/Avatars/)
- [x] AvatarGallery preview fix -- spawn przy menu (CenterEyeAnchor) nie na glowie
- [x] useFileScale=true (fix PINEA gigantyczna)
- [x] Skip unrigged models w imporcie
- [x] PlayerAvatar persistence (restore z PlayerPrefs)
- [ ] Zweryfikowac AvatarRegistry.asset po imporcie
- [ ] Przetestowac w Play Mode

### Object Spawner
- [x] ObjectSpawner + ObjectSpawnerSetup (bootstrap)
- [ ] Przetestowac spawn Revolvera w Play Mode

### Bounce Light
- [x] BounceLightSetup (bootstrap)
- [ ] Przetestowac wizualnie

---

## TODO -- PRZED BUILDEM NA QUESTA

- [ ] Build Settings: TESTBED_V6.unity jako aktywna scena
- [ ] Quest2Preset.Apply() -- upewnic sie ze URP ustawienia sa SAFE
- [ ] Test build + deploy ADB
- [ ] APK backup do C:\Users\boris\Desktop\PLAGA44\builds\

---

## TODO -- OTWARTE DECYZJE (Borys)

- [!] Meta XR SDK: manifest.json ma 83.0.0, MetaQuestSetup.cs const = 81.0.0 -- update const albo downgrade?
- [!] Unused packages: visualscripting, collab-proxy, modules.cloth, physics2d, tilemap -- usunac?
- [!] Assembly Definitions (Plaga44.Core, .UI, .Editor) -- przyspieszy kompilacje 3-10x, wymaga sortowania deps
- [!] PlayerAvatarImporter.cs -- potencjalny dead code (AvatarImport pokrywa Avatars/). Usunac?
- [!] SkyboxSetup traktuje pierwszy directional light jako sun -- teraz pomija "Bounce" w nazwie, ale brak dedykowanego sun GO moze byc problem

---

## TODO -- FICZERY (nastepne kroki)

### Tier 1 (core gameplay)
- [ ] NPC system (NPCStateController, NPCSpawner) -- z reference-branch
- [ ] Combat (HitDetector, ThrowableStone, M249)
- [ ] Audio zones (SpatialAudioManager, AmbientZone)

### Tier 2 (immersion)
- [ ] Body tracking (OculusConfig.SetBodyTracking)
- [ ] Hand tracking (OculusConfig.SetHandTracking)
- [ ] Face/Eye tracking
- [ ] Mixed Reality Passthrough

### Tier 3 (polish)
- [ ] Performance monitor (QualityScaler, SpaceWarp)
- [ ] Custom layers: Player, Enemy, Interactable, Ground, Water, Projectile
- [ ] Custom tags: Enemy, NPC, Weapon, Pickup, Trigger
- [ ] Networking

---

## TODO -- CONTENT

### Avatary (w Assets/PLAGA44/Avatars/)
- [x] Survivor_A_Lusth (Mixamo FBX, Humanoid)
- [x] Swat (Mixamo FBX, Humanoid)
- [x] Vanguard_By_T._Choonyung (Mixamo FBX, Humanoid)
- [x] PINEA (rigged FBX + packed0 textures)
- [x] PINEA-NEO (rigged FBX + packed0 textures)
- [ ] OBJ characters need DAE/FBX conversion: Anglojanek, Charon, Klaszczur, Niedziadek, wodnik, Zakazny, Female1
- [ ] Mixamo nonPBR characters (Ch11/20/32/35/36/48) -- do testow

### Bronie
- [x] Revolver (GameDevHQ FBX + prefab w Resources/Items/)
- [ ] M249 (exports/weapon-m249/M249_low.fbx) -- do zaimportowania
- [ ] Gun (exports/character-gun/Gun.fbx) -- do zaimportowania

### Terrain
- [ ] Reimport FloodedGrounds terrain layers -- dziury po usunietych materialach dirt/asfalt

---

## ZNANE BUGI

- [ ] CharacterRetargeter spam "Failed to retarget source frame data!" -- znalezc GO z komponentem
- [ ] LocomotionEventsConnection Handlers[0] null -- skonfigurowac albo usunac
- [ ] Missing Prefab StylizedCharacterLocomotion (guid 286d7e20) -- znalezc w backup
- [ ] CharacterController namespace warning -- broken script reference na prefabie?

---

## PLIKI REFERENCYJNE

- REFACTOR_PLAN.md -- szczegolowy log refactoru (zakonczony)
- DISCUSSION_TRACKING.md -- checklist code review z Borysem (0/44)
- CLAUDE.md -- CYBERNOMAD Config API reference
- reference-branch -- stara pelna implementacja
- bleeding-edge -- branch rozwojowy (remote)
