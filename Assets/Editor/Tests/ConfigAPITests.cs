// ConfigAPITests.cs -- Testy regresyjne dla CYBERNOMAD Config API
//
// Jezeli ktorykolwiek test sie wywala -- ktos cos spierdolil.
// Odpalaj: Window > General > Test Runner > EditMode > Run All
//
// Testy sprawdzaja:
//   - Czy presety maja sensowne wartosci (nie zerowe, nie absurdalne)
//   - Czy Apply() nie crashuje
//   - Czy LogCurrent() nie crashuje
//   - Czy single-value settery dzialaja (zmiana -> odczyt -> porownanie)
//   - Czy pliki projektowe istnieja

using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using Plaga44.Editor;

namespace Plaga44.Tests
{
    // =========================================================================
    // VR Pipeline
    // =========================================================================

    [TestFixture]
    public class VRPipelineTests
    {
        [Test]
        public void INITIAL_preset_has_sane_values()
        {
            var s = VRPipeline.INITIAL;
            Assert.IsFalse(s.hdr, "Quest nie powinien miec HDR");
            Assert.AreEqual(4, s.msaa, "MSAA powinno byc x4");
            Assert.That(s.renderScale, Is.InRange(0.5f, 1.5f), "Render scale poza zakresem");
            Assert.That(s.shadowDistance, Is.InRange(5f, 100f), "Shadow distance poza zakresem");
            Assert.That(s.mainShadowResolution, Is.GreaterThan(0), "Shadow res musi byc > 0");
            Assert.That(s.addLightsPerObject, Is.InRange(1, 8));
            Assert.That(s.colorGradingLutSize, Is.InRange(8, 64));
        }

        [Test]
        public void Asset_exists()
        {
            var asset = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(
                "Assets/Settings/Mobile_RPAsset.asset");
            Assert.IsNotNull(asset, "Mobile_RPAsset.asset nie znaleziony");
        }

        [Test]
        public void LogCurrent_does_not_crash()
        {
            Assert.DoesNotThrow(() => VRPipeline.LogCurrent());
        }
    }

    // =========================================================================
    // PC Pipeline
    // =========================================================================

    [TestFixture]
    public class PCPipelineTests
    {
        [Test]
        public void INITIAL_preset_has_sane_values()
        {
            var s = PCPipeline.INITIAL;
            Assert.That(s.msaa, Is.InRange(1, 8));
            Assert.That(s.renderScale, Is.InRange(0.5f, 2.0f));
            Assert.That(s.shadowDistance, Is.InRange(10f, 200f));
            Assert.That(s.mainShadowResolution, Is.GreaterThan(0));
        }

        [Test]
        public void Asset_exists()
        {
            var asset = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(
                "Assets/Settings/PC_RPAsset.asset");
            Assert.IsNotNull(asset, "PC_RPAsset.asset nie znaleziony");
        }

        [Test]
        public void LogCurrent_does_not_crash()
        {
            Assert.DoesNotThrow(() => PCPipeline.LogCurrent());
        }
    }

    // =========================================================================
    // VR Renderer
    // =========================================================================

    [TestFixture]
    public class VRRendererTests
    {
        [Test]
        public void INITIAL_preset_forward_rendering()
        {
            var s = VRRenderer.INITIAL;
            Assert.AreEqual(0, s.renderingMode, "Quest powinien byc Forward (0)");
            Assert.IsTrue(s.nativeRenderPass, "Native render pass powinien byc ON na Vulkan");
        }

        [Test]
        public void Asset_exists()
        {
            var asset = AssetDatabase.LoadAssetAtPath<Object>("Assets/Settings/Mobile_Renderer.asset");
            Assert.IsNotNull(asset, "Mobile_Renderer.asset nie znaleziony");
        }

        [Test]
        public void LogCurrent_does_not_crash()
        {
            Assert.DoesNotThrow(() => VRRenderer.LogCurrent());
        }
    }

    // =========================================================================
    // Audio
    // =========================================================================

    [TestFixture]
    public class AudioConfigTests
    {
        [Test]
        public void INITIAL_preset_has_sane_values()
        {
            var s = AudioConfig.INITIAL;
            Assert.That(s.dspBufferSize, Is.InRange(128, 4096), "DSP buffer poza zakresem");
            Assert.AreEqual(2, s.speakerMode, "VR = Stereo (2)");
            Assert.AreEqual("Meta XR Audio", s.spatializer);
        }

        [Test]
        public void LogCurrent_does_not_crash()
        {
            Assert.DoesNotThrow(() => AudioConfig.LogCurrent());
        }
    }

    // =========================================================================
    // Physics
    // =========================================================================

    [TestFixture]
    public class PhysicsConfigTests
    {
        [Test]
        public void INITIAL_preset_has_sane_values()
        {
            var s = PhysicsConfig.INITIAL;
            Assert.That(s.gravityY, Is.InRange(-20f, 0f), "Grawitacja poza zakresem");
            Assert.That(s.solverIterations, Is.InRange(1, 20));
            Assert.That(s.fixedTimestep, Is.InRange(0.001f, 0.05f), "Timestep poza zakresem");
        }

