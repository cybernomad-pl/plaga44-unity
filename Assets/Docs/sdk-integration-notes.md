# SDK Integration Notes -- PLAGA '44

Updated: 2026-02-23
Issues: #13 (SDK v85 eval), #17 (default assets review), #46 (ISDK integration)

---

## 1. Installed Package Versions

Currently pinned:

| Package | Version |
|---|---|
| com.meta.xr.sdk.core | 81.0.0 |
| com.meta.xr.sdk.interaction | 81.0.0 |
| com.meta.xr.sdk.interaction.ovr | 81.0.0 |
| com.meta.xr.sdk.audio | 81.0.0 |

Not yet installed (needed for advanced features):

| Package | Purpose |
|---|---|
| com.meta.xr.sdk.movement | Body tracking, CharacterRetargeter, ISDK integration |
| com.meta.xr.sdk.avatars | Ready-Player-Me style avatar support |
| com.meta.xr.sdk.platform | Leaderboards, Achievements, Cloud Saves |

---

## 2. SDK v85 Evaluation (Issue #13)

### Status: NOT YET EVALUATED

To evaluate v85, change all `com.meta.xr.*` entries in `Packages/manifest.json` to `85.0.0` and run:

    CYBERNOMAD > Meta SDK Setup > Check SDK Versions

### Known risks in upgrading from v81 to v85

- **Audio SDK**: Spatial audio integration with URP 17.x not confirmed for v85. Test `OculusSpatializerUnity` component before promoting.
- **Interaction SDK**: `HandGrabInteractor` API was stable through v83. Check release notes for v84+.
- **OVRSkeleton**: Check if `Bones` list initialization timing changed (v82 had a first-frame empty list bug).
- **v83 critical bug**: Oculus License validation error on some machines during IL2CPP build. If upgrading through v83, skip directly to v84+.

### Recommendation

