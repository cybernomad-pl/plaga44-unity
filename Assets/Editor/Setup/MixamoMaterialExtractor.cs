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
// Auto -- wywolywane przez AvatarRegistrySetup przed ScanAllForce.
// Bez menu item (polityka "wszystko automatycznie przy bootstrap").
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

        public static int ExtractAll()
        {
            if (!AssetDatabase.IsValidFolder(AvatarsRoot))
            {
                Debug.LogWarning($"{LOG} Folder missing: {AvatarsRoot}");
                return 0;
            }

            int extracted = 0;
            string[] guids = AssetDatabase.FindAssets("t:Model", new[] { AvatarsRoot });
            foreach (var guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (!path.EndsWith(".fbx", System.StringComparison.OrdinalIgnoreCase)) continue;

                if (ExtractOne(path)) extracted++;
            }

            if (extracted > 0)
            {
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
            }
            Debug.Log($"{LOG} Extracted textures/materials from {extracted} FBX files.");
            return extracted;
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
            //    + wymusz Humanoid + CreateFromThisModel (regeneracja avatara z T-pose
            //    tego konkretnego FBX, unika "Rig Error: Avatar Configuration mismatch").
            bool changed = false;
            if (importer.materialLocation != ModelImporterMaterialLocation.External)
            {
                importer.materialLocation = ModelImporterMaterialLocation.External;
                changed = true;
            }
            if (importer.animationType != ModelImporterAnimationType.Human)
            {
                importer.animationType = ModelImporterAnimationType.Human;
                changed = true;
            }
            if (importer.avatarSetup != ModelImporterAvatarSetup.CreateFromThisModel)
            {
                importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
                changed = true;
            }
            if (changed)
            {
                importer.SaveAndReimport();
                Debug.Log($"{LOG} [CFG] Humanoid + External materials + CreateFromThisModel: {fbxPath}");
            }

            AssetDatabase.ImportAsset(fbxPath, ImportAssetOptions.ForceUpdate);

            // 3. Post-process: converted/external materials moga miec Autodesk/Standard
            //    shader -> swap na URP/Lit i re-bind textures. Bez tego = rozowy render.
            ConvertMaterialsToUrp(folder, texFolder);

            Debug.Log($"{LOG} [OK] {Path.GetFileName(fbxPath)}");
            return true;
        }

        // ---- URP material conversion ----------------------------------------

        private static void ConvertMaterialsToUrp(string avatarFolder, string texFolder)
        {
            var urp = Shader.Find("Universal Render Pipeline/Lit");
            if (urp == null)
            {
                Debug.LogError($"{LOG} URP/Lit shader not found");
                return;
            }

            string[] matGuids = AssetDatabase.FindAssets("t:Material", new[] { avatarFolder });
            foreach (var g in matGuids)
            {
                string path = AssetDatabase.GUIDToAssetPath(g);
                var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (mat == null) continue;
                if (mat.shader != null && mat.shader.name == urp.name) continue; // already URP

                var diffuse = mat.HasProperty("_MainTex") ? mat.GetTexture("_MainTex") : null;
                if (diffuse == null && mat.HasProperty("_BaseMap")) diffuse = mat.GetTexture("_BaseMap");
                var normal  = mat.HasProperty("_BumpMap") ? mat.GetTexture("_BumpMap") : null;
                var specGloss = mat.HasProperty("_SpecGlossMap") ? mat.GetTexture("_SpecGlossMap")
                              : (mat.HasProperty("_MetallicGlossMap") ? mat.GetTexture("_MetallicGlossMap") : null);

                // Fallback: szukaj textures po nazwie w Textures/ folderze
                if (diffuse == null) diffuse = FindTexture(texFolder, mat.name, new[] { "diffuse", "albedo", "basecolor", "color" });
                if (normal  == null) normal  = FindTexture(texFolder, mat.name, new[] { "normal", "nrm", "_n" });
                if (specGloss == null) specGloss = FindTexture(texFolder, mat.name, new[] { "specular", "spec", "metallic", "gloss" });

                mat.shader = urp;
                // Metallic workflow (domyslny URP/Lit -- bez _SPECULAR_SETUP)
                mat.SetFloat("_WorkflowMode", 0f);
                mat.SetFloat("_Smoothness", 0.5f);
                mat.SetColor("_BaseColor", Color.white);
                if (diffuse != null) mat.SetTexture("_BaseMap", diffuse);
                if (normal != null)
                {
                    mat.SetTexture("_BumpMap", normal);
                    mat.EnableKeyword("_NORMALMAP");
                }
                if (specGloss != null)
                {
                    mat.SetTexture("_MetallicGlossMap", specGloss);
                    mat.EnableKeyword("_METALLICSPECGLOSSMAP");
                }

                EditorUtility.SetDirty(mat);
                Debug.Log($"{LOG} [URP] {mat.name} -> URP/Lit (diff={(diffuse != null)} nrm={(normal != null)} spec={(specGloss != null)})");
            }
        }

        private static Texture FindTexture(string texFolder, string matName, string[] keywords)
        {
            if (!AssetDatabase.IsValidFolder(texFolder)) return null;
            string[] guids = AssetDatabase.FindAssets("t:Texture2D", new[] { texFolder });
            Texture best = null;
            foreach (var g in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(g);
                string lower = Path.GetFileNameWithoutExtension(path).ToLowerInvariant();
                foreach (var kw in keywords)
                {
                    if (lower.Contains(kw))
                    {
                        var t = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
                        if (t != null) { best = t; break; }
                    }
                }
                if (best != null) break;
            }
            return best;
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