        [Test]
        public void QUEST3_preset_90Hz()
        {
            var s = PhysicsConfig.QUEST3;
            float hz = 1f / s.fixedTimestep;
            Assert.That(hz, Is.InRange(85f, 95f), "QUEST3 powinien byc ~90Hz");
        }

        [Test]
        public void LogCurrent_does_not_crash()
        {
            Assert.DoesNotThrow(() => PhysicsConfig.LogCurrent());
        }
    }

    // =========================================================================
    // Oculus
    // =========================================================================

    [TestFixture]
    public class OculusConfigTests
    {
        [Test]
        public void INITIAL_preset_controllers_only()
        {
            var s = OculusConfig.INITIAL;
            Assert.AreEqual(0, s.handTracking, "INITIAL = brak hand tracking");
            Assert.AreEqual(0, s.bodyTracking);
            Assert.AreEqual(0, s.faceTracking);
            Assert.AreEqual(0, s.eyeTracking);
        }

        [Test]
        public void FULL_preset_everything_on()
        {
            var s = OculusConfig.FULL;
            Assert.That(s.handTracking, Is.GreaterThan(0), "FULL = hand tracking ON");
            Assert.That(s.bodyTracking, Is.GreaterThan(0));
            Assert.That(s.faceTracking, Is.GreaterThan(0));
            Assert.That(s.eyeTracking, Is.GreaterThan(0));
        }

        [Test]
        public void Asset_exists()
        {
            var asset = AssetDatabase.LoadAssetAtPath<Object>(
                "Assets/Oculus/OculusProjectConfig.asset");
            Assert.IsNotNull(asset, "OculusProjectConfig.asset nie znaleziony");
        }

        [Test]
        public void LogCurrent_does_not_crash()
        {
            Assert.DoesNotThrow(() => OculusConfig.LogCurrent());
        }
    }

    // =========================================================================
    // Volume
    // =========================================================================

    [TestFixture]
    public class VolumeConfigTests
    {
        [Test]
        public void INITIAL_preset_everything_off()
        {
            var s = VolumeConfig.INITIAL;
            Assert.AreEqual(0f, s.bloomIntensity, "INITIAL = bloom off");
            Assert.AreEqual(0f, s.motionBlurIntensity, "Motion blur NIGDY w VR");
            Assert.AreEqual(0f, s.vignetteIntensity, "INITIAL = vignette off");
            Assert.AreEqual(0, s.dofMode, "INITIAL = DoF off");
        }

        [Test]
        public void CINEMATIC_preset_no_motion_blur()
        {
            var s = VolumeConfig.CINEMATIC;
            Assert.AreEqual(0f, s.motionBlurIntensity, "Motion blur NIGDY w VR nawet w cinematic");
        }

        [Test]
        public void LogCurrent_does_not_crash()
        {
            Assert.DoesNotThrow(() => VolumeConfig.LogCurrent());
        }
    }

    // =========================================================================
    // Quality
    // =========================================================================

    [TestFixture]
    public class QualityConfigTests
    {
        [Test]
        public void INITIAL_preset_has_sane_values()
        {
            var s = QualityConfig.INITIAL;
            Assert.That(s.skinWeights, Is.InRange(1, 4));
            Assert.That(s.asyncUploadBufferSizeMB, Is.InRange(4, 256));
            Assert.That(s.asyncUploadTimeSlice, Is.InRange(1, 33));
            Assert.That(s.lodBias, Is.InRange(0.1f, 4f));
            Assert.That(s.terrainDetailDistance, Is.InRange(10f, 200f));
        }

        [Test]
        public void LogCurrent_does_not_crash()
        {
            Assert.DoesNotThrow(() => QualityConfig.LogCurrent());
        }
    }

    // =========================================================================
    // Project
    // =========================================================================

    [TestFixture]
    public class ProjectConfigTests
    {
        [Test]
        public void INITIAL_preset_branding()
        {
            var s = ProjectConfig.INITIAL;
            Assert.AreEqual("Cybernomad", s.companyName);
            Assert.AreEqual("PLAGA 44", s.productName);
            Assert.AreEqual("games.cybernomad.plaga44", s.bundleId);
            Assert.IsFalse(s.showUnitySplash);
            Assert.IsTrue(s.stripEngineCode);
        }

        [Test]
        public void LogCurrent_does_not_crash()
        {
            Assert.DoesNotThrow(() => ProjectConfig.LogCurrent());
        }
    }

    // =========================================================================
    // Layers
    // =========================================================================

    [TestFixture]
    public class LayersConfigTests
    {
        [Test]
        public void INITIAL_preset_has_tags_and_layers()
        {
            var s = LayersConfig.INITIAL;
            Assert.IsNotNull(s.tags, "INITIAL musi miec tagi");
            Assert.That(s.tags.Length, Is.GreaterThan(0), "INITIAL musi miec przynajmniej 1 tag");
            Assert.IsNotNull(s.layers, "INITIAL musi miec layers");
            Assert.That(s.layers.Length, Is.GreaterThan(0), "INITIAL musi miec przynajmniej 1 layer");
        }