Stay on v81 for the current development sprint (#28 grab interactions, #29 throw mechanics).
Evaluate v85 in a separate branch once Movement SDK integration (#46) is actively developed.

---

## 3. Default Assets Available in Interaction SDK (Issue #17)

Run `CYBERNOMAD > Assets > Browse SDK Assets` to get a fresh scan with full paths.

### Environment Prefabs (for test scenes)

| Prefab | Use |
|---|---|
| `LocomotionEnvironment.prefab` | Full locomotion test -- floor, walls, obstacles |
| `RoomEnvironment.prefab` | Generic room -- good for grab testing |
| `SmallRoomEnvironment.prefab` | Compact test space -- fast load |
| `LargeRoom.prefab` | Open space -- locomotion / throwing |
| `Desk.prefab` | MR desk scene -- passthrough desk anchor |

Location: `Runtime/Sample/` inside `com.meta.xr.sdk.interaction` PackageCache

### Grabbable Props

| Prefab | Notes |
|---|---|
| `Box.prefab` | Basic box -- good for first grab test |
| `BigStone.prefab` | Heavy, two-hand grab demo |
| `ChessPiece.prefab` | Small precision grab |
| `Mug.prefab` | Handle-based grab -- good for Torch/knife analogy |
| `Torch.prefab` | Tool grip -- most relevant for PLAGA '44 weapons |
| `Key.prefab` | Small key object |
| `StoneCube.prefab` et al. | Geometric stone shapes -- directly relevant for kamien (#29) |

Location: `Runtime/Sample/Objects/` inside `com.meta.xr.sdk.interaction` PackageCache

### Interaction Templates (Editor Quick Actions)

| Prefab | Use |
|---|---|
| `Template_HandGrabInteractor.prefab` | Drop into hand anchor -- immediate hand grab |
| `Template_HandGrabInteraction.prefab` | Interaction setup on a grabbable object |
| `Template_ControllerGrabInteractor.prefab` | Controller-based grab (fallback for no hands) |
| `BaseInteractors.prefab` | Foundation -- all interactors in one prefab |

Location: `Editor/QuickActions/Templates/` inside `com.meta.xr.sdk.interaction` PackageCache

### Usage in PLAGA '44

For `kamien` (stone) grab (#28 + #29):
1. Place `StoneCube.prefab` or `BigStone.prefab` in scene
2. Add `Template_HandGrabInteraction.prefab` as child (or follow ISDK setup wizard)
3. Add `Template_HandGrabInteractor.prefab` under `LeftHandAnchor` and `RightHandAnchor` in OVRCameraRig

---

## 4. ISDK Integration -- Connecting Movement SDK + Interaction SDK (Issue #46)

### Architecture Overview

```
Quest 3 Hardware
    |
    | body tracking data
    v
OVRBody (component on avatar root)
    |
    v
CharacterRetargeter  [Movement SDK -- com.meta.xr.sdk.movement -- NOT YET INSTALLED]
    |  maps OVRBody joints to custom avatar skeleton
    v
Avatar Skeleton (custom rigged character)
    |
    | hand bone positions
    v
ISDK Skeleton Processor  [bridges body tracking into Interaction SDK]
    |
    v
HandGrabInteractor  [Interaction SDK -- already installed v81]
    |  drives finger poses based on grab type
    v
HandGrabInteractable (on the grabbed object -- kamien, noz, butelka)
    |
    v
AvatarGrabBridge.cs (this project)
    |  syncs visual hand/finger bones to match grab pose
    v
Visual Hand Mesh (player sees full body reaching and grabbing)
```

### What is available now (v81)

- `HandGrabInteractor` -- working
- `HandGrabInteractable` -- working
- `OVRSkeleton` / `OVRHand` / `OVRMesh` -- working (controller-driven hand poses set up in #26)
- `Template_HandGrabInteractor.prefab` -- available in PackageCache

### What requires Movement SDK (com.meta.xr.sdk.movement)

- `CharacterRetargeter` -- maps OVRBody skeleton to custom avatar
- `ISDK Skeleton Processor` -- bridges body tracking poses into HandGrabInteractor
- `OVRBody` full pipeline -- OVRBody component is in core SDK but retargeting pipeline requires movement SDK
- Full first-person body visibility (seeing your own body reaching for objects)

### How to connect Movement SDK + Interaction SDK

Step 1: Add Movement SDK to manifest.json

```json
"com.meta.xr.sdk.movement": "81.0.0"
```

Step 2: On the OVRCameraRig / avatar root:
- Add `OVRBody` component
- Add `CharacterRetargeter` component -- assign OVRBody as source, avatar animator as target

Step 3: On each hand interactor:
- Add `ISDK Skeleton Processor` (from Movement SDK)
- Connect to `HandGrabInteractor`

Step 4: Assign `ISDKIntegrationManager.cs` on a manager GameObject:
- Assign `ovrBodyComponent` field
- Assign `leftHandInteractorRoot` and `rightHandInteractorRoot`
- Enable `useBodyTracking = true` and `useHandGrab = true`

Step 5: For each grabbable (kamien, noz, butelka):
- Ensure `HandGrabInteractable` is on the prefab
- Define grab poses (HandGrabPose data)

Step 6: Assign `AvatarGrabBridge.cs` on the avatar root:
- Assign `leftOVRSkeleton` / `rightOVRSkeleton`
- Assign `leftHandRigRoot` / `rightHandRigRoot` from the avatar rig
- Enable `autoSyncEveryFrame = true` or let `ISDKIntegrationManager` call `SyncHandBones()` on grab events

### Reference sample

Meta ISDK Integration sample (public):
- https://developers.meta.com/horizon/documentation/unity/movement-advanced-samples/
- Sample scene: `ISDKIntegration` -- shows full pipeline with OVRBody + CharacterRetargeter + HandGrabInteractor
- Source (public): https://github.com/oculus-samples/Unity-Movement

### Priority order for PLAGA '44

1. Get basic `HandGrabInteractor` working with `kamien` prefab -- issue #28 (no Movement SDK needed)
2. Test throw mechanics via velocity tracking -- issue #29
3. Add Movement SDK, enable body tracking -- issue #30
4. Connect ISDK bridge (this PR) once body tracking is confirmed working

---

## 5. Editor Tools Added (this PR)

### CYBERNOMAD > Meta SDK Setup > Check SDK Versions

- Reads `manifest.json` and `packages-lock.json`
- Lists all installed Meta XR packages with versions
- Flags known bugs (v83 license bug, v82 OVRSkeleton timing, v85 Audio risk)
- Recommends upgrade or stay
- Script: `Assets/Editor/SDKVersionChecker.cs`

### CYBERNOMAD > Assets > Browse SDK Assets

- Scans `Library/PackageCache` for Meta XR prefabs
- Lists by category: ENVIRONMENT, PROP-GRAB, PROP-STONE, TEMPLATE, PROP-INTERACT
- Clickable paths in Console (highlight in Project window)
- Reports missing assets (not in current SDK version)
- Script: `Assets/Editor/DefaultAssetBrowser.cs`

### Runtime: ISDKIntegrationManager.cs

- `Assets/Scripts/Integration/ISDKIntegrationManager.cs`
- MonoBehaviour, namespace `Plaga44.Integration`
- Detects Movement SDK and Interaction SDK presence via reflection
- Configurable: `useBodyTracking`, `useHandGrab`
- Placeholder init methods ready for real SDK wiring

### Runtime: AvatarGrabBridge.cs

- `Assets/Scripts/Integration/AvatarGrabBridge.cs`
- MonoBehaviour, namespace `Plaga44.Integration`
- Reads OVRSkeleton bone transforms via reflection (no hard compile dependency)
- Syncs to avatar rig bones on grab events
- API: `OnGrabStarted(bool)`, `OnGrabEnded(bool)`, `SyncHandBones()`
