// =============================================================================
// AvatarImport.cs
// CYBERNOMAD -- Avatar import pipeline (DAE -> URP/Lit Specular -> Prefab -> Registry).
// AssetPostprocessor dla Assets/PLAGA44/Avatars/*. Aktualizuje AvatarRegistry.asset
// w Assets/PLAGA44/Resources/ zeby runtime Gallery go znalazla.
// =============================================================================
#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using Plaga44;

namespace Plaga44.Editor
{
    /// <summary>
    /// Auto-import avatarow rigged w Assets/PLAGA44/Avatars/.
    ///
    /// Konwencja folderu:
    ///   Assets/PLAGA44/Avatars/&lt;Name&gt;/&lt;Name&gt;.dae
    ///   Assets/PLAGA44/Avatars/&lt;Name&gt;/textures/&lt;Name&gt;_packed0_diffuse.png
    ///   Assets/PLAGA44/Avatars/&lt;Name&gt;/textures/&lt;Name&gt;_packed0_normal.png
    ///   Assets/PLAGA44/Avatars/&lt;Name&gt;/textures/&lt;Name&gt;_packed0_specular.png
    ///   Assets/PLAGA44/Avatars/&lt;Name&gt;/textures/&lt;Name&gt;_packed0_gloss.png
    ///
    /// Co robi:
    ///   1. Ustawia TextureImporter (sRGB, NormalMap, Linear dla gloss).
    ///   2. Ustawia ModelImporter (Generic rig, materials=None).
    ///   3. Scala specular (RGB) + gloss (R) -> _SpecGloss (RGBA).
    ///   4. Tworzy URP/Lit Specular material.
    ///   5. Tworzy prefab variant z przypietym materialem (dla WSZYSTKICH Renderers).
    ///   6. Aktualizuje Assets/PLAGA44/Resources/AvatarRegistry.asset.
    ///
    /// Triggery:
    ///   - AssetPostprocessor -- przy wrzuceniu plikow
    ///   - [InitializeOnLoad] -- skan przy starcie edytora
    ///   - Menu: CYBERNOMAD > Import > Rescan Avatars (force)
    /// </summary>
    public static class AvatarImportConfig
    {
        public const string AvatarsRoot = "Assets/PLAGA44/Avatars";
        public const string AnimationsRoot = "Assets/PLAGA44/Animations";
        public const string ResourcesRoot = "Assets/PLAGA44/Resources";
        public const string RegistryPath = ResourcesRoot + "/AvatarRegistry.asset";
        public const string LOG = "[PLAGA44][AvatarImport]";
    }

    // =========================================================================
    // PRE-PROCESSOR: Texture import settings
    // =========================================================================
    public class AvatarTexturePreprocessor : AssetPostprocessor
    {
        void OnPreprocessTexture()
        {
            if (!assetPath.StartsWith(AvatarImportConfig.AvatarsRoot)) return;
            var imp = (TextureImporter)assetImporter;
            string fn = Path.GetFileNameWithoutExtension(assetPath).ToLowerInvariant();

            if (fn.EndsWith("_normal"))
            {
                imp.textureType = TextureImporterType.NormalMap;
                imp.sRGBTexture = false;
            }
            else if (fn.EndsWith("_gloss"))
            {
                imp.textureType = TextureImporterType.Default;
                imp.sRGBTexture = false;
            }
            else if (fn.EndsWith("_diffuse") || fn.EndsWith("_specular"))
            {
                imp.textureType = TextureImporterType.Default;
                imp.sRGBTexture = true;
            }
            else if (fn.EndsWith("_specgloss"))
            {
                imp.textureType = TextureImporterType.Default;
                imp.sRGBTexture = true;
                imp.alphaSource = TextureImporterAlphaSource.FromInput;
                imp.alphaIsTransparency = false;
            }
            else
            {
                return;
            }
            imp.isReadable = true;
            imp.mipmapEnabled = true;
        }
    }

