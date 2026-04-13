// =============================================================================
// SettingsRegistry.cs
// CYBERNOMAD -- Runtime odpowiednik Config API.
//
// Kazdy modul Config API (AudioConfig, PhysicsConfig, itd.) ma tu swoj
// odpowiednik runtime -- lista tweakowalnych ustawien z get/set/min/max/step.
// Grupowane per nazwa modulu (ta sama co kafelek w HamburgerMenu).
//
// Wzorzec z VRQualityMenu (bleeding-edge) ale pogrupowany.
// =============================================================================

using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.XR;

namespace Plaga44.UI
{
    /// <summary>Pojedyncze ustawienie -- nazwa, getter, setter, zakres.</summary>
    public class SettingDef
    {
        public string name;
        public Func<float> get;
        public Action<float> set;
        public float min, max, step;
        public string format;
        public bool isHeader; // true = separator/naglowek sekcji

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

    /// <summary>Rejestr ustawien runtime -- per modul.</summary>
    public static class SettingsRegistry
    {
        private const string LOG = "[PLAGA44][Settings]";

        private static Dictionary<string, List<SettingDef>> _modules;
        private static bool _built;

        /// <summary>Zwraca ustawienia dla danego modulu (kafelka menu).</summary>
        public static List<SettingDef> GetSettings(string moduleName)
        {
            if (!_built) Build();
            if (_modules.TryGetValue(moduleName, out var list)) return list;
            return new List<SettingDef>();
        }

        /// <summary>Zwraca wszystkie nazwy modulow ktore maja ustawienia.</summary>
        public static string[] GetModuleNames()
        {
            if (!_built) Build();
            var names = new string[_modules.Count];
            _modules.Keys.CopyTo(names, 0);
            return names;
        }

        // =====================================================================
        // Budowanie -- odpala sie raz, lazy
        // =====================================================================

