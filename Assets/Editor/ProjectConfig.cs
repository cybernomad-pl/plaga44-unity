// ProjectConfig.cs -- CYBERNOMAD Editor Tool
//
// Steruje: ProjectSettings/ProjectSettings.asset
// Branding, splash, orientacja, scripting defines, stripping, ikona.
//
// Public API:
//   ProjectConfig.Apply(ProjectConfig.INITIAL);
//   ProjectConfig.SetCompanyName("Cybernomad");
//   ProjectConfig.AddScriptingDefine("LOCOMOTION_ONLY");
//   ProjectConfig.LogCurrent();

using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEngine;
using UnityEngine.Rendering;

namespace Plaga44.Editor
{
    public struct ProjectSettings_
    {
        public string companyName;
        public string productName;
        public string bundleId;
        public string bundleVersion;
        public int androidVersionCode;
        public int colorSpace;              // 0=Gamma, 1=Linear
        public int orientationDefault;      // 0=Portrait, 1=PortraitUD, 2=LandscapeR, 3=LandscapeL, 4=Auto
        public bool autoPortrait;
        public bool autoPortraitUD;
        public bool autoLandscapeR;
        public bool autoLandscapeL;
        public bool showUnitySplash;
        public bool stripEngineCode;
        public string[] scriptingDefines;   // null = nie zmieniaj
    }

    public static class ProjectConfig
    {
        private const string LOG = "[PLAGA44]";
        private const string ASSET = "ProjectSettings/ProjectSettings.asset";

        // ---------------------------------------------------------------------
        // Presety
        // ---------------------------------------------------------------------

        public static readonly ProjectSettings_ INITIAL = new ProjectSettings_
        {
            companyName         = "Cybernomad",
            productName         = "PLAGA 44",
            bundleId            = "games.cybernomad.plaga44",
            bundleVersion       = "0.1.0",
            androidVersionCode  = 1,
            colorSpace          = 1,            // Linear
            orientationDefault  = 3,            // LandscapeLeft
            autoPortrait        = false,
            autoPortraitUD      = false,
            autoLandscapeR      = false,
            autoLandscapeL      = true,
            showUnitySplash     = false,
            stripEngineCode     = true,
            scriptingDefines    = null,
        };

        // ---------------------------------------------------------------------
        // Apply
        // ---------------------------------------------------------------------

        public static void Apply(ProjectSettings_ s)
        {
            if (s.companyName != null) PlayerSettings.companyName = s.companyName;
            if (s.productName != null) PlayerSettings.productName = s.productName;
            if (s.bundleId != null) PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Android, s.bundleId);
            if (s.bundleVersion != null) PlayerSettings.bundleVersion = s.bundleVersion;
            if (s.androidVersionCode > 0) PlayerSettings.Android.bundleVersionCode = s.androidVersionCode;

            PlayerSettings.colorSpace = s.colorSpace == 1 ? ColorSpace.Linear : ColorSpace.Gamma;
            PlayerSettings.defaultInterfaceOrientation = (UIOrientation)s.orientationDefault;
            PlayerSettings.allowedAutorotateToPortrait = s.autoPortrait;
            PlayerSettings.allowedAutorotateToPortraitUpsideDown = s.autoPortraitUD;
            PlayerSettings.allowedAutorotateToLandscapeRight = s.autoLandscapeR;
            PlayerSettings.allowedAutorotateToLandscapeLeft = s.autoLandscapeL;
            PlayerSettings.SplashScreen.show = s.showUnitySplash;
            PlayerSettings.stripEngineCode = s.stripEngineCode;

            if (s.scriptingDefines != null)
                SetDefines(s.scriptingDefines);

            Debug.Log($"{LOG} Project applied: {s.companyName}/{s.productName} v{s.bundleVersion} " +
                      $"color={s.colorSpace} strip={s.stripEngineCode} splash={s.showUnitySplash}");
        }

        // ---------------------------------------------------------------------
        // Single value
        // ---------------------------------------------------------------------

        public static void SetCompanyName(string v)   { PlayerSettings.companyName = v; Log("companyName", v); }
        public static void SetProductName(string v)   { PlayerSettings.productName = v; Log("productName", v); }
        public static void SetBundleId(string v)      { PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Android, v); Log("bundleId", v); }
        public static void SetBundleVersion(string v) { PlayerSettings.bundleVersion = v; Log("bundleVersion", v); }
        public static void SetVersionCode(int v)      { PlayerSettings.Android.bundleVersionCode = v; Log("versionCode", v.ToString()); }
        public static void SetColorSpace(int v)       { PlayerSettings.colorSpace = v == 1 ? ColorSpace.Linear : ColorSpace.Gamma; Log("colorSpace", v.ToString()); }
        public static void SetStripEngineCode(bool v) { PlayerSettings.stripEngineCode = v; Log("stripEngineCode", v.ToString()); }
        public static void SetShowSplash(bool v)      { PlayerSettings.SplashScreen.show = v; Log("showSplash", v.ToString()); }

        // Scripting defines
        public static void AddScriptingDefine(string define)
        {
            var current = GetDefines();
            if (!current.Contains(define))
            {
                current.Add(define);
                SetDefines(current.ToArray());
                Debug.Log($"{LOG} Define added: {define}");
            }
        }

        public static void RemoveScriptingDefine(string define)
        {
            var current = GetDefines();
            if (current.Remove(define))
            {
                SetDefines(current.ToArray());
                Debug.Log($"{LOG} Define removed: {define}");
            }
        }

        public static List<string> GetDefines()
        {
            PlayerSettings.GetScriptingDefineSymbols(NamedBuildTarget.Android, out string[] defines);
            return defines.ToList();
        }

        // ---------------------------------------------------------------------
        // Log
        // ---------------------------------------------------------------------

        public static void LogCurrent()
        {
            Debug.Log($"{LOG} Project: {PlayerSettings.companyName}/{PlayerSettings.productName} " +
                      $"v{PlayerSettings.bundleVersion} ({PlayerSettings.Android.bundleVersionCode})");
            Debug.Log($"{LOG}   bundleId={PlayerSettings.GetApplicationIdentifier(NamedBuildTarget.Android)}");
            Debug.Log($"{LOG}   colorSpace={PlayerSettings.colorSpace} strip={PlayerSettings.stripEngineCode} " +
                      $"splash={PlayerSettings.SplashScreen.show}");
            Debug.Log($"{LOG}   defines: {string.Join(", ", GetDefines())}");
        }

        // ---------------------------------------------------------------------
        // Menu
        // ---------------------------------------------------------------------

        [MenuItem("CYBERNOMAD/Presets/Quest/Project INITIAL", false, 1)]
        static void MenuInitial() => Apply(INITIAL);
        [MenuItem("CYBERNOMAD/Status/Project", false, 100)]
        static void MenuShow() => LogCurrent();

        // ---------------------------------------------------------------------
        // Helpers
        // ---------------------------------------------------------------------

        static void SetDefines(string[] defines)
        {
            PlayerSettings.SetScriptingDefineSymbols(NamedBuildTarget.Android, defines);
        }

        static void Log(string field, string value) => Debug.Log($"{LOG} Project tweak: {field}={value}");
    }
}
