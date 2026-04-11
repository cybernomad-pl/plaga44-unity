using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace Plaga44.Editor
{
    /// <summary>
    /// Imports Mixamo characters split into logical parts (via KLAUDIA/mixamo_split_characters.py).
    ///
    /// Dual-mode:
    ///   1. Menu: "Plaga44/Import/Character Split" -- interactive, live Editor.
    ///   2. Batch: Unity.exe -batchmode -quit -executeMethod Plaga44.Editor.CharacterSplitImporter.ImportAll
    ///
    /// Layout:
    ///   Assets/PLAGA44/Characters/Imports/&lt;PackName&gt;/&lt;CharName&gt;/parts/*.obj
    ///   Assets/PLAGA44/Characters/Imports/&lt;PackName&gt;/&lt;CharName&gt;/textures/*.png
    ///   Assets/PLAGA44/Characters/Imports/&lt;PackName&gt;/&lt;CharName&gt;/manifest.json
    ///
    /// Output:
    ///   Assets/PLAGA44/Characters/Prefabs/&lt;PackName&gt;/&lt;CharName&gt;.prefab
    ///     root GameObject with N children, each = one split part as prefab instance.
    ///     Parts preserve original world coordinates, so children composed reconstruct full character.
    ///
    /// Notes:
    ///   - OBJ imports are static meshes (no rig). For rigged characters use original FBX import.
    ///   - This is for VISUAL VERIFICATION of split granularity, not runtime gameplay.
    /// </summary>
    public static class CharacterSplitImporter
    {
        private const string ImportsRoot = "Assets/PLAGA44/Characters/Imports";
        private const string PrefabsRoot = "Assets/PLAGA44/Characters/Prefabs";
        private const string LogPrefix = "[Plaga44.CharSplit]";

        // ---------- Menu entry ----------

        [MenuItem("Plaga44/Import/Character Split", priority = 100)]
        public static void ImportAllMenu()
        {
            var code = ImportAll();
            EditorUtility.DisplayDialog(
                "Character Split Import",
                code == 0 ? "Done. See Console for details." : "Failed. See Console.",
                "OK");
        }

        // ---------- Public entry (menu + batch) ----------

        public static int ImportAll()
        {
            try
            {
                if (!Directory.Exists(ImportsRoot))
                {
                    Debug.LogWarning($"{LogPrefix} No imports dir: {ImportsRoot}");
                    return ExitBatch(0);
                }

                int charCount = 0, partCount = 0, skipCount = 0;

                AssetDatabase.StartAssetEditing();
                try
                {
                    foreach (var packDir in Directory.GetDirectories(ImportsRoot))
                    {
                        var packName = Path.GetFileName(packDir);
                        Debug.Log($"{LogPrefix} Pack: {packName}");

                        foreach (var charDir in Directory.GetDirectories(packDir))
                        {
                            var partsDir = Path.Combine(charDir, "parts");
                            if (!Directory.Exists(partsDir))
                            {
                                Debug.LogWarning($"{LogPrefix}   skip (no parts/): {charDir}");
                                skipCount++;
                                continue;
                            }
                            var n = ProcessCharacter(packName, charDir, partsDir);
                            if (n > 0)
                            {
                                charCount++;
                                partCount += n;
                            }
                            else
                            {
                                skipCount++;
                            }
                        }
                    }
                }
                finally
                {
                    AssetDatabase.StopAssetEditing();
                    AssetDatabase.SaveAssets();
                    AssetDatabase.Refresh();
                }

                Debug.Log($"{LogPrefix} SUMMARY: {charCount} characters, {partCount} parts, {skipCount} skipped.");
                return ExitBatch(0);
            }
            catch (Exception ex)
            {
                Debug.LogError($"{LogPrefix} FAILED: {ex}");
                return ExitBatch(1);
            }
        }

        // ---------- Per-character processing ----------

        private static int ProcessCharacter(string packName, string charDir, string partsDir)
        {
            var charName = Path.GetFileName(charDir);
            Debug.Log($"{LogPrefix}   Character: {charName}");

            var objFiles = Directory.GetFiles(partsDir, "*.obj");
            if (objFiles.Length == 0)
            {
                Debug.LogWarning($"{LogPrefix}     no .obj files in {partsDir}");
                return 0;
            }
            Array.Sort(objFiles, StringComparer.Ordinal);

            // Configure import settings per OBJ (static mesh, no rig, unit scale)
            foreach (var obj in objFiles)
            {
                var rel = AbsToAssetPath(obj);
                if (string.IsNullOrEmpty(rel)) continue;

                // Force reimport via AssetDatabase so ModelImporter is available
                AssetDatabase.ImportAsset(rel, ImportAssetOptions.ForceSynchronousImport);
                var imp = AssetImporter.GetAtPath(rel) as ModelImporter;
                if (imp != null)
                {
                    bool dirty = false;
                    if (imp.globalScale != 1f) { imp.globalScale = 1f; dirty = true; }
                    if (imp.importBlendShapes) { imp.importBlendShapes = false; dirty = true; }
                    if (imp.importAnimation) { imp.importAnimation = false; dirty = true; }
                    if (imp.addCollider) { imp.addCollider = false; dirty = true; }
                    if (imp.materialImportMode != ModelImporterMaterialImportMode.None)
                    {
                        imp.materialImportMode = ModelImporterMaterialImportMode.None;
                        dirty = true;
                    }
                    if (imp.animationType != ModelImporterAnimationType.None)
                    {
                        imp.animationType = ModelImporterAnimationType.None;
                        dirty = true;
                    }
                    if (dirty) imp.SaveAndReimport();
                }
            }

            // Build assembled root
            var root = new GameObject(charName);
            root.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            root.transform.localScale = Vector3.one;

            int attached = 0;
            foreach (var obj in objFiles)
            {
                var rel = AbsToAssetPath(obj);
                if (string.IsNullOrEmpty(rel)) continue;
                var meshAsset = AssetDatabase.LoadAssetAtPath<GameObject>(rel);
                if (meshAsset == null)
                {
                    Debug.LogWarning($"{LogPrefix}     load failed: {rel}");
                    continue;
                }
                var inst = (GameObject)PrefabUtility.InstantiatePrefab(meshAsset);
                if (inst == null) continue;
                inst.transform.SetParent(root.transform, worldPositionStays: false);
                inst.name = Path.GetFileNameWithoutExtension(obj);
                attached++;
            }

            // Save as prefab
            var prefabPackDir = $"{PrefabsRoot}/{packName}";
            if (!Directory.Exists(prefabPackDir)) Directory.CreateDirectory(prefabPackDir);
            // Make sure Unity sees the dir as an asset folder
            AssetDatabase.ImportAsset(prefabPackDir, ImportAssetOptions.ForceSynchronousImport);

            var prefabPath = $"{prefabPackDir}/{charName}.prefab";
            var savedPrefab = PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
            UnityEngine.Object.DestroyImmediate(root);

            if (savedPrefab == null)
            {
                Debug.LogError($"{LogPrefix}     FAILED to save prefab: {prefabPath}");
                return 0;
            }

            Debug.Log($"{LogPrefix}     parts: {attached} -> {prefabPath}");
            return attached;
        }

        // ---------- Helpers ----------

        /// <summary>Converts absolute path to Assets-relative path (required by AssetDatabase).</summary>
        private static string AbsToAssetPath(string abs)
        {
            var full = Path.GetFullPath(abs).Replace('\\', '/');
            var projRoot = Path.GetFullPath(".").Replace('\\', '/');
            if (full.StartsWith(projRoot, StringComparison.OrdinalIgnoreCase))
            {
                var tail = full.Substring(projRoot.Length);
                if (tail.StartsWith("/")) tail = tail.Substring(1);
                return tail;
            }
            return null;
        }

        private static int ExitBatch(int code)
        {
            if (Application.isBatchMode)
            {
                // Must happen AFTER StopAssetEditing + SaveAssets, which is guaranteed by try/finally above.
                EditorApplication.Exit(code);
            }
            return code;
        }
    }
}
