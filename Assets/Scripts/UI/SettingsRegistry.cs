// =============================================================================
// SettingsRegistry.cs -- runtime settings per sekcja (kafelek w menu).
// Kazdy setting ma opis (1 zdanie) wyswietlany w submenu.
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
        public static List<SettingDef> GetSettings(string s) { if (!_built) Build(); return _sec.TryGetValue(s, out var l) ? l : new List<SettingDef>(); }
        public static string[] GetSectionNames() { if (!_built) Build(); return _names; }
        public static void Rebuild() { _built = false; _sec = null; }

        static SettingDef S(string n, string d, Func<float> g, Action<float> s, float mn, float mx, float st, string f="F1")
            => new SettingDef(n,d,g,s,mn,mx,st,f);

        static void Sec(string name, Action<List<SettingDef>> b)
        { var l = new List<SettingDef>(); b(l); if (l.Count > 0) _sec[name] = l; }

        static void Build()
        {
            _sec = new Dictionary<string, List<SettingDef>>();
            var urp = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
            var vol = UnityEngine.Object.FindAnyObjectByType<Volume>();
            var ter = UnityEngine.Object.FindAnyObjectByType<Terrain>();
            var sky = RenderSettings.skybox;
            var sun = FindSun();
            var tMat = ter != null ? ter.materialTemplate : null;
            ColorAdjustments ca = null; Vignette vig = null; WhiteBalance wb = null; LiftGammaGain lgg = null; Bloom blm = null;
            if (vol != null && vol.profile != null) { vol.profile.TryGet(out ca); vol.profile.TryGet(out vig); vol.profile.TryGet(out wb); vol.profile.TryGet(out lgg); vol.profile.TryGet(out blm); }

            // --- MISC ---
            Sec("MISC", s => {
                s.Add(S("Target FPS", "Limit klatek (-1=brak)", () => Application.targetFrameRate, v => Application.targetFrameRate=(int)v, -1, 120, 1, "F0"));
                s.Add(S("Time Scale", "Predkosc czasu (0=pauza, 1=norm, 2=szybko)", () => Time.timeScale, v => Time.timeScale=v, 0, 3, 0.1f));
                s.Add(S("Fixed Step", "Krok fizyki -- nizej = dokladniej ale ciezej", () => Time.fixedDeltaTime, v => Time.fixedDeltaTime=v, 0.005f, 0.05f, 0.005f, "F3"));
                s.Add(S("Max Delta", "Zapobiega teleportacji po lag spike", () => Time.maximumDeltaTime, v => Time.maximumDeltaTime=v, 0.01f, 0.5f, 0.01f, "F2"));
                s.Add(S("Shader LOD", "Globalny max LOD shaderow (nizej=prostsze)", () => Shader.globalMaximumLOD, v => Shader.globalMaximumLOD=(int)v, 100, 600, 100, "F0"));
                s.Add(S("Post FX", "Master on/off post-processingu", () => (vol!=null&&vol.enabled)?1:0, v => { if(vol) vol.enabled=v>0.5f; }, 0, 1, 1, "F0"));
            });

            // --- AUDIO ---
            Sec("AUDIO", s => {
                s.Add(S("Volume", "Glosnosc globalna", () => AudioListener.volume, v => AudioListener.volume=v, 0, 1, 0.05f, "F2"));
                s.Add(S("DSP Buffer", "Bufor audio -- wyzej=stabilniej, wiecej latency", () => AudioSettings.GetConfiguration().dspBufferSize, v => { var c=AudioSettings.GetConfiguration(); c.dspBufferSize=(int)v; AudioSettings.Reset(c); }, 256, 4096, 256, "F0"));
            });

            // --- PHYSICS ---
            Sec("PHYSICS", s => {
                s.Add(S("Gravity X", "Grawitacja boczna (0=brak)", () => Physics.gravity.x, v => { var g=Physics.gravity; g.x=v; Physics.gravity=g; }, -20, 20, 0.5f, "F1"));
                s.Add(S("Gravity Y", "Grawitacja pionowa (-9.81=Ziemia, 0=brak)", () => Physics.gravity.y, v => { var g=Physics.gravity; g.y=v; Physics.gravity=g; }, -20, 0, 0.5f, "F1"));
                s.Add(S("Gravity Z", "Grawitacja wzdluzna (0=brak)", () => Physics.gravity.z, v => { var g=Physics.gravity; g.z=v; Physics.gravity=g; }, -20, 20, 0.5f, "F1"));
                s.Add(S("Solver Iter", "Iteracje solvera -- wiecej=dokladniejsze kolizje", () => Physics.defaultSolverIterations, v => Physics.defaultSolverIterations=(int)v, 1, 25, 1, "F0"));
                s.Add(S("Contact Off", "Min odleglosc kontaktu kolizji", () => Physics.defaultContactOffset, v => Physics.defaultContactOffset=v, 0.001f, 0.1f, 0.005f, "F3"));
                s.Add(S("Sleep Thr", "Prog uspienia rigidbody (nizej=czulsze)", () => Physics.sleepThreshold, v => Physics.sleepThreshold=v, 0, 0.5f, 0.01f, "F2"));
                s.Add(S("Bounce Thr", "Min predkosc odbicia (nizej=wiecej odskokow)", () => Physics.bounceThreshold, v => Physics.bounceThreshold=v, 0, 5, 0.1f));
            });

            // --- SHADOWS ---
            Sec("SHADOWS", s => {
                if (urp != null) {
                    s.Add(S("Distance", "Zasieg cieni w metrach", () => urp.shadowDistance, v => urp.shadowDistance=v, 0, 150, 5, "F0"));
                    s.Add(S("Resolution", "Rozdzielczosc shadow mapy (wyzej=ostrzejsze)", () => urp.mainLightShadowmapResolution, v => urp.mainLightShadowmapResolution=(int)v, 256, 4096, 256, "F0"));
                    s.Add(S("Depth Bias", "Przesuniecie cienia -- zapobiega shadow acne", () => urp.shadowDepthBias, v => urp.shadowDepthBias=v, 0, 10, 0.5f));
                    s.Add(S("Normal Bias", "Przesuniecie wzdluz normali -- zapobiega peter-panning", () => urp.shadowNormalBias, v => urp.shadowNormalBias=v, 0, 10, 0.5f));
                }
                if (sun != null)
                    s.Add(S("Strength", "Intensywnosc cienia slonca (0=brak, 1=pelny)", () => sun.shadowStrength, v => sun.shadowStrength=v, 0, 1, 0.01f, "F2"));
            });

            // --- SUN ---
            if (sun != null) Sec("SUN", s => {
                s.Add(S("Intensity", "Jasnosc slonca", () => sun.intensity, v => sun.intensity=v, 0, 5, 0.1f));
                s.Add(S("Color R", "Czerwony kanal swiatla", () => sun.color.r, v => { var c=sun.color; c.r=v; sun.color=c; }, 0, 1, 0.02f, "F2"));
                s.Add(S("Color G", "Zielony kanal swiatla", () => sun.color.g, v => { var c=sun.color; c.g=v; sun.color=c; }, 0, 1, 0.02f, "F2"));
                s.Add(S("Color B", "Niebieski kanal swiatla", () => sun.color.b, v => { var c=sun.color; c.b=v; sun.color=c; }, 0, 1, 0.02f, "F2"));
                s.Add(S("Indirect", "Mnoznik odbitego swiatla (GI bounce)", () => sun.bounceIntensity, v => sun.bounceIntensity=v, 0, 5, 0.1f));
            });

            // --- FOG ---
            Sec("FOG", s => {
                s.Add(S("On/Off", "Wlacz/wylacz mgle", () => RenderSettings.fog?1:0, v => RenderSettings.fog=v>0.5f, 0, 1, 1, "F0"));
                s.Add(S("Density", "Gestosc mgly (exponential)", () => RenderSettings.fogDensity, v => RenderSettings.fogDensity=v, 0, 0.1f, 0.002f, "F3"));
                s.Add(S("Start", "Odleglosc startu mgly (linear)", () => RenderSettings.fogStartDistance, v => RenderSettings.fogStartDistance=v, 0, 200, 5, "F0"));
                s.Add(S("End", "Odleglosc pelnej mgly (linear)", () => RenderSettings.fogEndDistance, v => RenderSettings.fogEndDistance=v, 10, 500, 10, "F0"));
                s.Add(S("R", "Kolor mgly -- czerwony", () => RenderSettings.fogColor.r, v => { var c=RenderSettings.fogColor; c.r=v; RenderSettings.fogColor=c; }, 0, 1, 0.02f, "F2"));
                s.Add(S("G", "Kolor mgly -- zielony", () => RenderSettings.fogColor.g, v => { var c=RenderSettings.fogColor; c.g=v; RenderSettings.fogColor=c; }, 0, 1, 0.02f, "F2"));
                s.Add(S("B", "Kolor mgly -- niebieski", () => RenderSettings.fogColor.b, v => { var c=RenderSettings.fogColor; c.b=v; RenderSettings.fogColor=c; }, 0, 1, 0.02f, "F2"));
            });

            // --- AMBIENT ---
            Sec("AMBIENT", s => {
                s.Add(S("Intensity", "Jasnosc swiatla otoczenia", () => RenderSettings.ambientIntensity, v => RenderSettings.ambientIntensity=v, 0, 3, 0.1f));
                s.Add(S("R", "Ambient kolor czerwony", () => RenderSettings.ambientLight.r, v => { var c=RenderSettings.ambientLight; c.r=v; RenderSettings.ambientLight=c; }, 0, 1, 0.05f, "F2"));
                s.Add(S("G", "Ambient kolor zielony", () => RenderSettings.ambientLight.g, v => { var c=RenderSettings.ambientLight; c.g=v; RenderSettings.ambientLight=c; }, 0, 1, 0.05f, "F2"));
                s.Add(S("B", "Ambient kolor niebieski", () => RenderSettings.ambientLight.b, v => { var c=RenderSettings.ambientLight; c.b=v; RenderSettings.ambientLight=c; }, 0, 1, 0.05f, "F2"));
                s.Add(S("Reflection", "Intensywnosc reflection probes", () => RenderSettings.reflectionIntensity, v => RenderSettings.reflectionIntensity=v, 0, 2, 0.1f));
            });

            // --- QUALITY ---
            Sec("QUALITY", s => {
                if (urp != null) {
                    s.Add(S("MSAA", "Anti-aliasing (1=brak, 4=dobry, 8=ciezki)", () => urp.msaaSampleCount, v => urp.msaaSampleCount=(int)v, 1, 8, 1, "F0"));
                    s.Add(S("Render Scale", "Skala renderowania (nizej=szybciej, wyzej=ostrzej)", () => urp.renderScale, v => urp.renderScale=v, 0.3f, 2, 0.1f));
                }
                s.Add(S("Eye Tex Scale", "Skala tekstury oka VR", () => XRSettings.eyeTextureResolutionScale, v => XRSettings.eyeTextureResolutionScale=v, 0.3f, 2, 0.1f));
                s.Add(S("LOD Bias", "Mnoznik dystansu LOD (wyzej=wiecej detali)", () => QualitySettings.lodBias, v => QualitySettings.lodBias=v, 0.3f, 2, 0.1f));
                s.Add(S("Tex Mip", "Poziom mipmap tekstur (0=full, 3=najnizsza)", () => QualitySettings.globalTextureMipmapLimit, v => QualitySettings.globalTextureMipmapLimit=(int)v, 0, 3, 1, "F0"));
                s.Add(S("Skin Wts", "Ilosc koscii wplywajacych na vertex (1-4)", () => (float)QualitySettings.skinWeights, v => QualitySettings.skinWeights=(SkinWeights)(int)v, 1, 4, 1, "F0"));
                s.Add(S("VSync", "Synchronizacja z odswiezaniem (0=off, 1=60Hz)", () => QualitySettings.vSyncCount, v => QualitySettings.vSyncCount=(int)v, 0, 2, 1, "F0"));
                s.Add(S("Aniso", "Filtrowanie anizotropowe tekstur (0=off, 2=forced)", () => (float)QualitySettings.anisotropicFiltering, v => QualitySettings.anisotropicFiltering=(AnisotropicFiltering)(int)v, 0, 2, 1, "F0"));
            });

            // --- CAMERA ---
            Sec("CAMERA", s => {
                s.Add(S("Near Clip", "Min odleglosc renderowania (nizej=blizej rece)", () => Camera.main!=null?Camera.main.nearClipPlane:0.01f, v => { if(Camera.main) Camera.main.nearClipPlane=v; }, 0.01f, 1, 0.01f, "F2"));
                s.Add(S("Far Clip", "Max odleglosc renderowania", () => Camera.main!=null?Camera.main.farClipPlane:1000, v => { if(Camera.main) Camera.main.farClipPlane=v; }, 50, 5000, 50, "F0"));
                s.Add(S("FOV", "Pole widzenia kamery w stopniach", () => Camera.main!=null?Camera.main.fieldOfView:60, v => { if(Camera.main) Camera.main.fieldOfView=v; }, 30, 120, 1, "F0"));
            });

            // --- OCULUS ---
            Sec("OCULUS", s => {
                s.Add(S("FFR Level", "Foveated rendering (0=off, 4=max oszczednosc GPU)", () => (float)OVRManager.foveatedRenderingLevel, v => OVRManager.foveatedRenderingLevel=(OVRManager.FoveatedRenderingLevel)(int)v, 0, 4, 1, "F0"));
                s.Add(S("Refresh Hz", "Czestotliwosc odswiezania Quest", () => OVRManager.display!=null?OVRManager.display.displayFrequency:72, v => { if(OVRManager.display!=null) OVRManager.display.displayFrequency=v; }, 60, 120, 6, "F0"));
            });

            // --- SKYBOX ---
            if (sky != null) Sec("SKYBOX", s => {
                if (sky.HasColor("_Tint")) {
                    s.Add(S("Tint R", "Odcien nieba -- czerwony", () => sky.GetColor("_Tint").r, v => { var c=sky.GetColor("_Tint"); c.r=v; sky.SetColor("_Tint",c); }, 0, 2, 0.02f, "F2"));
                    s.Add(S("Tint G", "Odcien nieba -- zielony", () => sky.GetColor("_Tint").g, v => { var c=sky.GetColor("_Tint"); c.g=v; sky.SetColor("_Tint",c); }, 0, 2, 0.02f, "F2"));
                    s.Add(S("Tint B", "Odcien nieba -- niebieski", () => sky.GetColor("_Tint").b, v => { var c=sky.GetColor("_Tint"); c.b=v; sky.SetColor("_Tint",c); }, 0, 2, 0.02f, "F2"));
                }
                if (sky.HasFloat("_Exposure")) s.Add(S("Exposure", "Jasnosc nieba", () => sky.GetFloat("_Exposure"), v => sky.SetFloat("_Exposure",v), 0, 8, 0.1f));
                if (sky.HasFloat("_Rotation")) s.Add(S("Rotation", "Statyczny obrot skyboxa", () => sky.GetFloat("_Rotation"), v => sky.SetFloat("_Rotation",v), 0, 360, 5, "F0"));
                if (sky.HasFloat("_RotSpeed")) s.Add(S("Rot Speed", "Predkosc auto-obrotu nieba (0=stop)", () => sky.GetFloat("_RotSpeed"), v => sky.SetFloat("_RotSpeed",v), 0, 30, 0.5f));
                if (sky.HasColor("_GroundColor")) {
                    s.Add(S("Ground R", "Kolor horyzontu/ziemi -- czerwony", () => sky.GetColor("_GroundColor").r, v => { var c=sky.GetColor("_GroundColor"); c.r=v; sky.SetColor("_GroundColor",c); }, 0, 1, 0.02f, "F2"));
                    s.Add(S("Ground G", "Kolor horyzontu/ziemi -- zielony", () => sky.GetColor("_GroundColor").g, v => { var c=sky.GetColor("_GroundColor"); c.g=v; sky.SetColor("_GroundColor",c); }, 0, 1, 0.02f, "F2"));
                    s.Add(S("Ground B", "Kolor horyzontu/ziemi -- niebieski", () => sky.GetColor("_GroundColor").b, v => { var c=sky.GetColor("_GroundColor"); c.b=v; sky.SetColor("_GroundColor",c); }, 0, 1, 0.02f, "F2"));
                }
                if (sky.HasFloat("_GroundBlend")) s.Add(S("Ground Blend", "Wysokosc gradientu ziemi (-0.5 do 0.5)", () => sky.GetFloat("_GroundBlend"), v => sky.SetFloat("_GroundBlend",v), -0.5f, 0.5f, 0.01f, "F2"));
                if (sky.HasFloat("_GroundFade")) s.Add(S("Ground Fade", "Miekkosc przejscia niebo-ziemia", () => sky.GetFloat("_GroundFade"), v => sky.SetFloat("_GroundFade",v), 0.01f, 1, 0.02f, "F2"));
                if (sky.HasFloat("_CloudOpacity")) s.Add(S("Cloud Alpha", "Przezroczystosc chmur (0=brak, 2=mocne)", () => sky.GetFloat("_CloudOpacity"), v => sky.SetFloat("_CloudOpacity",v), 0, 2, 0.05f, "F2"));
                if (sky.HasColor("_CloudTint")) {
                    s.Add(S("Cloud R", "Kolor chmur -- czerwony", () => sky.GetColor("_CloudTint").r, v => { var c=sky.GetColor("_CloudTint"); c.r=v; sky.SetColor("_CloudTint",c); }, 0, 2, 0.02f, "F2"));
                    s.Add(S("Cloud G", "Kolor chmur -- zielony", () => sky.GetColor("_CloudTint").g, v => { var c=sky.GetColor("_CloudTint"); c.g=v; sky.SetColor("_CloudTint",c); }, 0, 2, 0.02f, "F2"));
                    s.Add(S("Cloud B", "Kolor chmur -- niebieski", () => sky.GetColor("_CloudTint").b, v => { var c=sky.GetColor("_CloudTint"); c.b=v; sky.SetColor("_CloudTint",c); }, 0, 2, 0.02f, "F2"));
                }
            });

            // --- TERRAIN ---
            if (ter != null) Sec("TERRAIN", s => {
                s.Add(S("Detail Dist", "Zasieg renderowania detali (trawa)", () => ter.detailObjectDistance, v => ter.detailObjectDistance=v, 0, 500, 10, "F0"));
                s.Add(S("Tree Dist", "Zasieg renderowania drzew (mesh)", () => ter.treeDistance, v => ter.treeDistance=v, 0, 5000, 100, "F0"));
                s.Add(S("Billboard", "Zasieg billboard drzew (dalej=taniej)", () => ter.treeBillboardDistance, v => ter.treeBillboardDistance=v, 0, 5000, 100, "F0"));
                s.Add(S("Max Trees", "Max drzew renderowanych jako mesh", () => ter.treeMaximumFullLODCount, v => ter.treeMaximumFullLODCount=(int)v, 0, 500, 10, "F0"));
                s.Add(S("Pixel Err", "Blad heightmapy (wyzej=mniej troj, szybciej)", () => ter.heightmapPixelError, v => ter.heightmapPixelError=v, 1, 200, 5, "F0"));
                s.Add(S("Basemap Dist", "Zasieg pelnej tekstury (dalej=ladniej)", () => ter.basemapDistance, v => ter.basemapDistance=v, 0, 2000, 50, "F0"));
                s.Add(S("Instanced", "GPU instancing terenu (1=szybciej)", () => ter.drawInstanced?1:0, v => ter.drawInstanced=v>0.5f, 0, 1, 1, "F0"));
                if (tMat != null && tMat.HasFloat("_NormalScale"))
                    s.Add(S("Normal Str", "Sila normal mapy terenu", () => tMat.GetFloat("_NormalScale"), v => tMat.SetFloat("_NormalScale",v), 0, 3, 0.1f));
                if (tMat != null && tMat.HasFloat("_Smoothness"))
                    s.Add(S("Smoothness", "Gladkosc terenu (0=matowy, 1=mokry)", () => tMat.GetFloat("_Smoothness"), v => tMat.SetFloat("_Smoothness",v), 0, 1, 0.05f, "F2"));
                if (tMat != null && tMat.HasFloat("_Metallic"))
                    s.Add(S("Metallic", "Metalicznosc terenu", () => tMat.GetFloat("_Metallic"), v => tMat.SetFloat("_Metallic",v), 0, 1, 0.05f, "F2"));
            });

            // --- BLOOM ---
            if (blm != null) Sec("BLOOM", s => {
                s.Add(S("Intensity", "Sila efektu bloom (poswiate)", () => blm.intensity.value, v => blm.intensity.Override(v), 0, 5, 0.1f));
                s.Add(S("Threshold", "Jasnosc od ktorej zaczyna sie bloom", () => blm.threshold.value, v => blm.threshold.Override(v), 0, 3, 0.1f));
                s.Add(S("Scatter", "Rozprzestrzenianie bloom (0=ostry, 1=miękki)", () => blm.scatter.value, v => blm.scatter.Override(v), 0, 1, 0.05f, "F2"));
            });

            // --- COLOR ---
            if (ca != null) Sec("COLOR", s => {
                s.Add(S("Exposure", "Jasnosc post-process (EV stops)", () => ca.postExposure.value, v => ca.postExposure.Override(v), -3, 3, 0.1f));
                s.Add(S("Contrast", "Roznica jasnych i ciemnych partii", () => ca.contrast.value, v => ca.contrast.Override(v), -100, 100, 5, "F0"));
                s.Add(S("Saturation", "Nasycenie kolorow (-100=B&W)", () => ca.saturation.value, v => ca.saturation.Override(v), -100, 100, 5, "F0"));
                s.Add(S("Hue Shift", "Obrot kola barw (-180..180)", () => ca.hueShift.value, v => ca.hueShift.Override(v), -180, 180, 5, "F0"));
                s.Add(S("Filter R", "Filtr koloru -- czerwony", () => ca.colorFilter.value.r, v => { var c=ca.colorFilter.value; c.r=v; ca.colorFilter.Override(c); }, 0, 1, 0.02f, "F2"));
                s.Add(S("Filter G", "Filtr koloru -- zielony", () => ca.colorFilter.value.g, v => { var c=ca.colorFilter.value; c.g=v; ca.colorFilter.Override(c); }, 0, 1, 0.02f, "F2"));
                s.Add(S("Filter B", "Filtr koloru -- niebieski", () => ca.colorFilter.value.b, v => { var c=ca.colorFilter.value; c.b=v; ca.colorFilter.Override(c); }, 0, 1, 0.02f, "F2"));
            });

            // --- COMFORT ---
            if (vig != null || wb != null) Sec("COMFORT", s => {
                if (vig != null) {
                    s.Add(S("Vignette", "Przyciemnienie krawedzi (anti motion sickness)", () => vig.intensity.value, v => vig.intensity.Override(v), 0, 1, 0.05f, "F2"));
                    s.Add(S("Vig Smooth", "Miekkosc winiety", () => vig.smoothness.value, v => vig.smoothness.Override(v), 0, 1, 0.05f, "F2"));
                }
                if (wb != null) {
                    s.Add(S("Temp", "Temperatura barwowa (- zimno, + cieplo)", () => wb.temperature.value, v => wb.temperature.Override(v), -100, 100, 5, "F0"));
                    s.Add(S("Tint", "Odcien magenta/zielony", () => wb.tint.value, v => wb.tint.Override(v), -100, 100, 5, "F0"));
                }
            });

            // --- LGG ---
            if (lgg != null) Sec("LGG", s => {
                s.Add(S("Lift R", "Cienie -- czerwony (color wheels)", () => lgg.lift.value.x, v => { var x=lgg.lift.value; x.x=v; lgg.lift.Override(x); }, -1, 1, 0.02f, "F2"));
                s.Add(S("Lift G", "Cienie -- zielony", () => lgg.lift.value.y, v => { var x=lgg.lift.value; x.y=v; lgg.lift.Override(x); }, -1, 1, 0.02f, "F2"));
                s.Add(S("Lift B", "Cienie -- niebieski", () => lgg.lift.value.z, v => { var x=lgg.lift.value; x.z=v; lgg.lift.Override(x); }, -1, 1, 0.02f, "F2"));
                s.Add(S("Lift W", "Cienie -- intensywnosc", () => lgg.lift.value.w, v => { var x=lgg.lift.value; x.w=v; lgg.lift.Override(x); }, -1, 1, 0.02f, "F2"));
                s.Add(S("Gamma R", "Polcienie -- czerwony", () => lgg.gamma.value.x, v => { var x=lgg.gamma.value; x.x=v; lgg.gamma.Override(x); }, -1, 1, 0.02f, "F2"));
                s.Add(S("Gamma G", "Polcienie -- zielony", () => lgg.gamma.value.y, v => { var x=lgg.gamma.value; x.y=v; lgg.gamma.Override(x); }, -1, 1, 0.02f, "F2"));
                s.Add(S("Gamma B", "Polcienie -- niebieski", () => lgg.gamma.value.z, v => { var x=lgg.gamma.value; x.z=v; lgg.gamma.Override(x); }, -1, 1, 0.02f, "F2"));
                s.Add(S("Gamma W", "Polcienie -- intensywnosc", () => lgg.gamma.value.w, v => { var x=lgg.gamma.value; x.w=v; lgg.gamma.Override(x); }, -1, 1, 0.02f, "F2"));
                s.Add(S("Gain R", "Jasne partie -- czerwony", () => lgg.gain.value.x, v => { var x=lgg.gain.value; x.x=v; lgg.gain.Override(x); }, -1, 1, 0.02f, "F2"));
                s.Add(S("Gain G", "Jasne partie -- zielony", () => lgg.gain.value.y, v => { var x=lgg.gain.value; x.y=v; lgg.gain.Override(x); }, -1, 1, 0.02f, "F2"));
                s.Add(S("Gain B", "Jasne partie -- niebieski", () => lgg.gain.value.z, v => { var x=lgg.gain.value; x.z=v; lgg.gain.Override(x); }, -1, 1, 0.02f, "F2"));
                s.Add(S("Gain W", "Jasne partie -- intensywnosc", () => lgg.gain.value.w, v => { var x=lgg.gain.value; x.w=v; lgg.gain.Override(x); }, -1, 1, 0.02f, "F2"));
            });

            // --- build index ---
            _names = new string[_sec.Count];
            _sec.Keys.CopyTo(_names, 0);
            _built = true;
            int total = 0; foreach (var kv in _sec) total += kv.Value.Count;
            Debug.Log($"[PLAGA44][Settings] Built: {_sec.Count} sections, {total} settings");
        }

        static Light FindSun()
        {
            foreach (var l in UnityEngine.Object.FindObjectsByType<Light>(FindObjectsSortMode.None))
                if (l.type == LightType.Directional) return l;
            return null;
        }
    }
}
