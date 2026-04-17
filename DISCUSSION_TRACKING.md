# PLAGA '44 -- Omawianie kodu z Borysem

Status refactoru: zakonczony, commit `5d49c12` na branchu `feat/revolver-holster`.
Teraz omawiam kazda klase z Borysem -- on patrzy, ja prezentuje zmiany i uzasadnienie.

Legenda: `[ ]` do omowienia, `[~]` w trakcie, `[x]` omowione.

## Kolejnosc (wg REFACTOR_PLAN)

### I. Editor (26 plikow)

- [ ] `Editor/Bootstrap.cs` (563 -> 475) ★ START
- [ ] `Editor/AvatarImport.cs` (579 -> 490)
- [ ] `Editor/MetaQuestSetup.cs` (317 -> 265) -- DEAD CODE usuniete
- [ ] `Editor/RevolverPrefabBuilder.cs` (119 -> 115)
- [ ] `Editor/PlayerAvatarImporter.cs` (31 -- minor)
- [ ] `Editor/BuildScript.cs` (85 -> 80) -- namespace fix
- [ ] `Editor/EditorConfig.cs` -- struct rename
- [ ] `Editor/BuildScenesConfig.cs` -- clean (bez zmian)
- [ ] `Editor/Pipeline.cs` -- clean (MEDIUM duplikacja noted)
- [ ] `Editor/URPGlobalConfig.cs` -- clean
- [ ] `Editor/VolumeConfig.cs` -- clean
- [ ] `Editor/RendererConfig.cs` -- clean
- [ ] `Editor/QualityConfig.cs` -- struct rename
- [ ] `Editor/PhysicsConfig.cs` -- clean
- [ ] `Editor/ProjectConfig.cs` -- struct rename
- [ ] `Editor/PackagesConfig.cs` -- clean
- [ ] `Editor/ManifestConfig.cs` -- clean
- [ ] `Editor/LayersConfig.cs` -- clean
- [ ] `Editor/OculusConfig.cs` -- clean
- [ ] `Editor/MiscConfig.cs` -- clean
- [ ] `Editor/GraphicsConfig.cs` -- struct rename
- [ ] `Editor/AudioConfig.cs` -- struct rename
- [ ] `Editor/NavMeshConfig.cs` -- clean
- [ ] `Editor/InputConfig.cs` -- clean
- [ ] `Editor/MemoryConfig.cs` -- clean
- [ ] `Editor/ISDKLocomotionSetup.cs` -- clean

### II. UI (4 pliki)

- [ ] `Scripts/UI/HamburgerMenu.cs` (736 -> 625) ★ DUZY
- [ ] `Scripts/UI/SettingsRegistry.cs` (640 -> 615, surgical)
- [ ] `Scripts/UI/MenuNotifier.cs` (nowy 143 linii)
- [ ] `Scripts/UI/SettingsLogger.cs` -- clean

### III. Runtime Core / Locomotion / Feedback (10 plikow)

- [ ] `Scripts/Locomotion/LocomotionController.cs` (324 -> 315)
- [ ] `Scripts/Core/PlayerAvatar.cs` (233 -> 245, metody extract)
- [ ] `Scripts/Core/AvatarGallery.cs` (219 -> 235, Start rozbity)
- [ ] `PLAGA44/AvatarRegistry.cs` -- clean
- [ ] `Scripts/Core/GameState.cs` -- clean (tutoriale w komentarzach)
- [ ] `Scripts/Feedback/HapticManager.cs` (178 -> 175, PlayImpact)
- [ ] `Scripts/Feedback/HapticOnGrab.cs` -- clean
- [ ] `Scripts/Core/ControllerModeHelper.cs` -- clean
- [ ] `Scripts/Locomotion/SmoothTurnController.cs` (58 -> 62)
- [ ] `Scripts/Core/SkyRotator.cs` (58 -> 62)

### IV. Inventory (4 pliki)

- [ ] `Scripts/Inventory/PlayerInventory.cs` -- clean
- [ ] `Scripts/Inventory/InventoryLoadout.cs` (87 -> 98, SpawnInto rozbity)
- [ ] `Scripts/Inventory/HolsterAnchor.cs` -- clean
- [ ] `Scripts/Inventory/PlagaGrabbable.cs` -- clean

### V. Setup projektu -- otwarte pytania

- [!] `Packages/manifest.json` -- Meta XR 83.0.0 (manifest) vs 81.0.0 (MetaQuestSetup const) -- **DECYZJA**
- [!] `Packages/manifest.json` -- unused deps: visualscripting, collab-proxy, modules.cloth, physics2d, tilemap -- **DECYZJA**
- [!] Brak Assembly Definitions -- Plaga44.Core, Plaga44.UI, Plaga44.Editor -- osobne zadanie, pozniej
- [ ] `EditorBuildSettings.asset` -- naprawione wczesniej (TESTBED.unity)
- [ ] Folder struktura `Assets/` -- OK

## Uwagi / pytania podczas omawiania

(Tu wpisuje na biezaco co Borys zauwazy albo zasugeruje)

---

**Aktualna pozycja: czekam na `Bootstrap.cs` (pierwsza klasa).**
