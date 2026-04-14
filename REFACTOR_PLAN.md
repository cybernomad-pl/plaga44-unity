# PLAGA '44 -- Refactor Plan (2026-04-15)

Cel: clean code, metody zamiast komentarzy, zero duplikacji, zero zmian public API / zachowania.
Legenda: `[ ]` pending, `[~]` in progress, `[x]` done, `[-]` skip (juz czysty), `[!]` problem wymaga decyzji Borysa.

## Editor (starting point -- Borys wybor)

- [x] `Editor/Bootstrap.cs` (563 -> ~475)
- [x] `Editor/AvatarImport.cs` (579 -> ~490)
- [x] `Editor/MetaQuestSetup.cs` (317 -> ~265, DEAD CODE removed)
- [x] `Editor/RevolverPrefabBuilder.cs` (119 -> ~115)
- [x] `Editor/PlayerAvatarImporter.cs` (31 -- cleanup, nadal ~33)
- [x] `Editor/BuildScript.cs` (85 -> ~80, + namespace fix + build-quest.sh update)
- [x] `Editor/EditorConfig.cs` (113) + `BuildScenesConfig.cs` (107) -- struct rename, clean
- [x] `Editor/Pipeline.cs` (251) + `URPGlobalConfig.cs` (248) + `VolumeConfig.cs` (256) -- clean (medium duplikacje Load/Set/Apply na pozniej)
- [x] `Editor/RendererConfig.cs` (173) + `QualityConfig.cs` (228) -- struct rename
- [x] `Editor/PhysicsConfig.cs` (172) + `ProjectConfig.cs` (169) + `PackagesConfig.cs` (168) -- struct rename
- [x] `Editor/ManifestConfig.cs` (191) + `LayersConfig.cs` (180) + `OculusConfig.cs` (147) -- clean
- [x] `Editor/MiscConfig.cs` (142) + `GraphicsConfig.cs` (135) + `AudioConfig.cs` (132) -- struct rename
- [x] `Editor/NavMeshConfig.cs` (96) + `InputConfig.cs` (53) + `MemoryConfig.cs` (44) + `ISDKLocomotionSetup.cs` (42) -- clean

## UI

- [x] `Scripts/UI/HamburgerMenu.cs` (736 -> ~625)
- [x] `Scripts/UI/SettingsRegistry.cs` (640 -> ~615, surgical)
- [-] `Scripts/UI/MenuNotifier.cs` (143) -- juz czysty (nowy kod)
- [-] `Scripts/UI/SettingsLogger.cs` (33) -- juz czysty (maly, focused)

## Runtime Core / Locomotion / Feedback

- [x] `Scripts/Locomotion/LocomotionController.cs` (324 -> ~315)
- [x] `Scripts/Core/PlayerAvatar.cs` (233 -> ~245, extracted methods)
- [x] `Scripts/Core/AvatarGallery.cs` (219 -> ~235, monster `Start()` rozbity)
- [-] `PLAGA44/AvatarRegistry.cs` -- juz czysty, nie ruszany
- [-] `Scripts/Core/GameState.cs` (195) -- opisowy tutorial w komentarzach, ale logika czysta. Zostawiam, Borys moze chciec ksztalcacych komentarzy.
- [x] `Scripts/Feedback/HapticManager.cs` (178 -> ~175, refactor `PlayImpact`)
- [-] `Scripts/Feedback/HapticOnGrab.cs` (80) -- clean
- [-] `Scripts/Core/ControllerModeHelper.cs` (47) -- clean (juz poprawione wczesniej przy grab vibration fix)
- [x] `Scripts/Locomotion/SmoothTurnController.cs` (58 -> 62, + OnDisable dla symetrii)
- [x] `Scripts/Core/SkyRotator.cs` (58 -> ~62, magic numbers + extract)

## Inventory

- [-] `Scripts/Inventory/PlayerInventory.cs` (114) -- clean
- [x] `Scripts/Inventory/InventoryLoadout.cs` (87 -> ~98, `SpawnInto` rozbity)
- [-] `Scripts/Inventory/HolsterAnchor.cs` (86) -- clean (minor: OnDrawGizmos cache -- nie ruszam, performance hot path w editor-only)
- [-] `Scripts/Inventory/PlagaGrabbable.cs` (74) -- clean (nazwa controller po name parsing to fallback Oculus SDK)

## Setup projektu (po kodzie)