    // =========================================================================
    // PRE-PROCESSOR: model import settings (DAE Generic + FBX Humanoid Mixamo)
    // Dziala dla Assets/PLAGA44/Avatars/ i Assets/PLAGA44/Animations/.
    // =========================================================================
    public class AvatarModelPreprocessor : AssetPostprocessor
    {
        void OnPreprocessModel()
        {
            bool isAvatar = assetPath.StartsWith(AvatarImportConfig.AvatarsRoot);
            bool isAnimation = assetPath.StartsWith(AvatarImportConfig.AnimationsRoot);
            if (!isAvatar && !isAnimation) return;

            var mi = (ModelImporter)assetImporter;
            string path = assetPath.ToLowerInvariant();

            if (path.EndsWith(".fbx"))
            {
                // Mixamo FBX convention -- Humanoid rig, 1m scale (Mixamo exports in cm)
                mi.animationType = ModelImporterAnimationType.Human;
                mi.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
                mi.globalScale = 1f;
                mi.useFileScale = false;
                mi.importBlendShapes = true;
                mi.importCameras = false;
                mi.importLights = false;
                mi.importVisibility = true;

                if (isAvatar)
                {
                    // Dla postaci: importuj materialy embedded (Mixamo nonPBR standard)
                    mi.materialImportMode = ModelImporterMaterialImportMode.ImportStandard;
                    mi.materialLocation = ModelImporterMaterialLocation.InPrefab;
                    mi.optimizeMeshPolygons = true;
                    mi.optimizeMeshVertices = true;
                }
                else
                {
                    // Dla animacji: bez mesh/materials, tylko clipy
                    mi.materialImportMode = ModelImporterMaterialImportMode.None;
                    mi.importAnimation = true;
                }
            }
            else if (path.EndsWith(".dae"))
            {
                // DAE z Blendera / Collada -- zazwyczaj Generic rig, nie-Mixamo
                mi.animationType = ModelImporterAnimationType.Generic;
                mi.materialImportMode = ModelImporterMaterialImportMode.None;
                mi.importCameras = false;
                mi.importLights = false;
                mi.importVisibility = true;
                mi.importBlendShapes = true;
                mi.optimizeMeshPolygons = true;
                mi.optimizeMeshVertices = true;
            }
        }
    }

    // =========================================================================
    // POST-PROCESSOR: rebuild for affected avatars
    // =========================================================================
    public class AvatarMaterialPostprocessor : AssetPostprocessor
    {
        static void OnPostprocessAllAssets(
            string[] imported, string[] deleted, string[] moved, string[] movedFromPath)
        {
            if (AvatarBuilder.IsBuilding) return;

            var folders = new HashSet<string>();
            foreach (var p in imported) AddFolder(p, folders);
            foreach (var p in moved) AddFolder(p, folders);

            if (folders.Count == 0) return;

            bool anyBuilt = false;
            foreach (var folder in folders)
                if (AvatarBuilder.Build(folder)) anyBuilt = true;

            if (anyBuilt) AvatarRegistryBuilder.Rebuild();
        }

        static void AddFolder(string assetPath, HashSet<string> set)
        {
            if (!assetPath.StartsWith(AvatarImportConfig.AvatarsRoot + "/")) return;

            // Ignoruj nasze wlasne artefakty -- inaczej nieskonczona petla
            string fn = Path.GetFileName(assetPath).ToLowerInvariant();
            if (fn.EndsWith(".prefab")) return;
            if (fn.EndsWith("_mat.mat")) return;
            if (fn.EndsWith("_specgloss.png")) return;

            var rel = assetPath.Substring(AvatarImportConfig.AvatarsRoot.Length + 1);
            int slash = rel.IndexOf('/');
            if (slash < 0) return;
            set.Add(AvatarImportConfig.AvatarsRoot + "/" + rel.Substring(0, slash));
        }
    }

    // =========================================================================
    // INITIALIZE ON LOAD: scan at editor start
    // =========================================================================
    [InitializeOnLoad]
    public static class AvatarAutoImport
    {
        static AvatarAutoImport()
        {
            EditorApplication.delayCall += ScanAllSilent;
        }

        static void ScanAllSilent() => ScanAll(false);

        [MenuItem("CYBERNOMAD/Import/Rescan Avatars")]
        public static void ScanAllForce() => ScanAll(true);

        static void ScanAll(bool verbose)
        {
            if (!AssetDatabase.IsValidFolder(AvatarImportConfig.AvatarsRoot))
            {
                if (verbose) Debug.LogWarning($"{AvatarImportConfig.LOG} folder missing: {AvatarImportConfig.AvatarsRoot}");
                return;
            }

            AvatarBuilder.IsBuilding = true;
            try
            {
                int built = 0;
                foreach (var dir in Directory.GetDirectories(AvatarImportConfig.AvatarsRoot))
                {
                    string unityPath = dir.Replace('\\', '/');
                    if (AvatarBuilder.Build(unityPath, force: verbose)) built++;
                }
                if (verbose || built > 0)
                {
                    AvatarRegistryBuilder.Rebuild();
                    AssetDatabase.SaveAssets();
                    AssetDatabase.Refresh();
                    Debug.Log($"{AvatarImportConfig.LOG} Rescan done. Avatars processed: {built}");
                }
            }
            finally
            {
                AvatarBuilder.IsBuilding = false;
            }
        }
    }

