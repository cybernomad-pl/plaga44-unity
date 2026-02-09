# PLAGA '44

Unity 6.3 LTS (6000.3.7f1) | Meta Quest 2/3 | Meta XR SDK + OpenXR

---

## Setup

### 1. Scoped Registry (juz w manifest.json)

```json
"scopedRegistries": [{
  "name": "Meta XR",
  "url": "https://npm.developer.oculus.com",
  "scopes": ["com.meta.xr"]
}]
```

### 2. Build Platform

File > Build Profiles > Meta Quest > Switch Platform
(auto-instaluje OpenXR + meta-openxr)

### 3. Meta XR Core SDK

Package Manager > "Meta XR Core SDK" > Install

**BUG v83 + Unity 6.3**: license error. Fix: uzyj v81 albo Asset Store.
- https://communityforums.atmeta.com/discussions/Questions_Discussions/unity-6-3---meta-xr-core-license-error/1357387

### 4. XR Plugin Management

Edit > Project Settings > XR Plug-in Management > Android:
- [x] OpenXR
- [x] Meta Quest Feature Group
- Interaction Profile: Oculus Touch Controller Profile

### 5. Scena

1. Usun Main Camera
2. Meta > Tools > Building Blocks > (+) Camera Rig
3. (+) Controller Tracking
4. Opcjonalnie: (+) Hand Tracking

---

## Pakiety

| Pakiet | Co robi |
|--------|---------|
| `com.unity.xr.openxr` >= 1.14 | Bazowy OpenXR |
| `com.unity.xr.meta-openxr` 2.4 | Meta extensions |
| `com.meta.xr.sdk.core` | OVRCameraRig, Building Blocks, Passthrough |
| `com.meta.xr.sdk.interaction` | Grab, poke, rece, kontrolery |
| `com.meta.xr.sdk.audio` | Spatial audio 3D |

---

## Budzety Quest 2

| Metric | Wartosc |
|--------|---------|
| Trojkaty/frame | 750K - 1M |
| Postac | 5K - 15K tri |
| Bone weights | 2 max |
| Bones/postac | < 75 |
| Tekstury postaci | 1K ASTC 6x6 |
| Tekstury env | 2K ASTC 6x6 |
| Draw calls | 80 - 600 |
| FPS | 72 min, 90 target |
| Rendering | Single Pass Instanced, Vulkan, 4x MSAA |
| Foveated Rendering | ON |

---

## Migracja na czysty OpenXR (gdyby trzeba)

OVRCameraRig -> XR Origin, OVRInput -> Input System, Building Blocks -> XR Interaction Toolkit. ~2-4 tygodnie.

OVRPlugin blokuje non-Meta headsety na PCVR. Dla Quest-only nie problem.

---

## Zrodla

- Meta XR UPM: https://developers.meta.com/horizon/documentation/unity/unity-package-manager/
- Unity Meta Quest packages: https://docs.unity3d.com/6000.3/Documentation/Manual/xr-meta-quest-packages.html
- meta-openxr: https://docs.unity3d.com/6000.3/Documentation/Manual/com.unity.xr.meta-openxr.html
- Build profile: https://docs.unity3d.com/6000.3/Documentation/Manual/xr-meta-quest-build-profile.html
- XR Plugin Management: https://developers.meta.com/horizon/documentation/unity/unity-xr-plugin/
- OVRCameraRig: https://developers.meta.com/horizon/documentation/unity/unity-ovrcamerarig/
- Building Blocks: https://developers.meta.com/horizon/documentation/unity/bb-overview/
- Body/Hand Tracking: https://developers.meta.com/horizon/documentation/unity/move-unity-getting-started/
- Passthrough: https://developers.meta.com/horizon/documentation/unity/unity-passthrough-tutorial/
- Performance: https://developers.meta.com/horizon/documentation/unity/unity-perf/
- VR settings: https://developers.meta.com/horizon/blog/tech-note-unity-settings-for-mobile-vr/
- OpenXR migration: https://developers.meta.com/horizon/blog/oculus-all-in-on-openxr-deprecates-proprietary-apis/
- Meta XR Core SDK (Asset Store): https://assetstore.unity.com/packages/tools/integration/meta-xr-core-sdk-269169