- [x] `ProjectSettings/*.asset` -- EditorBuildSettings naprawione wczesniej (TESTBED_V2 -> TESTBED.unity)
- [!] `Packages/manifest.json` -- **rozjezdz wersji Meta XR SDK**: manifest ma 83.0.0, `MetaQuestSetup.cs` const `META_SDK_VERSION = "81.0.0"`. Borys musi zdecydowac: update const na 83.0.0 albo downgrade manifest.
- [!] `Packages/manifest.json` -- **potencjalne unused dependencies**: `com.unity.visualscripting`, `com.unity.collab-proxy`, `com.unity.modules.cloth`, `com.unity.modules.physics2d`, `com.unity.modules.tilemap`. Moga byc usuniete jesli niepotrzebne (szybszy build, mniejsza size).
- [!] **Brak Assembly Definitions** -- caly Runtime kompiluje sie w `Assembly-CSharp`, Editor w `Assembly-CSharp-Editor`. Rozbicie na .asmdef (np. Plaga44.Core, Plaga44.UI, Plaga44.Editor.Config) bedzie przyspieszac kompilacje 3-10x przy incrementalnych zmianach, ale **wymaga sortowania zaleznosci** -- wieksza akcja, odkladam do osobnego zadania.
- [x] Folder struktura `Assets/` -- czytelnie podzielone na GameDevHQ, PLAGA44, Potok, FloodedGrounds, Oculus, Scripts, Editor, Settings, Resources, XR. OK.

---

## Log zmian per plik (wypelniam podczas refactoru)

<!-- KLASA.cs: krotki opis zmian + wierzcholki -->

### SettingsRegistry.cs (640 -> ~615, surgical refactor)

- **`Build()` (375 linii z Sec() deklaracjami) NIE ruszone** -- zbyt ryzykowne bez testow. Kazdy Sec() to lambda z closure po lokalnych zmiennych (urp/sun/vol/ca/etc). Full rewrite wymagałby przekierowania 22 metod + SceneContext struct -- odkladam.
- **`PrefsKeys` private static class** -- single source of truth dla PlayerPrefs keys. Koniec z magic strings rozrzuconymi w `$"{PresetPrefix}{slot}___count"` (z 3 underscore magic!). Nowe API: `PrefsKeys.Current(section, name)`, `PrefsKeys.PresetValue(slot, name)`, `PrefsKeys.PresetCount(slot)`, `PrefsKeys.PresetSavedAt(slot)`.
- **`GetSlotName(int slot)`** -- switch expression zamiast ternary 2-poziom `slot == 1 ? "HI-END" : slot == 2 ? "CUSTOM" : "SAFE"` w loop. Ekstend Support dla slot > 3.
- **`LoadPreset`** rozbity: `LoadPresetValues` osobna funkcja (czytelniejsza obsluga try/catch).
- **Build() konczowka** (flat list + defaults + restore z PlayerPrefs) -> `FinalizeBuild` + `CollectFlatSettingsList` + `RestorePersistedValues`.
- `HashSet<string>` -- pelny namespace `System.Collections.Generic.HashSet<string>` zastapiony przez `using` (bo juz jest).
- Zero zmian public API (SavePreset/LoadPreset/IsPresetSaved/GetPresetTimestamp/ResetToDefaults/LogAll), zero zmian PlayerPrefs keys (kompatybilnosc danych), zero zmian logow.

### HamburgerMenu.cs (736 -> ~625)

