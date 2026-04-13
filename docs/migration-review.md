# Migration Review: origin/main -> TESTBED_V6

Data przegladu: 2026-03-14
Przeglad: Klaudia
Kontekst: 24 PR-y zmergowane do main + zmiany lokalne V6

---

## PRZEJRZANE

### 1. SkyboxSetup.cs (Editor, 22 linii)
- **Zrodlo:** origin/main, commit 37bed9a, 20c4e0b
- **Co robi:** Menu item ustawiajacy `_CloudOpacity=0` na materiale BGR_Sky1
- **Problem:** Property `_CloudOpacity` NIE ISTNIEJE w shaderze FG_Skybox_Rotating. Shader uzywa `_CloudBoost` i `_CloudThreshold`. Skrypt jest no-op.
- **V6 ma juz:** Bootstrap.cs waliduje skybox na starcie. SettingsRegistry.cs ma runtime slidery do CloudBoost, CloudThreshold, Tint, Exposure, Rotation.
- **Referencje w V6:** Bootstrap.cs:21,96,214-232; SettingsRegistry.cs:59,237-253
- **Werdykt:** SKIP -- bezuzyteczny, V6 ma lepsze rozwiazanie

### 2. PlayerModelImporter.cs (Editor, 23 linii)
- **Zrodlo:** origin/main, commity: 8fa9403 (feat), d640747 (fix bone mapping), 033a4bf (feat Mixamo), 4635adc (remove+strip), d8795f6 (V6 baseline)
- **Co robi:** AssetPostprocessor -- auto Humanoid rig na FBX zawierajacym "PLAYER_rigged"
- **Historia:** Dodany, potem stripniety (4635adc "fresh start with Mixamo"), potem wrocil w baseline V6 na main
- **V6 stan:** BRAK pliku na dysku. BRAK modelu PLAYER_rigged FBX. Zero referencji w kodzie.
- **Zaleznosci:** Zadne -- standalone postprocessor
- **Ryzyko:** SAFE -- czeka na FBX, nic nie zepsuje
- **Werdykt:** OCZEKUJE DECYZJI -- potrzebny dopiero jak bedzie model gracza

---

### 3. AutoPlayOnStart.cs (Core, 23 linii)
- **Zrodlo:** origin/main, commity: 0b8ea45 (feat SceneSetup Locomotion), 8a6e2b0 (verbose logging)
- **Co robi:** MonoBehaviour -- w Start() wola GameState.Play() zeby lokomocja dzialala
- **Powod istnienia:** main mial GameState.Current = Splash (domyslnie). Bez tego skryptu CanMove=false i gracz nie mogl sie ruszac.
- **V6 stan:** GameState.cs linia 76: `Current = GamePhase.Playing` -- default zmieniony. Skrypt niepotrzebny.
- **Referencje:** Dodawany przez SceneSetup.BuildLocomotionTestbed() ktory tez nie istnieje w V6
- **Werdykt:** SKIP -- V6 rozwiazal to inaczej (zmiana defaulta)

## DO PRZEGLĄDU (kolejka)

4. PCPreset.cs (Editor, 35 linii)
4. PCPreset.cs (Editor, 35 linii)
5. TerrainCleaner.cs (Editor, 50 linii)
6. InventoryMenuSetup.cs (UI, 64 linii) -- DANGER: stare HamburgerMenu API
7. EditorCameraHeight.cs (Locomotion, 95 linii)
8. EditorMouseLook.cs (Locomotion, 105 linii)
9. PlayerAvatar.cs (Core, 107 linii)
10. SDKVersionChecker.cs (Editor, 139 linii)
11. LocomotionManager.cs (Locomotion, 196 linii)
12. BuildScript.cs (Editor, 222 linii)
13. SprintModifier.cs (Locomotion, 276 linii)
14. MetaQuestSetup.cs (Editor, 318 linii) -- DANGER: wola SceneSetup
15. DebugHUD.cs (UI, 337 linii)
16. Quest2Preset.cs (Editor, 358 linii)
17. ConfigAPITests.cs (Editor/Tests, 487 linii) -- DANGER: stare klasy
18. SceneSetup.cs (Editor, 588 linii) -- DANGER: zalezy od wszystkiego
19. AvatarRetargeter.cs (Core, 717 linii)
20. InventoryScreen.cs (UI, 809 linii) -- DANGER: stare HamburgerMenu API

### DIFFERS (pliki w obu ale rozne)
21. GameState.cs -- default Splash->Playing
22. LocomotionController.cs -- usunieto WASD fallback
23. HamburgerMenu.cs -- kompletny rewrite
24. manifest.json -- SDK v81->v83, usunieto audio SDK
25. ProjectSettings.asset -- nowy define OVR_DISABLE_HAND_PINCH
26. XRGeneralSettingsPerBuildTarget -- brak Android XR config
27. OpenXR Package Settings -- rozne feature GUIDs
28-46. 19x Config API pliki -- TYLKO zmiana menu path (SAFE)
