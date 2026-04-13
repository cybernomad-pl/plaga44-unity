# MIGRATION PLAN: bleeding-edge -> testbed-v6

Data: 2026-04-13
Baseline: tag `baseline-v6-working` (66d6923)

## TESTBED MA (5 runtime scripts)
- GameState.cs
- LocomotionController.cs
- SmoothTurnController.cs (nowy, nieskomitowany)
- HamburgerMenu.cs
- SettingsRegistry.cs (~120 runtime settings, save/load presetow)

## TIER 1 -- samodzielne, zero zaleznosci, migruj TERAZ
- [ ] Core/StartupLogger.cs -- loguje stan systemu przy starcie (GPU, RAM, SDK)
- [ ] Performance/PerformanceMonitor.cs -- FPS/memory HUD overlay
- [x] Core/SkyRotator.cs -- obraca skybox w runtime (w SKYBOX sekcji SettingsRegistry)
- [x] UI/VersionHUD.cs -- wersja buildu w tytule HamburgerMenu (nie osobny HUD)
- [ ] Core/SprintModifier.cs -- sprint (L3) + skok (B), zalezy od LocomotionController
- [ ] Locomotion/ComfortVignette.cs -- winieta przy ruchu, zalezy od NormalisedSpeed
- [ ] Core/PerformanceBenchmark.cs -- mierzy FPS, frame time, GC
- [ ] Core/SceneDefaults.cs -- domyslne parametry sceny (presety SAFE/HI-END)

## TIER 2 -- wymaga obiektow na scenie
- [ ] Core/VRItemSpawner.cs -- spawn prefabow FloodedGrounds
- [ ] UI/VFXSpawnerMenu.cs -- spawn VFX
- [ ] Interactions/MakeGrabbable.cs -- ISDK grabbable
- [ ] Interaction/ThrowHandler.cs -- fizyka rzucania
- [ ] Gameplay/ThrowableStone.cs -- kamien do rzucania
- [ ] Core/TerrainDeformer.cs -- deformacja terenu noise
- [ ] Audio/SpatialAudioManager.cs -- 3D audio
- [ ] Audio/AmbientZone.cs -- strefy ambient
- [ ] Feedback/HapticManager.cs -- centralne API haptyki
- [ ] Core/PlayerAvatar.cs -- avatar gracza (rigged mesh)

## TIER 3 -- duze systemy, duzo zaleznosci
- [ ] AI/ (7 skryptow) -- EnemyAI, patrol, spawner, health
- [ ] BodyTracking/ (3) -- body tracking, calibration
- [ ] EyeTracking/ (4) -- eye tracking, gaze
- [ ] FaceTracking/ (3) -- face expressions, emotion
- [ ] IK/ (4) -- crouch, lean, seat
- [ ] Input/ (6) -- microgestures, quick wheel
- [ ] MixedReality/ (3) -- passthrough, portals
- [ ] NPC/ (4) -- NPC locomotion, state machine
- [ ] Networking/ (5) -- multiplayer (stub)
- [ ] Platform/ (5) -- achievements, leaderboards
- [ ] Weapons/ (3) -- M249 disassembly
- [ ] Rules/ (4) -- combat rules
- [ ] Gameplay/ (6) -- hit detection, ragdoll, death

## BRANCHE DO USUNIECIA (zmergowane do bleeding-edge)
Wszystkie klaudia1-20 i wrk1-14 zostaly zmergowane. Do skasowania:
- klaudia1/hand-grab, klaudia1/hand-grab-v2
- klaudia2/throw-mechanics, klaudia3/throwable-stone
- klaudia4/hit-detection, klaudia5/ragdoll-death
- klaudia6/spatial-audio, klaudia7/haptic-feedback
- klaudia8/mixed-reality, klaudia9/body-tracking
- klaudia10/face-tracking, klaudia11/eye-tracking
- klaudia12/ai-motion, klaudia13/hip-pinning-ik
- klaudia14/locomotion, klaudia15/multiplayer
- klaudia16/spacewarp, klaudia17/platform-sdk
- klaudia18/microgestures, klaudia19/combat-rules
- klaudia20/sdk-assets-review
- wrk1/fix-hitzone-duplicate, wrk1/fix-hitzonetype-duplication
- wrk1/fix-charactercontroller-collision-risk
- wrk1/flooded-grounds-setup, wrk1/floodedgrounds-showcase
- wrk1/introspection-menu-110, wrk1/npc-poseable-system
- wrk1/stone-throw-demo, wrk1/vfx-spawner-menu
- wrk3/perf-config, wrk4/haptic-feedback
- wrk5/enemy-ai, wrk6/vr-ui
- wrk14/body-rig-avatar, wrk14/klaszczur-ai-framework
- wrk14/vr-menu-locomotion
- claude/session-teleportation-SjgtE
- zjebane
- old_approach, reference-branch, setup-meta-sdk
- plaga-baseline, plague-baseline, plaga44/demo-baseline
- testbed-v3
