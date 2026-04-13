// =============================================================================
// SettingsRegistry.cs
// CYBERNOMAD -- Runtime settings pogrupowane per SEKCJA (kazda = kafelek w menu).
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

        public SettingDef(string n, Func<float> g, Action<float> s,
            float mn, float mx, float st, string fmt = "F1")
        {
            name = n; get = g; set = s; min = mn; max = mx; step = st; format = fmt;
        }
    }

    public static class SettingsRegistry
    {
        private const string LOG = "[PLAGA44][Settings]";
        private static Dictionary<string, List<SettingDef>> _sections;
        private static string[] _sectionNames;
        private static bool _built;

        public static List<SettingDef> GetSettings(string section)
        {
            if (!_built) Build();
            if (_sections.TryGetValue(section, out var list)) return list;
            return new List<SettingDef>();
        }

        public static string[] GetSectionNames()
        {
            if (!_built) Build();
            return _sectionNames;
        }

        public static void Rebuild() { _built = false; _sections = null; }

        private static void Build()
        {
            _sections = new Dictionary<string, List<SettingDef>>();

            var urp = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
            var volume = UnityEngine.Object.FindAnyObjectByType<Volume>();
            var terrain = UnityEngine.Object.FindAnyObjectByType<Terrain>();
            var skyboxMat = RenderSettings.skybox;
            var sun = FindSun();

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
            Section("MISC", s => {
                s.Add(new SettingDef("Target Framerate", () => Application.targetFrameRate, v => Application.targetFrameRate = (int)v, -1, 120, 1, "F0"));
                s.Add(new SettingDef("Time Scale", () => Time.timeScale, v => Time.timeScale = v, 0f, 3f, 0.1f));
                s.Add(new SettingDef("Fixed Timestep", () => Time.fixedDeltaTime, v => Time.fixedDeltaTime = v, 0.005f, 0.05f, 0.005f, "F3"));
                s.Add(new SettingDef("Max Delta Time", () => Time.maximumDeltaTime, v => Time.maximumDeltaTime = v, 0.01f, 0.5f, 0.01f, "F2"));
                s.Add(new SettingDef("Shader Max LOD", () => Shader.globalMaximumLOD, v => Shader.globalMaximumLOD = (int)v, 100, 600, 100, "F0"));
                s.Add(new SettingDef("Post FX On/Off", () => (volume != null && volume.enabled) ? 1 : 0, v => { if (volume) volume.enabled = v > 0.5f; }, 0, 1, 1, "F0"));
            });

            // =============================================================
            Section("AUDIO", s => {
                s.Add(new SettingDef("Master Volume", () => AudioListener.volume, v => AudioListener.volume = v, 0f, 1f, 0.05f, "F2"));
                s.Add(new SettingDef("DSP Buffer", () => AudioSettings.GetConfiguration().dspBufferSize, v => { var c = AudioSettings.GetConfiguration(); c.dspBufferSize = (int)v; AudioSettings.Reset(c); }, 256, 4096, 256, "F0"));
            });

            // =============================================================
            Section("PHYSICS", s => {
                s.Add(new SettingDef("Gravity X", () => Physics.gravity.x, v => { var g = Physics.gravity; g.x = v; Physics.gravity = g; }, -20f, 20f, 0.5f, "F1"));
                s.Add(new SettingDef("Gravity Y", () => Physics.gravity.y, v => { var g = Physics.gravity; g.y = v; Physics.gravity = g; }, -20f, 0f, 0.5f, "F1"));
                s.Add(new SettingDef("Gravity Z", () => Physics.gravity.z, v => { var g = Physics.gravity; g.z = v; Physics.gravity = g; }, -20f, 20f, 0.5f, "F1"));
                s.Add(new SettingDef("Bounce Threshold", () => Physics.bounceThreshold, v => Physics.bounceThreshold = v, 0f, 5f, 0.1f));
                s.Add(new SettingDef("Solver Iterations", () => Physics.defaultSolverIterations, v => Physics.defaultSolverIterations = (int)v, 1, 25, 1, "F0"));
                s.Add(new SettingDef("Contact Offset", () => Physics.defaultContactOffset, v => Physics.defaultContactOffset = v, 0.001f, 0.1f, 0.005f, "F3"));
                s.Add(new SettingDef("Sleep Threshold", () => Physics.sleepThreshold, v => Physics.sleepThreshold = v, 0f, 0.5f, 0.01f, "F2"));
            });

            // =============================================================
            Section("SHADOWS", s => {
                if (urp != null)
                {
                    s.Add(new SettingDef("Distance", () => urp.shadowDistance, v => urp.shadowDistance = v, 0, 150, 5, "F0"));
                    s.Add(new SettingDef("Resolution", () => urp.mainLightShadowmapResolution, v => urp.mainLightShadowmapResolution = (int)v, 256, 4096, 256, "F0"));
                    s.Add(new SettingDef("Depth Bias", () => urp.shadowDepthBias, v => urp.shadowDepthBias = v, 0, 10, 0.5f));
                    s.Add(new SettingDef("Normal Bias", () => urp.shadowNormalBias, v => urp.shadowNormalBias = v, 0, 10, 0.5f));
                }
                if (sun != null)
                {
                    s.Add(new SettingDef("Strength", () => sun.shadowStrength, v => sun.shadowStrength = v, 0, 1, 0.01f, "F2"));
                }
            });

            // =============================================================
            Section("SUN", s => {
                if (sun != null)
                {
                    s.Add(new SettingDef("Intensity", () => sun.intensity, v => sun.intensity = v, 0, 5, 0.1f));
                    s.Add(new SettingDef("Color R", () => sun.color.r, v => { var c = sun.color; c.r = v; sun.color = c; }, 0, 1, 0.02f, "F2"));
                    s.Add(new SettingDef("Color G", () => sun.color.g, v => { var c = sun.color; c.g = v; sun.color = c; }, 0, 1, 0.02f, "F2"));
                    s.Add(new SettingDef("Color B", () => sun.color.b, v => { var c = sun.color; c.b = v; sun.color = c; }, 0, 1, 0.02f, "F2"));
                    s.Add(new SettingDef("Indirect", () => sun.bounceIntensity, v => sun.bounceIntensity = v, 0, 5, 0.1f));
                }
            });

            // =============================================================
            Section("FOG", s => {
                s.Add(new SettingDef("On/Off", () => RenderSettings.fog ? 1 : 0, v => RenderSettings.fog = v > 0.5f, 0, 1, 1, "F0"));
                s.Add(new SettingDef("Density", () => RenderSettings.fogDensity, v => RenderSettings.fogDensity = v, 0, 0.1f, 0.002f, "F3"));
                s.Add(new SettingDef("Start", () => RenderSettings.fogStartDistance, v => RenderSettings.fogStartDistance = v, 0, 200, 5, "F0"));
                s.Add(new SettingDef("End", () => RenderSettings.fogEndDistance, v => RenderSettings.fogEndDistance = v, 10, 500, 10, "F0"));
                s.Add(new SettingDef("Color R", () => RenderSettings.fogColor.r, v => { var c = RenderSettings.fogColor; c.r = v; RenderSettings.fogColor = c; }, 0, 1, 0.02f, "F2"));
                s.Add(new SettingDef("Color G", () => RenderSettings.fogColor.g, v => { var c = RenderSettings.fogColor; c.g = v; RenderSettings.fogColor = c; }, 0, 1, 0.02f, "F2"));
                s.Add(new SettingDef("Color B", () => RenderSettings.fogColor.b, v => { var c = RenderSettings.fogColor; c.b = v; RenderSettings.fogColor = c; }, 0, 1, 0.02f, "F2"));
            });

            // =============================================================
            Section("AMBIENT", s => {
                s.Add(new SettingDef("Intensity", () => RenderSettings.ambientIntensity, v => RenderSettings.ambientIntensity = v, 0, 3, 0.1f));
                s.Add(new SettingDef("Color R", () => RenderSettings.ambientLight.r, v => { var c = RenderSettings.ambientLight; c.r = v; RenderSettings.ambientLight = c; }, 0, 1, 0.05f, "F2"));
                s.Add(new SettingDef("Color G", () => RenderSettings.ambientLight.g, v => { var c = RenderSettings.ambientLight; c.g = v; RenderSettings.ambientLight = c; }, 0, 1, 0.05f, "F2"));
                s.Add(new SettingDef("Color B", () => RenderSettings.ambientLight.b, v => { var c = RenderSettings.ambientLight; c.b = v; RenderSettings.ambientLight = c; }, 0, 1, 0.05f, "F2"));
                s.Add(new SettingDef("Reflection", () => RenderSettings.reflectionIntensity, v => RenderSettings.reflectionIntensity = v, 0, 2, 0.1f));
            });

            // =============================================================
            Section("QUALITY", s => {
                if (urp != null)
                {
                    s.Add(new SettingDef("MSAA", () => urp.msaaSampleCount, v => urp.msaaSampleCount = (int)v, 1, 8, 1, "F0"));
                    s.Add(new SettingDef("Render Scale", () => urp.renderScale, v => urp.renderScale = v, 0.3f, 2.0f, 0.1f));
                }
                s.Add(new SettingDef("Eye Texture Scale", () => XRSettings.eyeTextureResolutionScale, v => XRSettings.eyeTextureResolutionScale = v, 0.3f, 2.0f, 0.1f));
                s.Add(new SettingDef("LOD Bias", () => QualitySettings.lodBias, v => QualitySettings.lodBias = v, 0.3f, 2.0f, 0.1f));
                s.Add(new SettingDef("Texture Mip", () => QualitySettings.globalTextureMipmapLimit, v => QualitySettings.globalTextureMipmapLimit = (int)v, 0, 3, 1, "F0"));
                s.Add(new SettingDef("Skin Weights", () => (float)QualitySettings.skinWeights, v => QualitySettings.skinWeights = (SkinWeights)(int)v, 1, 4, 1, "F0"));
                s.Add(new SettingDef("VSync", () => QualitySettings.vSyncCount, v => QualitySettings.vSyncCount = (int)v, 0, 2, 1, "F0"));
                s.Add(new SettingDef("Aniso Filter", () => (float)QualitySettings.anisotropicFiltering, v => QualitySettings.anisotropicFiltering = (AnisotropicFiltering)(int)v, 0, 2, 1, "F0"));
            });

            // =============================================================
            Section("CAMERA", s => {
                s.Add(new SettingDef("Near Clip", () => Camera.main != null ? Camera.main.nearClipPlane : 0.01f, v => { if (Camera.main) Camera.main.nearClipPlane = v; }, 0.01f, 1f, 0.01f, "F2"));
                s.Add(new SettingDef("Far Clip", () => Camera.main != null ? Camera.main.farClipPlane : 1000, v => { if (Camera.main) Camera.main.farClipPlane = v; }, 50, 5000, 50, "F0"));
                s.Add(new SettingDef("FOV", () => Camera.main != null ? Camera.main.fieldOfView : 60, v => { if (Camera.main) Camera.main.fieldOfView = v; }, 30, 120, 1, "F0"));
            });

            // =============================================================
            Section("OCULUS", s => {
                s.Add(new SettingDef("Foveated Lvl", () => (float)OVRManager.foveatedRenderingLevel, v => OVRManager.foveatedRenderingLevel = (OVRManager.FoveatedRenderingLevel)(int)v, 0, 4, 1, "F0"));
                s.Add(new SettingDef("Refresh Rate", () => OVRManager.display != null ? OVRManager.display.displayFrequency : 72, v => { if (OVRManager.display != null) OVRManager.display.displayFrequency = v; }, 60, 120, 6, "F0"));
            });

            // =============================================================
            if (bloom != null)
            {
                Section("BLOOM", s => {
                    s.Add(new SettingDef("Intensity", () => bloom.intensity.value, v => bloom.intensity.Override(v), 0, 5, 0.1f));
                    s.Add(new SettingDef("Threshold", () => bloom.threshold.value, v => bloom.threshold.Override(v), 0, 3, 0.1f));
                    s.Add(new SettingDef("Scatter", () => bloom.scatter.value, v => bloom.scatter.Override(v), 0, 1, 0.05f, "F2"));
                });
            }

            // =============================================================
            if (colorAdj != null)
            {
                Section("COLOR", s => {
                    s.Add(new SettingDef("Exposure", () => colorAdj.postExposure.value, v => colorAdj.postExposure.Override(v), -3f, 3f, 0.1f));
                    s.Add(new SettingDef("Contrast", () => colorAdj.contrast.value, v => colorAdj.contrast.Override(v), -100, 100, 5, "F0"));
                    s.Add(new SettingDef("Saturation", () => colorAdj.saturation.value, v => colorAdj.saturation.Override(v), -100, 100, 5, "F0"));
                    s.Add(new SettingDef("Hue Shift", () => colorAdj.hueShift.value, v => colorAdj.hueShift.Override(v), -180, 180, 5, "F0"));
                    s.Add(new SettingDef("Filter R", () => colorAdj.colorFilter.value.r, v => { var c = colorAdj.colorFilter.value; c.r = v; colorAdj.colorFilter.Override(c); }, 0, 1, 0.02f, "F2"));
                    s.Add(new SettingDef("Filter G", () => colorAdj.colorFilter.value.g, v => { var c = colorAdj.colorFilter.value; c.g = v; colorAdj.colorFilter.Override(c); }, 0, 1, 0.02f, "F2"));
                    s.Add(new SettingDef("Filter B", () => colorAdj.colorFilter.value.b, v => { var c = colorAdj.colorFilter.value; c.b = v; colorAdj.colorFilter.Override(c); }, 0, 1, 0.02f, "F2"));
                });
            }

            // =============================================================
            if (vignette != null || whiteBalance != null)
            {
                Section("COMFORT", s => {
                    if (vignette != null)
                    {
                        s.Add(new SettingDef("Vignette", () => vignette.intensity.value, v => vignette.intensity.Override(v), 0, 1, 0.05f, "F2"));
                        s.Add(new SettingDef("Vig Smooth", () => vignette.smoothness.value, v => vignette.smoothness.Override(v), 0, 1, 0.05f, "F2"));
                    }
                    if (whiteBalance != null)
                    {
                        s.Add(new SettingDef("Temperature", () => whiteBalance.temperature.value, v => whiteBalance.temperature.Override(v), -100, 100, 5, "F0"));
                        s.Add(new SettingDef("Tint", () => whiteBalance.tint.value, v => whiteBalance.tint.Override(v), -100, 100, 5, "F0"));
                    }
                });
            }

            // =============================================================
            if (lgg != null)
            {
                Section("LGG", s => {
                    s.Add(new SettingDef("Lift R", () => lgg.lift.value.x, v => { var x = lgg.lift.value; x.x = v; lgg.lift.Override(x); }, -1, 1, 0.02f, "F2"));
                    s.Add(new SettingDef("Lift G", () => lgg.lift.value.y, v => { var x = lgg.lift.value; x.y = v; lgg.lift.Override(x); }, -1, 1, 0.02f, "F2"));
                    s.Add(new SettingDef("Lift B", () => lgg.lift.value.z, v => { var x = lgg.lift.value; x.z = v; lgg.lift.Override(x); }, -1, 1, 0.02f, "F2"));
                    s.Add(new SettingDef("Gamma R", () => lgg.gamma.value.x, v => { var x = lgg.gamma.value; x.x = v; lgg.gamma.Override(x); }, -1, 1, 0.02f, "F2"));
                    s.Add(new SettingDef("Gamma G", () => lgg.gamma.value.y, v => { var x = lgg.gamma.value; x.y = v; lgg.gamma.Override(x); }, -1, 1, 0.02f, "F2"));
                    s.Add(new SettingDef("Gamma B", () => lgg.gamma.value.z, v => { var x = lgg.gamma.value; x.z = v; lgg.gamma.Override(x); }, -1, 1, 0.02f, "F2"));
                    s.Add(new SettingDef("Gain R", () => lgg.gain.value.x, v => { var x = lgg.gain.value; x.x = v; lgg.gain.Override(x); }, -1, 1, 0.02f, "F2"));
                    s.Add(new SettingDef("Gain G", () => lgg.gain.value.y, v => { var x = lgg.gain.value; x.y = v; lgg.gain.Override(x); }, -1, 1, 0.02f, "F2"));
                    s.Add(new SettingDef("Gain B", () => lgg.gain.value.z, v => { var x = lgg.gain.value; x.z = v; lgg.gain.Override(x); }, -1, 1, 0.02f, "F2"));
                });
            }

            // =============================================================
            if (skyboxMat != null)
            {
                Section("SKYBOX", s => {
                    if (skyboxMat.HasColor("_Tint"))
                    {
                        s.Add(new SettingDef("Tint R", () => skyboxMat.GetColor("_Tint").r, v => { var c = skyboxMat.GetColor("_Tint"); c.r = v; skyboxMat.SetColor("_Tint", c); }, 0, 2, 0.05f, "F2"));
                        s.Add(new SettingDef("Tint G", () => skyboxMat.GetColor("_Tint").g, v => { var c = skyboxMat.GetColor("_Tint"); c.g = v; skyboxMat.SetColor("_Tint", c); }, 0, 2, 0.05f, "F2"));
                        s.Add(new SettingDef("Tint B", () => skyboxMat.GetColor("_Tint").b, v => { var c = skyboxMat.GetColor("_Tint"); c.b = v; skyboxMat.SetColor("_Tint", c); }, 0, 2, 0.05f, "F2"));
                    }
                    if (skyboxMat.HasFloat("_Exposure"))
                        s.Add(new SettingDef("Exposure", () => skyboxMat.GetFloat("_Exposure"), v => skyboxMat.SetFloat("_Exposure", v), 0, 5, 0.1f));
                    if (skyboxMat.HasFloat("_Rotation"))
                        s.Add(new SettingDef("Rotation", () => skyboxMat.GetFloat("_Rotation"), v => skyboxMat.SetFloat("_Rotation", v), 0, 360, 10, "F0"));
                    if (skyboxMat.HasFloat("_CloudBoost"))
                        s.Add(new SettingDef("Cloud Bright", () => skyboxMat.GetFloat("_CloudBoost"), v => skyboxMat.SetFloat("_CloudBoost", v), 0, 5, 0.01f, "F2"));
                    if (skyboxMat.HasFloat("_CloudThreshold"))
                        s.Add(new SettingDef("Cloud Thresh", () => skyboxMat.GetFloat("_CloudThreshold"), v => skyboxMat.SetFloat("_CloudThreshold", v), 0, 1, 0.001f, "F3"));
                });
            }

            // =============================================================
            if (terrain != null)
            {
                Section("TERRAIN", s => {
                    s.Add(new SettingDef("Detail Dist", () => terrain.detailObjectDistance, v => terrain.detailObjectDistance = v, 0, 500, 10, "F0"));
                    s.Add(new SettingDef("Tree Dist", () => terrain.treeDistance, v => terrain.treeDistance = v, 0, 5000, 100, "F0"));
                    s.Add(new SettingDef("Billboard Dist", () => terrain.treeBillboardDistance, v => terrain.treeBillboardDistance = v, 0, 5000, 100, "F0"));
                    s.Add(new SettingDef("Max Trees", () => terrain.treeMaximumFullLODCount, v => terrain.treeMaximumFullLODCount = (int)v, 0, 500, 10, "F0"));
                    s.Add(new SettingDef("Pixel Error", () => terrain.heightmapPixelError, v => terrain.heightmapPixelError = v, 1, 200, 5, "F0"));
                    s.Add(new SettingDef("Base Map Dist", () => terrain.basemapDistance, v => terrain.basemapDistance = v, 0, 2000, 50, "F0"));
                    s.Add(new SettingDef("Instanced", () => terrain.drawInstanced ? 1 : 0, v => terrain.drawInstanced = v > 0.5f, 0, 1, 1, "F0"));
                });
            }

            // Buduj tablice nazw sekcji
            _sectionNames = new string[_sections.Count];
            _sections.Keys.CopyTo(_sectionNames, 0);

            _built = true;
            int total = 0;
            foreach (var kv in _sections) total += kv.Value.Count;
            Debug.Log($"{LOG} Built: {_sections.Count} sections, {total} settings");
        }

        private static void Section(string name, Action<List<SettingDef>> builder)
        {
            var list = new List<SettingDef>();
            builder(list);
            if (list.Count > 0)
                _sections[name] = list;
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
