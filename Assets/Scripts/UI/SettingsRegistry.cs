// =============================================================================
// SettingsRegistry.cs -- KOMPLETNY runtime settings per sekcja.
// Kazdy setting ma opis. Save/Load presetow do PlayerPrefs.
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
        private static List<SettingDef> _allSettings; // flat lista do save/load

        public static List<SettingDef> GetSettings(string s) { if (!_built) Build(); return _sec.TryGetValue(s, out var l) ? l : new List<SettingDef>(); }
        public static string[] GetSectionNames() { if (!_built) Build(); return _names; }
        public static void Rebuild() { _built = false; _sec = null; }

        static SettingDef S(string n, string d, Func<float> g, Action<float> s, float mn, float mx, float st, string f="F1")
            => new SettingDef(n,d,g,s,mn,mx,st,f);

        static void Sec(string name, Action<List<SettingDef>> b)
        { var l = new List<SettingDef>(); b(l); if (l.Count > 0) _sec[name] = l; }

        // =====================================================================
        // Save / Load presets do PlayerPrefs
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

            // Skrypty na scenie
            var loco = UnityEngine.Object.FindAnyObjectByType<Plaga44.Locomotion.LocomotionController>();
            var cc = loco != null ? loco.GetComponent<CharacterController>() : null;

            // =============================================================
            // LOCOMOTION
            // =============================================================
            if (loco != null) Sec("LOCOMOTION", s => {
                s.Add(S("Move Speed", "Predkosc chodzenia m/s", () => loco.moveSpeed, v => loco.moveSpeed=v, 0.5f, 10, 0.5f));
                s.Add(S("Strafe", "Mnoznik predkosci strafe (0.8=80%)", () => loco.strafeFactor, v => loco.strafeFactor=v, 0.1f, 1, 0.05f, "F2"));
                s.Add(S("Speed (RO)", "Aktualna znorm. predkosc (0-1)", () => loco.NormalisedSpeed, v => {}, 0, 1, 0, "F2"));
                s.Add(S("VVel (RO)", "Predkosc pionowa (spadanie/skok)", () => loco.VerticalVelocity, v => {}, -100, 100, 0, "F1"));
                s.Add(S("Grounded", "Czy gracz stoi na ziemi (RO)", () => loco.IsGrounded?1:0, v => {}, 0, 1, 0, "F0"));
            });

            // =============================================================
            // CHARACTER CTRL
            // =============================================================
            if (cc != null) Sec("CHAR CTRL", s => {
                s.Add(S("Height", "Wysokosc CharacterController", () => cc.height, v => cc.height=v, 0.5f, 3, 0.1f));
                s.Add(S("Radius", "Promien kolizji gracza", () => cc.radius, v => cc.radius=v, 0.1f, 1, 0.05f, "F2"));
                s.Add(S("Skin Width", "Tolerancja penetracji kolizji", () => cc.skinWidth, v => cc.skinWidth=v, 0.01f, 0.2f, 0.01f, "F2"));
                s.Add(S("Step Offset", "Max wysokosc schodka", () => cc.stepOffset, v => cc.stepOffset=v, 0, 1, 0.05f, "F2"));
                s.Add(S("Slope Limit", "Max kat pochylni (stopnie)", () => cc.slopeLimit, v => cc.slopeLimit=v, 0, 90, 5, "F0"));
                s.Add(S("Center Y", "Przesuniecie Y srodka kolizji", () => cc.center.y, v => cc.center=new Vector3(cc.center.x,v,cc.center.z), 0, 2, 0.05f, "F2"));
            });

            // =============================================================
            // GAME STATE
            // =============================================================
            Sec("GAME STATE", s => {
                s.Add(S("Phase", "Faza gry (0=Splash 1=Menu 2=Load 3=Play 4=Inv 5=Pause 6=Dead)", () => (float)GameState.Current, v => GameState.SetState((GamePhase)(int)v), 0, 6, 1, "F0"));
            });

            // =============================================================
            // MISC
            // =============================================================
            Sec("MISC", s => {
                s.Add(S("Target FPS", "Limit klatek (-1=brak)", () => Application.targetFrameRate, v => Application.targetFrameRate=(int)v, -1, 120, 1, "F0"));
                s.Add(S("Time Scale", "Predkosc czasu (0=pauza, 1=norm)", () => Time.timeScale, v => Time.timeScale=v, 0, 3, 0.1f));
                s.Add(S("Fixed Step", "Krok fizyki w sek", () => Time.fixedDeltaTime, v => Time.fixedDeltaTime=v, 0.005f, 0.05f, 0.005f, "F3"));
                s.Add(S("Max Delta", "Zapobiega teleportacji po lag spike", () => Time.maximumDeltaTime, v => Time.maximumDeltaTime=v, 0.01f, 0.5f, 0.01f, "F2"));
                s.Add(S("Shader LOD", "Max LOD shaderow (nizej=prostsze)", () => Shader.globalMaximumLOD, v => Shader.globalMaximumLOD=(int)v, 100, 600, 100, "F0"));
                s.Add(S("Post FX", "On/off post-processingu", () => (vol!=null&&vol.enabled)?1:0, v => { if(vol) vol.enabled=v>0.5f; }, 0, 1, 1, "F0"));
            });

            // =============================================================
            // AUDIO
            // =============================================================
            Sec("AUDIO", s => {
                s.Add(S("Volume", "Glosnosc globalna", () => AudioListener.volume, v => AudioListener.volume=v, 0, 1, 0.05f, "F2"));
                s.Add(S("DSP Buffer", "Bufor audio (wyzej=stabilniej, latency)", () => AudioSettings.GetConfiguration().dspBufferSize, v => { var c=AudioSettings.GetConfiguration(); c.dspBufferSize=(int)v; AudioSettings.Reset(c); }, 256, 4096, 256, "F0"));
            });

            // =============================================================
            // PHYSICS
            // =============================================================
            Sec("PHYSICS", s => {
                s.Add(S("Gravity X", "Grawitacja boczna", () => Physics.gravity.x, v => { var g=Physics.gravity; g.x=v; Physics.gravity=g; }, -20, 20, 0.5f, "F1"));
                s.Add(S("Gravity Y", "Grawitacja pionowa (-9.81=Ziemia)", () => Physics.gravity.y, v => { var g=Physics.gravity; g.y=v; Physics.gravity=g; }, -20, 20, 0.5f, "F1"));
                s.Add(S("Gravity Z", "Grawitacja wzdluzna", () => Physics.gravity.z, v => { var g=Physics.gravity; g.z=v; Physics.gravity=g; }, -20, 20, 0.5f, "F1"));
                s.Add(S("Solver Iter", "Iteracje solvera kolizji", () => Physics.defaultSolverIterations, v => Physics.defaultSolverIterations=(int)v, 1, 25, 1, "F0"));
                s.Add(S("Contact Off", "Min odleglosc kontaktu", () => Physics.defaultContactOffset, v => Physics.defaultContactOffset=v, 0.001f, 0.1f, 0.005f, "F3"));
                s.Add(S("Sleep Thr", "Prog uspienia rigidbody", () => Physics.sleepThreshold, v => Physics.sleepThreshold=v, 0, 0.5f, 0.01f, "F2"));
                s.Add(S("Bounce Thr", "Min predkosc odbicia", () => Physics.bounceThreshold, v => Physics.bounceThreshold=v, 0, 5, 0.1f));
            });

            // =============================================================
            // SHADOWS
            // =============================================================
            Sec("SHADOWS", s => {
                if (urp != null) {
                    s.Add(S("Distance", "Zasieg cieni (m)", () => urp.shadowDistance, v => urp.shadowDistance=v, 0, 150, 5, "F0"));
                    s.Add(S("Resolution", "Shadow map px", () => urp.mainLightShadowmapResolution, v => urp.mainLightShadowmapResolution=(int)v, 256, 4096, 256, "F0"));
                    s.Add(S("Depth Bias", "Zapobiega shadow acne", () => urp.shadowDepthBias, v => urp.shadowDepthBias=v, 0, 10, 0.5f));
                    s.Add(S("Normal Bias", "Zapobiega peter-panning", () => urp.shadowNormalBias, v => urp.shadowNormalBias=v, 0, 10, 0.5f));
                }
                if (sun != null)
                    s.Add(S("Strength", "Intensywnosc cienia (0-1)", () => sun.shadowStrength, v => sun.shadowStrength=v, 0, 1, 0.01f, "F2"));
            });

            // =============================================================
            // SUN
            // =============================================================
            if (sun != null) Sec("SUN", s => {
                s.Add(S("Intensity", "Jasnosc slonca", () => sun.intensity, v => sun.intensity=v, 0, 5, 0.1f));
                s.Add(S("R", "Kolor slonca czerwony", () => sun.color.r, v => { var c=sun.color; c.r=v; sun.color=c; }, 0, 1, 0.02f, "F2"));
                s.Add(S("G", "Kolor slonca zielony", () => sun.color.g, v => { var c=sun.color; c.g=v; sun.color=c; }, 0, 1, 0.02f, "F2"));
                s.Add(S("B", "Kolor slonca niebieski", () => sun.color.b, v => { var c=sun.color; c.b=v; sun.color=c; }, 0, 1, 0.02f, "F2"));
                s.Add(S("Indirect", "Bounce light mnoznik", () => sun.bounceIntensity, v => sun.bounceIntensity=v, 0, 5, 0.1f));
                s.Add(S("Rot X", "Kat slonca X (wysokosc)", () => sun.transform.eulerAngles.x, v => sun.transform.eulerAngles=new Vector3(v,sun.transform.eulerAngles.y,0), 0, 90, 1, "F0"));
                s.Add(S("Rot Y", "Kat slonca Y (azymut)", () => sun.transform.eulerAngles.y, v => sun.transform.eulerAngles=new Vector3(sun.transform.eulerAngles.x,v,0), 0, 360, 5, "F0"));
            });

            // =============================================================
            // FOG
            // =============================================================
            Sec("FOG", s => {
                s.Add(S("On/Off", "Wlacz/wylacz mgle", () => RenderSettings.fog?1:0, v => RenderSettings.fog=v>0.5f, 0, 1, 1, "F0"));
                s.Add(S("Density", "Gestosc (exponential)", () => RenderSettings.fogDensity, v => RenderSettings.fogDensity=v, 0, 0.1f, 0.002f, "F3"));
                s.Add(S("Start", "Dystans startu (linear)", () => RenderSettings.fogStartDistance, v => RenderSettings.fogStartDistance=v, 0, 200, 5, "F0"));
                s.Add(S("End", "Dystans pelnej mgly (linear)", () => RenderSettings.fogEndDistance, v => RenderSettings.fogEndDistance=v, 10, 500, 10, "F0"));
                s.Add(S("R", "Kolor mgly R", () => RenderSettings.fogColor.r, v => { var c=RenderSettings.fogColor; c.r=v; RenderSettings.fogColor=c; }, 0, 1, 0.02f, "F2"));
                s.Add(S("G", "Kolor mgly G", () => RenderSettings.fogColor.g, v => { var c=RenderSettings.fogColor; c.g=v; RenderSettings.fogColor=c; }, 0, 1, 0.02f, "F2"));
                s.Add(S("B", "Kolor mgly B", () => RenderSettings.fogColor.b, v => { var c=RenderSettings.fogColor; c.b=v; RenderSettings.fogColor=c; }, 0, 1, 0.02f, "F2"));
            });

            // =============================================================
            // AMBIENT
            // =============================================================
            Sec("AMBIENT", s => {
                s.Add(S("Intensity", "Jasnosc ambient", () => RenderSettings.ambientIntensity, v => RenderSettings.ambientIntensity=v, 0, 3, 0.1f));
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
                    s.Add(S("Render Scale", "Rozdzielczosc renderowania", () => urp.renderScale, v => urp.renderScale=v, 0.3f, 2, 0.1f));
                }
                s.Add(S("Eye Tex", "Skala tekstury oka VR", () => XRSettings.eyeTextureResolutionScale, v => XRSettings.eyeTextureResolutionScale=v, 0.3f, 2, 0.1f));
                s.Add(S("LOD Bias", "Dystans LOD (wyzej=wiecej detali)", () => QualitySettings.lodBias, v => QualitySettings.lodBias=v, 0.3f, 2, 0.1f));
                s.Add(S("Tex Mip", "Mipmap (0=full, 3=low)", () => QualitySettings.globalTextureMipmapLimit, v => QualitySettings.globalTextureMipmapLimit=(int)v, 0, 3, 1, "F0"));
                s.Add(S("Skin Wts", "Kosci na vertex (1-4)", () => (float)QualitySettings.skinWeights, v => QualitySettings.skinWeights=(SkinWeights)(int)v, 1, 4, 1, "F0"));
                s.Add(S("VSync", "Sync z monitorem", () => QualitySettings.vSyncCount, v => QualitySettings.vSyncCount=(int)v, 0, 2, 1, "F0"));
                s.Add(S("Aniso", "Anizotropowe filtrowanie", () => (float)QualitySettings.anisotropicFiltering, v => QualitySettings.anisotropicFiltering=(AnisotropicFiltering)(int)v, 0, 2, 1, "F0"));
            });

            // =============================================================
            // CAMERA
            // =============================================================
            Sec("CAMERA", s => {
                s.Add(S("Near Clip", "Min dystans renderowania", () => Camera.main!=null?Camera.main.nearClipPlane:0.01f, v => { if(Camera.main) Camera.main.nearClipPlane=v; }, 0.01f, 1, 0.01f, "F2"));
                s.Add(S("Far Clip", "Max dystans renderowania", () => Camera.main!=null?Camera.main.farClipPlane:1000, v => { if(Camera.main) Camera.main.farClipPlane=v; }, 50, 5000, 50, "F0"));
                s.Add(S("FOV", "Pole widzenia (stopnie)", () => Camera.main!=null?Camera.main.fieldOfView:60, v => { if(Camera.main) Camera.main.fieldOfView=v; }, 30, 120, 1, "F0"));
            });

            // =============================================================
            // OCULUS
            // =============================================================
            Sec("OCULUS", s => {
                s.Add(S("FFR Level", "Foveated rendering (0=off, 4=max)", () => (float)OVRManager.foveatedRenderingLevel, v => OVRManager.foveatedRenderingLevel=(OVRManager.FoveatedRenderingLevel)(int)v, 0, 4, 1, "F0"));
                s.Add(S("Refresh Hz", "Odswiezanie Quest", () => OVRManager.display!=null?OVRManager.display.displayFrequency:72, v => { if(OVRManager.display!=null) OVRManager.display.displayFrequency=v; }, 60, 120, 6, "F0"));
            });

            // =============================================================
            // SKYBOX (kompletny shader)
            // =============================================================
            if (sky != null) Sec("SKYBOX", s => {
                if (sky.HasColor("_Tint")) {
                    s.Add(S("Tint R", "Odcien nieba R", () => sky.GetColor("_Tint").r, v => { var c=sky.GetColor("_Tint"); c.r=v; sky.SetColor("_Tint",c); }, 0, 2, 0.02f, "F2"));
                    s.Add(S("Tint G", "Odcien nieba G", () => sky.GetColor("_Tint").g, v => { var c=sky.GetColor("_Tint"); c.g=v; sky.SetColor("_Tint",c); }, 0, 2, 0.02f, "F2"));
                    s.Add(S("Tint B", "Odcien nieba B", () => sky.GetColor("_Tint").b, v => { var c=sky.GetColor("_Tint"); c.b=v; sky.SetColor("_Tint",c); }, 0, 2, 0.02f, "F2"));
                }
                if (sky.HasFloat("_Exposure")) s.Add(S("Exposure", "Jasnosc nieba", () => sky.GetFloat("_Exposure"), v => sky.SetFloat("_Exposure",v), 0, 8, 0.1f));
                if (sky.HasFloat("_Rotation")) s.Add(S("Rotation", "Obrot skyboxa (deg)", () => sky.GetFloat("_Rotation"), v => sky.SetFloat("_Rotation",v), 0, 360, 5, "F0"));
                if (sky.HasFloat("_RotSpeed")) s.Add(S("Rot Speed", "Auto-obrot nieba (deg/s, 0=stop)", () => sky.GetFloat("_RotSpeed"), v => sky.SetFloat("_RotSpeed",v), 0, 30, 0.5f));
                if (sky.HasColor("_GroundColor")) {
                    s.Add(S("Ground R", "Kolor ziemi/horyzontu R", () => sky.GetColor("_GroundColor").r, v => { var c=sky.GetColor("_GroundColor"); c.r=v; sky.SetColor("_GroundColor",c); }, 0, 1, 0.02f, "F2"));
                    s.Add(S("Ground G", "Kolor ziemi/horyzontu G", () => sky.GetColor("_GroundColor").g, v => { var c=sky.GetColor("_GroundColor"); c.g=v; sky.SetColor("_GroundColor",c); }, 0, 1, 0.02f, "F2"));
                    s.Add(S("Ground B", "Kolor ziemi/horyzontu B", () => sky.GetColor("_GroundColor").b, v => { var c=sky.GetColor("_GroundColor"); c.b=v; sky.SetColor("_GroundColor",c); }, 0, 1, 0.02f, "F2"));
                }
                if (sky.HasFloat("_GroundBlend")) s.Add(S("Ground Blend", "Wysokosc horyzontu (-0.5..0.5)", () => sky.GetFloat("_GroundBlend"), v => sky.SetFloat("_GroundBlend",v), -0.5f, 0.5f, 0.01f, "F2"));
                if (sky.HasFloat("_GroundFade")) s.Add(S("Ground Fade", "Miekkosc przejscia niebo-ziemia", () => sky.GetFloat("_GroundFade"), v => sky.SetFloat("_GroundFade",v), 0.01f, 1, 0.02f, "F2"));
                if (sky.HasFloat("_CloudOpacity")) s.Add(S("Cloud Alpha", "Widocznosc chmur (0-2)", () => sky.GetFloat("_CloudOpacity"), v => sky.SetFloat("_CloudOpacity",v), 0, 2, 0.05f, "F2"));
                if (sky.HasColor("_CloudTint")) {
                    s.Add(S("Cloud R", "Kolor chmur R", () => sky.GetColor("_CloudTint").r, v => { var c=sky.GetColor("_CloudTint"); c.r=v; sky.SetColor("_CloudTint",c); }, 0, 2, 0.02f, "F2"));
                    s.Add(S("Cloud G", "Kolor chmur G", () => sky.GetColor("_CloudTint").g, v => { var c=sky.GetColor("_CloudTint"); c.g=v; sky.SetColor("_CloudTint",c); }, 0, 2, 0.02f, "F2"));
                    s.Add(S("Cloud B", "Kolor chmur B", () => sky.GetColor("_CloudTint").b, v => { var c=sky.GetColor("_CloudTint"); c.b=v; sky.SetColor("_CloudTint",c); }, 0, 2, 0.02f, "F2"));
                }
            });

            // =============================================================
            // TERRAIN
            // =============================================================
            if (ter != null) Sec("TERRAIN", s => {
                s.Add(S("Detail Dist", "Zasieg detali (trawa)", () => ter.detailObjectDistance, v => ter.detailObjectDistance=v, 0, 500, 10, "F0"));
                s.Add(S("Tree Dist", "Zasieg drzew (mesh)", () => ter.treeDistance, v => ter.treeDistance=v, 0, 5000, 100, "F0"));
                s.Add(S("Billboard", "Zasieg billboard drzew", () => ter.treeBillboardDistance, v => ter.treeBillboardDistance=v, 0, 5000, 100, "F0"));
                s.Add(S("Max Trees", "Max drzew mesh", () => ter.treeMaximumFullLODCount, v => ter.treeMaximumFullLODCount=(int)v, 0, 500, 10, "F0"));
                s.Add(S("Pixel Err", "Blad heightmapy (wyzej=szybciej)", () => ter.heightmapPixelError, v => ter.heightmapPixelError=v, 1, 200, 5, "F0"));
                s.Add(S("Basemap", "Zasieg pelnej tekstury", () => ter.basemapDistance, v => ter.basemapDistance=v, 0, 2000, 50, "F0"));
                s.Add(S("Instanced", "GPU instancing (1=on)", () => ter.drawInstanced?1:0, v => ter.drawInstanced=v>0.5f, 0, 1, 1, "F0"));
                if (tMat != null) {
                    if (tMat.HasFloat("_NormalScale")) s.Add(S("Normal", "Sila normal mapy", () => tMat.GetFloat("_NormalScale"), v => tMat.SetFloat("_NormalScale",v), 0, 3, 0.1f));
                    if (tMat.HasFloat("_Smoothness")) s.Add(S("Smooth", "Gladkosc (0=mat, 1=mokry)", () => tMat.GetFloat("_Smoothness"), v => tMat.SetFloat("_Smoothness",v), 0, 1, 0.05f, "F2"));
                    if (tMat.HasFloat("_Metallic")) s.Add(S("Metal", "Metalicznosc", () => tMat.GetFloat("_Metallic"), v => tMat.SetFloat("_Metallic",v), 0, 1, 0.05f, "F2"));
                }
            });

            // =============================================================
            // BLOOM
            // =============================================================
            if (blm != null) Sec("BLOOM", s => {
                s.Add(S("Intensity", "Sila bloom (poswiate)", () => blm.intensity.value, v => blm.intensity.Override(v), 0, 5, 0.1f));
                s.Add(S("Threshold", "Prog jasnosci bloom", () => blm.threshold.value, v => blm.threshold.Override(v), 0, 3, 0.1f));
                s.Add(S("Scatter", "Rozprzestrzenianie (0=ostry)", () => blm.scatter.value, v => blm.scatter.Override(v), 0, 1, 0.05f, "F2"));
            });

            // =============================================================
            // COLOR
            // =============================================================
            if (ca != null) Sec("COLOR", s => {
                s.Add(S("Exposure", "Jasnosc post-EV", () => ca.postExposure.value, v => ca.postExposure.Override(v), -3, 3, 0.1f));
                s.Add(S("Contrast", "Kontrast", () => ca.contrast.value, v => ca.contrast.Override(v), -100, 100, 5, "F0"));
                s.Add(S("Saturation", "Nasycenie (-100=B&W)", () => ca.saturation.value, v => ca.saturation.Override(v), -100, 100, 5, "F0"));
                s.Add(S("Hue Shift", "Obrot barw (-180..180)", () => ca.hueShift.value, v => ca.hueShift.Override(v), -180, 180, 5, "F0"));
                s.Add(S("Filter R", "Filtr koloru R", () => ca.colorFilter.value.r, v => { var c=ca.colorFilter.value; c.r=v; ca.colorFilter.Override(c); }, 0, 1, 0.02f, "F2"));
                s.Add(S("Filter G", "Filtr koloru G", () => ca.colorFilter.value.g, v => { var c=ca.colorFilter.value; c.g=v; ca.colorFilter.Override(c); }, 0, 1, 0.02f, "F2"));
                s.Add(S("Filter B", "Filtr koloru B", () => ca.colorFilter.value.b, v => { var c=ca.colorFilter.value; c.b=v; ca.colorFilter.Override(c); }, 0, 1, 0.02f, "F2"));
            });

            // =============================================================
            // COMFORT
            // =============================================================
            if (vig != null || wb != null) Sec("COMFORT", s => {
                if (vig != null) {
                    s.Add(S("Vignette", "Przyciemnienie krawedzi", () => vig.intensity.value, v => vig.intensity.Override(v), 0, 1, 0.05f, "F2"));
                    s.Add(S("Vig Smooth", "Miekkosc winiety", () => vig.smoothness.value, v => vig.smoothness.Override(v), 0, 1, 0.05f, "F2"));
                }
                if (wb != null) {
                    s.Add(S("Temp", "Temperatura barwowa", () => wb.temperature.value, v => wb.temperature.Override(v), -100, 100, 5, "F0"));
                    s.Add(S("Tint", "Odcien magenta/zielony", () => wb.tint.value, v => wb.tint.Override(v), -100, 100, 5, "F0"));
                }
            });

            // =============================================================
            // LGG
            // =============================================================
            if (lgg != null) Sec("LGG", s => {
                s.Add(S("Lift R", "Cienie R", () => lgg.lift.value.x, v => { var x=lgg.lift.value; x.x=v; lgg.lift.Override(x); }, -1, 1, 0.02f, "F2"));
                s.Add(S("Lift G", "Cienie G", () => lgg.lift.value.y, v => { var x=lgg.lift.value; x.y=v; lgg.lift.Override(x); }, -1, 1, 0.02f, "F2"));
                s.Add(S("Lift B", "Cienie B", () => lgg.lift.value.z, v => { var x=lgg.lift.value; x.z=v; lgg.lift.Override(x); }, -1, 1, 0.02f, "F2"));
                s.Add(S("Lift W", "Cienie intensywnosc", () => lgg.lift.value.w, v => { var x=lgg.lift.value; x.w=v; lgg.lift.Override(x); }, -1, 1, 0.02f, "F2"));
                s.Add(S("Gamma R", "Polcienie R", () => lgg.gamma.value.x, v => { var x=lgg.gamma.value; x.x=v; lgg.gamma.Override(x); }, -1, 1, 0.02f, "F2"));
                s.Add(S("Gamma G", "Polcienie G", () => lgg.gamma.value.y, v => { var x=lgg.gamma.value; x.y=v; lgg.gamma.Override(x); }, -1, 1, 0.02f, "F2"));
                s.Add(S("Gamma B", "Polcienie B", () => lgg.gamma.value.z, v => { var x=lgg.gamma.value; x.z=v; lgg.gamma.Override(x); }, -1, 1, 0.02f, "F2"));
                s.Add(S("Gamma W", "Polcienie intensywnosc", () => lgg.gamma.value.w, v => { var x=lgg.gamma.value; x.w=v; lgg.gamma.Override(x); }, -1, 1, 0.02f, "F2"));
                s.Add(S("Gain R", "Jasne R", () => lgg.gain.value.x, v => { var x=lgg.gain.value; x.x=v; lgg.gain.Override(x); }, -1, 1, 0.02f, "F2"));
                s.Add(S("Gain G", "Jasne G", () => lgg.gain.value.y, v => { var x=lgg.gain.value; x.y=v; lgg.gain.Override(x); }, -1, 1, 0.02f, "F2"));
                s.Add(S("Gain B", "Jasne B", () => lgg.gain.value.z, v => { var x=lgg.gain.value; x.z=v; lgg.gain.Override(x); }, -1, 1, 0.02f, "F2"));
                s.Add(S("Gain W", "Jasne intensywnosc", () => lgg.gain.value.w, v => { var x=lgg.gain.value; x.w=v; lgg.gain.Override(x); }, -1, 1, 0.02f, "F2"));
            });

            // =============================================================
            // PRESETS (save/load jako "ustawienia")
            // =============================================================
            Sec("PRESETS", s => {
                // Slot 1-3: wartosc 0=nic, 1=save, 2=load
                for (int slot = 1; slot <= 3; slot++)
                {
                    int sl = slot; // capture
                    string slotName = slot == 1 ? "HI-END" : slot == 2 ? "CUSTOM" : "SAFE";
                    s.Add(S($"SAVE {sl}:{slotName}", $"Zapisz wszystkie ustawienia do slotu {sl}", () => 0, v => { if(v>0.5f) SavePreset(sl); }, 0, 1, 1, "F0"));
                    s.Add(S($"LOAD {sl}:{slotName}", $"Wczytaj ustawienia ze slotu {sl}", () => 0, v => { if(v>0.5f) LoadPreset(sl); }, 0, 1, 1, "F0"));
                }
                s.Add(S("LOG ALL", "Wypisz wszystkie ustawienia do konsoli", () => 0, v => { if(v>0.5f) LogAll(); }, 0, 1, 1, "F0"));
            });

            // --- build index + flat list ---
            _names = new string[_sec.Count];
            _sec.Keys.CopyTo(_names, 0);
            _allSettings.Clear();
            foreach (var kv in _sec)
                foreach (var setting in kv.Value)
                    if (setting.step > 0) // skip read-only i akcje
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
