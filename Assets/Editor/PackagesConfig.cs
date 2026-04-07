// PackagesConfig.cs -- CYBERNOMAD Editor Tool
//
// Steruje: Packages/manifest.json
//
// Public API:
//   PackagesConfig.AddPackage("com.unity.textmeshpro", "4.0.0");
//   PackagesConfig.RemovePackage("com.unity.visualscripting");
//   PackagesConfig.SetVersion("com.meta.xr.sdk.core", "85.0.0");
//   PackagesConfig.LogCurrent();

using System.IO;
using System.Text.RegularExpressions;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Plaga44.Editor
{
    public static class PackagesConfig
    {
        private const string LOG = "[PLAGA44]";

        // ---------------------------------------------------------------------
        // Public API
        // ---------------------------------------------------------------------

        /// <summary>Dodaj pakiet do manifest.json. Jesli juz jest -- zmienia wersje.</summary>
        public static void AddPackage(string packageId, string version)
        {
            string path = GetPath();
            if (path == null) return;

            string manifest = File.ReadAllText(path);

            if (manifest.Contains($"\"{packageId}\""))
            {
                // Zmien wersje
                var regex = new Regex($@"""{Regex.Escape(packageId)}""\s*:\s*""[^""]+""");
                manifest = regex.Replace(manifest, $"\"{packageId}\": \"{version}\"");
                File.WriteAllText(path, manifest);
                Debug.Log($"{LOG} Package updated: {packageId}@{version}");
            }
            else
            {
                // Dodaj nowy
                int depsIdx = manifest.IndexOf("\"dependencies\"");
                int braceIdx = manifest.IndexOf('{', depsIdx);
                if (braceIdx < 0) return;

                string entry = $"\n    \"{packageId}\": \"{version}\",";
                manifest = manifest.Substring(0, braceIdx + 1) + entry + manifest.Substring(braceIdx + 1);
                File.WriteAllText(path, manifest);
                Debug.Log($"{LOG} Package added: {packageId}@{version}");
            }

            UnityEditor.PackageManager.Client.Resolve();
        }

        /// <summary>Usun pakiet z manifest.json.</summary>
        public static void RemovePackage(string packageId)
        {
            string path = GetPath();
            if (path == null) return;

            string manifest = File.ReadAllText(path);
            if (!manifest.Contains($"\"{packageId}\""))
            {
                Debug.Log($"{LOG} Package not in manifest: {packageId}");
                return;
            }

            // Usun linie z pakietem (z przecinkiem i newline)
            var regex = new Regex($@"\s*""{Regex.Escape(packageId)}""\s*:\s*""[^""]+""[,]?\n?");
            manifest = regex.Replace(manifest, "");
            File.WriteAllText(path, manifest);
            Debug.Log($"{LOG} Package removed: {packageId}");

            UnityEditor.PackageManager.Client.Resolve();
        }

        /// <summary>Alias dla AddPackage -- zmienia wersje istniejacego pakietu.</summary>
        public static void SetVersion(string packageId, string version) => AddPackage(packageId, version);

        /// <summary>Ustaw wersje wszystkich Meta XR pakietow naraz.</summary>
        public static void SetMetaXRVersion(string version)
        {
            string[] metaPackages = {
                "com.meta.xr.sdk.core",
                "com.meta.xr.sdk.interaction",
                "com.meta.xr.sdk.interaction.ovr",
                "com.meta.xr.sdk.audio",
            };

            foreach (var pkg in metaPackages)
                SetVersion(pkg, version);

            Debug.Log($"{LOG} All Meta XR packages set to {version}");
        }

        /// <summary>Zwraca wersje pakietu z manifest.json lub null.</summary>
        public static string GetVersion(string packageId)
        {
            string path = GetPath();
            if (path == null) return null;

            string manifest = File.ReadAllText(path);
            var match = Regex.Match(manifest, $@"""{Regex.Escape(packageId)}""\s*:\s*""([^""]+)""");
            return match.Success ? match.Groups[1].Value : null;
        }

        /// <summary>Zwraca liste wszystkich pakietow z manifest.json.</summary>
        public static Dictionary<string, string> GetAll()
        {
            var result = new Dictionary<string, string>();
            string path = GetPath();
            if (path == null) return result;

            string manifest = File.ReadAllText(path);
            var matches = Regex.Matches(manifest, @"""([\w\.\-]+)""\s*:\s*""([\d\.\w\-]+)""");
            foreach (Match m in matches)
                result[m.Groups[1].Value] = m.Groups[2].Value;

            return result;
        }

        // ---------------------------------------------------------------------
        // Log
        // ---------------------------------------------------------------------

        public static void LogCurrent()
        {
            var packages = GetAll();
            Debug.Log($"{LOG} Packages ({packages.Count}):");
            foreach (var kv in packages)
                Debug.Log($"{LOG}   {kv.Key} @ {kv.Value}");
        }

        public static void LogMetaXR()
        {
            var packages = GetAll();
            Debug.Log($"{LOG} Meta XR packages:");
            foreach (var kv in packages)
                if (kv.Key.StartsWith("com.meta.xr"))
                    Debug.Log($"{LOG}   {kv.Key} @ {kv.Value}");
        }

        // ---------------------------------------------------------------------
        // Menu
        // ---------------------------------------------------------------------

        [MenuItem("CYBERNOMAD/Status/Packages", false, 1)]
        static void MenuShowAll() => LogCurrent();

        [MenuItem("CYBERNOMAD/Status/Packages (Meta XR)", false, 2)]
        static void MenuShowMeta() => LogMetaXR();

        // ---------------------------------------------------------------------
        // Helpers
        // ---------------------------------------------------------------------

        static string GetPath()
        {
            string p = Path.Combine(Application.dataPath, "..", "Packages", "manifest.json");
            if (!File.Exists(p)) { Debug.LogError($"{LOG} manifest.json not found"); return null; }
            return p;
        }
    }
}
