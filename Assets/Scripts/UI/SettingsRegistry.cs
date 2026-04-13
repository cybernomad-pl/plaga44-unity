// =============================================================================
// SettingsRegistry.cs -- Complete runtime settings per section.
// Each setting has a description. Save/Load presets to PlayerPrefs.
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
        public string name, desc;
        public Func<float> get;
        public Action<float> set;
        public float min, max, step;
        public string format;
        public SettingDef(string n, string d, Func<float> g, Action<float> s, float mn, float mx, float st, string fmt = "F1")
        { name = n; desc = d; get = g; set = s; min = mn; max = mx; step = st; format = fmt; }
    }

    public static class SettingsRegistry
    {
        private static Dictionary<string, List<SettingDef>> _sec;
        private static string[] _names;
        private static bool _built;
        private static List<SettingDef> _allSettings; // flat list for save/load

        public static List<SettingDef> GetSettings(string s) { if (!_built) Build(); return _sec.TryGetValue(s, out var l) ? l : new List<SettingDef>(); }
        public static string[] GetSectionNames() { if (!_built) Build(); return _names; }
        public static void Rebuild() { _built = false; _sec = null; }

        static SettingDef S(string n, string d, Func<float> g, Action<float> s, float mn, float mx, float st, string f="F1")
            => new SettingDef(n,d,g,s,mn,mx,st,f);

        static void Sec(string name, Action<List<SettingDef>> b)
        { var l = new List<SettingDef>(); b(l); if (l.Count > 0) _sec[name] = l; }

        // =====================================================================
        // Save / Load presets to PlayerPrefs
        // =====================================================================

        public static void SavePreset(int slot)
        {
            if (!_built) Build();
            string prefix = $"Plaga44_Preset{slot}_";
            foreach (var s in _allSettings)
            {
                float val = s.get();
                PlayerPrefs.SetFloat(prefix + s.name, val);
            }
            PlayerPrefs.SetInt(prefix + "__count", _allSettings.Count);
            PlayerPrefs.Save();
            Debug.Log($"[PLAGA44][Settings] SAVED preset {slot} ({_allSettings.Count} values)");
        }

        public static void LoadPreset(int slot)
        {
            if (!_built) Build();
            string prefix = $"Plaga44_Preset{slot}_";
            if (!PlayerPrefs.HasKey(prefix + "__count"))
            {
                Debug.LogWarning($"[PLAGA44][Settings] Preset {slot} not found");
                return;
            }
            int loaded = 0;
            foreach (var s in _allSettings)
            {
                string key = prefix + s.name;
                if (PlayerPrefs.HasKey(key))
                {
                    float val = PlayerPrefs.GetFloat(key);
                    val = Mathf.Clamp(val, s.min, s.max);
                    s.set(val);
                    loaded++;
                }
            }
            Debug.Log($"[PLAGA44][Settings] LOADED preset {slot} ({loaded} values)");
        }

        public static void LogAll()
        {
            if (!_built) Build();
            var sb = new System.Text.StringBuilder();
            sb.AppendLine("=== PLAGA44 SETTINGS DUMP ===");
            foreach (var kv in _sec)
            {
                sb.AppendLine($"\n--- {kv.Key} ---");
                foreach (var s in kv.Value)
                    sb.AppendLine($"  {s.name} = {s.get().ToString(s.format)}  [{s.min}..{s.max}]  // {s.desc}");
            }
            Debug.Log(sb.ToString());
        }

        // =====================================================================
        // Build
        // =====================================================================

        static void Build()
        {
            _sec = new Dictionary<string, List<SettingDef>>();
            _allSettings = new List<SettingDef>();

            var urp = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
            var vol = UnityEngine.Object.FindAnyObjectByType<Volume>();
            var ter = UnityEngine.Object.FindAnyObjectByType<Terrain>();
            var sky = RenderSettings.skybox;
            var sun = FindSun();
            var tMat = ter != null ? ter.materialTemplate : null;
            ColorAdjustments ca = null; Vignette vig = null; WhiteBalance wb = null; LiftGammaGain lgg = null; Bloom blm = null;
            if (vol != null && vol.profile != null) { vol.profile.TryGet(out ca); vol.profile.TryGet(out vig); vol.profile.TryGet(out wb); vol.profile.TryGet(out lgg); vol.profile.TryGet(out blm); }

            // Scene scripts
            var loco = UnityEngine.Object.FindAnyObjectByType<Plaga44.Locomotion.LocomotionController>();
            var cc = loco != null ? loco.GetComponent<UnityEngine.CharacterController>() : null;
            var smoothTurn = UnityEngine.Object.FindAnyObjectByType<Plaga44.Locomotion.SmoothTurnController>();
            var skyRot = UnityEngine.Object.FindAnyObjectByType<Plaga44.SkyRotator>();

            // =============================================================
            // LOCOMOTION
            // =============================================================
            if (loco != null) Sec("LOCOMOTION", s => {
                s.Add(S("Move Speed", "Walk speed m/s", () => loco.moveSpeed, v => loco.moveSpeed=v, 0.5f, 10, 0.5f));
                s.Add(S("Strafe", "Strafe speed multiplier (0.8=80%)", () => loco.strafeFactor, v => loco.strafeFactor=v, 0.1f, 1, 0.05f, "F2"));
                s.Add(S("Speed (RO)", "Current normalised speed (0-1)", () => loco.NormalisedSpeed, v => {}, 0, 1, 0, "F2"));
                s.Add(S("VVel (RO)", "Vertical velocity (fall/jump)", () => loco.VerticalVelocity, v => {}, -100, 100, 0, "F1"));
                s.Add(S("Grounded", "Is player grounded (RO)", () => loco.IsGrounded?1:0, v => {}, 0, 1, 0, "F0"));
            });

            // =============================================================
            // SMOOTH TURN
            // =============================================================
            if (smoothTurn != null) Sec("SMOOTH TURN", s => {
                s.Add(S("Turn Speed", "Max rotation speed deg/s", () => smoothTurn.turnSpeed, v => smoothTurn.turnSpeed=v, 30, 360, 10, "F0"));
                s.Add(S("Dead Zone", "Stick dead zone threshold", () => smoothTurn.deadZone, v => smoothTurn.deadZone=v, 0.05f, 0.5f, 0.05f, "F2"));
            });

            // =============================================================
            // CHARACTER CTRL
            // =============================================================
            if (cc != null) Sec("CHAR CTRL", s => {
                s.Add(S("Height", "CharacterController height", () => cc.height, v => cc.height=v, 0.5f, 3, 0.1f));
                s.Add(S("Radius", "Player collision radius", () => cc.radius, v => cc.radius=v, 0.1f, 1, 0.05f, "F2"));
                s.Add(S("Skin Width", "Collision penetration tolerance", () => cc.skinWidth, v => cc.skinWidth=v, 0.01f, 0.2f, 0.01f, "F2"));
                s.Add(S("Step Offset", "Max step height", () => cc.stepOffset, v => cc.stepOffset=v, 0, 1, 0.05f, "F2"));
                s.Add(S("Slope Limit", "Max slope angle (degrees)", () => cc.slopeLimit, v => cc.slopeLimit=v, 0, 90, 5, "F0"));
                s.Add(S("Center Y", "Collision center Y offset", () => cc.center.y, v => cc.center=new Vector3(cc.center.x,v,cc.center.z), 0, 2, 0.05f, "F2"));
            });

            // =============================================================
            // GAME STATE
            // =============================================================
            Sec("GAME STATE", s => {
                s.Add(S("Phase", "Game phase (0=Splash 1=Menu 2=Load 3=Play 4=Inv 5=Pause 6=Dead)", () => (float)GameState.Current, v => GameState.SetState((GamePhase)(int)v), 0, 6, 1, "F0"));
            });

            // =============================================================
            // MISC
            // =============================================================
            Sec("MISC", s => {
                s.Add(S("Target FPS", "Frame rate limit (-1=unlimited)", () => Application.targetFrameRate, v => Application.targetFrameRate=(int)v, -1, 120, 1, "F0"));
                s.Add(S("Time Scale", "Time speed (0=paused, 1=normal)", () => Time.timeScale, v => Time.timeScale=v, 0, 3, 0.1f));
                s.Add(S("Fixed Step", "Physics step in seconds", () => Time.fixedDeltaTime, v => Time.fixedDeltaTime=v, 0.005f, 0.05f, 0.005f, "F3"));
                s.Add(S("Max Delta", "Prevents teleport after lag spike", () => Time.maximumDeltaTime, v => Time.maximumDeltaTime=v, 0.01f, 0.5f, 0.01f, "F2"));
                s.Add(S("Shader LOD", "Max shader LOD (lower=simpler)", () => Shader.globalMaximumLOD, v => Shader.globalMaximumLOD=(int)v, 100, 600, 100, "F0"));
                s.Add(S("Post FX", "Post-processing on/off", () => (vol!=null&&vol.enabled)?1:0, v => { if(vol) vol.enabled=v>0.5f; }, 0, 1, 1, "F0"));
            });

            // =============================================================
            // AUDIO
            // =============================================================
            Sec("AUDIO", s => {
                s.Add(S("Volume", "Global volume", () => AudioListener.volume, v => AudioListener.volume=v, 0, 1, 0.05f, "F2"));
                s.Add(S("DSP Buffer", "Audio buffer (higher=more stable, more latency)", () => AudioSettings.GetConfiguration().dspBufferSize, v => { var c=AudioSettings.GetConfiguration(); c.dspBufferSize=(int)v; AudioSettings.Reset(c); }, 256, 4096, 256, "F0"));
            });

            // =============================================================
            // PHYSICS
            // =============================================================
            Sec("PHYSICS", s => {
                s.Add(S("Gravity X", "Lateral gravity", () => Physics.gravity.x, v => { var g=Physics.gravity; g.x=v; Physics.gravity=g; }, -20, 20, 0.5f, "F1"));
                s.Add(S("Gravity Y", "Vertical gravity (-9.81=Earth)", () => Physics.gravity.y, v => { var g=Physics.gravity; g.y=v; Physics.gravity=g; }, -20, 20, 0.5f, "F1"));
                s.Add(S("Gravity Z", "Forward gravity", () => Physics.gravity.z, v => { var g=Physics.gravity; g.z=v; Physics.gravity=g; }, -20, 20, 0.5f, "F1"));
                s.Add(S("Solver Iter", "Collision solver iterations", () => Physics.defaultSolverIterations, v => Physics.defaultSolverIterations=(int)v, 1, 25, 1, "F0"));
                s.Add(S("Contact Off", "Min contact distance", () => Physics.defaultContactOffset, v => Physics.defaultContactOffset=v, 0.001f, 0.1f, 0.005f, "F3"));
                s.Add(S("Sleep Thr", "Rigidbody sleep threshold", () => Physics.sleepThreshold, v => Physics.sleepThreshold=v, 0, 0.5f, 0.01f, "F2"));
                s.Add(S("Bounce Thr", "Min bounce velocity", () => Physics.bounceThreshold, v => Physics.bounceThreshold=v, 0, 5, 0.1f));
            });

            // =============================================================
            // SHADOWS
            // =============================================================
            Sec("SHADOWS", s => {
                if (urp != null) {
                    s.Add(S("Distance", "Shadow range (m)", () => urp.shadowDistance, v => urp.shadowDistance=v, 0, 150, 5, "F0"));
                    s.Add(S("Resolution", "Shadow map px", () => urp.mainLightShadowmapResolution, v => urp.mainLightShadowmapResolution=(int)v, 256, 4096, 256, "F0"));
                    s.Add(S("Depth Bias", "Prevents shadow acne", () => urp.shadowDepthBias, v => urp.shadowDepthBias=v, 0, 10, 0.5f));
                    s.Add(S("Normal Bias", "Prevents peter-panning", () => urp.shadowNormalBias, v => urp.shadowNormalBias=v, 0, 10, 0.5f));
                }
                if (sun != null)
                    s.Add(S("Strength", "Shadow intensity (0-1)", () => sun.shadowStrength, v => sun.shadowStrength=v, 0, 1, 0.01f, "F2"));
            });

            // =============================================================
            // SUN
            // =============================================================
            if (sun != null) Sec("SUN", s => {
                s.Add(S("Intensity", "Sun brightness", () => sun.intensity, v => sun.intensity=v, 0, 5, 0.1f));
                s.Add(S("R", "Sun color red", () => sun.color.r, v => { var c=sun.color; c.r=v; sun.color=c; }, 0, 1, 0.02f, "F2"));
                s.Add(S("G", "Sun color green", () => sun.color.g, v => { var c=sun.color; c.g=v; sun.color=c; }, 0, 1, 0.02f, "F2"));
                s.Add(S("B", "Sun color blue", () => sun.color.b, v => { var c=sun.color; c.b=v; sun.color=c; }, 0, 1, 0.02f, "F2"));
                s.Add(S("Indirect", "Bounce light multiplier", () => sun.bounceIntensity, v => sun.bounceIntensity=v, 0, 5, 0.1f));
                s.Add(S("Rot X", "Sun angle X (elevation)", () => sun.transform.eulerAngles.x, v => sun.transform.eulerAngles=new Vector3(v,sun.transform.eulerAngles.y,0), 0, 90, 1, "F0"));
                s.Add(S("Rot Y", "Sun angle Y (azimuth)", () => sun.transform.eulerAngles.y, v => sun.transform.eulerAngles=new Vector3(sun.transform.eulerAngles.x,v,0), 0, 360, 5, "F0"));
            });

            // =============================================================
            // FOG
            // =============================================================
            Sec("FOG", s => {
                s.Add(S("On/Off", "Toggle fog", () => RenderSettings.fog?1:0, v => RenderSettings.fog=v>0.5f, 0, 1, 1, "F0"));
                s.Add(S("Density", "Density (exponential)", () => RenderSettings.fogDensity, v => RenderSettings.fogDensity=v, 0, 0.1f, 0.002f, "F3"));
                s.Add(S("Start", "Start distance (linear)", () => RenderSettings.fogStartDistance, v => RenderSettings.fogStartDistance=v, 0, 200, 5, "F0"));
                s.Add(S("End", "Full fog distance (linear)", () => RenderSettings.fogEndDistance, v => RenderSettings.fogEndDistance=v, 10, 500, 10, "F0"));
                s.Add(S("R", "Fog color R", () => RenderSettings.fogColor.r, v => { var c=RenderSettings.fogColor; c.r=v; RenderSettings.fogColor=c; }, 0, 1, 0.02f, "F2"));
                s.Add(S("G", "Fog color G", () => RenderSettings.fogColor.g, v => { var c=RenderSettings.fogColor; c.g=v; RenderSettings.fogColor=c; }, 0, 1, 0.02f, "F2"));
                s.Add(S("B", "Fog color B", () => RenderSettings.fogColor.b, v => { var c=RenderSettings.fogColor; c.b=v; RenderSettings.fogColor=c; }, 0, 1, 0.02f, "F2"));
            });

            // =============================================================
            // AMBIENT
            // =============================================================
            Sec("AMBIENT", s => {
                s.Add(S("Intensity", "Ambient brightness", () => RenderSettings.ambientIntensity, v => RenderSettings.ambientIntensity=v, 0, 3, 0.1f));
                s.Add(S("R", "Ambient R", () => RenderSettings.ambientLight.r, v => { var c=RenderSettings.ambientLight; c.r=v; RenderSettings.ambientLight=c; }, 0, 1, 0.05f, "F2"));
                s.Add(S("G", "Ambient G", () => RenderSettings.ambientLight.g, v => { var c=RenderSettings.ambientLight; c.g=v; RenderSettings.ambientLight=c; }, 0, 1, 0.05f, "F2"));
                s.Add(S("B", "Ambient B", () => RenderSettings.ambientLight.b, v => { var c=RenderSettings.ambientLight; c.b=v; RenderSettings.ambientLight=c; }, 0, 1, 0.05f, "F2"));
                s.Add(S("Reflection", "Reflection probe intensity", () => RenderSettings.reflectionIntensity, v => RenderSettings.reflectionIntensity=v, 0, 2, 0.1f));
            });

            // =============================================================
            // QUALITY
            // =============================================================
            Sec("QUALITY", s => {
                if (urp != null) {
                    s.Add(S("MSAA", "Anti-aliasing (1/2/4/8)", () => urp.msaaSampleCount, v => urp.msaaSampleCount=(int)v, 1, 8, 1, "F0"));
                    s.Add(S("Render Scale", "Render resolution scale", () => urp.renderScale, v => urp.renderScale=v, 0.3f, 2, 0.1f));
                }
                s.Add(S("Eye Tex", "VR eye texture scale", () => XRSettings.eyeTextureResolutionScale, v => XRSettings.eyeTextureResolutionScale=v, 0.3f, 2, 0.1f));
                s.Add(S("LOD Bias", "LOD distance (higher=more detail)", () => QualitySettings.lodBias, v => QualitySettings.lodBias=v, 0.3f, 2, 0.1f));
                s.Add(S("Tex Mip", "Mipmap level (0=full, 3=low)", () => QualitySettings.globalTextureMipmapLimit, v => QualitySettings.globalTextureMipmapLimit=(int)v, 0, 3, 1, "F0"));
                s.Add(S("Skin Wts", "Bones per vertex (1-4)", () => (float)QualitySettings.skinWeights, v => QualitySettings.skinWeights=(SkinWeights)(int)v, 1, 4, 1, "F0"));
                s.Add(S("VSync", "Sync to display", () => QualitySettings.vSyncCount, v => QualitySettings.vSyncCount=(int)v, 0, 2, 1, "F0"));
                s.Add(S("Aniso", "Anisotropic filtering", () => (float)QualitySettings.anisotropicFiltering, v => QualitySettings.anisotropicFiltering=(AnisotropicFiltering)(int)v, 0, 2, 1, "F0"));
            });

            // =============================================================
            // CAMERA
            // =============================================================
            Sec("CAMERA", s => {
                s.Add(S("Near Clip", "Min render distance", () => Camera.main!=null?Camera.main.nearClipPlane:0.01f, v => { if(Camera.main) Camera.main.nearClipPlane=v; }, 0.01f, 1, 0.01f, "F2"));
                s.Add(S("Far Clip", "Max render distance", () => Camera.main!=null?Camera.main.farClipPlane:1000, v => { if(Camera.main) Camera.main.farClipPlane=v; }, 50, 5000, 50, "F0"));
                s.Add(S("FOV", "Field of view (degrees)", () => Camera.main!=null?Camera.main.fieldOfView:60, v => { if(Camera.main) Camera.main.fieldOfView=v; }, 30, 120, 1, "F0"));
            });

            // =============================================================
            // OCULUS
            // =============================================================
            Sec("OCULUS", s => {
                try {
                    s.Add(S("FFR Level", "Foveated rendering (0=off, 4=max)", () => (float)OVRManager.foveatedRenderingLevel, v => OVRManager.foveatedRenderingLevel=(OVRManager.FoveatedRenderingLevel)(int)v, 0, 4, 1, "F0"));
                    s.Add(S("Refresh Hz", "Quest refresh rate", () => OVRManager.display!=null?OVRManager.display.displayFrequency:72, v => { if(OVRManager.display!=null) OVRManager.display.displayFrequency=v; }, 60, 120, 6, "F0"));
                } catch (Exception ex) {
                    Debug.LogWarning($"[PLAGA44][Settings] OVRManager unavailable: {ex.Message}");
                }
            });

            // =============================================================
            // SKYBOX (full shader)
            // =============================================================
            if (sky != null) Sec("SKYBOX", s => {
                if (sky.HasColor("_Tint")) {
                    s.Add(S("Tint R", "Sky tint R", () => sky.GetColor("_Tint").r, v => { var c=sky.GetColor("_Tint"); c.r=v; sky.SetColor("_Tint",c); }, 0, 2, 0.02f, "F2"));
                    s.Add(S("Tint G", "Sky tint G", () => sky.GetColor("_Tint").g, v => { var c=sky.GetColor("_Tint"); c.g=v; sky.SetColor("_Tint",c); }, 0, 2, 0.02f, "F2"));
                    s.Add(S("Tint B", "Sky tint B", () => sky.GetColor("_Tint").b, v => { var c=sky.GetColor("_Tint"); c.b=v; sky.SetColor("_Tint",c); }, 0, 2, 0.02f, "F2"));
                }
                if (sky.HasFloat("_Exposure")) s.Add(S("Exposure", "Sky brightness", () => sky.GetFloat("_Exposure"), v => sky.SetFloat("_Exposure",v), 0, 8, 0.1f));
                if (sky.HasFloat("_Rotation")) s.Add(S("Rotation", "Skybox rotation (deg)", () => sky.GetFloat("_Rotation"), v => sky.SetFloat("_Rotation",v), 0, 360, 5, "F0"));
                // _RotSpeed shader property pominieta -- SkyRotator skrypt ogarnia rotacje
                if (sky.HasColor("_GroundColor")) {
                    s.Add(S("Ground R", "Ground/horizon color R", () => sky.GetColor("_GroundColor").r, v => { var c=sky.GetColor("_GroundColor"); c.r=v; sky.SetColor("_GroundColor",c); }, 0, 1, 0.02f, "F2"));
                    s.Add(S("Ground G", "Ground/horizon color G", () => sky.GetColor("_GroundColor").g, v => { var c=sky.GetColor("_GroundColor"); c.g=v; sky.SetColor("_GroundColor",c); }, 0, 1, 0.02f, "F2"));
                    s.Add(S("Ground B", "Ground/horizon color B", () => sky.GetColor("_GroundColor").b, v => { var c=sky.GetColor("_GroundColor"); c.b=v; sky.SetColor("_GroundColor",c); }, 0, 1, 0.02f, "F2"));
                }
                if (sky.HasFloat("_GroundBlend")) s.Add(S("Ground Blend", "Horizon height (-0.5..0.5)", () => sky.GetFloat("_GroundBlend"), v => sky.SetFloat("_GroundBlend",v), -0.5f, 0.5f, 0.01f, "F2"));
                if (sky.HasFloat("_GroundFade")) s.Add(S("Ground Fade", "Sky-ground transition softness", () => sky.GetFloat("_GroundFade"), v => sky.SetFloat("_GroundFade",v), 0.01f, 1, 0.02f, "F2"));
                if (sky.HasFloat("_CloudOpacity")) s.Add(S("Cloud Alpha", "Cloud visibility (0-2)", () => sky.GetFloat("_CloudOpacity"), v => sky.SetFloat("_CloudOpacity",v), 0, 2, 0.05f, "F2"));
                if (sky.HasColor("_CloudTint")) {
                    s.Add(S("Cloud R", "Cloud color R", () => sky.GetColor("_CloudTint").r, v => { var c=sky.GetColor("_CloudTint"); c.r=v; sky.SetColor("_CloudTint",c); }, 0, 2, 0.02f, "F2"));
                    s.Add(S("Cloud G", "Cloud color G", () => sky.GetColor("_CloudTint").g, v => { var c=sky.GetColor("_CloudTint"); c.g=v; sky.SetColor("_CloudTint",c); }, 0, 2, 0.02f, "F2"));
                    s.Add(S("Cloud B", "Cloud color B", () => sky.GetColor("_CloudTint").b, v => { var c=sky.GetColor("_CloudTint"); c.b=v; sky.SetColor("_CloudTint",c); }, 0, 2, 0.02f, "F2"));
                }
                // SkyRotator script speed
                if (skyRot != null)
                    s.Add(S("Rot Speed", "Predkosc auto-rotacji nieba (skrypt)", () => skyRot.rotationSpeed, v => skyRot.rotationSpeed=v, 0, 5, 0.1f));
            });

            // =============================================================
            // TERRAIN
            // =============================================================
            if (ter != null) Sec("TERRAIN", s => {
                s.Add(S("Detail Dist", "Detail range (grass)", () => ter.detailObjectDistance, v => ter.detailObjectDistance=v, 0, 500, 10, "F0"));
                s.Add(S("Tree Dist", "Tree mesh range", () => ter.treeDistance, v => ter.treeDistance=v, 0, 5000, 100, "F0"));
                s.Add(S("Billboard", "Tree billboard range", () => ter.treeBillboardDistance, v => ter.treeBillboardDistance=v, 0, 5000, 100, "F0"));
                s.Add(S("Max Trees", "Max full LOD trees", () => ter.treeMaximumFullLODCount, v => ter.treeMaximumFullLODCount=(int)v, 0, 500, 10, "F0"));
                s.Add(S("Pixel Err", "Heightmap error (higher=faster)", () => ter.heightmapPixelError, v => ter.heightmapPixelError=v, 1, 200, 5, "F0"));
                s.Add(S("Basemap", "Full texture range", () => ter.basemapDistance, v => ter.basemapDistance=v, 0, 2000, 50, "F0"));
                s.Add(S("Instanced", "GPU instancing (1=on)", () => ter.drawInstanced?1:0, v => ter.drawInstanced=v>0.5f, 0, 1, 1, "F0"));
                if (tMat != null) {
                    if (tMat.HasFloat("_NormalScale")) s.Add(S("Normal", "Normal map strength", () => tMat.GetFloat("_NormalScale"), v => tMat.SetFloat("_NormalScale",v), 0, 3, 0.1f));
                    if (tMat.HasFloat("_Smoothness")) s.Add(S("Smooth", "Smoothness (0=matte, 1=wet)", () => tMat.GetFloat("_Smoothness"), v => tMat.SetFloat("_Smoothness",v), 0, 1, 0.05f, "F2"));
                    if (tMat.HasFloat("_Metallic")) s.Add(S("Metal", "Metallic", () => tMat.GetFloat("_Metallic"), v => tMat.SetFloat("_Metallic",v), 0, 1, 0.05f, "F2"));
                }
            });

            // =============================================================
            // BLOOM
            // =============================================================
            if (blm != null) Sec("BLOOM", s => {
                s.Add(S("Intensity", "Bloom glow strength", () => blm.intensity.value, v => blm.intensity.Override(v), 0, 5, 0.1f));
                s.Add(S("Threshold", "Bloom brightness threshold", () => blm.threshold.value, v => blm.threshold.Override(v), 0, 3, 0.1f));
                s.Add(S("Scatter", "Spread (0=sharp)", () => blm.scatter.value, v => blm.scatter.Override(v), 0, 1, 0.05f, "F2"));
            });

            // =============================================================
            // COLOR
            // =============================================================
            if (ca != null) Sec("COLOR", s => {
                s.Add(S("Exposure", "Post-exposure EV", () => ca.postExposure.value, v => ca.postExposure.Override(v), -3, 3, 0.1f));
                s.Add(S("Contrast", "Contrast", () => ca.contrast.value, v => ca.contrast.Override(v), -100, 100, 5, "F0"));
                s.Add(S("Saturation", "Saturation (-100=B&W)", () => ca.saturation.value, v => ca.saturation.Override(v), -100, 100, 5, "F0"));
                s.Add(S("Hue Shift", "Hue rotation (-180..180)", () => ca.hueShift.value, v => ca.hueShift.Override(v), -180, 180, 5, "F0"));
                s.Add(S("Filter R", "Color filter R", () => ca.colorFilter.value.r, v => { var c=ca.colorFilter.value; c.r=v; ca.colorFilter.Override(c); }, 0, 1, 0.02f, "F2"));
                s.Add(S("Filter G", "Color filter G", () => ca.colorFilter.value.g, v => { var c=ca.colorFilter.value; c.g=v; ca.colorFilter.Override(c); }, 0, 1, 0.02f, "F2"));
                s.Add(S("Filter B", "Color filter B", () => ca.colorFilter.value.b, v => { var c=ca.colorFilter.value; c.b=v; ca.colorFilter.Override(c); }, 0, 1, 0.02f, "F2"));
            });

            // =============================================================
            // COMFORT
            // =============================================================
            if (vig != null || wb != null) Sec("COMFORT", s => {
                if (vig != null) {
                    s.Add(S("Vignette", "Edge darkening", () => vig.intensity.value, v => vig.intensity.Override(v), 0, 1, 0.05f, "F2"));
                    s.Add(S("Vig Smooth", "Vignette softness", () => vig.smoothness.value, v => vig.smoothness.Override(v), 0, 1, 0.05f, "F2"));
                }
                if (wb != null) {
                    s.Add(S("Temp", "Color temperature", () => wb.temperature.value, v => wb.temperature.Override(v), -100, 100, 5, "F0"));
                    s.Add(S("Tint", "Magenta/green tint", () => wb.tint.value, v => wb.tint.Override(v), -100, 100, 5, "F0"));
                }
            });

            // =============================================================
            // LGG
            // =============================================================
            if (lgg != null) Sec("LGG", s => {
                s.Add(S("Lift R", "Shadows R", () => lgg.lift.value.x, v => { var x=lgg.lift.value; x.x=v; lgg.lift.Override(x); }, -1, 1, 0.02f, "F2"));
                s.Add(S("Lift G", "Shadows G", () => lgg.lift.value.y, v => { var x=lgg.lift.value; x.y=v; lgg.lift.Override(x); }, -1, 1, 0.02f, "F2"));
                s.Add(S("Lift B", "Shadows B", () => lgg.lift.value.z, v => { var x=lgg.lift.value; x.z=v; lgg.lift.Override(x); }, -1, 1, 0.02f, "F2"));
                s.Add(S("Lift W", "Shadows intensity", () => lgg.lift.value.w, v => { var x=lgg.lift.value; x.w=v; lgg.lift.Override(x); }, -1, 1, 0.02f, "F2"));
                s.Add(S("Gamma R", "Midtones R", () => lgg.gamma.value.x, v => { var x=lgg.gamma.value; x.x=v; lgg.gamma.Override(x); }, -1, 1, 0.02f, "F2"));
                s.Add(S("Gamma G", "Midtones G", () => lgg.gamma.value.y, v => { var x=lgg.gamma.value; x.y=v; lgg.gamma.Override(x); }, -1, 1, 0.02f, "F2"));
                s.Add(S("Gamma B", "Midtones B", () => lgg.gamma.value.z, v => { var x=lgg.gamma.value; x.z=v; lgg.gamma.Override(x); }, -1, 1, 0.02f, "F2"));
                s.Add(S("Gamma W", "Midtones intensity", () => lgg.gamma.value.w, v => { var x=lgg.gamma.value; x.w=v; lgg.gamma.Override(x); }, -1, 1, 0.02f, "F2"));
                s.Add(S("Gain R", "Highlights R", () => lgg.gain.value.x, v => { var x=lgg.gain.value; x.x=v; lgg.gain.Override(x); }, -1, 1, 0.02f, "F2"));
                s.Add(S("Gain G", "Highlights G", () => lgg.gain.value.y, v => { var x=lgg.gain.value; x.y=v; lgg.gain.Override(x); }, -1, 1, 0.02f, "F2"));
                s.Add(S("Gain B", "Highlights B", () => lgg.gain.value.z, v => { var x=lgg.gain.value; x.z=v; lgg.gain.Override(x); }, -1, 1, 0.02f, "F2"));
                s.Add(S("Gain W", "Highlights intensity", () => lgg.gain.value.w, v => { var x=lgg.gain.value; x.w=v; lgg.gain.Override(x); }, -1, 1, 0.02f, "F2"));
            });

            // =============================================================
            // PRESETS (save/load as "settings")
            // =============================================================
            Sec("PRESETS", s => {
                // Slot 1-3: value 0=none, 1=save, 2=load
                for (int slot = 1; slot <= 3; slot++)
                {
                    int sl = slot; // capture
                    string slotName = slot == 1 ? "HI-END" : slot == 2 ? "CUSTOM" : "SAFE";
                    s.Add(S($"SAVE {sl}:{slotName}", $"Save all settings to slot {sl}", () => 0, v => { if(v>0.5f) SavePreset(sl); }, 0, 1, 1, "F0"));
                    s.Add(S($"LOAD {sl}:{slotName}", $"Load settings from slot {sl}", () => 0, v => { if(v>0.5f) LoadPreset(sl); }, 0, 1, 1, "F0"));
                }
                s.Add(S("LOG ALL", "Print all settings to console", () => 0, v => { if(v>0.5f) LogAll(); }, 0, 1, 1, "F0"));
            });

            // --- build index + flat list ---
            _names = new string[_sec.Count];
            _sec.Keys.CopyTo(_names, 0);
            _allSettings.Clear();
            foreach (var kv in _sec)
                foreach (var setting in kv.Value)
                    if (setting.step > 0) // skip read-only and actions
                        _allSettings.Add(setting);

            _built = true;
            Debug.Log($"[PLAGA44][Settings] Built: {_sec.Count} sections, {_allSettings.Count} saveable settings");
        }

        static Light FindSun()
        {
            foreach (var l in UnityEngine.Object.FindObjectsByType<Light>(FindObjectsSortMode.None))
                if (l.type == LightType.Directional) return l;
            return null;
        }
    }
}