        private static void Build()
        {
            _modules = new Dictionary<string, List<SettingDef>>();

            var urp = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
            var volume = UnityEngine.Object.FindAnyObjectByType<Volume>();
            ColorAdjustments colorAdj = null;
            Tonemapping tonemapping = null;
            Vignette vignette = null;
            WhiteBalance whiteBalance = null;
            LiftGammaGain lgg = null;

            if (volume != null && volume.profile != null)
            {
                volume.profile.TryGet(out colorAdj);
                volume.profile.TryGet(out tonemapping);
                volume.profile.TryGet(out vignette);
                volume.profile.TryGet(out whiteBalance);
                volume.profile.TryGet(out lgg);
            }

            var skyboxMat = RenderSettings.skybox;

            // =============================================================
            // MISC. -- ogolne rzeczy
            // =============================================================
            var misc = new List<SettingDef>();
            misc.Add(new SettingDef("Time Scale",
                () => Time.timeScale,
                v => Time.timeScale = v,
                0f, 2f, 0.1f));
            misc.Add(new SettingDef("Fixed Timestep",
                () => Time.fixedDeltaTime,
                v => Time.fixedDeltaTime = v,
                0.005f, 0.05f, 0.005f, "F3"));
            _modules["MISC."] = misc;

            // =============================================================
            // Audio
            // =============================================================
            var audio = new List<SettingDef>();
            audio.Add(new SettingDef("Master Volume",
                () => AudioListener.volume,
                v => AudioListener.volume = v,
                0f, 1f, 0.05f, "F2"));
            _modules["Audio"] = audio;

            // =============================================================
            // Physics
            // =============================================================
            var physics = new List<SettingDef>();
            physics.Add(new SettingDef("Gravity Y",
                () => UnityEngine.Physics.gravity.y,
                v => UnityEngine.Physics.gravity = new Vector3(0, v, 0),
                -20f, 0f, 0.5f, "F1"));
            physics.Add(new SettingDef("Bounce Threshold",
                () => UnityEngine.Physics.bounceThreshold,
                v => UnityEngine.Physics.bounceThreshold = v,
                0f, 5f, 0.1f));
            _modules["Physics"] = physics;

            // =============================================================
            // Quality
            // =============================================================
            var quality = new List<SettingDef>();
            if (urp != null)
            {
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
            quality.Add(new SettingDef("LOD Bias",
                () => QualitySettings.lodBias,
                v => QualitySettings.lodBias = v,
                0.3f, 2.0f, 0.1f));
            quality.Add(new SettingDef("Texture Mip Level",
                () => QualitySettings.globalTextureMipmapLimit,
                v => QualitySettings.globalTextureMipmapLimit = (int)v,
                0, 3, 1, "F0"));
            _modules["Quality"] = quality;

            // =============================================================
            // Graphics (Lighting + Fog + Ambient)
            // =============================================================
            var gfx = new List<SettingDef>();
            gfx.Add(SettingDef.Header("--- LIGHT ---"));
            gfx.Add(new SettingDef("Sun Intensity",
                () => { var l = FindSun(); return l != null ? l.intensity : 1; },
                v => { var l = FindSun(); if (l) l.intensity = v; },
                0, 5, 0.1f));
            gfx.Add(new SettingDef("Sun R",
                () => { var l = FindSun(); return l != null ? l.color.r : 1; },
                v => { var l = FindSun(); if (l) { var c = l.color; c.r = v; l.color = c; } },
                0, 1, 0.02f, "F2"));
            gfx.Add(new SettingDef("Sun G",
                () => { var l = FindSun(); return l != null ? l.color.g : 1; },
                v => { var l = FindSun(); if (l) { var c = l.color; c.g = v; l.color = c; } },
                0, 1, 0.02f, "F2"));
            gfx.Add(new SettingDef("Sun B",
                () => { var l = FindSun(); return l != null ? l.color.b : 1; },
                v => { var l = FindSun(); if (l) { var c = l.color; c.b = v; l.color = c; } },
                0, 1, 0.02f, "F2"));
            gfx.Add(new SettingDef("Shadow Strength",
                () => { var l = FindSun(); return l != null ? l.shadowStrength : 1; },
                v => { var l = FindSun(); if (l) l.shadowStrength = v; },
                0, 1, 0.01f, "F2"));

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
            _modules["Graphics"] = gfx;

            // =============================================================
            // Oculus (OVR runtime)
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
            // Pipeline (URP asset tweaks)
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
            // Renderer (camera)
            // =============================================================
            var renderer = new List<SettingDef>();
            renderer.Add(new SettingDef("Near Clip",
                () => Camera.main != null ? Camera.main.nearClipPlane : 0.01f,
                v => { if (Camera.main) Camera.main.nearClipPlane = v; },
                0.01f, 1f, 0.01f, "F2"));
            renderer.Add(new SettingDef("Far Clip",
                () => Camera.main != null ? Camera.main.farClipPlane : 1000,
                v => { if (Camera.main) Camera.main.farClipPlane = v; },
                50, 2000, 50, "F0"));
            _modules["Renderer"] = renderer;

            // =============================================================
            // URP (global)
            // =============================================================
            // Wiekszosci URP global ustawien nie da sie zmienic runtime -- placeholder
            var urpg = new List<SettingDef>();
            urpg.Add(new SettingDef("(editor-only)", () => 0, v => {}, 0, 0, 0));
            _modules["URP"] = urpg;

            // =============================================================
            // Volume (post-process)
            // =============================================================
            var vol = new List<SettingDef>();
            vol.Add(new SettingDef("Post Process On/Off",
                () => (volume != null && volume.enabled) ? 1 : 0,
                v => { if (volume) volume.enabled = v > 0.5f; },
                0, 1, 1, "F0"));

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
            }

            if (vignette != null)
            {
                vol.Add(SettingDef.Header("--- VIGNETTE ---"));
                vol.Add(new SettingDef("Vignette Intensity",
                    () => vignette.intensity.value,
                    v => vignette.intensity.Override(v),
                    0, 1, 0.05f, "F2"));
                vol.Add(new SettingDef("Vignette Smoothness",
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
            // Skybox (pod Layers -- bo nie ma runtime layers)
            // =============================================================
            var sky = new List<SettingDef>();
            if (skyboxMat != null)
            {
                sky.Add(new SettingDef("Sky Exposure",
                    () => skyboxMat.HasFloat("_Exposure") ? skyboxMat.GetFloat("_Exposure") : 1f,
                    v => { if (skyboxMat.HasFloat("_Exposure")) skyboxMat.SetFloat("_Exposure", v); },
                    0, 5, 0.1f));
                sky.Add(new SettingDef("Sky Rotation",
                    () => skyboxMat.HasFloat("_Rotation") ? skyboxMat.GetFloat("_Rotation") : 0f,
                    v => { if (skyboxMat.HasFloat("_Rotation")) skyboxMat.SetFloat("_Rotation", v); },
                    0, 360, 10, "F0"));
            }
            _modules["Layers"] = sky; // reuse kafelek Layers dla skybox runtime

            // =============================================================
            // Moduly editor-only -- puste w runtime
            // =============================================================
            foreach (var name in new[] { "Manifest", "Packages", "Input",
                "Memory", "NavMesh", "Project", "Editor", "Build" })
            {
                _modules[name] = new List<SettingDef>
                {
                    new SettingDef($"({name} -- editor only)", () => 0, v => {}, 0, 0, 0) { isHeader = true }
                };
            }

            _built = true;
            Debug.Log($"{LOG} Built: {_modules.Count} modules");
        }

        // =====================================================================
        // Helpers
        // =====================================================================

        private static Light FindSun()
        {
            var lights = UnityEngine.Object.FindObjectsByType<Light>(FindObjectsSortMode.None);
            foreach (var l in lights)
                if (l.type == LightType.Directional) return l;
            return null;
        }

        /// <summary>Wymus przebudowanie rejestru (np. po zmianie sceny).</summary>
        public static void Rebuild()
        {
            _built = false;
            _modules = null;
        }
    }
}
