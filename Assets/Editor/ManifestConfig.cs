// ManifestConfig.cs -- CYBERNOMAD Editor Tool
//
// Steruje: Assets/Plugins/Android/AndroidManifest.xml
//
// Public API:
//   ManifestConfig.SetPermission("android.permission.RECORD_AUDIO", true);
//   ManifestConfig.SetMetaData("com.oculus.ossplash.background", "black");
//   ManifestConfig.SetSupportedDevices("quest3|quest3s");
//   ManifestConfig.AddFeature("android.hardware.microphone", false);
//   ManifestConfig.LogCurrent();

using System.IO;
using System.Xml;
using UnityEditor;
using UnityEngine;

namespace Plaga44.Editor
{
    public static class ManifestConfig
    {
        private const string LOG = "[PLAGA44]";
        private const string MANIFEST = "Assets/Plugins/Android/AndroidManifest.xml";
        private const string NS_ANDROID = "http://schemas.android.com/apk/res/android";

        // ---------------------------------------------------------------------
        // Permissions
        // ---------------------------------------------------------------------

        /// <summary>Add or remove a uses-permission.</summary>
        public static void SetPermission(string permission, bool enabled)
        {
            var doc = Load();
            if (doc == null) return;

            var manifest = doc.DocumentElement;
            var nsm = Nsm(doc);

            // Find existing
            var existing = manifest.SelectSingleNode(
                $"uses-permission[@android:name='{permission}']", nsm);

            if (enabled && existing == null)
            {
                var node = doc.CreateElement("uses-permission");
                node.SetAttribute("name", NS_ANDROID, permission);
                manifest.AppendChild(node);
                Save(doc);
                Debug.Log($"{LOG} Manifest: added permission {permission}");
            }
            else if (!enabled && existing != null)
            {
                manifest.RemoveChild(existing);
                Save(doc);
                Debug.Log($"{LOG} Manifest: removed permission {permission}");
            }
        }

        // ---------------------------------------------------------------------
        // Features
        // ---------------------------------------------------------------------

        /// <summary>Add a uses-feature. required=true means app won't install without it.</summary>
        public static void AddFeature(string feature, bool required)
        {
            var doc = Load();
            if (doc == null) return;

            var manifest = doc.DocumentElement;
            var nsm = Nsm(doc);

            var existing = manifest.SelectSingleNode(
                $"uses-feature[@android:name='{feature}']", nsm);
            if (existing != null) { manifest.RemoveChild(existing); }

            var node = doc.CreateElement("uses-feature");
            node.SetAttribute("name", NS_ANDROID, feature);
            node.SetAttribute("required", NS_ANDROID, required ? "true" : "false");
            manifest.AppendChild(node);

            Save(doc);
            Debug.Log($"{LOG} Manifest: feature {feature} required={required}");
        }

        // ---------------------------------------------------------------------
        // Meta-data (application level)
        // ---------------------------------------------------------------------

        /// <summary>Set a meta-data value inside application tag.</summary>
        public static void SetMetaData(string name, string value)
        {
            var doc = Load();
            if (doc == null) return;

            var app = doc.DocumentElement.SelectSingleNode("application");
            if (app == null) { Debug.LogError($"{LOG} No <application> in manifest"); return; }

            var nsm = Nsm(doc);
            var existing = app.SelectSingleNode(
                $"meta-data[@android:name='{name}']", nsm);

            if (existing != null)
            {
                ((XmlElement)existing).SetAttribute("value", NS_ANDROID, value);
            }
            else
            {
                var node = doc.CreateElement("meta-data");
                node.SetAttribute("name", NS_ANDROID, name);
                node.SetAttribute("value", NS_ANDROID, value);
                app.AppendChild(node);
            }

            Save(doc);
            Debug.Log($"{LOG} Manifest: meta-data {name}={value}");
        }

        // ---------------------------------------------------------------------
        // Convenience
        // ---------------------------------------------------------------------

        public static void SetSupportedDevices(string devices) =>
            SetMetaData("com.oculus.supportedDevices", devices);

        public static void SetSplashBackground(string color) =>
            SetMetaData("com.oculus.ossplash.background", color);

        // ---------------------------------------------------------------------
        // Log
        // ---------------------------------------------------------------------

        public static void LogCurrent()
        {
            var doc = Load();
            if (doc == null) return;

            var nsm = Nsm(doc);
            Debug.Log($"{LOG} AndroidManifest.xml:");

            foreach (XmlNode n in doc.DocumentElement.SelectNodes("uses-permission", nsm))
                Debug.Log($"{LOG}   permission: {((XmlElement)n).GetAttribute("name", NS_ANDROID)}");

            foreach (XmlNode n in doc.DocumentElement.SelectNodes("uses-feature", nsm))
                Debug.Log($"{LOG}   feature: {((XmlElement)n).GetAttribute("name", NS_ANDROID)} " +
                          $"required={((XmlElement)n).GetAttribute("required", NS_ANDROID)}");

            var app = doc.DocumentElement.SelectSingleNode("application");
            if (app != null)
            {
                foreach (XmlNode n in app.SelectNodes("meta-data"))
                {
                    var el = (XmlElement)n;
                    Debug.Log($"{LOG}   meta: {el.GetAttribute("name", NS_ANDROID)}={el.GetAttribute("value", NS_ANDROID)}");
                }
            }
        }

        // ---------------------------------------------------------------------
        // Menu
        // ---------------------------------------------------------------------

        [MenuItem("CYBERNOMAD/Status/Manifest", false, 100)]
        static void MenuShow() => LogCurrent();

        // ---------------------------------------------------------------------
        // Helpers
        // ---------------------------------------------------------------------

        static XmlDocument Load()
        {
            string fullPath = Path.Combine(Application.dataPath, "..", MANIFEST);
            if (!File.Exists(fullPath)) { Debug.LogError($"{LOG} {MANIFEST} not found"); return null; }
            var doc = new XmlDocument();
            doc.Load(fullPath);
            return doc;
        }

        static void Save(XmlDocument doc)
        {
            string fullPath = Path.Combine(Application.dataPath, "..", MANIFEST);
            doc.Save(fullPath);
            AssetDatabase.Refresh();
        }

        static XmlNamespaceManager Nsm(XmlDocument doc)
        {
            var nsm = new XmlNamespaceManager(doc.NameTable);
            nsm.AddNamespace("android", NS_ANDROID);
            return nsm;
        }
    }
}
