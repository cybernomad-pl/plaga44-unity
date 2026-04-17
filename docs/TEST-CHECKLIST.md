# PLAGA '44 TESTBED V7 -- TEST CHECKLIST

End-to-end test scenarios for everything currently on `main`.
Based on TESTBED_V7 at commit `40c419b` (post PR #137 + #138).

**Scope:** Cold-start each scenario unless noted. Test on Quest via Link.
**Status legend:** `[ ]` not tested · `[x]` pass · `[!]` fail · `[~]` partial

---

## 🎬 DEMO -- JEDNA ŚCIEŻKA POKRYWAJĄCA WSZYSTKO

~15 minut od cold start. Każdy punkt = widoczna rzecz lub widoczny log.

### Akt I -- Start i spadek (2 min)

1. [ ] Unity otwarty, commit `main` HEAD (40c419b)
2. [ ] Hit Play
3. [ ] Console: `[Bootstrap] === Setup START ===` → 8× `[OK]` → `=== Setup DONE ===`
4. [ ] Scena otwiera się, widać terrain, skybox, sun, bounce light
5. [ ] Scena: `Spawned 1/1 avatars ... [deferred until grounded]` -- gallery pustka (player w powietrzu)
6. [ ] VR headset ON → widok Z 1km nad terenem
7. [ ] **Patrz w dół** -- widać terrain z góry (2048×2048m)
8. [ ] Gracz spada (vVel ramps -9.81×t²/2)
9. [ ] Pod 50m widać detale terrain (warstwy: grass/dirt)
10. [ ] Uderza w ziemię → `[Locomotion] Grounded: False -> True, vVel=-XX`
11. [ ] Po landing: `[Gallery] Spawned ... [deferred until grounded]` (PINEA kafelek pojawia się)
12. [ ] `[ObjectSpawner] Spawned 'Revolver' at (...)` -- Revolver na wirtualnym stole przed graczem
13. [ ] Widać robota SDK na sobie (defaultRig ACTIVE)

### Akt II -- Ruch i stance (2 min)

14. [ ] L thumbstick forward → gracz idzie do przodu
15. [ ] L thumbstick w bok → strafe
16. [ ] R thumbstick X → smooth turn
17. [ ] Obróć głowę, idź do przodu → rusza się zgodnie z głową (head-relative)
18. [ ] R stick DOWN tap → `Stance: Stand -> Crouch` + kamera obniża się
19. [ ] R stick DOWN tap → `Stance: Crouch -> Prone` + jeszcze niżej
20. [ ] Na pronie L stick → **gracz nie rusza się** (movement blocked)
21. [ ] R stick UP tap → `Prone -> Crouch`
22. [ ] R stick UP tap → `Crouch -> Stand`

### Akt III -- Latanie (3 min)

23. [ ] R stick UP hold → `Fly: ASCENDING` + `Stance: Stand -> Floating`
24. [ ] Gracz wznosi się, speed narasta do 8 m/s
25. [ ] Na 20m puść stick → `Fly: HOVERING`, drift ±0.3 m/s
26. [ ] W hoverze R stick DOWN tap → **nic się nie dzieje** (stance locked w Floating)
27. [ ] R stick UP krótko → `ASCENDING (from hover)` boost
28. [ ] Puść stick → HOVERING znowu
29. [ ] R stick DOWN długo → `Fly: DROPPING` + gravity włącza się
30. [ ] Lądowanie → `EndFlight` + `Stance: Floating -> Stand`

### Akt IV -- Hamburger menu + hands animation test (2 min)

31. [ ] Start button → `[Menu] OPEN` + `GameState: Playing -> Paused (timeScale=1)`
32. [ ] Canvas pojawia się 1.4m przed oczami, transparent BG
33. [ ] **PATRZ NA RĘCE SDK** -- palce dalej się animują (timeScale=1 fix)
34. [ ] Nawiguj L thumbstick: GAMEPLAY → LOCOMOTION → widać sliders
35. [ ] B/Y back → grupy kafelków
36. [ ] Wejdź w VISUAL → SUN → zmień Intensity sliderem → słońce zmienia jasność LIVE
37. [ ] B/Y back → SYSTEM → PROFILE → Target 1 → Quest/PCVR preset apply
38. [ ] Start button → `[Menu] CLOSE` + `Paused -> Playing`

### Akt V -- Grab Revolver + haptic (3 min)

39. [ ] Podejdź do prawej ręki (biodro) → Revolver w holsterze RightHip
40. [ ] Wyciągnij REAL ręką do Revolvera, squeeze grip → `[PlagaGrabber] Toggle GRAB via RTouch`
41. [ ] `[Grabbable] GrabBegin: Revolver by RTouch`
42. [ ] Haptic pulse (grab event)
43. [ ] `[FingerFreezer] FREEZE RTouch: 15 bones locked`
44. [ ] **PATRZ NA ROBOT RĘKĘ** -- palce zatrzymały się w aktualnej pozie
45. [ ] Puszczenie grip → Revolver ZOSTAJE w ręce (toggle!)
46. [ ] Trzymaj Revolver, squeeze grip (hold) → ciągły buzz
47. [ ] `[Haptic] gripHold START ctrl=RTouch amp=0.15`
48. [ ] Puść grip → buzz stops (object zostaje)
49. [ ] Naciśnij trigger → ostry puls
50. [ ] `[Haptic] triggerPull ctrl=RTouch`

### Akt VI -- Item grip calibration (2 min)

51. [ ] Trzymaj Revolver, otwórz menu
52. [ ] GAMEPLAY → ITEM GRIP → 9 slider entries
53. [ ] Ruch Pos Z +0.1 → **Revolver przesuwa się 10cm do przodu w ręce LIVE**
54. [ ] Ruch Rot Y 90° → Revolver obraca się LIVE
55. [ ] Scale 2.0 → Revolver 2x większy LIVE
56. [ ] Wróć Scale do 1.0, Rot Y do 0, Pos Z zostaw 0.1
57. [ ] Klik SAVE GRIP → banner "ITEM GRIP SAVED for 'Revolver'"
58. [ ] `Plaga44_ItemGrip_Revolver_posZ = 0.1` w PlayerPrefs
59. [ ] Zamknij menu, puść grab (squeeze grip 2 razy)
60. [ ] Chwyć Revolver ponownie → **saved offset auto-applied** (Revolver +10cm od ręki)

### Akt VII -- Avatar swap (2 min)

61. [ ] Menu → GAMEPLAY → AVATAR → slider Mode 0 → 1
62. [ ] `[Avatar] Preview mode=1 (PINEA_YNG5)`
63. [ ] Gallery instance PINEA aktywuje się w rzędzie (ziemia przed graczem)
64. [ ] B/Y back z AVATAR → `Confirmed avatar mode=1`
65. [ ] `defaultRig StylizedCharacterLocomotion -> INACTIVE` (robot znika)
66. [ ] `Spawned Avatar_PINEA_YNG5 on player`
67. [ ] Widzisz PINEA na własnym ciele (look down = PINEA body)
68. [ ] Menu → AVATAR → Mode 1 → 0 → `defaultRig -> ACTIVE` (robot wraca)

### Akt VIII -- Item browser spawn (1 min)

69. [ ] Menu → GAMEPLAY → ITEMS → slider 0 → 1
70. [ ] `[ItemBrowser] Item: Revolver -- spawned in front of player`
71. [ ] Revolver (ItemPreview) pojawia się 1.2m przed głową, 0.5m niżej
72. [ ] Floating (gravity=false)
73. [ ] Zamknij menu, chwyć ten preview → normalny grab flow
74. [ ] Slider back to 0 → preview despawnuje

### Akt IX -- Exit (30 sek)

75. [ ] Menu → SYSTEM → EXIT → QUIT GAME → slider 1
76. [ ] Editor: `Application.Quit` → play mode stops
77. [ ] `[Menu] CLOSE` + `FlushPlayerPrefs`
78. [ ] PlayerPrefs zapisane:
    - `Plaga44_Current_AVATAR_Mode`
    - `Plaga44_ItemGrip_Revolver_*` (wszystkie 7 pól)
    - `Plaga44_Current_SUN_Intensity`
    - ... wszystkie zmienione ustawienia

### SUCCESS METRICS

Demo **PASS** jeśli:
- [ ] Wszystkie 78 punktów bez failu
- [ ] 0 error/exception w logu (poza znanymi filtrowanymi przez RetargeterGuard)
- [ ] Frame rate stabilny (72 fps Quest 2, 90 fps Quest 3)
- [ ] Żadna akcja nie wymagała restart play mode

### CZAS TRWANIA

Nowy user: ~20 min (z czytaniem menu)
Doświadczony tester: ~8 min (bez pauz)

---

## E2E #1 -- COLD START (Bootstrap + spawn)

Opens fresh Unity project, hits Play, player lands and sees world.

- [ ] Open project in Unity
- [ ] Bootstrap auto-runs -- Console shows `[Bootstrap] === Setup START ===`
- [ ] All 8 setup steps complete `[OK]` or `[CHANGED]` (no `[FAIL]`)
- [ ] Console shows `AvatarRegistry rebuilt -- 1 avatars (broken=0)`
- [ ] Console shows `[PlayerRigSetup] [FIX] defaultRig -> StylizedCharacterLocomotion`
- [ ] Hit Play -- player rig positioned at terrain center + 1000m altitude
- [ ] Console `[Avatar] Start: ... defaultRig='StylizedCharacterLocomotion' ... maxMode=1`
- [ ] Console `[Avatar] defaultRig 'StylizedCharacterLocomotion' -> ACTIVE` (robot visible)
- [ ] Player falls from 1km, vVel ramps to ~-9.8×t
- [ ] On landing: `[Locomotion] Grounded: False -> True, vVel=-XX`
- [ ] Gallery spawns only AFTER landing: `[Gallery] Spawned 1/1 avatars ... [deferred until grounded]`
- [ ] ObjectSpawner fires after landing: `[ObjectSpawner] Spawned 'Revolver' at ...`
- [ ] No NullReferenceException in log
- [ ] No `ShowDefaultRig(true) -- defaultRig is NULL!` errors

---

## E2E #2 -- MOVEMENT BASICS

- [ ] L thumbstick forward → player walks forward (head-relative)
- [ ] L thumbstick back → walks backward
- [ ] L thumbstick side → strafe (× strafeFactor=0.8)
- [ ] Smooth turn L/R via R thumbstick X axis
- [ ] Head rotation → forward direction rotates with head (not body)
- [ ] Walking on uphill terrain → CC stays grounded (no bouncing)

---

## E2E #3 -- STANCE SYSTEM (ground only)

Start on ground, Stand stance.

- [ ] R thumbstick DOWN tap → `Stance: Stand -> Crouch, targetH=1.0`
- [ ] CC height lerps smoothly from 1.8 → 1.0 over ~0.3s
- [ ] TrackingSpace Y drops → camera visually lowers
- [ ] R thumbstick DOWN tap again → `Crouch -> Prone, targetH=0.5`
- [ ] Prone: L thumbstick movement BLOCKED (player can't move)
- [ ] R thumbstick UP tap → `Prone -> Crouch`
- [ ] R thumbstick UP tap → `Crouch -> Stand`
- [ ] Edge detection: holding stick DOWN = no cycle (one tap per push)

---

## E2E #4 -- FLY SYSTEM

Start on ground, Stand stance.

- [ ] R thumbstick UP hold → `Fly: ASCENDING` + `Stance: Stand -> Floating`
- [ ] CC height stays 1.8m (no visual pop from Floating)
- [ ] Gravity OFF, player rises with increasing _flySpeed
- [ ] Max speed capped at flyMaxSpeed (8 m/s default)
- [ ] Release R stick → `Fly: HOVERING`, random drift ±0.3 m/s
- [ ] While HOVERING: R stick DOWN tap → NO stance change (UpdateStance early return)
- [ ] R stick UP again → `Fly: ASCENDING (from hover)` (boost)
- [ ] R stick DOWN → `Fly: DROPPING` + gravity returns
- [ ] On landing → `EndFlight` → `Stance: Floating -> Stand`

---

## E2E #5 -- HAMBURGER MENU

- [ ] Start button on either controller → `[Menu] OPEN`
- [ ] Canvas appears 1.4m in front of CenterEyeAnchor
- [ ] Canvas tracks head (face-player)
- [ ] `[GameState] Playing -> Paused (timeScale=1)`
- [ ] SDK hands keep animating (timeScale=1 fix)
- [ ] L thumbstick navigation between tiles
- [ ] A/X = enter, B/Y = back
- [ ] Enter GAMEPLAY group → 8 sections visible (LOCOMOTION, ..., ITEM GRIP, GAME STATE, NAVMESH)
- [ ] Enter LOCOMOTION section → slider list
- [ ] Trigger +/- adjusts value (hold accelerates)
- [ ] Start button again → `[Menu] CLOSE`, `Paused -> Playing`

---

## E2E #6 -- GRAB SYSTEM (toggle mode)

Start with Revolver in RightHip holster.

- [ ] Approach RightHip, squeeze grip → `[PlagaGrabber] Toggle GRAB via RTouch`
- [ ] `[Grabbable] GrabBegin: Revolver by RTouch`
- [ ] Haptic grab pulse fired (`PlayGrab`)
- [ ] **Finger freeze**: `[FingerFreezer] FREEZE RTouch: 15 bones locked`
- [ ] SDK robot right hand fingers LOCKED at current pose
- [ ] Release grip → Revolver STAYS in hand (toggle, not hold-to-grab)
- [ ] Squeeze grip again → release
- [ ] `[PlagaGrabber] Toggle RELEASE: Revolver from RTouch`
- [ ] `[FingerFreezer] UNFREEZE RTouch: 15 bones released`
- [ ] Fingers animate again (SDK tracking resumes)
- [ ] Drop near holster → `[Holster] Revolver snaps back`

---

## E2E #7 -- HAPTIC FEEDBACK

While holding Revolver:

- [ ] Physically hold grip button down → continuous gentle buzz
- [ ] `[Haptic] gripHold START | ctrl=RTouch amp=0.15`
- [ ] Release grip (object stays due to toggle) → buzz stops
- [ ] `[Haptic] gripHold STOP`
- [ ] Press index trigger → sharp pulse
- [ ] `[Haptic] triggerPull ctrl=RTouch`
- [ ] Grab event haptic: short pulse on grab (different from grip hold)
- [ ] Release event haptic: short pulse on release

---

## E2E #8 -- ITEM GRIP CALIBRATION

Grab Revolver, open menu → GAMEPLAY → ITEM GRIP.

- [ ] 9 entries visible: Pos X/Y/Z, Rot X/Y/Z, Scale, SAVE GRIP, RESET GRIP
- [ ] Move Pos Z slider +0.05 → Revolver moves 5cm forward LIVE in hand
- [ ] Move Rot Y slider 90 → Revolver rotates 90° around Y axis LIVE
- [ ] Scale 1.0 → 2.0 → Revolver doubles size LIVE
- [ ] Click SAVE GRIP → banner "ITEM GRIP SAVED for 'Revolver'"
- [ ] PlayerPrefs written: `Plaga44_ItemGrip_Revolver_posZ = 0.05`
- [ ] Release grab, close menu, grab again → saved offset auto-applied
- [ ] RESET GRIP → config cleared, back to defaults
- [ ] Banner "ITEM GRIP RESET for 'Revolver'"

---

## E2E #9 -- AVATAR SWAP

Start in None mode (robot visible).

- [ ] Open menu → GAMEPLAY → AVATAR
- [ ] Slider "Mode" 0 → 1 → `[Avatar] Preview mode=1 (PINEA_YNG5)`
- [ ] PINEA gallery instance activates: `[Gallery] SetActiveIndex(0)`
- [ ] PINEA preview visible (T-pose, NO shader pink errors in log)
- [ ] Navigate back (B/Y) → `[Avatar] Confirmed avatar mode=1`
- [ ] Robot body DEACTIVATES: `defaultRig 'StylizedCharacterLocomotion' -> INACTIVE`
- [ ] PINEA spawns on player rig: `[Avatar] Spawned 'Avatar_PINEA_YNG5' mode=1 (humanoid=True)`
- [ ] Menu → AVATAR, slider back to 0 → robot visible again

---

## E2E #10 -- ITEM BROWSER

Open menu → GAMEPLAY → ITEMS (slider).

- [ ] `[ItemBrowser] Loaded 1 items: Revolver`
- [ ] Slider 0 → 1 → Revolver spawns "on table" (head-relative, -0.5m below eyes, 1.2m forward)
- [ ] `[ItemBrowser] Item: Revolver -- spawned in front of player`
- [ ] Physics: Revolver floats (gravity=false, kinematic=false)
- [ ] Grab Revolver with hand → normal grab flow (E2E #6)
- [ ] Back to slider 0 → `DespawnPreview`, Revolver removed

---

## E2E #11 -- OBJECT SPAWNER (startup loadout)

Verify ObjectSpawner spawns Revolver on virtual table at ground level (post-landing).

- [ ] After landing: `[ObjectSpawner] Spawned 'Revolver' at (X, Y, Z)`
- [ ] Position is head-relative: in front of player, ~0.5m below eye level
- [ ] Revolver is grabbable (has PlagaGrabbable + HapticOnGrab)
- [ ] Has Rigidbody + Collider (auto-wired by WireComponents)

---

## E2E #12 -- SETTINGS PERSISTENCE

- [ ] Open menu → LOCOMOTION → change Move Speed to 5.0
- [ ] Close menu → `FlushPlayerPrefs`
- [ ] PlayerPrefs: `Plaga44_Current_LOCOMOTION_Move Speed = 5.0`
- [ ] PlayerPrefs: `Plaga44_Default_LOCOMOTION_Move Speed = 5.0` (auto-default)
- [ ] Stop play mode, start again → Move Speed restored to 5.0
- [ ] Menu → MISC → RESET ALL → all settings to defaults
- [ ] Menu → MISC → LOG ALL → console dump of all settings

---

## E2E #13 -- PAUSE STATE

- [ ] Menu OPEN → `[GameState] Playing -> Paused (timeScale=1)`
- [ ] `CanMove=false` blocks LocomotionController.Update
- [ ] `CanMove=false` blocks SmoothTurnController.Update
- [ ] `timeScale=1` → SDK hands KEEP ANIMATING (check finger twitching)
- [ ] `timeScale=1` → SkyRotator keeps rotating (harmless)
- [ ] Menu CLOSE → `Paused -> Playing` → movement restored

---

## E2E #14 -- SDK ERROR FILTER (RetargeterGuard)

Throughout session, check that known SDK spam is suppressed:

- [ ] No "NativeArray[Meta.XR.Movement" errors (filtered)
- [ ] No "Failed to retarget source frame data" (filtered)
- [ ] No "LocomotionEventsConnection" (filtered)
- [ ] No "AssertCollectionItems" (filtered)
- [ ] Actual errors DO appear (RetargeterGuard doesn't block legit errors)

---

## E2E #15 -- TERRAIN

- [ ] Terrain size: 2048 × 2048 (horizontalScale=2.0)
- [ ] Terrain material: URP/Terrain/Lit (no pink shader)
- [ ] Terrain has 3 layers (grass/dirt/... from FloodedGrounds)
- [ ] Terrain height profile: varied heights (not flat)
- [ ] Walking/flying over terrain: no shader flickering

---

## E2E #16 -- LIGHTING

- [ ] Directional Light (sun) visible, shadows soft
- [ ] Bounce Light (fill): rotation (-90, 0, 0), no shadows
- [ ] Sun+bounce combo → no fully black shadowed areas
- [ ] Menu SUN sliders → live update
- [ ] Menu SHADOWS sliders → live update

---

## E2E #17 -- SKY ROTATION

- [ ] SkyRotator rotates skybox continuously (~0.3 deg/s default)
- [ ] Menu SKYBOX → Rot Speed slider → rate changes
- [ ] Skybox material (BGR_Sky1) renders correctly on Quest

---

## EDGE CASES

### EC #1 -- Fly + menu + resume
- [ ] Start flying (ASCENDING)
- [ ] Open menu mid-flight
- [ ] _verticalVelocity persists (player hangs in air)
- [ ] Close menu → flying resumes from same state
- [ ] Check no unexpected altitude spike

### EC #2 -- Grab while prone
- [ ] Go prone (R stick DOWN×2)
- [ ] Try to grab Revolver in holster
- [ ] Should still work (grab doesn't require standing)

### EC #3 -- Toggle grab twice fast
- [ ] Grab grip, immediate second grip press
- [ ] Check m_grabbedObj cleared before second GrabBegin
- [ ] No null refs, no stuck state

### EC #4 -- Avatar broken entry
- [ ] Drop a .dae into Avatars/ without valid rig
- [ ] Rescan avatars
- [ ] Registry marks entry broken=true, errorMessage set
- [ ] Gallery skips broken: `Skipping broken avatar [i]`
- [ ] Menu AVATAR slider shows "AVATAR_ERROR" on that mode

### EC #5 -- Change avatar while holding item
- [ ] Grab Revolver
- [ ] Open menu, swap to PINEA
- [ ] Robot disappears, PINEA appears
- [ ] Revolver still held? Check finger freezer state

### EC #6 -- StratoJump landing impact
- [ ] Spawn from 1km
- [ ] Land: vVel ≈ -140 m/s at impact?
- [ ] CC grounded check fires correctly
- [ ] No CC penetration through terrain

### EC #7 -- Fly off map edge
- [ ] Fly past terrain boundary (X > 2048 or < 0)
- [ ] terrain.SampleHeight outside range → returns 0
- [ ] Gravity pulls to Y=0 (no bottom collider yet)

### EC #8 -- Rapid stance cycling
- [ ] Rapid tap R DOWN/UP/DOWN/UP
- [ ] Edge detection prevents double-trigger
- [ ] LerpStance smooths all transitions
- [ ] No CC stuck in wrong height

---

## REGRESSION -- PR #138 (restore + fixes)

- [ ] SDK Samples present: Assets/Samples/Meta XR Movement SDK/
- [ ] StylizedCharacterLocomotion prefab referenced correctly in scene
- [ ] No "Missing Prefab" errors on scene open
- [ ] PINEA_YNG5.dae avatarSetup=1 (CreateFromThisModel)
- [ ] AvatarRegistry: 1 avatars, broken=0
- [ ] Bootstrap Setup completes in <100ms typically

## REGRESSION -- PR #137 (item grip + freezer)

- [ ] HandFingerFreezer component on StylizedCharacterLocomotion
- [ ] ItemGripConfig PlayerPrefs keys work (load/save/clear)
- [ ] PlagaGrabbable.BaseName strips "(Clone)" and "ItemPreview_"
- [ ] SettingsRegistry "ITEM GRIP" section present, 9 entries
- [ ] HamburgerMenu GAMEPLAY shows ITEM GRIP tile

---

## PERFORMANCE

- [ ] Target 72 fps on Quest 2 (72Hz mode)
- [ ] Target 90 fps on Quest 3 (90Hz mode)
- [ ] No stutters on fly/stance transitions
- [ ] No stutters on menu open/close
- [ ] GC spikes: check Profiler for allocations
- [ ] Shader compilation: no freeze on first grab/spawn

---

## DEPLOYMENT

- [ ] Build APK via Build/Script (CYBERNOMAD > Build > Quest APK)
- [ ] APK size < 200MB
- [ ] ADB install successful
- [ ] APK runs standalone on Quest (no PC)
- [ ] Logcat shows same [PLAGA44] logs as editor
- [ ] Hand tracking works standalone (no controllers)

---

## KNOWN ISSUES (not yet fixed)

- [!] #139 FLY: thumbstick threshold calibration (R stick UP nie łapie zawsze)
- [!] #140 FLY: hover drift za słaby (float nieodczuwalny)
- [!] #141 FLY: brak momentum w powietrzu
- [!] #142 SPRINT: nie działa wcale
- [!] #143 SPRINT: momentum missing (depends #142)
- [!] #144 SKYBOX: Cloud Alpha + Cloud RGB do usunięcia z menu
- [!] #145 MENU: Gallery/ItemBrowser preview spawn daleko, ma być W menu

---

**Last updated:** 2026-04-17 (post PR #137 + #138 merge)
**Next sprint:** tackle #139-#145 (kanban mode)
