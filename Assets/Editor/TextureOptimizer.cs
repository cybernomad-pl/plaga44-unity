using UnityEditor;
using UnityEngine;
using System.IO;

/// <summary>
/// Forces all textures to use ASTC 6x6 compression
/// and max 2048 resolution on Android (Quest 3).
/// Run from menu: PLAGA44 > Optimize Textures for Quest
/// Also runs automatically during build via BuildScript.
/// </summary>
public static class TextureOptimizer
{
    private const int MAX_SIZE_QUEST = 2048;
    private const TextureImporterFormat FORMAT_QUEST = TextureImporterFormat.ASTC_6x6;

    [MenuItem("PLAGA44/Optimize Textures for Quest")]
    public static void OptimizeAll()
    {
        string[] guids = AssetDatabase.FindAssets("t:Texture2D", new[] { "Assets" });
        int modified = 0;

        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null) continue;

            // Get or create Android platform settings
            var android = importer.GetPlatformTextureSettings("Android");

            bool needsUpdate = false;

            if (!android.overridden)
            {
                android.overridden = true;
                needsUpdate = true;
            }

            if (android.maxTextureSize > MAX_SIZE_QUEST)
            {
                android.maxTextureSize = MAX_SIZE_QUEST;
                needsUpdate = true;
            }

            if (android.format != FORMAT_QUEST)
            {
                android.format = FORMAT_QUEST;
                needsUpdate = true;
            }

            if (needsUpdate)
            {
                importer.SetPlatformTextureSettings(android);
                importer.SaveAndReimport();
                modified++;
            }
        }

        Debug.Log($"[TextureOptimizer] Optimized {modified}/{guids.Length} textures for Quest (ASTC 6x6, max {MAX_SIZE_QUEST})");
    }
}