    // =========================================================================
    // REGISTRY BUILDER
    // =========================================================================
    public static class AvatarRegistryBuilder
    {
        public static void Rebuild()
        {
            EnsureResourcesFolder();

            var reg = AssetDatabase.LoadAssetAtPath<AvatarRegistry>(AvatarImportConfig.RegistryPath);
            if (reg == null)
            {
                reg = ScriptableObject.CreateInstance<AvatarRegistry>();
                AssetDatabase.CreateAsset(reg, AvatarImportConfig.RegistryPath);
                Debug.Log($"{AvatarImportConfig.LOG} AvatarRegistry created: {AvatarImportConfig.RegistryPath}");
            }

            reg.avatars = new List<AvatarRegistry.Entry>();
            if (!AssetDatabase.IsValidFolder(AvatarImportConfig.AvatarsRoot))
            {
                EditorUtility.SetDirty(reg);
                return;
            }

            int brokenCount = 0;
            foreach (var dir in Directory.GetDirectories(AvatarImportConfig.AvatarsRoot))
            {
                string unityPath = dir.Replace('\\', '/');
                string name = Path.GetFileName(unityPath);
                string prefabPath = $"{unityPath}/{name}.prefab";
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
                if (prefab == null) continue;

                var entry = new AvatarRegistry.Entry { name = name, prefab = prefab };
                ValidateRig(prefab, entry);
                if (entry.broken)
                {
                    brokenCount++;
                    Debug.LogWarning($"{AvatarImportConfig.LOG} BROKEN avatar '{name}': {entry.errorMessage}");
                }
                reg.avatars.Add(entry);
            }

            EditorUtility.SetDirty(reg);
            Debug.Log($"{AvatarImportConfig.LOG} Registry rebuilt -- {reg.avatars.Count} avatars (broken={brokenCount})");
        }

        // Sprawdz czy prefab ma valid Humanoid avatar (lub Generic z mesh).
        // Broken == brak Animator albo Animator.avatar == null/!isValid.
        static void ValidateRig(GameObject prefab, AvatarRegistry.Entry entry)
        {
            var animator = prefab.GetComponentInChildren<Animator>(true);
            if (animator == null)
            {
                // Brak Animator -- moze byc OK dla Generic mesh, ale jak nie ma renderer to broken
                var renderer = prefab.GetComponentInChildren<Renderer>(true);
                if (renderer == null)
                {
                    entry.broken = true;
                    entry.errorMessage = "no Animator and no Renderer";
                }
                return;
            }
            if (animator.avatar == null)
            {
                entry.broken = true;
                entry.errorMessage = "Animator.avatar is null (rig import failed)";
                return;
            }
            if (!animator.avatar.isValid)
            {
                entry.broken = true;
                entry.errorMessage = "Animator.avatar invalid (rig structure broken)";
                return;
            }
            // Sprawdz czy ma SkinnedMeshRenderer (dla zywych avatarow)
            var smr = prefab.GetComponentInChildren<SkinnedMeshRenderer>(true);
            if (smr == null)
            {
                entry.broken = true;
                entry.errorMessage = "no SkinnedMeshRenderer (mesh missing or unrigged)";
            }
        }

        static void EnsureResourcesFolder()
        {
            if (AssetDatabase.IsValidFolder(AvatarImportConfig.ResourcesRoot)) return;
            AssetDatabase.CreateFolder("Assets/PLAGA44", "Resources");
        }
    }

    // =========================================================================
    // CORE BUILDER
    // =========================================================================
    public static class AvatarBuilder
    {
        /// <summary>Global flag: true gdy jestesmy w srodku budowania -- zapobiega reentrantnym postprocess callom.</summary>
        public static bool IsBuilding;