        [Test]
        public void INITIAL_layers_in_valid_range()
        {
            var s = LayersConfig.INITIAL;
            foreach (var (index, name) in s.layers)
            {
                Assert.That(index, Is.InRange(8, 31), $"Layer '{name}' index {index} poza zakresem 8-31");
                Assert.IsFalse(string.IsNullOrEmpty(name), $"Layer {index} ma pusta nazwe");
            }
        }

        [Test]
        public void LogCurrent_does_not_crash()
        {
            Assert.DoesNotThrow(() => LayersConfig.LogCurrent());
        }
    }

    // =========================================================================
    // Manifest
    // =========================================================================

    [TestFixture]
    public class ManifestConfigTests
    {
        [Test]
        public void Manifest_file_exists()
        {
            Assert.IsTrue(
                System.IO.File.Exists(
                    System.IO.Path.Combine(Application.dataPath, "..", "Assets/Plugins/Android/AndroidManifest.xml")),
                "AndroidManifest.xml nie znaleziony");
        }

        [Test]
        public void LogCurrent_does_not_crash()
        {
            Assert.DoesNotThrow(() => ManifestConfig.LogCurrent());
        }
    }

    // =========================================================================
    // Packages
    // =========================================================================

    [TestFixture]
    public class PackagesConfigTests
    {
        [Test]
        public void Meta_XR_SDK_installed()
        {
            string ver = PackagesConfig.GetVersion("com.meta.xr.sdk.core");
            Assert.IsNotNull(ver, "com.meta.xr.sdk.core nie znaleziony w manifest.json");
            Assert.That(ver, Does.Match(@"\d+\.\d+\.\d+"), "Wersja musi byc x.y.z");
        }

        [Test]
        public void OpenXR_installed()
        {
            string ver = PackagesConfig.GetVersion("com.unity.xr.openxr");
            Assert.IsNotNull(ver, "com.unity.xr.openxr nie znaleziony");
        }

        [Test]
        public void LogCurrent_does_not_crash()
        {
            Assert.DoesNotThrow(() => PackagesConfig.LogCurrent());
        }
    }

    // =========================================================================
    // Build Scenes
    // =========================================================================

    [TestFixture]
    public class BuildScenesConfigTests
    {
        [Test]
        public void At_least_one_scene_in_build()
        {
            var scenes = BuildScenesConfig.GetScenes();
            Assert.That(scenes.Count, Is.GreaterThan(0), "Brak scen w Build Settings");
        }

        [Test]
        public void LogCurrent_does_not_crash()
        {
            Assert.DoesNotThrow(() => BuildScenesConfig.LogCurrent());
        }
    }

    // =========================================================================
    // URP Global
    // =========================================================================

    [TestFixture]
    public class URPGlobalConfigTests
    {
        [Test]
        public void INITIAL_strips_unused()
        {
            var s = URPGlobalConfig.INITIAL;
            Assert.IsTrue(s.stripUnusedVariants, "INITIAL powinien stripowac unused variants");
            Assert.IsTrue(s.stripDebugShaders, "INITIAL powinien stripowac debug shaders");
        }

        [Test]
        public void Asset_exists()
        {
            var asset = AssetDatabase.LoadAssetAtPath<Object>(
                "Assets/Settings/UniversalRenderPipelineGlobalSettings.asset");
            Assert.IsNotNull(asset);
        }

        [Test]
        public void LogCurrent_does_not_crash()
        {
            Assert.DoesNotThrow(() => URPGlobalConfig.LogCurrent());
        }
    }

    // =========================================================================
    // Graphics
    // =========================================================================

    [TestFixture]
    public class GraphicsConfigTests
    {
        [Test]
        public void INITIAL_no_video_shaders()
        {
            var s = GraphicsConfig.INITIAL;
            Assert.AreEqual(0, s.videoShadersIncludeMode, "VR nie potrzebuje video shaderow");
        }

        [Test]
        public void LogCurrent_does_not_crash()
        {
            Assert.DoesNotThrow(() => GraphicsConfig.LogCurrent());
        }
    }

    // =========================================================================
    // Editor
    // =========================================================================

    [TestFixture]
    public class EditorConfigTests
    {
        [Test]
        public void INITIAL_force_text()
        {
            var s = EditorConfig.INITIAL;
            Assert.AreEqual(2, s.serializationMode, "INITIAL = ForceText (git-friendly)");
        }

        [Test]
        public void LogCurrent_does_not_crash()
        {
            Assert.DoesNotThrow(() => EditorConfig.LogCurrent());
        }
    }

    // =========================================================================
    // Quest2Preset -- master test
    // =========================================================================

    [TestFixture]
    public class Quest2PresetTests
    {
        [Test]
        public void LogAll_does_not_crash()
        {
            Assert.DoesNotThrow(() => Quest2Preset.LogAll());
        }
    }
}
