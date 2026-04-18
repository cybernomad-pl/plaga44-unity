# V8 Play Session Observations -- 2026-04-18

Źródło: `/mnt/c/Users/boris/AppData/Local/Unity/Editor/Editor.log`

## Bootstrap -- ALL GREEN

```
[Bootstrap] === Setup START ===
[TerrainSetup]         OK 4ms  (2048x2048 scale=2, 0 tree prototypes)
[SkyboxSetup]          OK 1ms
[BounceLightSetup]     OK 2ms
[PlayerRigSetup]       OK 4ms  (defaultRig wired, StratoJump +1000m, HandFingerFreezer)
[InventorySetup]       OK 2ms  (HapticManager, PlayerInventory, PlagaGrabber L+R)
[SceneSingletonsSetup] OK 2ms  (HamburgerMenu, SkyRotator, AvatarGallery, ItemBrowser)
[ObjectSpawnerSetup]   OK 0ms
[AvatarRegistrySetup]  OK 1 avatars (PINEA_YNG5)
[LightingCleanup]      OK (no LightingDataAsset)
```

Zero exceptions w Bootstrap. Zero "Tree prefab missing" (issue #178 hotfix #182 działa).

## Runtime -- co Borys TESTOWAŁ

Sesja była krótka. Borys 3x otwierał menu, potem SYSTEM > EXIT > QUIT GAME.

Logi pokazują:
- `Gallery ForceSpawnNow` wywołane przy każdym menu OPEN (issue #157 fix działa)
- Pozycje Y=982, 948, 917 -- gracz W POWIETRZU (po StratoJump 1000m, jeszcze spada)
- `HideAllPreviews` wywoływane przy menu CLOSE (issue #158 fix działa)
- PINEA components: `renderers=4 skinned=4 animator=yes avatar=PINEA_YNG5Avatar valid=True`

PINEA NormalizeToHeight: `h=214.46 -> scale=0.0084`
  -> DAE ma skalę 214m zamiast 1.8m (prefab/FBX scale issue)
  -> Normalize to działa, ale scale=0.0084 jest ekstremalny -- detale mogą ginąć
  -> Nie krytyczne, ale warto zweryfikować w źródłowym DAE

## CZEGO NIE BYŁO W TESTACH (do zrobienia)

- [ ] Fly system (threshold #139, drift #140, momentum #141, hover nav #162)
- [ ] Sprint (#142 L thumbstick click, #143 ramp)
- [ ] Stance cycle (Stand → Crouch → Prone)
- [ ] Menu AVATAR slider swap mode 0↔1 (PINEA spawn na graczu)
- [ ] Menu ITEMS slider -- spawn Revolver na stole
- [ ] Grab toggle (#137 PlagaGrabber)
- [ ] Continuous haptic (grip hold buzz)
- [ ] Trigger haptic
- [ ] Finger freeze na grab
- [ ] Item grip calibration sliders

## ISSUES porównanie -- co jest POTWIERDZONE działające

| Issue | Status z logów |
|-------|----------------|
| #178 tree prefabs removed      | ✓ `Tree prototypes: 0, instances: 0 (clean)` |
| #182 persist cleared trees     | ✓ no warnings `Tree prefab missing` |
| #177 LightingCleanup           | ✓ `[OK] No LightingDataAsset (clean)` |
| #157 avatar gallery mid-flight | ✓ ForceSpawnNow triggers on menu open |
| #158 preview cleanup on close  | ✓ HideAllPreviews: 1 destroyed |
| #147 SpecGloss combine (PINEA) | ✓ no `SpecGloss combine failed` |
| #155 PINEA pink                | ? Borys nie testował w menu, ale valid=True, avatar OK |

## ISSUES POZOSTAŁE do testów

- #139 fly threshold 0.15
- #140 hover drift ±1.5
- #141 air momentum
- #142 sprint click
- #143 sprint ramp
- #152 fog density auto-switch
- #162 hover R stick normal accel
- #164 fly max speed 25
- #163 holster deprecated (brak Revolvera na biodrze = potwierdzone?)

## WARNINGS / KNOWN ISSUES niezałatwione

1. **Meta XR OpenXR exception** -- `Could not find Oculus Touch Interaction Profile`
   Profile JEST enabled w settings (m_enabled=1 dla Android+Standalone),
   ale Meta XR Setup Tool nie znajduje go w swojej query.
   Impact: spam w logu, NIE blokuje.

2. **PINEA scale ekstremalny (0.0084)** -- h=214m źródłowe -> 1.8m target.
   NormalizeToHeight działa ale optymalniej byłoby fix skali w imporcie DAE
   lub źródłowym Blender exporcie.

3. **Reverted #184 HSV sliders** -- powodowało czarne miganie
   (NaN przy V=0, round-trip precision loss). Wnioski w comment #183.

## NASTĘPNE TESTY (propozycja)

Po relaunchu:
1. Fly test suite (#139-#143, #162, #164) -- 5 min
2. Grab + haptic (#137 PlagaGrabber + HandFingerFreezer) -- 3 min
3. Avatar swap mode 1 (PINEA na graczu) -- kluczowe dla #156 i #155

## Log metrics

- Total PLAGA44 log lines w sesji: ~207
- Zero unfiltered errors/exceptions
- 3x menu open/close (minimalna interakcja)
- Bootstrap full Setup ~20ms