        public static bool Build(string folder, bool force = false)
        {
            string name = Path.GetFileName(folder);

            // Szukaj modelu: preferuj DAE (pipeline PBR), potem FBX (Mixamo embedded materials)
            string modelPathDae = $"{folder}/{name}.dae";
            string modelPathFbx = $"{folder}/{name}.fbx";
            string modelPath = null;
            bool isDae = false;
            if (File.Exists(modelPathDae)) { modelPath = modelPathDae; isDae = true; }
            else if (File.Exists(modelPathFbx)) { modelPath = modelPathFbx; isDae = false; }
            else return false;

            string prefabPath = $"{folder}/{name}.prefab";

            bool wasBuilding = IsBuilding;
            IsBuilding = true;
            try
            {
                string matPath = null;

                if (isDae)
                {
                    // === DAE pipeline: scal spec+gloss, zbuduj URP/Lit Specular material ===
                    string texDir = $"{folder}/textures";
                    string tDiffuse = $"{texDir}/{name}_packed0_diffuse.png";
                    string tNormal = $"{texDir}/{name}_packed0_normal.png";
                    string tSpec = $"{texDir}/{name}_packed0_specular.png";
                    string tGloss = $"{texDir}/{name}_packed0_gloss.png";
                    string tSpecGloss = $"{texDir}/{name}_SpecGloss.png";
                    matPath = $"{folder}/{name}_Mat.mat";

                    if (File.Exists(tSpec) && File.Exists(tGloss))
                    {
                        bool needRebuild = force || !File.Exists(tSpecGloss)
                            || File.GetLastWriteTimeUtc(tSpecGloss) < File.GetLastWriteTimeUtc(tSpec)
                            || File.GetLastWriteTimeUtc(tSpecGloss) < File.GetLastWriteTimeUtc(tGloss);
                        if (needRebuild)
                        {
                            CombineSpecGloss(tSpec, tGloss, tSpecGloss);
                            AssetDatabase.ImportAsset(tSpecGloss, ImportAssetOptions.ForceUpdate);
                        }
                    }

                    bool matExists = File.Exists(matPath);
                    CreateOrUpdateMaterial(matPath, tDiffuse, tNormal, tSpecGloss, updateAll: !matExists || force);
                    if (!matExists) Debug.Log($"{AvatarImportConfig.LOG} material created: {matPath}");
                }
                // Dla FBX: materialy juz w modelu (Mixamo embedded, InPrefab). Tylko prefab wariant.

                CreateOrUpdatePrefab(modelPath, matPath, prefabPath, force);

                return true;
            }
            finally
            {
                IsBuilding = wasBuilding;
            }
        }

        // ---------- SpecGloss combine ----------
        static void CombineSpecGloss(string specPath, string glossPath, string outPath)
        {
            try
            {
                var spec = LoadPng(specPath);
                var gloss = LoadPng(glossPath);
                if (spec == null || gloss == null) return;

                int w = spec.width;
                int h = spec.height;
                Texture2D glossUsed = gloss;
                if (gloss.width != w || gloss.height != h)
                {
                    glossUsed = Resize(gloss, w, h);
                    Object.DestroyImmediate(gloss);
                }

                var specPx = spec.GetPixels32();
                var glossPx = glossUsed.GetPixels32();
                var outPx = new Color32[w * h];
                for (int i = 0; i < outPx.Length; i++)
                {
                    outPx[i] = new Color32(specPx[i].r, specPx[i].g, specPx[i].b, glossPx[i].r);
                }

                var combined = new Texture2D(w, h, TextureFormat.RGBA32, false);
                combined.SetPixels32(outPx);
                combined.Apply();
                File.WriteAllBytes(outPath, combined.EncodeToPNG());

                Object.DestroyImmediate(spec);
                Object.DestroyImmediate(glossUsed);
                Object.DestroyImmediate(combined);
                Debug.Log($"{AvatarImportConfig.LOG} SpecGloss combined: {w}x{h} -> {outPath}");
            }
            catch (System.Exception e)
            {
                Debug.LogError($"{AvatarImportConfig.LOG} SpecGloss combine failed: {e.Message}");
            }
        }

        static Texture2D LoadPng(string path)
        {
            byte[] bytes = File.ReadAllBytes(path);
            var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            if (!tex.LoadImage(bytes)) { Object.DestroyImmediate(tex); return null; }
            return tex;
        }

        static Texture2D Resize(Texture2D src, int w, int h)
        {
            var rt = RenderTexture.GetTemporary(w, h, 0, RenderTextureFormat.ARGB32);
            Graphics.Blit(src, rt);
            var prev = RenderTexture.active;
            RenderTexture.active = rt;
            var dst = new Texture2D(w, h, TextureFormat.RGBA32, false);
            dst.ReadPixels(new Rect(0, 0, w, h), 0, 0);
            dst.Apply();
            RenderTexture.active = prev;
            RenderTexture.ReleaseTemporary(rt);
            return dst;
        }

