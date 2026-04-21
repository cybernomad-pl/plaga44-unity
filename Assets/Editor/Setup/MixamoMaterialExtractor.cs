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
            // Retarget live z Questa -- klipy animacji z FBX niepotrzebne.
            // Ich obecnosc powoduje "Rig Error: Bone length in configuration does
            // not match position in animation file" bo klipy maja absolute positions
            // z eksportu Mixamo, a avatar regenerowany (CreateFromThisModel) ma
            // swieze bone lengths. Usunac klipy = usunac mismatch.
            if (importer.importAnimation)
            {
                importer.importAnimation = false;
                changed = true;
            }
            // Zachowaj pelna hierarchie bones -- retargeter SDK wymaga dostepu
            // do transformow per-bone. Optimize zwija je do bind pose binding.
            if (importer.optimizeGameObjects)
            {
                importer.optimizeGameObjects = false;
                changed = true;
            }
            // VRAM optymalizacja Quest -- mesh nie musi byc CPU-readable.
            if (importer.isReadable)
            {
                importer.isReadable = false;
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

                // ZERO FALLBACK (CLAUDE.md). Jesli material nie ma _MainTex/_BaseMap/_BumpMap
                // przypisane explicite -> nie zgaduj po nazwach plikow. Brak textury = log
                // warning, Borys decyduje co z tym zrobic.
                if (diffuse == null)
                    Debug.LogWarning($"{LOG} '{mat.name}': brak _MainTex/_BaseMap binding -- renderowanie bez textury diffuse");

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
