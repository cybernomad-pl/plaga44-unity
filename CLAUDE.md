# PLAGA '44 -- Unity Project

Unity 6 (6000.3.7f1) | URP 17.3.0 | Meta XR SDK 81.0.0 | Quest 2/3

## CYBERNOMAD Config API

Caly projekt jest sterowalny przez API w Assets/Editor/*.cs.
NIGDY nie edytuj ProjectSettings/*.asset ani Assets/Settings/*.asset recznie.
ZAWSZE uzywaj naszego API.

### Presety -- jeden przycisk

```csharp
// Konfiguruje CALY projekt pod Quest 2
Quest2Preset.Apply();

// Loguje WSZYSTKIE ustawienia
Quest2Preset.LogAll();
```

### VR Pipeline (Mobile_RPAsset -- Quest rendering)

```csharp
VRPipeline.Apply(VRPipeline.INITIAL);
VRPipeline.Apply(new PipelineSettings { hdr = false, msaa = 4, renderScale = 1.0f, shadowDistance = 20f, ... });
VRPipeline.SetMSAA(4);
VRPipeline.SetHDR(false);
VRPipeline.SetRenderScale(1.0f);
VRPipeline.SetShadowDistance(20f);
VRPipeline.SetShadowResolution(1024);
VRPipeline.SetSoftShadows(false);
VRPipeline.SetAdditionalLights(2);
VRPipeline.SetValue("m_MSAA", 4);  // dowolne pole YAML
VRPipeline.LogCurrent();
```

### PC Pipeline (PC_RPAsset -- Editor/Standalone)

```csharp
PCPipeline.Apply(PCPipeline.INITIAL);
PCPipeline.SetMSAA(4);
PCPipeline.SetShadowDistance(100f);
PCPipeline.LogCurrent();
// Te same metody co VRPipeline
```

### VR Renderer (Mobile_Renderer)

```csharp
VRRenderer.Apply(VRRenderer.INITIAL);
VRRenderer.SetRenderingMode(0);     // 0=Forward, 1=Forward+, 2=Deferred
VRRenderer.SetNativeRenderPass(true);
VRRenderer.SetDepthPriming(0);      // 0=Disabled, 1=Auto, 2=Forced
VRRenderer.SetShadowTransparent(false);
VRRenderer.LogCurrent();
```

### PC Renderer (PC_Renderer)

```csharp
PCRenderer.Apply(PCRenderer.INITIAL);
PCRenderer.Apply(PCRenderer.HIEND);  // Deferred + depth priming
PCRenderer.SetRenderingMode(2);      // Deferred
PCRenderer.LogCurrent();
```

### Volume (post-processing)

```csharp
VolumeConfig.Apply(VolumeConfig.INITIAL);    // all off
VolumeConfig.Apply(VolumeConfig.CINEMATIC);  // bloom + ACES + vignette
VolumeConfig.SetBloom(0.3f);
VolumeConfig.SetBloomThreshold(0.9f);
VolumeConfig.SetTonemapping(2);      // 0=None, 1=Neutral, 2=ACES
VolumeConfig.SetVignette(0.2f);
VolumeConfig.SetExposure(0.5f);
VolumeConfig.SetContrast(10f);
VolumeConfig.SetSaturation(10f);
VolumeConfig.SetMotionBlur(0f);      // NIGDY w VR
VolumeConfig.SetChromaticAberration(0.05f);
VolumeConfig.SetFilmGrain(0.1f);
VolumeConfig.SetDoF(0);             // 0=Off, 1=Gaussian, 2=Bokeh
VolumeConfig.LogCurrent();
```

### Quality (LOD, terrain, async upload)

```csharp
QualityConfig.Apply(QualityConfig.INITIAL);
QualityConfig.SetSkinWeights(2);     // 1=One, 2=Two, 4=Four bones
QualityConfig.SetAnisotropic(1);     // 0=Disabled, 1=PerTexture, 2=ForcedOn
QualityConfig.SetTextureMipmapLimit(0); // 0=Full, 1=Half, 2=Quarter
QualityConfig.SetStreamingMipmaps(false);
QualityConfig.SetAsyncUploadBuffer(32);  // MB
QualityConfig.SetAsyncUploadTimeSlice(4); // ms
QualityConfig.SetLODBias(1.0f);
QualityConfig.SetLODCrossFade(false);
QualityConfig.SetTerrainPixelError(5f);
QualityConfig.SetTerrainDetailDistance(40f);
QualityConfig.LogCurrent();
```

### Audio

```csharp
AudioConfig.Apply(AudioConfig.INITIAL);
AudioConfig.SetDSPBuffer(512);       // 256, 512, 1024
AudioConfig.SetSpeakerMode(2);       // 2=Stereo
AudioConfig.SetSpatializer("Meta XR Audio");
AudioConfig.SetSampleRate(0);        // 0=system, 44100, 48000
AudioConfig.LogCurrent();
```

### Physics (gravity, solver, timestep)

```csharp
PhysicsConfig.Apply(PhysicsConfig.INITIAL);    // 72Hz
PhysicsConfig.Apply(PhysicsConfig.QUEST3);     // 90Hz
PhysicsConfig.SetGravity(-9.81f);
PhysicsConfig.SetSolverIterations(4);
PhysicsConfig.SetFixedTimestep(0.01388889f);   // 72Hz
PhysicsConfig.SetFixedTimestep(0.01111111f);   // 90Hz
PhysicsConfig.LogCurrent();
```

### Oculus (tracking features)

```csharp
OculusConfig.Apply(OculusConfig.INITIAL);  // controllers only
OculusConfig.Apply(OculusConfig.FULL);     // all tracking on
OculusConfig.SetHandTracking(true);
OculusConfig.SetBodyTracking(true);
OculusConfig.SetFaceTracking(true);
OculusConfig.SetEyeTracking(true);
OculusConfig.SetAnchorSupport(true);       // MR
OculusConfig.SetSceneSupport(true);        // MR
OculusConfig.LogCurrent();
```

### Android Manifest

```csharp
ManifestConfig.SetPermission("android.permission.RECORD_AUDIO", true);
ManifestConfig.AddFeature("android.hardware.microphone", false);
ManifestConfig.SetMetaData("com.oculus.ossplash.background", "black");
ManifestConfig.SetSupportedDevices("quest3|quest3s");
ManifestConfig.LogCurrent();
```

### Layers & Tags

```csharp
LayersConfig.Apply(LayersConfig.INITIAL);
LayersConfig.AddLayer("Water", 16);
LayersConfig.AddTag("Destructible");
LayersConfig.RemoveLayer(16);
LayersConfig.LogCurrent();
```

### Packages (manifest.json)

```csharp
PackagesConfig.AddPackage("com.unity.textmeshpro", "4.0.0");
PackagesConfig.RemovePackage("com.unity.visualscripting");
PackagesConfig.SetVersion("com.meta.xr.sdk.core", "85.0.0");
PackagesConfig.SetMetaXRVersion("85.0.0");  // wszystkie Meta naraz
PackagesConfig.GetVersion("com.meta.xr.sdk.core");  // "81.0.0"
PackagesConfig.LogCurrent();
PackagesConfig.LogMetaXR();
```

### Project (branding, defines, stripping)

```csharp
ProjectConfig.Apply(ProjectConfig.INITIAL);
ProjectConfig.SetCompanyName("Cybernomad");
ProjectConfig.SetProductName("PLAGA 44");
ProjectConfig.SetBundleVersion("0.2.0");
ProjectConfig.SetVersionCode(2);
ProjectConfig.SetStripEngineCode(true);
ProjectConfig.SetShowSplash(false);
ProjectConfig.AddScriptingDefine("LOCOMOTION_ONLY");
ProjectConfig.RemoveScriptingDefine("LOCOMOTION_ONLY");
ProjectConfig.LogCurrent();
```

### Build Scenes

```csharp
BuildScenesConfig.SetScenes(new[] { "Assets/TESTBED_V2.unity" });
BuildScenesConfig.AddScene("Assets/Scenes/Level1.unity");
BuildScenesConfig.RemoveScene("Assets/Scenes/Old.unity");
BuildScenesConfig.EnableScene("Assets/TESTBED_V2.unity", false);
BuildScenesConfig.LogCurrent();
```

### Editor Settings

```csharp
EditorConfig.Apply(EditorConfig.INITIAL);
EditorConfig.SetSerializationMode(2);    // ForceText
EditorConfig.SetEnterPlayModeOptions(true, 1);  // DisableDomainReload
EditorConfig.LogCurrent();
```

### URP Global (shader stripping, render graph)

```csharp
URPGlobalConfig.Apply(URPGlobalConfig.INITIAL);
URPGlobalConfig.Apply(URPGlobalConfig.DEBUG);
URPGlobalConfig.SetStripUnusedVariants(true);
URPGlobalConfig.SetStripDebugShaders(true);
URPGlobalConfig.SetRenderCompatibilityMode(false);  // Render Graph ON
URPGlobalConfig.SetShaderVariantLog(0);  // 0=Off, 1=SRP, 2=All
URPGlobalConfig.SetRenderingLayerName(0, "Default");
URPGlobalConfig.LogCurrent();
```

### Graphics (stripping, preload)

```csharp
GraphicsConfig.Apply(GraphicsConfig.INITIAL);
GraphicsConfig.SetVideoShaders(0);       // 0=Never, 1=Referenced, 2=Always
GraphicsConfig.SetShaderPreloadLimit(50); // ms
GraphicsConfig.SetFogStripping(0);       // 0=Automatic
GraphicsConfig.LogCurrent();
```

### NavMesh

```csharp
NavMeshConfig.SetAreaCost(0, 1.0f);      // Walkable
NavMeshConfig.SetAreaName(3, "Water");
NavMeshConfig.LogCurrent();
```

### Memory, Input, Misc

```csharp
MemoryConfig.SetValue("m_EditorMemorySettings.m_MainAllocatorBlockSize", 16777216);
InputConfig.LogCurrent();
MiscConfig.SetInt("ProjectSettings/VFXManager.asset", "m_FixedTimeStep", 0.016f);
MiscConfig.LogAsset("ProjectSettings/VFXManager.asset");
```

## Git Flow

- **main** = stabilna baza
- **bleeding-edge** = branch rozwojowy
- **reference-branch** = stara pelna implementacja (punkt odniesienia)
- Worker branches: `wrkX/nazwa-taska`
- NIGDY nie merguj bleeding-edge do main bez testowania
- NIGDY nie dodawaj Co-Authored-By do commitow

## Struktura

```
Assets/
  Editor/           -- CYBERNOMAD tools (Config API)
  Settings/         -- URP pipeline + renderer assets
  TESTBED_V2.unity  -- aktywna scena
```

## Build

```
Quest 2: CYBERNOMAD > Presets > Apply QUEST 2
Build:   CYBERNOMAD > Meta SDK Setup > 2. Switch to Android
         Ctrl+B lub build script
```
