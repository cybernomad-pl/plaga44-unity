// =============================================================================
// SettingsRegistry.cs
// CYBERNOMAD -- Runtime odpowiednik Config API.
//
// Kazdy modul Config API ma tu swoj odpowiednik runtime -- lista tweakowalnych
// ustawien z get/set/min/max/step. Grupowane per kafelek w HamburgerMenu.
// =============================================================================

using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.XR;

namespace Plaga44.UI
{
    public class SettingDef
    {
        public string name;
        public Func<float> get;
        public Action<float> set;
        public float min, max, step;
        public string format;
        public bool isHeader;

        public SettingDef(string n, Func<float> g, Action<float> s,
            float mn, float mx, float st, string fmt = "F1")
        {
            name = n; get = g; set = s; min = mn; max = mx; step = st; format = fmt;
        }

        public static SettingDef Header(string title)
        {
            return new SettingDef(title, () => 0, v => {}, 0, 0, 0) { isHeader = true };
        }
    }

    public static class SettingsRegistry
    {
        private const string LOG = "[PLAGA44][Settings]";
        private static Dictionary<string, List<SettingDef>> _modules;
        private static bool _built;

        public static List<SettingDef> GetSettings(string moduleName)
        {
            if (!_built) Build();
            if (_modules.TryGetValue(moduleName, out var list)) return list;
            return new List<SettingDef>();
        }

        public static void Rebuild() { _built = false; _modules = null; }

