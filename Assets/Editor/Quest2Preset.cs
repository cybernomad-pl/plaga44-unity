// Quest2Preset.cs -- CYBERNOMAD Editor Tool
//
// JEDEN PRZYCISK -- konfiguruje CALY projekt pod Quest 2.
// Wywoluje API ze wszystkich Configow.
//
// Public API:
//   Quest2Preset.Apply();
//   Quest2Preset.LogAll();
//
// Menu: CYBERNOMAD > Presets > Apply QUEST 2

using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace Plaga44.Editor
{
    public static class Quest2Preset
    {
        private const string LOG = "[PLAGA44/QUEST2]";

        [MenuItem("CYBERNOMAD/Presets/Quest/--- Apply All ---", false, 0)]
        public static void Apply()
        {
            Debug.Log($"{LOG} ========== APPLYING QUEST 2 PRESET ==========");

            ApplyProject();
            ApplyVRPipeline();
            ApplyVRRenderer();
            ApplyVolume();
            ApplyQuality();
            ApplyAudio();
            ApplyPhysics();
            ApplyOculus();
            ApplyURPGlobal();
            ApplyGraphics();
            ApplyEditor();
            ApplyLayers();
            EnsureSSAO();

            Debug.Log($"{LOG} ========== QUEST 2 PRESET COMPLETE ==========");
        }

        [MenuItem("CYBERNOMAD/Presets/Quest/--- Log All ---", false, 100)]
        public static void LogAll()
        {
            Debug.Log($"{LOG} ========== FULL PROJECT STATUS ==========");
            ProjectConfig.LogCurrent();
            VRPipeline.LogCurrent();
            VRRenderer.LogCurrent();
            VolumeConfig.LogCurrent();
            QualityConfig.LogCurrent();
            AudioConfig.LogCurrent();
            PhysicsConfig.LogCurrent();
            OculusConfig.LogCurrent();
            URPGlobalConfig.LogCurrent();
            GraphicsConfig.LogCurrent();
            EditorConfig.LogCurrent();
            LayersConfig.LogCurrent();
            BuildScenesConfig.LogCurrent();
            PackagesConfig.LogMetaXR();
            Debug.Log($"{LOG} ==========================================");
        }

        // =====================================================================
        // Poszczegolne systemy
        // =====================================================================

        static void ApplyProject()
        {
            ProjectConfig.Apply(new ProjectSettings_
            {
                companyName         = "Cybernomad",
                productName         = "PLAGA 44",
                bundleId            = "games.cybernomad.plaga44",
                bundleVersion       = "0.1.0",
                androidVersionCode  = 1,
                colorSpace          = 1,        // Linear
                orientationDefault  = 3,        // LandscapeLeft
                autoPortrait        = false,
                autoPortraitUD      = false,
                autoLandscapeR      = false,
                autoLandscapeL      = true,
                showUnitySplash     = false,
                stripEngineCode     = true,
                scriptingDefines    = null,
            });
        }

        static void ApplyVRPipeline()
        {
            VRPipeline.Apply(new PipelineSettings
            {
                hdr                         = false,    // Quest 2 nie ma HDR display
                msaa                        = 4,        // x4 -- kluczowe w VR
                renderScale                 = 1.0f,     // pelna rozdzielczosc
                shadowDistance               = 20f,      // 20m wystarczy
                mainShadowResolution        = 1024,     // dobry balans
                addShadowResolution         = 512,
                addLightsPerObject          = 2,
                reflectionProbeBlending     = false,    // za ciezkie
                reflectionProbeBoxProjection = false,
                lightLayers                 = false,
                lensFlareData               = false,
                lensFlareScreenSpace        = false,
                colorGradingLutSize         = 16,
                softShadows                 = false,
            });
        }

        static void ApplyVRRenderer()
        {
            VRRenderer.Apply(new RendererSettings
            {
                renderingMode           = 0,        // Forward (Deferred za ciezki na Quest)
                nativeRenderPass        = true,     // Vulkan optymalizacja
                depthPrimingMode        = 0,        // Disabled
                copyDepthMode           = 0,        // AfterOpaques
                shadowTransparentReceive = false,   // oszczednosc GPU
                intermediateTextureMode = 0,        // Auto
            });
        }

        static void ApplyVolume()
        {
            VolumeConfig.Apply(new VolumeSettings
            {
                bloomIntensity       = 0f,      // off -- oszczednosc
                bloomThreshold       = 0.9f,
                bloomScatter         = 0.7f,
                tonemappingMode      = 0,       // None (HDR off)
                vignetteIntensity    = 0f,      // locomotion vignette osobno
                vignetteSmoothness   = 0.2f,
                postExposure         = 0f,
                contrast             = 0f,
                saturation           = 0f,
                motionBlurIntensity  = 0f,      // NIGDY w VR
                chromaticAberration  = 0f,
                filmGrainIntensity   = 0f,
                dofMode              = 0,       // off
            });
        }

        static void ApplyQuality()
        {
            QualityConfig.Apply(new QualitySettings_
            {
                skinWeights             = 2,        // TwoBones
                anisotropicTextures     = 1,        // PerTexture
                globalTextureMipmapLimit = 0,       // Full
                streamingMipmapsActive  = false,
                streamingMipmapsBudgetMB = 256,
                asyncUploadTimeSlice    = 4,        // 4ms
                asyncUploadBufferSizeMB = 32,       // 32MB
                lodBias                 = 1.0f,
                maximumLODLevel         = 0,
                enableLODCrossFade      = false,    // oszczednosc
                particleRaycastBudget   = 64,
                terrainPixelError       = 5,
                terrainDetailDistance    = 40,
                terrainBasemapDistance   = 500,
                terrainTreeDistance      = 2000,
            });
        }

        static void ApplyAudio()
        {
            AudioConfig.Apply(new AudioSettings_
            {
                dspBufferSize    = 512,             // mniej latency
                speakerMode      = 2,               // Stereo
                spatializer      = "Meta XR Audio",
                ambisonicDecoder = "Meta XR Audio",
                sampleRate       = 0,               // system default
            });
        }

        static void ApplyPhysics()
        {
            PhysicsConfig.Apply(new PhysicsSettings
            {
                gravityY            = -9.81f,
                solverIterations    = 4,
                defaultContactOffset = 0.01f,
                bounceThreshold     = 2f,
                fixedTimestep       = 0.01388889f,  // 72Hz (Quest 2)
                maxTimestep         = 0.33333334f,
            });
        }

        static void ApplyOculus()
        {
            OculusConfig.Apply(new OculusSettings
            {
                handTracking     = 0,   // controllers only na start
                handTrackingFreq = 0,
                bodyTracking     = 0,
                faceTracking     = 0,
                eyeTracking      = 0,
                anchorSupport    = 0,
                sceneSupport     = 0,
                renderModel      = 0,
            });
        }

        static void ApplyURPGlobal()
        {
            URPGlobalConfig.Apply(new URPGlobalSettings
            {
                stripUnusedVariants         = true,
                stripUnusedPostProcessing   = true,
                stripDebugShaders           = true,
                stripScreenCoordOverride    = true,
                renderCompatibilityMode     = false,    // Render Graph ON
                shaderVariantLogLevel       = 0,
                exportShaderVariants        = false,
                renderingLayerNames         = new[] {
                    "Default", "Characters", "Environment", "VFX",
                    "UI", "Unused5", "Unused6", "Unused7"
                },
            });
        }

        static void ApplyGraphics()
        {
            GraphicsConfig.Apply(new GraphicsSettings_
            {
                transparencySortMode        = 0,
                lightmapStripping           = 0,
                fogStripping                = 0,
                instancingStripping         = 0,
                brgStripping                = 0,
                videoShadersIncludeMode     = 0,    // Never
                preloadShadersBatchTimeLimit = 50,   // 50ms
            });
        }

        static void ApplyEditor()
        {
            EditorConfig.Apply(new EditorSettings_
            {
                serializationMode        = 2,       // ForceText
                spritePackerMode         = 0,       // Disabled
                lineEndingsForNewScripts = 0,       // OS
                enterPlayModeOptionsEnabled = true,
                enterPlayModeOptions     = 1,       // DisableDomainReload
            });
        }

        static void ApplyLayers()
        {
            LayersConfig.Apply(new LayerSettings
            {
                tags = new[] { "Player", "Enemy", "NPC", "Weapon", "Pickup", "Interactable", "Trigger" },
                layers = new[]
                {
                    (8,  "Player"),
                    (9,  "Enemy"),
                    (10, "NPC"),
                    (11, "Interactable"),
                    (12, "Ground"),
                    (13, "Projectile"),
                    (14, "Trigger"),
                    (15, "Hand"),
                },
            });
        }

        // =====================================================================
        // SSAO -- renderer feature, wymaga Unity API
        // =====================================================================

        static void EnsureSSAO()
        {
            var renderer = AssetDatabase.LoadAssetAtPath<ScriptableRendererData>(
                "Assets/Settings/Mobile_Renderer.asset");
            if (renderer == null)
            {
                Debug.LogError($"{LOG} Mobile_Renderer.asset not found");
                return;
            }

            // Sprawdz czy SSAO juz jest
            foreach (var feature in renderer.rendererFeatures)
            {
                if (feature != null && feature.GetType().Name.Contains("ScreenSpaceAmbientOcclusion"))
                {
                    Debug.Log($"{LOG} SSAO already present on Mobile_Renderer");
                    ConfigureSSAO(feature);
                    return;
                }
            }

            // Dodaj SSAO
            var ssao = ScriptableObject.CreateInstance<ScreenSpaceAmbientOcclusion>();
            ssao.name = "ScreenSpaceAmbientOcclusion";
            ConfigureSSAO(ssao);

            AssetDatabase.AddObjectToAsset(ssao, renderer);

            // Dodaj do listy features przez SerializedObject
            var so = new SerializedObject(renderer);
            var features = so.FindProperty("m_RendererFeatures");
            features.InsertArrayElementAtIndex(features.arraySize);
            features.GetArrayElementAtIndex(features.arraySize - 1).objectReferenceValue = ssao;

            // Update feature map
            var map = so.FindProperty("m_RendererFeatureMap");
            if (map != null)
            {
                map.stringValue = "";  // Unity regeneruje
            }

            so.ApplyModifiedProperties();
            EditorUtility.SetDirty(renderer);
            AssetDatabase.SaveAssets();

            Debug.Log($"{LOG} SSAO added to Mobile_Renderer (max quality for realism testing)");
        }

        static void ConfigureSSAO(ScriptableRendererFeature ssao)
        {
            var so = new SerializedObject(ssao);

            // Max quality SSAO
            var settings = so.FindProperty("m_Settings");
            if (settings != null)
            {
                var intensity = settings.FindPropertyRelative("Intensity");
                if (intensity != null) intensity.floatValue = 1.0f;         // max

                var radius = settings.FindPropertyRelative("Radius");
                if (radius != null) radius.floatValue = 0.5f;              // szeroki

                var samples = settings.FindPropertyRelative("Samples");
                if (samples != null) samples.intValue = 2;                  // High

                var downsample = settings.FindPropertyRelative("Downsample");
                if (downsample != null) downsample.intValue = 0;            // Full res

                var source = settings.FindPropertyRelative("Source");
                if (source != null) source.intValue = 1;                    // DepthNormals

                var directStrength = settings.FindPropertyRelative("DirectLightingStrength");
                if (directStrength != null) directStrength.floatValue = 0.5f;

                var falloff = settings.FindPropertyRelative("Falloff");
                if (falloff != null) falloff.floatValue = 100f;
            }

            var active = so.FindProperty("m_Active");
            if (active != null) active.boolValue = true;

            so.ApplyModifiedProperties();
            EditorUtility.SetDirty(ssao);
        }
    }
}