        // ---------- Material ----------
        static void CreateOrUpdateMaterial(
            string matPath, string tDiffuse, string tNormal, string tSpecGloss, bool updateAll)
        {
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null)
            {
                Debug.LogError($"{AvatarImportConfig.LOG} URP/Lit shader not found");
                return;
            }

            var mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
            bool created = false;
            if (mat == null)
            {
                mat = new Material(shader);
                AssetDatabase.CreateAsset(mat, matPath);
                created = true;
            }

            if (created || updateAll)
            {
                mat.shader = shader;
                mat.SetFloat("_WorkflowMode", 1f); // 0=Metallic, 1=Specular
                mat.SetFloat("_SmoothnessTextureChannel", 0f);
                mat.SetFloat("_Smoothness", 0.5f);
                mat.SetFloat("_SpecularHighlights", 1f);
                mat.SetFloat("_EnvironmentReflections", 1f);
                mat.SetFloat("_BumpScale", 1f);
                mat.SetColor("_BaseColor", Color.white);
                mat.SetColor("_SpecColor", Color.white);
            }

            var diffuse = AssetDatabase.LoadAssetAtPath<Texture2D>(tDiffuse);
            var normal = AssetDatabase.LoadAssetAtPath<Texture2D>(tNormal);
            var specGloss = AssetDatabase.LoadAssetAtPath<Texture2D>(tSpecGloss);

            if (diffuse != null) mat.SetTexture("_BaseMap", diffuse);
            if (normal != null)
            {
                mat.SetTexture("_BumpMap", normal);
                mat.EnableKeyword("_NORMALMAP");
            }
            if (specGloss != null)
            {
                mat.SetTexture("_SpecGlossMap", specGloss);
                mat.EnableKeyword("_SPECGLOSSMAP");
                mat.EnableKeyword("_METALLICSPECGLOSSMAP");
            }

            EditorUtility.SetDirty(mat);
        }

        // ---------- Prefab variant ----------
        // matPath moze byc null -- wtedy prefab uzywa materialow embedded w modelu (FBX Mixamo).
        static void CreateOrUpdatePrefab(string modelPath, string matPath, string prefabPath, bool force)
        {
            // Skip if prefab is up-to-date
            if (File.Exists(prefabPath) && !force)
            {
                var prefabTime = File.GetLastWriteTimeUtc(prefabPath);
                var modelTime = File.GetLastWriteTimeUtc(modelPath);
                var matTime = (!string.IsNullOrEmpty(matPath) && File.Exists(matPath))
                    ? File.GetLastWriteTimeUtc(matPath) : prefabTime;
                if (prefabTime >= modelTime && prefabTime >= matTime)
                {
                    return;
                }
            }

            var modelAsset = AssetDatabase.LoadAssetAtPath<GameObject>(modelPath);
            if (modelAsset == null)
            {
                Debug.LogWarning($"{AvatarImportConfig.LOG} cannot load model for prefab: {modelPath}");
                return;
            }

            Material material = null;
            if (!string.IsNullOrEmpty(matPath))
            {
                material = AssetDatabase.LoadAssetAtPath<Material>(matPath);
                if (material == null)
                {
                    Debug.LogWarning($"{AvatarImportConfig.LOG} material missing for prefab (will keep embedded): {matPath}");
                }
            }

            var instance = (GameObject)PrefabUtility.InstantiatePrefab(modelAsset);
            if (instance == null)
            {
                Debug.LogError($"{AvatarImportConfig.LOG} InstantiatePrefab failed: {modelPath}");
                return;
            }

            try
            {
                // Jesli mamy zewnetrzny material (DAE pipeline) -- podmien wszystkie sloty.
                // Jesli brak -- zostaw embedded (FBX Mixamo).
                if (material != null)
                {
                    var renderers = instance.GetComponentsInChildren<Renderer>(includeInactive: true);
                    foreach (var r in renderers)
                    {
                        var mats = r.sharedMaterials;
                        for (int i = 0; i < mats.Length; i++) mats[i] = material;
                        r.sharedMaterials = mats;
                    }
                }

                PrefabUtility.SaveAsPrefabAsset(instance, prefabPath, out bool ok);
                if (ok) Debug.Log($"{AvatarImportConfig.LOG} prefab saved: {prefabPath}");
                else Debug.LogError($"{AvatarImportConfig.LOG} SaveAsPrefabAsset failed: {prefabPath}");
            }
            finally
            {
                Object.DestroyImmediate(instance);
            }
        }
    }
}
#endif