        // =====================================================================
        private static void Build()
        {
            _modules = new Dictionary<string, List<SettingDef>>();

            var urp = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
            var volume = UnityEngine.Object.FindAnyObjectByType<Volume>();
            var terrain = UnityEngine.Object.FindAnyObjectByType<Terrain>();
            var skyboxMat = RenderSettings.skybox;

            ColorAdjustments colorAdj = null;
            Vignette vignette = null;
            WhiteBalance whiteBalance = null;
            LiftGammaGain lgg = null;
            Bloom bloom = null;

            if (volume != null && volume.profile != null)
            {
                volume.profile.TryGet(out colorAdj);
                volume.profile.TryGet(out vignette);
                volume.profile.TryGet(out whiteBalance);
                volume.profile.TryGet(out lgg);
                volume.profile.TryGet(out bloom);
            }

            // =============================================================
            // MISC.
            // =============================================================
            var misc = new List<SettingDef>();
            misc.Add(new SettingDef("Target Framerate",
                () => Application.targetFrameRate,
                v => Application.targetFrameRate = (int)v,
                -1, 120, 1, "F0"));
            misc.Add(new SettingDef("Time Scale",
                () => Time.timeScale,
                v => Time.timeScale = v,
                0f, 3f, 0.1f));
            misc.Add(new SettingDef("Fixed Timestep",
                () => Time.fixedDeltaTime,
                v => Time.fixedDeltaTime = v,
                0.005f, 0.05f, 0.005f, "F3"));
            misc.Add(new SettingDef("Max Delta Time",
                () => Time.maximumDeltaTime,
                v => Time.maximumDeltaTime = v,
                0.01f, 0.5f, 0.01f, "F2"));
            misc.Add(new SettingDef("Shader Max LOD",
                () => Shader.globalMaximumLOD,
                v => Shader.globalMaximumLOD = (int)v,
                100, 600, 100, "F0"));
            _modules["MISC."] = misc;

            // =============================================================
            // Audio
            // =============================================================
            var audio = new List<SettingDef>();
            audio.Add(new SettingDef("Master Volume",
                () => AudioListener.volume,
                v => AudioListener.volume = v,
                0f, 1f, 0.05f, "F2"));
            audio.Add(new SettingDef("DSP Buffer Size",
                () => AudioSettings.GetConfiguration().dspBufferSize,
                v => { var c = AudioSettings.GetConfiguration(); c.dspBufferSize = (int)v; AudioSettings.Reset(c); },
                256, 4096, 256, "F0"));
            _modules["Audio"] = audio;

            // =============================================================
            // Physics
            // =============================================================
            var physics = new List<SettingDef>();
            physics.Add(new SettingDef("Gravity X",
                () => UnityEngine.Physics.gravity.x,
                v => { var g = UnityEngine.Physics.gravity; g.x = v; UnityEngine.Physics.gravity = g; },
                -20f, 20f, 0.5f, "F1"));
            physics.Add(new SettingDef("Gravity Y",
                () => UnityEngine.Physics.gravity.y,
                v => { var g = UnityEngine.Physics.gravity; g.y = v; UnityEngine.Physics.gravity = g; },
                -20f, 0f, 0.5f, "F1"));
            physics.Add(new SettingDef("Gravity Z",
                () => UnityEngine.Physics.gravity.z,
                v => { var g = UnityEngine.Physics.gravity; g.z = v; UnityEngine.Physics.gravity = g; },
                -20f, 20f, 0.5f, "F1"));
            physics.Add(new SettingDef("Bounce Threshold",
                () => UnityEngine.Physics.bounceThreshold,
                v => UnityEngine.Physics.bounceThreshold = v,
                0f, 5f, 0.1f));
            physics.Add(new SettingDef("Solver Iterations",
                () => UnityEngine.Physics.defaultSolverIterations,
                v => UnityEngine.Physics.defaultSolverIterations = (int)v,
                1, 25, 1, "F0"));
            physics.Add(new SettingDef("Contact Offset",
                () => UnityEngine.Physics.defaultContactOffset,
                v => UnityEngine.Physics.defaultContactOffset = v,
                0.001f, 0.1f, 0.005f, "F3"));
            physics.Add(new SettingDef("Sleep Threshold",
                () => UnityEngine.Physics.sleepThreshold,
                v => UnityEngine.Physics.sleepThreshold = v,
                0f, 0.5f, 0.01f, "F2"));
            _modules["Physics"] = physics;

            // =============================================================
            // Quality
            // =============================================================
            var quality = new List<SettingDef>();
            if (urp != null)
            {
                quality.Add(SettingDef.Header("--- URP ---"));
                quality.Add(new SettingDef("MSAA",
                    () => urp.msaaSampleCount,
                    v => urp.msaaSampleCount = (int)v,
                    1, 8, 1, "F0"));
                quality.Add(new SettingDef("Shadow Distance",
                    () => urp.shadowDistance,
                    v => urp.shadowDistance = v,
                    0, 150, 5, "F0"));
                quality.Add(new SettingDef("Shadow Resolution",
                    () => urp.mainLightShadowmapResolution,
                    v => urp.mainLightShadowmapResolution = (int)v,
                    256, 4096, 256, "F0"));
                quality.Add(new SettingDef("Shadow Depth Bias",
                    () => urp.shadowDepthBias,
                    v => urp.shadowDepthBias = v,
                    0, 10, 0.5f));
                quality.Add(new SettingDef("Shadow Normal Bias",
                    () => urp.shadowNormalBias,
                    v => urp.shadowNormalBias = v,
                    0, 10, 0.5f));
            }
            quality.Add(SettingDef.Header("--- GLOBAL ---"));
            quality.Add(new SettingDef("LOD Bias",
                () => QualitySettings.lodBias,
                v => QualitySettings.lodBias = v,
                0.3f, 2.0f, 0.1f));
            quality.Add(new SettingDef("Texture Mip Level",
                () => QualitySettings.globalTextureMipmapLimit,
                v => QualitySettings.globalTextureMipmapLimit = (int)v,
                0, 3, 1, "F0"));
            quality.Add(new SettingDef("Skin Weights",
                () => (float)QualitySettings.skinWeights,
                v => QualitySettings.skinWeights = (SkinWeights)(int)v,
                1, 4, 1, "F0"));
            quality.Add(new SettingDef("VSync Count",
                () => QualitySettings.vSyncCount,
                v => QualitySettings.vSyncCount = (int)v,
                0, 2, 1, "F0"));
            quality.Add(new SettingDef("Aniso Filtering",
                () => (float)QualitySettings.anisotropicFiltering,
                v => QualitySettings.anisotropicFiltering = (AnisotropicFiltering)(int)v,
                0, 2, 1, "F0"));
            _modules["Quality"] = quality;

            // =============================================================
            // Graphics (Light + Fog + Ambient)
            // =============================================================
            var sun = FindSun(); // cache -- nie szukaj co frame
            var gfx = new List<SettingDef>();
            gfx.Add(SettingDef.Header("--- SUN ---"));
            gfx.Add(new SettingDef("Intensity",
                () => sun != null ? sun.intensity : 1,
                v => { if (sun) sun.intensity = v; },
                0, 5, 0.1f));
            gfx.Add(new SettingDef("Sun R",
                () => sun != null ? sun.color.r : 1; },
                v => { if (sun) { var c = sun.color; c.r = v; sun.color = c; } },
                0, 1, 0.02f, "F2"));
            gfx.Add(new SettingDef("Sun G",
                () => sun != null ? sun.color.g : 1; },
                v => { if (sun) { var c = sun.color; c.g = v; sun.color = c; } },
                0, 1, 0.02f, "F2"));
            gfx.Add(new SettingDef("Sun B",
                () => sun != null ? sun.color.b : 1; },
                v => { if (sun) { var c = sun.color; c.b = v; sun.color = c; } },
                0, 1, 0.02f, "F2"));
            gfx.Add(new SettingDef("Shadow Strength",
                () => sun != null ? sun.shadowStrength : 1; },
                v => { if (sun) sun.shadowStrength = v; },
                0, 1, 0.01f, "F2"));
            gfx.Add(new SettingDef("Indirect Multiplier",
                () => sun != null ? sun.bounceIntensity : 1; },
                v => { if (sun) sun.bounceIntensity = v; },
                0, 5, 0.1f));

            gfx.Add(SettingDef.Header("--- FOG ---"));
            gfx.Add(new SettingDef("Fog On/Off",
                () => RenderSettings.fog ? 1 : 0,
                v => RenderSettings.fog = v > 0.5f,
                0, 1, 1, "F0"));
            gfx.Add(new SettingDef("Fog Density",
                () => RenderSettings.fogDensity,
                v => RenderSettings.fogDensity = v,
                0, 0.1f, 0.002f, "F3"));
            gfx.Add(new SettingDef("Fog Start",
                () => RenderSettings.fogStartDistance,
                v => RenderSettings.fogStartDistance = v,
                0, 200, 5, "F0"));
            gfx.Add(new SettingDef("Fog End",
                () => RenderSettings.fogEndDistance,
                v => RenderSettings.fogEndDistance = v,
                10, 500, 10, "F0"));
            gfx.Add(new SettingDef("Fog R",
                () => RenderSettings.fogColor.r,
                v => { var c = RenderSettings.fogColor; c.r = v; RenderSettings.fogColor = c; },
                0, 1, 0.02f, "F2"));
            gfx.Add(new SettingDef("Fog G",
                () => RenderSettings.fogColor.g,
                v => { var c = RenderSettings.fogColor; c.g = v; RenderSettings.fogColor = c; },
                0, 1, 0.02f, "F2"));
            gfx.Add(new SettingDef("Fog B",
                () => RenderSettings.fogColor.b,
                v => { var c = RenderSettings.fogColor; c.b = v; RenderSettings.fogColor = c; },
                0, 1, 0.02f, "F2"));

            gfx.Add(SettingDef.Header("--- AMBIENT ---"));
            gfx.Add(new SettingDef("Ambient Intensity",
                () => RenderSettings.ambientIntensity,
                v => RenderSettings.ambientIntensity = v,
                0, 3, 0.1f));
            gfx.Add(new SettingDef("Ambient R",
                () => RenderSettings.ambientLight.r,
                v => { var c = RenderSettings.ambientLight; c.r = v; RenderSettings.ambientLight = c; },
                0, 1, 0.05f, "F2"));
            gfx.Add(new SettingDef("Ambient G",
                () => RenderSettings.ambientLight.g,
                v => { var c = RenderSettings.ambientLight; c.g = v; RenderSettings.ambientLight = c; },
                0, 1, 0.05f, "F2"));
            gfx.Add(new SettingDef("Ambient B",
                () => RenderSettings.ambientLight.b,
                v => { var c = RenderSettings.ambientLight; c.b = v; RenderSettings.ambientLight = c; },
                0, 1, 0.05f, "F2"));
            gfx.Add(new SettingDef("Reflection Intensity",
                () => RenderSettings.reflectionIntensity,
                v => RenderSettings.reflectionIntensity = v,
                0, 2, 0.1f));
            _modules["Graphics"] = gfx;

            // =============================================================
            // Oculus
            // =============================================================
            var oculus = new List<SettingDef>();
            oculus.Add(new SettingDef("Foveated Render Lvl",
                () => (float)OVRManager.foveatedRenderingLevel,
                v => OVRManager.foveatedRenderingLevel = (OVRManager.FoveatedRenderingLevel)(int)v,
                0, 4, 1, "F0"));
            oculus.Add(new SettingDef("Refresh Rate",
                () => OVRManager.display != null ? OVRManager.display.displayFrequency : 72,
                v => { if (OVRManager.display != null) OVRManager.display.displayFrequency = v; },
                60, 120, 6, "F0"));
            _modules["Oculus"] = oculus;

            // =============================================================
            // Pipeline
            // =============================================================
            var pipeline = new List<SettingDef>();
            if (urp != null)
            {
                pipeline.Add(new SettingDef("Render Scale",
                    () => urp.renderScale,
                    v => urp.renderScale = v,
                    0.3f, 2.0f, 0.1f));
                pipeline.Add(new SettingDef("Eye Texture Scale",
                    () => XRSettings.eyeTextureResolutionScale,
                    v => XRSettings.eyeTextureResolutionScale = v,
                    0.3f, 2.0f, 0.1f));
            }
            _modules["Pipeline"] = pipeline;

            // =============================================================
            // Renderer (Camera)
            // =============================================================
            var renderer = new List<SettingDef>();
            renderer.Add(new SettingDef("Near Clip",
                () => Camera.main != null ? Camera.main.nearClipPlane : 0.01f,
                v => { if (Camera.main) Camera.main.nearClipPlane = v; },
                0.01f, 1f, 0.01f, "F2"));
            renderer.Add(new SettingDef("Far Clip",
                () => Camera.main != null ? Camera.main.farClipPlane : 1000,
                v => { if (Camera.main) Camera.main.farClipPlane = v; },
                50, 5000, 50, "F0"));
            renderer.Add(new SettingDef("FOV",
                () => Camera.main != null ? Camera.main.fieldOfView : 60,
                v => { if (Camera.main) Camera.main.fieldOfView = v; },
                30, 120, 1, "F0"));
            renderer.Add(new SettingDef("Depth",
                () => Camera.main != null ? Camera.main.depth : 0,
                v => { if (Camera.main) Camera.main.depth = v; },
                -10, 10, 1, "F0"));
            _modules["Renderer"] = renderer;

            // =============================================================
            // URP -- placeholder
            // =============================================================
            var urpg = new List<SettingDef>();
            urpg.Add(new SettingDef("(editor-only settings)", () => 0, v => {}, 0, 0, 0) { isHeader = true });
            _modules["URP"] = urpg;

            // =============================================================
            // Volume (post-process)
            // =============================================================
            var vol = new List<SettingDef>();
            vol.Add(new SettingDef("Post Process On/Off",
                () => (volume != null && volume.enabled) ? 1 : 0,
                v => { if (volume) volume.enabled = v > 0.5f; },
                0, 1, 1, "F0"));

            if (bloom != null)
            {
                vol.Add(SettingDef.Header("--- BLOOM ---"));
                vol.Add(new SettingDef("Bloom Intensity",
                    () => bloom.intensity.value,
                    v => bloom.intensity.Override(v),
                    0, 5, 0.1f));
                vol.Add(new SettingDef("Bloom Threshold",
                    () => bloom.threshold.value,
                    v => bloom.threshold.Override(v),
                    0, 3, 0.1f));
                vol.Add(new SettingDef("Bloom Scatter",
                    () => bloom.scatter.value,
                    v => bloom.scatter.Override(v),
                    0, 1, 0.05f, "F2"));
            }

            if (colorAdj != null)
            {
                vol.Add(SettingDef.Header("--- COLOR ---"));
                vol.Add(new SettingDef("Exposure",
                    () => colorAdj.postExposure.value,
                    v => colorAdj.postExposure.Override(v),
                    -3f, 3f, 0.1f));
                vol.Add(new SettingDef("Contrast",
                    () => colorAdj.contrast.value,
                    v => colorAdj.contrast.Override(v),
                    -100, 100, 5, "F0"));
                vol.Add(new SettingDef("Saturation",
                    () => colorAdj.saturation.value,
                    v => colorAdj.saturation.Override(v),
                    -100, 100, 5, "F0"));
                vol.Add(new SettingDef("Hue Shift",
                    () => colorAdj.hueShift.value,
                    v => colorAdj.hueShift.Override(v),
                    -180, 180, 5, "F0"));
                vol.Add(new SettingDef("Color R",
                    () => colorAdj.colorFilter.value.r,
                    v => { var c = colorAdj.colorFilter.value; c.r = v; colorAdj.colorFilter.Override(c); },
                    0, 1, 0.02f, "F2"));
                vol.Add(new SettingDef("Color G",
                    () => colorAdj.colorFilter.value.g,
                    v => { var c = colorAdj.colorFilter.value; c.g = v; colorAdj.colorFilter.Override(c); },
                    0, 1, 0.02f, "F2"));
                vol.Add(new SettingDef("Color B",
                    () => colorAdj.colorFilter.value.b,
                    v => { var c = colorAdj.colorFilter.value; c.b = v; colorAdj.colorFilter.Override(c); },
                    0, 1, 0.02f, "F2"));
            }

            if (vignette != null)
            {
                vol.Add(SettingDef.Header("--- VIGNETTE ---"));
                vol.Add(new SettingDef("Intensity",
                    () => vignette.intensity.value,
                    v => vignette.intensity.Override(v),
                    0, 1, 0.05f, "F2"));
                vol.Add(new SettingDef("Smoothness",
                    () => vignette.smoothness.value,
                    v => vignette.smoothness.Override(v),
                    0, 1, 0.05f, "F2"));
            }

            if (whiteBalance != null)
            {
                vol.Add(SettingDef.Header("--- WHITE BAL ---"));
                vol.Add(new SettingDef("Temperature",
                    () => whiteBalance.temperature.value,
                    v => whiteBalance.temperature.Override(v),
                    -100, 100, 5, "F0"));
                vol.Add(new SettingDef("Tint",
                    () => whiteBalance.tint.value,
                    v => whiteBalance.tint.Override(v),
                    -100, 100, 5, "F0"));
            }

            if (lgg != null)
            {
                vol.Add(SettingDef.Header("--- LIFT ---"));
                vol.Add(new SettingDef("Lift R", () => lgg.lift.value.x, v => { var x = lgg.lift.value; x.x = v; lgg.lift.Override(x); }, -1, 1, 0.02f, "F2"));
                vol.Add(new SettingDef("Lift G", () => lgg.lift.value.y, v => { var x = lgg.lift.value; x.y = v; lgg.lift.Override(x); }, -1, 1, 0.02f, "F2"));
                vol.Add(new SettingDef("Lift B", () => lgg.lift.value.z, v => { var x = lgg.lift.value; x.z = v; lgg.lift.Override(x); }, -1, 1, 0.02f, "F2"));
                vol.Add(SettingDef.Header("--- GAMMA ---"));
                vol.Add(new SettingDef("Gamma R", () => lgg.gamma.value.x, v => { var x = lgg.gamma.value; x.x = v; lgg.gamma.Override(x); }, -1, 1, 0.02f, "F2"));
                vol.Add(new SettingDef("Gamma G", () => lgg.gamma.value.y, v => { var x = lgg.gamma.value; x.y = v; lgg.gamma.Override(x); }, -1, 1, 0.02f, "F2"));
                vol.Add(new SettingDef("Gamma B", () => lgg.gamma.value.z, v => { var x = lgg.gamma.value; x.z = v; lgg.gamma.Override(x); }, -1, 1, 0.02f, "F2"));
                vol.Add(SettingDef.Header("--- GAIN ---"));
                vol.Add(new SettingDef("Gain R", () => lgg.gain.value.x, v => { var x = lgg.gain.value; x.x = v; lgg.gain.Override(x); }, -1, 1, 0.02f, "F2"));
                vol.Add(new SettingDef("Gain G", () => lgg.gain.value.y, v => { var x = lgg.gain.value; x.y = v; lgg.gain.Override(x); }, -1, 1, 0.02f, "F2"));
                vol.Add(new SettingDef("Gain B", () => lgg.gain.value.z, v => { var x = lgg.gain.value; x.z = v; lgg.gain.Override(x); }, -1, 1, 0.02f, "F2"));
            }
            _modules["Volume"] = vol;

            // =============================================================
            // Layers -> Skybox (runtime)
            // =============================================================
            var sky = new List<SettingDef>();
            if (skyboxMat != null)
            {
                if (skyboxMat.HasColor("_Tint"))
                {
                    sky.Add(new SettingDef("Sky Tint R",
                        () => skyboxMat.GetColor("_Tint").r,
                        v => { var c = skyboxMat.GetColor("_Tint"); c.r = v; skyboxMat.SetColor("_Tint", c); },
                        0, 2, 0.05f, "F2"));
                    sky.Add(new SettingDef("Sky Tint G",
                        () => skyboxMat.GetColor("_Tint").g,
                        v => { var c = skyboxMat.GetColor("_Tint"); c.g = v; skyboxMat.SetColor("_Tint", c); },
                        0, 2, 0.05f, "F2"));
                    sky.Add(new SettingDef("Sky Tint B",
                        () => skyboxMat.GetColor("_Tint").b,
                        v => { var c = skyboxMat.GetColor("_Tint"); c.b = v; skyboxMat.SetColor("_Tint", c); },
                        0, 2, 0.05f, "F2"));
                }
                if (skyboxMat.HasFloat("_Exposure"))
                    sky.Add(new SettingDef("Sky Exposure",
                        () => skyboxMat.GetFloat("_Exposure"),
                        v => skyboxMat.SetFloat("_Exposure", v),
                        0, 5, 0.1f));
                if (skyboxMat.HasFloat("_Rotation"))
                    sky.Add(new SettingDef("Sky Rotation",
                        () => skyboxMat.GetFloat("_Rotation"),
                        v => skyboxMat.SetFloat("_Rotation", v),
                        0, 360, 10, "F0"));
                if (skyboxMat.HasFloat("_CloudBoost"))
                    sky.Add(new SettingDef("Cloud Brightness",
                        () => skyboxMat.GetFloat("_CloudBoost"),
                        v => skyboxMat.SetFloat("_CloudBoost", v),
                        0, 5, 0.01f, "F2"));
                if (skyboxMat.HasFloat("_CloudThreshold"))
                    sky.Add(new SettingDef("Cloud Threshold",
                        () => skyboxMat.GetFloat("_CloudThreshold"),
                        v => skyboxMat.SetFloat("_CloudThreshold", v),
                        0, 1, 0.001f, "F3"));
            }
            _modules["Layers"] = sky;

            // =============================================================
            // Manifest -> Terrain (runtime)
            // =============================================================
            var terr = new List<SettingDef>();
            if (terrain != null)
            {
                terr.Add(new SettingDef("Draw Distance",
                    () => terrain.detailObjectDistance,
                    v => terrain.detailObjectDistance = v,
                    0, 500, 10, "F0"));
                terr.Add(new SettingDef("Tree Distance",
                    () => terrain.treeDistance,
                    v => terrain.treeDistance = v,
                    0, 5000, 100, "F0"));
                terr.Add(new SettingDef("Tree Billboard Dist",
                    () => terrain.treeBillboardDistance,
                    v => terrain.treeBillboardDistance = v,
                    0, 5000, 100, "F0"));
                terr.Add(new SettingDef("Max Mesh Trees",
                    () => terrain.treeMaximumFullLODCount,
                    v => terrain.treeMaximumFullLODCount = (int)v,
                    0, 500, 10, "F0"));
                terr.Add(new SettingDef("Pixel Error",
                    () => terrain.heightmapPixelError,
                    v => terrain.heightmapPixelError = v,
                    1, 200, 5, "F0"));
                terr.Add(new SettingDef("Base Map Dist",
                    () => terrain.basemapDistance,
                    v => terrain.basemapDistance = v,
                    0, 2000, 50, "F0"));
                terr.Add(new SettingDef("Draw Instanced",
                    () => terrain.drawInstanced ? 1 : 0,
                    v => terrain.drawInstanced = v > 0.5f,
                    0, 1, 1, "F0"));
            }
            else
            {
                terr.Add(new SettingDef("(no terrain)", () => 0, v => {}, 0, 0, 0) { isHeader = true });
            }
            _modules["Manifest"] = terr; // reuse Manifest kafelek dla Terrain

            // =============================================================
            // Editor-only placeholders
            // =============================================================
            foreach (var name in new[] { "Packages", "Input", "Memory",
                "NavMesh", "Project", "Editor", "Build" })
            {
                _modules[name] = new List<SettingDef>
                {
                    new SettingDef($"({name} -- editor only)", () => 0, v => {}, 0, 0, 0) { isHeader = true }
                };
            }

            _built = true;
            int total = 0;
            foreach (var kv in _modules) total += kv.Value.Count;
            Debug.Log($"{LOG} Built: {_modules.Count} modules, {total} settings total");
        }

        private static Light FindSun()
        {
            var lights = UnityEngine.Object.FindObjectsByType<Light>(FindObjectsSortMode.None);
            foreach (var l in lights)
                if (l.type == LightType.Directional) return l;
            return null;
        }
    }
}