- **Magic numbers -> const** (~25 stalych): canvas (W/H/scale/drop), tile layout (TOP/GROUP rozmiary/spacing/font), settings list (row height/font/gap), chrome (title/version/footer height/Y/font), input thresholds (stick/trigger repeat).
- **Section names** (`AvatarSection`, `PresetsSection`) -- koniec z magic stringami "AVATAR"/"PRESETS" w `UpdateSettingsDisplay`.
- **`BuildCanvas` (79 linii)** -> rozbity na: `CreateCanvasRoot`, `CreateBackground`, `CreateTitleLabel`, `CreateVersionLabel`, `CreateFooterLabel`, `CreateFooterValue`, `CreateNotifier` + helpery `CreateAnchoredTopRow`/`CreateAnchoredBottomRow`, `LegacyFont()`.
- **`UpdateSettingsDisplay` (67 linii)** -> rozbity na: `RenderRows`, `RenderFooterForSelection`, `FormatSettingRow`, `BuildFooterValue`. Wprowadzony `RowContext` readonly struct -- jedno obliczenie section/avatar per-call zamiast 3x lookup per row.
- **`HandleSettingsInput`** -> `UpdateSettingsSelection` + `UpdateSettingsValueByStick` + `UpdateSettingsValueByTriggers`. Trigger accel logic teraz oddzielna metoda.
- **`HandleTileInput`** -> `TryMoveIndex` helper + `GetStrongerThumbstick()`.
- **`GoForward/GoBack`** -> switch zamiast if/else-if. `GoForward` -> `EnterGroup`/`EnterSettings`.
- **`PressedEnter/PressedBack`** -> static helpers (czytelniejszy Update).
- **`PlaceInFrontOfPlayer`** -> `PlaceInFrontOfRig` / `PlaceFallback`.
- **`RenderPresetLine`** -> `TryParseSlotFromPresetName` helper.
- **`CreateTile`** -> `CreateTileLabel` osobno (rozdzielenie image + text).
- Zero zmian public API (`Instance`, `MenuOpen`, `Toggle/Open/Close`, `Update` behaviour), zero zmian logow.

### Config klasy (14 plikow, ~2400 linii)

- **Struct rename** w 5 plikach: `AudioSettings_` -> `AudioPreset`, `EditorSettings_` -> `EditorPreset`, `GraphicsSettings_` -> `GraphicsPreset`, `ProjectSettings_` -> `ProjectPreset`, `QualitySettings_` -> `QualityPreset`. Usuwa kolizja naming z Unity `UnityEngine.AudioSettings` / `UnityEditor.EditorSettings` / `UnityEngine.QualitySettings`.
- **Reszta konwencji** CYBERNOMAD Config API (presety + Apply + SetXxx + LogCurrent + [MenuItem]) **jest juz dobra**. Szczególnie czyste: MiscConfig, ManifestConfig, InputConfig, MemoryConfig, BuildScenesConfig, ISDKLocomotionSetup.
- **Noted for later** (MEDIUM refactor, wymaga wspolnego helpera SerializedObject): duplikacje `Load+Set+ApplyModifiedProperties+SaveAssets` wzorca w: Pipeline, PhysicsConfig, OculusConfig, URPGlobalConfig, VolumeConfig. Nie poprawione teraz, bo wymaga nowej klasy `SerializedAssetAccessor` + edycji 5 plikow na raz -- ryzyko regresji. Odklada sie do osobnego punktu.

### RevolverPrefabBuilder.cs (119 -> ~115)

- `BuildPrefab(bool force)` -- unused parametr `force` usuniety
- `BuildPrefab` (58 linii) rozbity: `EnsureResourcesItemsFolder`, `BuildRevolverInstance`, `AttachPhysics`, `AttachCollider`, `AttachFeedbackAndGrab`, `SaveAsResourcesPrefab`
- Magic numbers (mass 1.1kg, damping 0.5/0.8, fallback bounds) -> const
- Zero zmian public API (EnsurePrefab, RebuildMenu signatury takie same)

### PlayerAvatarImporter.cs (31 -- minor cleanup)

- `is ModelImporter mi` pattern matching (zamiast `as` + null check)
- `TargetNameToken` const zamiast magic string w porownaniu
- Dodany komentarz uwagi: ten importer jest **potencjalnie dead code** (AvatarImport juz pokrywa Avatars/) -- Borys do decyzji o usunieciu
- Private helper `ConfigureMixamoHumanoid`

### BuildScript.cs (85 -> ~80)

- Dodany namespace `Plaga44.Editor` (byla goli top-level)
- **BREAKING w zewnetrznym build-quest.sh** -- entry point zmienila sie. Fix w build-quest.sh juz zrobiony (`Plaga44.Editor.BuildScript.Build`)
- `BuildQuest` rozbity: `ResolveBuildScenes`, `EnsureBuildDir`, `SwitchToAndroid`, `BuildApk`, `LogFailedBuild`
- `using UnityEditor.Build.Reporting` -- bez `UnityEditor.Build.Reporting.BuildResult` prefix

### MetaQuestSetup.cs (317 -> ~265)

