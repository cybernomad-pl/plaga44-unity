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
using UnityEngine.AI;

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
        private static float _activeProfile = 0;

        public static List<SettingDef> GetSettings(string s) { if (!_built) Build(); return _sec.TryGetValue(s, out var l) ? l : new List<SettingDef>(); }
        public static string[] GetSectionNames() { if (!_built) Build(); return _names; }
        public static void Rebuild() { _built = false; _sec = null; }

        // Sekcje ktorych aktualnych wartosci NIE trwalimy (akcje, stan gry, read-only)
        private static readonly HashSet<string> NON_PERSISTENT_SECTIONS =
            new HashSet<string> { "GAME STATE" };

        /// <summary>PlayerPrefs keys -- single source of truth, koniec z literal strings w kodzie.</summary>
        private static class PrefsKeys
        {
            private const string CurrentPrefix = "Plaga44_Current_";
            private const string DefaultPrefix = "Plaga44_Default_";

            public static string Current(string section, string name) => $"{CurrentPrefix}{section}_{name}";
            public static string Default(string section, string name) => $"{DefaultPrefix}{section}_{name}";
        }

        private static string DefaultsDictKey(string section, string name) => $"{section}_{name}";

        // Last action info -- HamburgerMenu pokazuje jako toast przez kilka sekund
        public static string LastActionMessage { get; private set; } = "";
        public static float LastActionTime { get; private set; } = -999f;
        public static bool LastActionSuccess { get; private set; } = true;

        /// <summary>Event invokowany po kazdej akcji (save/load/reset/error).
        /// MenuNotifier subskrybuje, pokazuje banner w canvas menu.</summary>
        public static event System.Action<string, bool> OnAction;

        static void SetAction(string msg, bool success = true)
        {
            LastActionMessage = msg;
            LastActionTime = Time.unscaledTime;
            LastActionSuccess = success;
            Debug.Log($"[PLAGA44][Settings] {msg}");
            OnAction?.Invoke(msg, success);
        }

        // Current section name (set by Sec, captured by S for log context).
        private static string _currentSection = "?";

        // Flag ze zapis do PlayerPrefs jest pending (faktyczny flush w HamburgerMenu.Close / OnApplicationQuit)
        public static bool PendingSave { get; private set; }

        /// <summary>Flush PlayerPrefs do dysku. Wywolywane przy zamknieciu menu / aplikacji.</summary>
        public static void FlushPlayerPrefs()
        {
            if (!PendingSave) return;
            PlayerPrefs.Save();
            PendingSave = false;
        }

        static SettingDef S(string n, string d, Func<float> g, Action<float> s, float mn, float mx, float st, string f="F1")
        {
            string sectionCapture = _currentSection;
            bool persistent = st > 0 && !NON_PERSISTENT_SECTIONS.Contains(sectionCapture);
            string prefKey = persistent ? PrefsKeys.Current(sectionCapture, n) : null;

            Action<float> wrappedSetter = (v) =>
            {
                float oldVal = g();
                s(v);
                if (!Mathf.Approximately(oldVal, v))
                {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                    SettingsLogger.Log(n, oldVal, v, sectionCapture);
#endif
                    if (prefKey != null)
                    {
                        PlayerPrefs.SetFloat(prefKey, v);
                        // Auto-update default so next session starts with this value
                        string defKey = PrefsKeys.Default(sectionCapture, n);
                        PlayerPrefs.SetFloat(defKey, v);
                        PendingSave = true;
                    }
                }
            };
            return new SettingDef(n, d, g, wrappedSetter, mn, mx, st, f);
        }

        static void Sec(string name, Action<List<SettingDef>> b)
        {
            _currentSection = name;
            try
            {
                var l = new List<SettingDef>();
                b(l);
                if (l.Count > 0) _sec[name] = l;
            }
            finally { _currentSection = "?"; }
        }

        // =====================================================================
        // Defaults snapshot -- captured on first Build()
        // =====================================================================

        private static Dictionary<string, float> _defaults;

        private static void CaptureDefaults()
        {
            _defaults = new Dictionary<string, float>();
            foreach (var kv in _sec)
            {
                if (NON_PERSISTENT_SECTIONS.Contains(kv.Key)) continue;
                foreach (var s in kv.Value)
                {
                    if (s.step <= 0) continue; // skip read-only / actions
                    string dictKey = DefaultsDictKey(kv.Key, s.name);
                    // Prefer persisted defaults (from SaveCurrentAsDefaults) over live scene value
                    string prefKey = PrefsKeys.Default(kv.Key, s.name);
                    _defaults[dictKey] = PlayerPrefs.HasKey(prefKey)
                        ? PlayerPrefs.GetFloat(prefKey)
                        : s.get();
                }
            }
            Debug.Log($"[PLAGA44][Settings] Captured {_defaults.Count} default values");
        }

        public static void ResetToDefaults()
        {
            if (_defaults == null) { SetAction("RESET FAILED -- no defaults captured", false); return; }
            int count = 0;
            foreach (var kv in _sec)
            {
                if (NON_PERSISTENT_SECTIONS.Contains(kv.Key)) continue;
                foreach (var s in kv.Value)
                {
                    if (s.step <= 0) continue;
                    string dictKey = DefaultsDictKey(kv.Key, s.name);
                    if (_defaults.TryGetValue(dictKey, out float val))
                    {
                        s.set(Mathf.Clamp(val, s.min, s.max));
                        count++;
                    }
                }
            }
            SetAction($"RESET {count} values to defaults", true);
        }

        /// <summary>Zapisuje biezace wartosci jako nowe domyslne.
        /// Po tej operacji RESET ALL przywroci do stanu z tego momentu, nie ze startu aplikacji.</summary>
        public static void SaveCurrentAsDefaults()
        {
            if (!_built) Build();
            int count = 0;
            foreach (var kv in _sec)
            {
                if (NON_PERSISTENT_SECTIONS.Contains(kv.Key)) continue;
                foreach (var s in kv.Value)
                {
                    if (s.step <= 0) continue;
                    float val = s.get();
                    PlayerPrefs.SetFloat(PrefsKeys.Default(kv.Key, s.name), val);
                    _defaults[DefaultsDictKey(kv.Key, s.name)] = val;
                    count++;
                }
            }
            PlayerPrefs.Save();
            PendingSave = false;
            SetAction($"DEFAULTS SET ({count} values saved)", true);
        }

        // Presets removed -- auto-save on every change replaces manual preset slots.

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
                s.Add(S("Fly Accel", "Fly acceleration m/s^2 while R stick UP held", () => loco.flyAcceleration, v => loco.flyAcceleration=v, 1f, 50f, 1f, "F0"));
                s.Add(S("Fly Max Speed", "Max fly speed cap m/s", () => loco.flyMaxSpeed, v => loco.flyMaxSpeed=v, 5f, 50f, 1f, "F0"));
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
                s.Add(S("Height", "CharacterController height", () => cc.height, v => { cc.height=v; cc.center=new Vector3(cc.center.x,v*0.5f,cc.center.z); }, 0.5f, 3, 0.1f));
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
            // AVATAR -- dynamiczny max z AvatarGallery (fallback=1 jesli brak)
            // =============================================================
            var playerAvatar = UnityEngine.Object.FindAnyObjectByType<Plaga44.PlayerAvatar>();
            if (playerAvatar != null) Sec("AVATAR", s => {
                int maxMode = Mathf.Max(1, playerAvatar.MaxMode);
                string modeDesc = maxMode > 1
                    ? $"0=None(SDK rig), 1..{maxMode}=Avatar z Gallery"
                    : "0=None(SDK rig)";
                s.Add(S("Mode", modeDesc, () => playerAvatar.avatarMode, v => playerAvatar.PreviewAvatarMode((int)v), 0, maxMode, 1, "F0"));
                s.Add(S("Hide Head", "Hide head+neck to avoid camera clipping (player avatars)", () => playerAvatar.hideHead?1:0, v => playerAvatar.hideHead=v>0.5f, 0, 1, 1, "F0"));
                s.Add(S("Y Offset", "Avatar feet offset from rig base", () => playerAvatar.yOffset, v => playerAvatar.yOffset=v, -1f, 1f, 0.05f, "F2"));
            });

            // =============================================================
            // ITEMS -- item browser (like AVATAR but for held items)
            // =============================================================
            // ITEMS -- max jest dynamiczny bo ItemBrowser.LoadItems() moze jeszcze nie odpalic
            Sec("ITEMS", s => {
                s.Add(new SettingDef("Item", "0=None, 1..N=Item",
                    () => {
                        var ib = Plaga44.ItemBrowser.Instance;
                        return ib != null ? ib.SelectedItem : 0;
                    },
                    v => {
                        var ib = Plaga44.ItemBrowser.Instance;
                        if (ib != null) ib.SetItem((int)v);
                    },
                    0, 10, 1, "F0")); // max=10 jako bufor, SetItem clampuje do MaxItem
            });

            // =============================================================
            // ITEM GRIP -- per-item grip calibration (pos/rot/scale)
            // Live tuning of the currently spawned/held item. SAVE persists
            // in PlayerPrefs keyed by item name. Applied automatically on next grab.
            // =============================================================
            Sec("ITEM GRIP", s => {
                // Helpers -- operate on currently spawned ItemBrowser preview OR currently held item
                System.Func<Plaga44.Inventory.PlagaGrabbable> findTarget = () =>
                {
                    var ib = Plaga44.ItemBrowser.Instance;
                    if (ib != null && ib.CurrentSpawned != null)
                    {
                        var pg = ib.CurrentSpawned.GetComponent<Plaga44.Inventory.PlagaGrabbable>();
                        if (pg != null) return pg;
                    }
                    // Fallback: any currently grabbed PlagaGrabbable in scene
                    foreach (var g in UnityEngine.Object.FindObjectsByType<Plaga44.Inventory.PlagaGrabbable>(FindObjectsSortMode.None))
                        if (g.isGrabbed) return g;
                    return null;
                };
                System.Func<Plaga44.Inventory.ItemGripConfig> getCfg = () =>
                {
                    var t = findTarget();
                    return t != null ? t.GripConfig : Plaga44.Inventory.ItemGripConfig.Default;
                };
                // Write mutated config back via property setter (triggers ApplyGripConfig live)
                System.Action<Plaga44.Inventory.ItemGripConfig> write = (newCfg) =>
                {
                    var t = findTarget();
                    if (t != null) t.GripConfig = newCfg;
                };

                s.Add(S("Pos X",  "Local X offset relative to hand grip (m)",
                    () => getCfg().offsetPos.x,
                    v => { var c = getCfg(); c.offsetPos.x = v; write(c); }, -0.3f, 0.3f, 0.005f, "F3"));
                s.Add(S("Pos Y",  "Local Y offset (m)",
                    () => getCfg().offsetPos.y,
                    v => { var c = getCfg(); c.offsetPos.y = v; write(c); }, -0.3f, 0.3f, 0.005f, "F3"));
                s.Add(S("Pos Z",  "Local Z offset (m)",
                    () => getCfg().offsetPos.z,
                    v => { var c = getCfg(); c.offsetPos.z = v; write(c); }, -0.3f, 0.3f, 0.005f, "F3"));
                s.Add(S("Rot X",  "Local X rotation (deg)",
                    () => getCfg().offsetRotEuler.x,
                    v => { var c = getCfg(); c.offsetRotEuler.x = v; write(c); }, -180f, 180f, 5f, "F0"));
                s.Add(S("Rot Y",  "Local Y rotation (deg)",
                    () => getCfg().offsetRotEuler.y,
                    v => { var c = getCfg(); c.offsetRotEuler.y = v; write(c); }, -180f, 180f, 5f, "F0"));
                s.Add(S("Rot Z",  "Local Z rotation (deg)",
                    () => getCfg().offsetRotEuler.z,
                    v => { var c = getCfg(); c.offsetRotEuler.z = v; write(c); }, -180f, 180f, 5f, "F0"));
                s.Add(S("Scale",  "Uniform scale multiplier (1.0 = original prefab size)",
                    () => getCfg().scale,
                    v => { var c = getCfg(); c.scale = v; write(c); }, 0.1f, 5f, 0.05f, "F2"));
                s.Add(S("SAVE GRIP", "Save current grip offset to PlayerPrefs (per item name)", () => 0,
                    v => { if (v > 0.5f) {
                        var t = findTarget();
                        if (t != null) { Plaga44.Inventory.ItemGripConfig.Save(t.BaseName, t.GripConfig); SetAction($"ITEM GRIP SAVED for '{t.BaseName}'", true); }
                        else SetAction("ITEM GRIP: no active item", false);
                    } }, 0, 1, 1, "F0"));
                s.Add(S("RESET GRIP", "Clear saved grip (reset to prefab defaults)", () => 0,
                    v => { if (v > 0.5f) {
                        var t = findTarget();
                        if (t != null) {
                            Plaga44.Inventory.ItemGripConfig.Clear(t.BaseName);
                            t.GripConfig = Plaga44.Inventory.ItemGripConfig.Default;
                            SetAction($"ITEM GRIP RESET for '{t.BaseName}'", true);
                        } else SetAction("ITEM GRIP: no active item", false);
                    } }, 0, 1, 1, "F0"));
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
                s.Add(S("RESET ALL", "Reset all settings to saved defaults", () => 0, v => { if(v>0.5f) ResetToDefaults(); }, 0, 1, 1, "F0"));
                s.Add(S("LOG ALL", "Print all settings to console", () => 0, v => { if(v>0.5f) LogAll(); }, 0, 1, 1, "F0"));
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
                if (sky.HasFloat("_Rotation")) s.Add(S("Rotation", "Skybox static rotation (degrees)", () => sky.GetFloat("_Rotation"), v => sky.SetFloat("_Rotation",v), 0, 360, 5, "F0"));
                if (sky.HasFloat("_CloudBoost")) s.Add(S("Cloud Bright", "Cloud brightness multiplier (1=normal)", () => sky.GetFloat("_CloudBoost"), v => sky.SetFloat("_CloudBoost",v), 0, 5, 0.1f));
                if (sky.HasFloat("_CloudThreshold")) s.Add(S("Cloud Thresh", "Luminance threshold for cloud effect (lower=more clouds)", () => sky.GetFloat("_CloudThreshold"), v => sky.SetFloat("_CloudThreshold",v), 0, 1, 0.01f, "F2"));
                // Cloud Alpha + Cloud R/G/B removed per issue #144 -- clutter without value.
                if (sky.HasColor("_GroundColor")) {
                    s.Add(S("Ground R", "Ground/horizon color R", () => sky.GetColor("_GroundColor").r, v => { var c=sky.GetColor("_GroundColor"); c.r=v; sky.SetColor("_GroundColor",c); }, 0, 1, 0.02f, "F2"));
                    s.Add(S("Ground G", "Ground/horizon color G", () => sky.GetColor("_GroundColor").g, v => { var c=sky.GetColor("_GroundColor"); c.g=v; sky.SetColor("_GroundColor",c); }, 0, 1, 0.02f, "F2"));
                    s.Add(S("Ground B", "Ground/horizon color B", () => sky.GetColor("_GroundColor").b, v => { var c=sky.GetColor("_GroundColor"); c.b=v; sky.SetColor("_GroundColor",c); }, 0, 1, 0.02f, "F2"));
                }
                if (sky.HasFloat("_GroundBlend")) s.Add(S("Ground Blend", "Horizon height (-0.5..0.5)", () => sky.GetFloat("_GroundBlend"), v => sky.SetFloat("_GroundBlend",v), -0.5f, 0.5f, 0.01f, "F2"));
                if (sky.HasFloat("_GroundFade")) s.Add(S("Ground Fade", "Sky-ground transition softness", () => sky.GetFloat("_GroundFade"), v => sky.SetFloat("_GroundFade",v), 0.01f, 1, 0.02f, "F2"));
                if (sky.HasFloat("_RotSpeed")) s.Add(S("Shader Rot Speed", "Built-in shader sky rotation (deg/s)", () => sky.GetFloat("_RotSpeed"), v => sky.SetFloat("_RotSpeed",v), 0, 30, 0.5f));
                // SkyRotator script speed
                if (skyRot != null)
                    s.Add(S("Rot Speed", "SkyRotator script speed (deg/s)", () => skyRot.rotationSpeed, v => skyRot.rotationSpeed=v, 0, 5, 0.1f));
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
            // PROFILE -- runtime preset switcher
            // =============================================================
            Sec("PROFILE", s => {
                // 0=Quest, 1=PCVR -- applies a batch of settings
                s.Add(S("Target", "0=Quest 1=PCVR -- applies preset", () => _activeProfile, v => { ApplyProfile((int)v); }, 0, 1, 1, "F0"));
            });

            // =============================================================
            // URP -- additional pipeline info
            // =============================================================
            if (urp != null) Sec("URP", s => {
                s.Add(S("HDR", "High dynamic range (1=on)", () => urp.supportsHDR?1:0, v => {}, 0, 1, 0, "F0")); // read-only at runtime
                s.Add(S("Depth Tex", "Camera depth texture (1=on)", () => urp.supportsCameraDepthTexture?1:0, v => {}, 0, 1, 0, "F0")); // RO
                s.Add(S("Opaque Tex", "Camera opaque texture (1=on)", () => urp.supportsCameraOpaqueTexture?1:0, v => {}, 0, 1, 0, "F0")); // RO
                s.Add(S("SH Mode", "Spherical harmonics eval mode", () => (float)urp.shEvalMode, v => {}, 0, 2, 0, "F0")); // RO
            });

            // =============================================================
            // NAVMESH -- agent settings if present
            // =============================================================
            var agent = UnityEngine.Object.FindAnyObjectByType<NavMeshAgent>();
            if (agent != null) Sec("NAVMESH", s => {
                s.Add(S("Agent Speed", "NavMesh agent speed", () => agent.speed, v => agent.speed=v, 0, 20, 0.5f));
                s.Add(S("Agent Accel", "NavMesh agent acceleration", () => agent.acceleration, v => agent.acceleration=v, 0, 50, 1, "F0"));
                s.Add(S("Agent Radius", "NavMesh agent radius", () => agent.radius, v => agent.radius=v, 0.1f, 2, 0.05f, "F2"));
                s.Add(S("Stop Dist", "NavMesh stopping distance", () => agent.stoppingDistance, v => agent.stoppingDistance=v, 0, 10, 0.1f));
            });

            // =============================================================
            // EXIT
            // =============================================================
            Sec("EXIT", s => {
                s.Add(S("QUIT GAME", "Exit application", () => 0, v => {
                    if (v > 0.5f)
                    {
#if UNITY_EDITOR
                        UnityEditor.EditorApplication.isPlaying = false;
#else
                        Application.Quit();
#endif
                    }
                }, 0, 1, 1, "F0"));
            });

            FinalizeBuild();
        }

        private static void FinalizeBuild()
        {
            CollectFlatSettingsList();
            if (_defaults == null) CaptureDefaults(); // musi byc PRZED RestorePersistedValues
            _built = true; // PRZED restore -- blokuje reentrant Build() z action setterow (LOG ALL etc)
            int restored = RestorePersistedValues();
            Debug.Log($"[PLAGA44][Settings] Built: {_sec.Count} sections, {_allSettings.Count} saveable settings, {restored} restored from PlayerPrefs");
        }

        private static void CollectFlatSettingsList()
        {
            _names = new string[_sec.Count];
            _sec.Keys.CopyTo(_names, 0);
            _allSettings.Clear();
            foreach (var kv in _sec)
                foreach (var setting in kv.Value)
                    if (setting.step > 0) // skip read-only and actions
                        _allSettings.Add(setting);
        }

        // Action-type settings (getter always returns 0, setter fires action on >0.5).
        // These must NOT be restored from PlayerPrefs -- restoring 1.0 would re-trigger the action.
        private static readonly HashSet<string> ACTION_SETTINGS =
            new HashSet<string> { "RESET ALL", "LOG ALL", "QUIT GAME", "SAVE GRIP", "RESET GRIP" };

        private static int RestorePersistedValues()
        {
            int restored = 0;
            foreach (var kv in _sec)
            {
                if (NON_PERSISTENT_SECTIONS.Contains(kv.Key)) continue;
                foreach (var setting in kv.Value)
                {
                    if (setting.step <= 0) continue;
                    if (ACTION_SETTINGS.Contains(setting.name)) continue; // skip action buttons
                    string key = PrefsKeys.Current(kv.Key, setting.name);
                    if (!PlayerPrefs.HasKey(key)) continue;
                    setting.set(Mathf.Clamp(PlayerPrefs.GetFloat(key), setting.min, setting.max));
                    restored++;
                }
            }
            return restored;
        }

        static void ApplyProfile(int profile)
        {
            var urp = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
            var ter = UnityEngine.Object.FindAnyObjectByType<Terrain>();
            string label = profile == 0 ? "Quest" : "PCVR";

            if (profile == 0)
            {
                // Quest profile -- low settings for mobile VR
                if (urp != null) { urp.renderScale = 0.8f; urp.shadowDistance = 20f; urp.msaaSampleCount = 4; }
                QualitySettings.lodBias = 0.7f;
                if (ter != null) { ter.detailObjectDistance = 80f; ter.treeDistance = 500f; }
                XRSettings.eyeTextureResolutionScale = 0.8f;
                try { OVRManager.foveatedRenderingLevel = (OVRManager.FoveatedRenderingLevel)3; } catch {}
                Application.targetFrameRate = 72;
            }
            else
            {
                // PCVR profile -- high quality for desktop VR
                if (urp != null) { urp.renderScale = 1.2f; urp.shadowDistance = 80f; urp.msaaSampleCount = 4; }
                QualitySettings.lodBias = 1.5f;
                if (ter != null) { ter.detailObjectDistance = 250f; ter.treeDistance = 2000f; }
                XRSettings.eyeTextureResolutionScale = 1.2f;
                try { OVRManager.foveatedRenderingLevel = (OVRManager.FoveatedRenderingLevel)0; } catch {}
                Application.targetFrameRate = -1;
            }

            _activeProfile = profile;
            Rebuild();
            Debug.Log($"[PLAGA44][Settings] Applied profile: {label}");
        }

        static Light FindSun()
        {
            foreach (var l in UnityEngine.Object.FindObjectsByType<Light>(FindObjectsSortMode.None))
                if (l.type == LightType.Directional && !l.name.Contains("Bounce")) return l;
            return null;
        }
    }
}
