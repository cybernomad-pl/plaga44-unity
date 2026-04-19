// =============================================================================
// MixamoMaterialExtractor.cs
// CYBERNOMAD -- Ekstrahuje embedded textures + materials z Mixamo FBX do
// External folderow. Bez tego avatary importowane z embedded textures
// renderuja sie na bialo (URP/Lit domyslny mat, zero texture binding).
//
// Proces per avatar:
//   1. Znajdz FBX w Assets/PLAGA44/Avatars/<Name>/
//   2. ExtractTextures -> <Folder>/Textures/
//   3. materialLocation = External -> Unity stworzy materials w <Folder>/Materials/
//   4. Reimport FBX z ForceUpdate
//
// Menu: CYBERNOMAD > Fix > Extract Avatar Textures & Materials
// =============================================================================
#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Plaga44.Editor.Setup
{
    public static class MixamoMaterialExtractor
    {
        private const string LOG        = "[PLAGA44][MixamoExtractor]";
        private const string AvatarsRoot = "Assets/PLAGA44/Avatars";

        [MenuItem("CYBERNOMAD/Fix/Extract Avatar Textures & Materials", false, 420)]
        public static void ExtractAll()
        {
            if (!AssetDatabase.IsValidFolder(AvatarsRoot))
            {
                Debug.LogWarning($"{LOG} Folder missing: {AvatarsRoot}");
                return;
            }

            int extracted = 0;
            string[] guids = AssetDatabase.FindAssets("t:Model", new[] { AvatarsRoot });
            foreach (var guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (!path.EndsWith(".fbx", System.StringComparison.OrdinalIgnoreCase)) continue;

                if (ExtractOne(path)) extracted++;
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            string msg = $"Extracted textures/materials from {extracted} FBX files.";
            Debug.Log($"{LOG} {msg}");
            EditorUtility.DisplayDialog("Mixamo Extractor", msg, "OK");
        }

        private static bool ExtractOne(string fbxPath)
        {
            var importer = AssetImporter.GetAtPath(fbxPath) as ModelImporter;
            if (importer == null)
            {
                Debug.LogWarning($"{LOG} [SKIP] Not a ModelImporter: {fbxPath}");
                return false;
            }

            string folder = Path.GetDirectoryName(fbxPath);
            string texFolder = Path.Combine(folder, "Textures").Replace('\\', '/');
            string matFolder = Path.Combine(folder, "Materials").Replace('\\', '/');

            // 1. Extract embedded textures -> <Folder>/Textures/
            EnsureFolder(texFolder);
            bool texExtracted = importer.ExtractTextures(texFolder);
            if (texExtracted)
                Debug.Log($"{LOG} [TEX] Extracted textures -> {texFolder}");

            // 2. Switch material mode to External -> Unity tworzy .mat w folderze fbx
            if (importer.materialLocation != ModelImporterMaterialLocation.External)
            {
                importer.materialLocation = ModelImporterMaterialLocation.External;
                importer.SaveAndReimport();
                Debug.Log($"{LOG} [MAT] Switched to External materialLocation: {fbxPath}");
            }

            AssetDatabase.ImportAsset(fbxPath, ImportAssetOptions.ForceUpdate);
            Debug.Log($"{LOG} [OK] {Path.GetFileName(fbxPath)}");
            return true;
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = Path.GetDirectoryName(path).Replace('\\', '/');
            string name   = Path.GetFileName(path);
            AssetDatabase.CreateFolder(parent, name);
        }
    }
}
#endif