- **DEAD CODE** usuniete: `CleanTerrainDataAssets` -- nikt jej nie wolal
- Magic strings -> const (`XrSettingsFolder`, `OpenXRLoaderTypeName`, `ScopedRegistryUrl`, `DependenciesToken`, `MetaCoreMarker`)
- Paczki: `string[][]` -> `(string id, string version)[]` -- czytelniej
- `AutoCheck` rozbity: `OfferAndroidSwitchIfNeeded`, `OfferFullSetup`
- `AddScopedRegistry` rozbity: `FindDependenciesLineStart`, `BuildRegistryBlock`
- `AddPackagesToManifest` rozbity: `TryInsertPackage`
- `EnableOpenXRLoader` (50 linii) rozbity: `LoadOrCreatePerBuildTargetSettings`, `EnsureAndroidGeneralSettings`, `AssignOpenXRLoader`
- Zero zmian zachowania / logow / menu paths

### AvatarImport.cs (579 -> ~490)

- Nowe helpery: `UrpLit` (stale nazw property + keywords URP/Lit), `AvatarPaths` (sciezki plikow -- Model/Prefab/Material/Texture). Koniec z hardcoded `"_BaseMap"`, `"_NORMALMAP"` itd.
- Suffixy tekstur (`_packed0_diffuse` itd.) -> `AvatarImportConfig.Suffix*` const
- `AvatarTexturePreprocessor`: `OnPreprocessTexture` rozbity na `TryConfigureByFilename` + `SetSRGB/SetLinear/SetSpecGloss`
- `AvatarModelPreprocessor`: `OnPreprocessModel` rozbity na `ConfigureFbx/ConfigureDae` + `ApplyCommonImportFlags` (DRY dla wspolnych flag importCameras/Lights/Visibility/BlendShapes)
- `AvatarMaterialPostprocessor.AddFolder` -> `AddTouchedFolder` + `IsBuilderArtefact` (czytelna ochrona przed petla)
- `AvatarAutoImport.ScanAll` -> orchestration + `BuildAllAvatarFolders`
- `AvatarRegistryBuilder.Rebuild` rozbity na: `LoadOrCreateRegistry`, `BuildEntry`, `ValidateRig` + `MarkBroken`
- `AvatarBuilder.Build` (60 linii) -> orchestration + `TryResolveModel`, `BuildDaeMaterial`, `NeedsSpecGlossRebuild`
- `CombineSpecGloss` rozbity: `LoadPng`, `MatchSize`, `MergeRgbAndRedToAlpha`, `Resize`
- `CreateOrUpdateMaterial` -> `LoadOrCreateMaterial`, `ApplySpecularWorkflowDefaults`, `BindTextures`
- `CreateOrUpdatePrefab` -> `IsPrefabUpToDate`, `TryLoadMaterial`, `AssignMaterialToAllRenderers`, `SavePrefabInstance`
- Zero zmian public API, logow i zachowania

### Bootstrap.cs (563 -> 475)

- 18 magic strings/numbers -> grupowane `const` na gorze (paths, scene names, shaders, CC defaults, spawn Y, grab volume, sun)
- `ValidatePlayerRig` (125 linii) -> rozbity na: `EnsureCharacterController`, `EnsureLocomotion`, `EnsureSmoothTurn`, `EnsurePlayerAvatar` + `ResetAvatarToDefaultMode` / `ClearLegacyPrefabOverride` / `WireDefaultRig` / `ActivateDefaultRig` / `PlacePlayerAboveTerrain`
- `ValidateTerrain` -> `CreateTerrainFromAsset`, `HasValidMaterial`, `CreateAndAssignTerrainMaterial`, `EnsureFolder`
- `ValidateDirectionalLight` -> `FindDirectionalLight`, `CreateDirectionalLight`
- `EnsureGrabberOnHand` -> `CreateGrabVolume`, `EnsureKinematicRigidbody`
- `EnsureComponent<T>` rozszerzony o `Action<T> configure` -- usuwa duplikacje 4 blokow `if null then AddComponent + init`
- Nowy helper `EnsureSceneSingleton<T>` -- `ValidateHamburgerMenu` + `ValidateSkyRotator` teraz jednolinijkowe
- `ValidateScene` -> orchestration + `SaveSceneIfDirty` + `FocusCameraOnTerrain` extract
- Usingi: dodane `Plaga44.Locomotion/UI/Feedback/Inventory` -> typy bez prefix `Plaga44.`
- Usuniete XML docs dla privates (nazwa mowi za siebie)
- **ZERO zmian public API** (Bootstrap ctor, LoadFromMenu, RunBootstrap, menu paths)
- **ZERO zmian logow** -- wszystkie `[OK]/[ADDED]/[FIX]/[WARN]` brzmia identycznie
