# PLAGA '44 -- TEST CHECKLIST (bleeding-edge)

Data: 2026-04-03
Branch: bleeding-edge (synced with main)
Build: _______________
Tester: _______________
Device: Quest 2 / Quest 3 / Quest 3S (zakreslic)

---

## 1. BUILD & DEPLOY

- [ ] Build APK (menu: CYBERNOMAD > Build Quest APK)
- [ ] `adb install` na Quest -- sukces
- [ ] Gra startuje bez crash
- [ ] Splash screen -> scena PLAGA44_Demo laduje sie

## 2. LOCOMOTION

- [ ] L-stick: smooth move (przod/tyl)
- [ ] L-stick: strafe (lewo/prawo)
- [ ] R-stick: snap turn 45 stopni
- [ ] L3 (lewy stick press): sprint 3x
- [ ] B button: jump
- [ ] Comfort vignette przy ruchu (jesli wlaczony)

## 3. INTERAKCJE VR

- [ ] Grab obiektu kontrolerem (kamien)
- [ ] Grab obiektu kontrolerem (bron)
- [ ] Throw -- obiekt leci z poprawna predkoscia
- [ ] Gaze-corrected throw -- rzut leci blizej spojrzenia
- [ ] Crouch (X button) -- postac opuszcza sie o 0.35m

## 4. HIT DETECTION & DAMAGE

- [ ] Rzut w glowe -> ragdoll smierc
- [ ] Rzut w konczynę -> detachment (odpadniecie)
- [ ] Rzut w tors -> explode (wszystkie czesci)
- [ ] Haptic na hit: silniejszy na glowe, slabszy na konczyne
- [ ] Miss haptic: soft thud na powierzchni bez HitZone
- [ ] Impact sound odgrywa sie przy trafieniu

## 5. AI (jesli enemy na scenie)

- [ ] Idle/Patrol -- kolor zielony, krazy po waypoints
- [ ] Alert -- kolor zolty, gracz w hearing range (5m)
- [ ] Chase -- kolor pomaranczowy, biegnie za graczem
- [ ] Attack -- kolor czerwony, melee w range 2m
- [ ] Death -- kolor szary, ragdoll

## 6. UI

- [ ] Y button (Start): Quality Menu otwiera sie
- [ ] Quality Menu: L-stick gora/dol wybiera slider
- [ ] Quality Menu: L-stick lewo/prawo zmienia wartosc
- [ ] X button: Item Spawner otwiera sie
- [ ] Item Spawner: L-trigger spawnuje obiekt
- [ ] Start button: Pause Menu
- [ ] Pause Menu: Resume wraca do gry
- [ ] Look down (1s, pitch < -45): Introspection Menu
- [ ] Introspection Menu: 4 sekcje (stats/ekwipunek/cialo/psychika)
- [ ] Introspection Menu: zamyka sie gdy patrzysz do przodu
- [ ] VFX Spawner Menu: otwiera sie
- [ ] VFX Spawner: spawniuje Projectiles
- [ ] VFX Spawner: spawniuje AoE
- [ ] VFX Spawner: spawniuje Sparks

## 7. AUDIO

- [ ] Spatial audio -- dzwiek z kierunku zrodla
- [ ] Impact sound -- rozny dzwiek na roznych powierzchniach
- [ ] Ambient zones -- crossfade przy wejsciu/wyjsciu ze strefy

## 8. HAPTIC FEEDBACK

- [ ] Grab: pulse 0.3 amp przy lapaniu
- [ ] Release: kick 0.6 amp przy puszczeniu
- [ ] Hit target: skalowane wg body zone
- [ ] Miss: soft thud 0.15 amp

## 9. PERFORMANCE

- [ ] FPS counter widoczny
- [ ] Stabilne 72+ FPS w idle
- [ ] Stabilne 72+ FPS podczas gameplay
- [ ] Brak visual glitchy/artefaktow
- [ ] Preset Safe -- laduje sie poprawnie
- [ ] Preset Balanced -- laduje sie poprawnie
- [ ] Preset HiEnd -- laduje sie poprawnie

## 10. BODY/EYE/FACE TRACKING (Quest 3 only)

- [ ] Body tracking -- skeleton widoczny w debug
- [ ] Eye tracking -- gaze debug pokazuje kierunek
- [ ] Face tracking -- blendshapes reaguja na mimike
- [ ] N/A -- testowane na Quest 2 (brak tracking)

## 11. NOWE FICZERY (sesja 2026-04-03)

- [ ] FloodedGrounds Showcase (CYBERNOMAD > Scene > Build)
- [ ] VFX pack zaimportowany -- prefaby widoczne w Project
- [ ] VFX Spawner Menu -- 3 kategorie (Projectiles/AoE/Sparks)
- [ ] IntrospectionMenu v2 -- HapticManager + OVRCameraRig
- [ ] Audio files w Assets/Audio/ (126 WAV)
- [ ] Namespace fixy -- ZERO warnings w konsoli Unity
- [ ] BodyRegion enum (ex-HitZoneType) -- brak compile errors

---

## UWAGI TESTERA

```
_________________________________________________________________

_________________________________________________________________

_________________________________________________________________

_________________________________________________________________

_________________________________________________________________
```

## WYNIK

- [ ] PASS -- gotowe do dalszego developmentu
- [ ] FAIL -- lista blokerow ponizej

### BLOKERY

```
_________________________________________________________________

_________________________________________________________________

_________________________________________________________________
```
