// =============================================================================
// AvatarImport.cs
// CYBERNOMAD -- Avatar import pipeline (DAE -> URP/Lit Specular -> Prefab -> Registry).
// Dziala dla Assets/PLAGA44/Avatars/. FBX (Mixamo) -- Humanoid, embedded mats.
// DAE (Blender) -- Generic rig, scala spec+gloss, URP/Lit Specular.
// =============================================================================
#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using Plaga44;

namespace Plaga44.Editor
{
    // =========================================================================
    // Paths + suffixes (single source of truth)
    // =========================================================================
    public static class AvatarImportConfig
    {
        public const string AvatarsRoot = "Assets/PLAGA44/Avatars";
        public const string AnimationsRoot = "Assets/PLAGA44/Animations";
        public const string ResourcesRoot = "Assets/PLAGA44/Resources";
        public const string ResourcesParent = "Assets/PLAGA44";
        public const string ResourcesFolderName = "Resources";
        public const string RegistryPath = ResourcesRoot + "/AvatarRegistry.asset";
        public const string LOG = "[PLAGA44][AvatarImport]";

        // Texture naming convention -- Substance/Blender export (packed0)
        public const string SuffixDiffuse = "_packed0_diffuse";
        public const string SuffixNormal = "_packed0_normal";
        public const string SuffixSpecular = "_packed0_specular";
        public const string SuffixGloss = "_packed0_gloss";
        public const string SuffixSpecGloss = "_SpecGloss";
        public const string TextureExt = ".png";

        // File extensions
        public const string ExtDae = ".dae";
        public const string ExtFbx = ".fbx";
        public const string ExtPrefab = ".prefab";
        public const string ExtMaterial = "_Mat.mat";
        public const string TexturesFolder = "textures";
    }

    // =========================================================================
    // URP/Lit material constants
    // =========================================================================
    internal static class UrpLit
    {
        public const string ShaderName = "Universal Render Pipeline/Lit";

        // Properties
        public const string WorkflowMode = "_WorkflowMode";
        public const string SmoothnessTextureChannel = "_SmoothnessTextureChannel";
        public const string Smoothness = "_Smoothness";
        public const string SpecularHighlights = "_SpecularHighlights";
        public const string EnvironmentReflections = "_EnvironmentReflections";
        public const string BumpScale = "_BumpScale";
        public const string BaseColor = "_BaseColor";
        public const string SpecColor = "_SpecColor";
        public const string BaseMap = "_BaseMap";
        public const string BumpMap = "_BumpMap";
        public const string SpecGlossMap = "_SpecGlossMap";

        // Keywords
        public const string KeywordNormalMap = "_NORMALMAP";
        public const string KeywordSpecGlossMap = "_SPECGLOSSMAP";
        public const string KeywordMetallicSpecGlossMap = "_METALLICSPECGLOSSMAP";

        // Workflow: 0=Metallic, 1=Specular
        public const float WorkflowSpecular = 1f;
        // Smoothness source: 0=SpecularAlpha, 1=AlbedoAlpha
        public const float SmoothnessFromSpecularAlpha = 0f;
    }

    // =========================================================================
    // Path helpers
    // =========================================================================
    internal static class AvatarPaths
    {
        public static string Model(string folder, string name, string ext) => $"{folder}/{name}{ext}";
        public static string Prefab(string folder, string name) => $"{folder}/{name}{AvatarImportConfig.ExtPrefab}";
        public static string Material(string folder, string name) => $"{folder}/{name}{AvatarImportConfig.ExtMaterial}";
        public static string Texture(string folder, string name, string suffix)
            => $"{folder}/{AvatarImportConfig.TexturesFolder}/{name}{suffix}{AvatarImportConfig.TextureExt}";
    }

    // =========================================================================
    // PRE-PROCESSOR: texture import settings
    // =========================================================================
    public class AvatarTexturePreprocessor : AssetPostprocessor
    {
        private void OnPreprocessTexture()
        {
            if (!assetPath.StartsWith(AvatarImportConfig.AvatarsRoot)) return;

            var imp = (TextureImporter)assetImporter;
            string fn = Path.GetFileNameWithoutExtension(assetPath).ToLowerInvariant();

            if (!TryConfigureByFilename(imp, fn)) return;
            imp.isReadable = true;
            imp.mipmapEnabled = true;
        }

        private static bool TryConfigureByFilename(TextureImporter imp, string fn)
        {
            if (fn.EndsWith("_normal")) { SetLinear(imp, TextureImporterType.NormalMap); return true; }
            if (fn.EndsWith("_gloss")) { SetLinear(imp, TextureImporterType.Default); return true; }
            if (fn.EndsWith("_diffuse") || fn.EndsWith("_specular")) { SetSRGB(imp); return true; }
            if (fn.EndsWith("_specgloss")) { SetSpecGloss(imp); return true; }
            return false;
        }

        private static void SetLinear(TextureImporter imp, TextureImporterType type)
        {
            imp.textureType = type;
            imp.sRGBTexture = false;
        }

        private static void SetSRGB(TextureImporter imp)
        {
            imp.textureType = TextureImporterType.Default;
            imp.sRGBTexture = true;
        }

        private static void SetSpecGloss(TextureImporter imp)
        {
            imp.textureType = TextureImporterType.Default;
            imp.sRGBTexture = true;
            imp.alphaSource = TextureImporterAlphaSource.FromInput;
            imp.alphaIsTransparency = false;
        }
    }

    // =========================================================================
    // PRE-PROCESSOR: model import settings (DAE Generic, FBX Mixamo Humanoid)
    // =========================================================================
    public class AvatarModelPreprocessor : AssetPostprocessor
    {
        private void OnPreprocessModel()
        {
            bool isAvatar = assetPath.StartsWith(AvatarImportConfig.AvatarsRoot);
            bool isAnimation = assetPath.StartsWith(AvatarImportConfig.AnimationsRoot);
            if (!isAvatar && !isAnimation) return;

            var mi = (ModelImporter)assetImporter;
            string path = assetPath.ToLowerInvariant();

            if (path.EndsWith(AvatarImportConfig.ExtFbx))
                ConfigureFbx(mi, isAvatar);
            else if (path.EndsWith(AvatarImportConfig.ExtDae))
                ConfigureDae(mi);
        }

        private static void ConfigureFbx(ModelImporter mi, bool isAvatar)
        {
            ApplyCommonImportFlags(mi);
            mi.animationType = ModelImporterAnimationType.Human;
            mi.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
            mi.globalScale = 1f;
            mi.useFileScale = false;

            if (isAvatar)
            {
                mi.materialImportMode = ModelImporterMaterialImportMode.ImportStandard;
                mi.materialLocation = ModelImporterMaterialLocation.InPrefab;
                mi.optimizeMeshPolygons = true;
                mi.optimizeMeshVertices = true;
            }
            else
            {
                mi.materialImportMode = ModelImporterMaterialImportMode.None;
                mi.importAnimation = true;
            }
        }

        private static void ConfigureDae(ModelImporter mi)
        {
            ApplyCommonImportFlags(mi);
            mi.animationType = ModelImporterAnimationType.Generic;
            mi.materialImportMode = ModelImporterMaterialImportMode.None;
            mi.optimizeMeshPolygons = true;
            mi.optimizeMeshVertices = true;
        }

        private static void ApplyCommonImportFlags(ModelImporter mi)
        {
            mi.importCameras = false;
            mi.importLights = false;
            mi.importVisibility = true;
            mi.importBlendShapes = true;
        }
    }

    // =========================================================================
    // POST-PROCESSOR: rebuild avatars touched by this import pass
    // =========================================================================
    public class AvatarMaterialPostprocessor : AssetPostprocessor
    {
        private static void OnPostprocessAllAssets(
            string[] imported, string[] _d, string[] moved, string[] _mf)
        {
            if (AvatarBuilder.IsBuilding) return;

            var folders = new HashSet<string>();
            foreach (var p in imported) AddTouchedFolder(p, folders);
            foreach (var p in moved) AddTouchedFolder(p, folders);
            if (folders.Count == 0) return;

            bool anyBuilt = false;
            foreach (var folder in folders)
                if (AvatarBuilder.Build(folder)) anyBuilt = true;

            if (anyBuilt) AvatarRegistryBuilder.Rebuild();
        }

        private static void AddTouchedFolder(string assetPath, HashSet<string> set)
        {
            if (!assetPath.StartsWith(AvatarImportConfig.AvatarsRoot + "/")) return;
            if (IsBuilderArtefact(assetPath)) return; // inaczej nieskonczona petla

            var rel = assetPath.Substring(AvatarImportConfig.AvatarsRoot.Length + 1);
            int slash = rel.IndexOf('/');
            if (slash < 0) return;
            set.Add(AvatarImportConfig.AvatarsRoot + "/" + rel.Substring(0, slash));
        }

        private static bool IsBuilderArtefact(string assetPath)
        {
            string fn = Path.GetFileName(assetPath).ToLowerInvariant();
            return fn.EndsWith(".prefab") || fn.EndsWith("_mat.mat") || fn.EndsWith("_specgloss.png");
        }
    }

    // =========================================================================
    // INITIALIZE ON LOAD: scan all avatars at editor start + menu
    // =========================================================================
    [InitializeOnLoad]
    public static class AvatarAutoImport
    {
        static AvatarAutoImport() => EditorApplication.delayCall += ScanAllSilent;

        private static void ScanAllSilent() => ScanAll(verbose: false);

        [MenuItem("CYBERNOMAD/Import/Rescan Avatars")]
        public static void ScanAllForce() => ScanAll(verbose: true);

        private static void ScanAll(bool verbose)
        {
            if (!AssetDatabase.IsValidFolder(AvatarImportConfig.AvatarsRoot))
            {
                if (verbose) Debug.LogWarning($"{AvatarImportConfig.LOG} folder missing: {AvatarImportConfig.AvatarsRoot}");
                return;
            }

            AvatarBuilder.IsBuilding = true;
            try
            {
                int built = BuildAllAvatarFolders(verbose);
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

        private static int BuildAllAvatarFolders(bool force)
        {
            int built = 0;
            foreach (var dir in Directory.GetDirectories(AvatarImportConfig.AvatarsRoot))
            {
                string unityPath = dir.Replace('\\', '/');
                if (AvatarBuilder.Build(unityPath, force)) built++;
            }
            return built;
        }
    }

    // =========================================================================
    // REGISTRY BUILDER
    // =========================================================================
    public static class AvatarRegistryBuilder
    {
        private const string LOG = AvatarImportConfig.LOG;

        public static void Rebuild()
        {
            EnsureResourcesFolder();
            var reg = LoadOrCreateRegistry();

            reg.avatars = new List<AvatarRegistry.Entry>();
            if (!AssetDatabase.IsValidFolder(AvatarImportConfig.AvatarsRoot))
            {
                EditorUtility.SetDirty(reg);
                return;
            }

            int brokenCount = 0;
            foreach (var dir in Directory.GetDirectories(AvatarImportConfig.AvatarsRoot))
            {
                var entry = BuildEntry(dir.Replace('\\', '/'));
                if (entry == null) continue;
                if (entry.broken) brokenCount++;
                reg.avatars.Add(entry);
            }

            EditorUtility.SetDirty(reg);
            AssetDatabase.SaveAssetIfDirty(reg); // force physical write -- bez tego Resources.Load zwroci null
            Debug.Log($"{LOG} Registry rebuilt -- {reg.avatars.Count} avatars (broken={brokenCount}) [saved to disk]");
        }

        private static AvatarRegistry LoadOrCreateRegistry()
        {
            var reg = AssetDatabase.LoadAssetAtPath<AvatarRegistry>(AvatarImportConfig.RegistryPath);
            if (reg != null) return reg;
            reg = ScriptableObject.CreateInstance<AvatarRegistry>();
            AssetDatabase.CreateAsset(reg, AvatarImportConfig.RegistryPath);
            Debug.Log($"{LOG} AvatarRegistry created: {AvatarImportConfig.RegistryPath}");
            return reg;
        }

        private static AvatarRegistry.Entry BuildEntry(string unityPath)
        {
            string name = Path.GetFileName(unityPath);
            string prefabPath = AvatarPaths.Prefab(unityPath, name);
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if (prefab == null) return null;

            var entry = new AvatarRegistry.Entry { name = name, prefab = prefab };
            ValidateRig(prefab, entry);
            if (entry.broken)
                Debug.LogWarning($"{LOG} BROKEN avatar '{name}': {entry.errorMessage}");
            return entry;
        }

        private static void ValidateRig(GameObject prefab, AvatarRegistry.Entry entry)
        {
            var animator = prefab.GetComponentInChildren<Animator>(true);
            if (animator == null)
            {
                if (prefab.GetComponentInChildren<Renderer>(true) == null)
                    MarkBroken(entry, "no Animator and no Renderer");
                return;
            }
            if (animator.avatar == null)
            {
                MarkBroken(entry, "Animator.avatar is null (rig import failed)");
                return;
            }
            if (!animator.avatar.isValid)
            {
                MarkBroken(entry, "Animator.avatar invalid (rig structure broken)");
                return;
            }
            if (prefab.GetComponentInChildren<SkinnedMeshRenderer>(true) == null)
                MarkBroken(entry, "no SkinnedMeshRenderer (mesh missing or unrigged)");
        }

        private static void MarkBroken(AvatarRegistry.Entry entry, string reason)
        {
            entry.broken = true;
            entry.errorMessage = reason;
        }

        private static void EnsureResourcesFolder()
        {
            if (AssetDatabase.IsValidFolder(AvatarImportConfig.ResourcesRoot)) return;
            AssetDatabase.CreateFolder(AvatarImportConfig.ResourcesParent, AvatarImportConfig.ResourcesFolderName);
        }
    }

    // =========================================================================
    // CORE BUILDER -- per-avatar folder build (DAE pipeline or FBX embedded)
    // =========================================================================
    public static class AvatarBuilder
    {
        private const string LOG = AvatarImportConfig.LOG;

        /// <summary>Global flag: true gdy jestesmy w srodku budowania -- zapobiega reentrantnym postprocess callom.</summary>
        public static bool IsBuilding;

        public static bool Build(string folder, bool force = false)
        {
            string name = Path.GetFileName(folder);
            if (!TryResolveModel(folder, name, out string modelPath, out bool isDae))
                return false;

            bool wasBuilding = IsBuilding;
            IsBuilding = true;
            try
            {
                string matPath = isDae ? BuildDaeMaterial(folder, name, force) : null;
                CreateOrUpdatePrefab(modelPath, matPath, AvatarPaths.Prefab(folder, name), force);
                return true;
            }
            finally
            {
                IsBuilding = wasBuilding;
            }
        }

        private static bool TryResolveModel(string folder, string name, out string modelPath, out bool isDae)
        {
            string dae = AvatarPaths.Model(folder, name, AvatarImportConfig.ExtDae);
            if (File.Exists(dae)) { modelPath = dae; isDae = true; return true; }

            string fbx = AvatarPaths.Model(folder, name, AvatarImportConfig.ExtFbx);
            if (File.Exists(fbx)) { modelPath = fbx; isDae = false; return true; }

            modelPath = null; isDae = false;
            return false;
        }

        // --- DAE pipeline: scal spec+gloss, zbuduj URP/Lit Specular ----------
        private static string BuildDaeMaterial(string folder, string name, bool force)
        {
            string tSpec = AvatarPaths.Texture(folder, name, AvatarImportConfig.SuffixSpecular);
            string tGloss = AvatarPaths.Texture(folder, name, AvatarImportConfig.SuffixGloss);
            string tSpecGloss = AvatarPaths.Texture(folder, name, AvatarImportConfig.SuffixSpecGloss);
            string tDiffuse = AvatarPaths.Texture(folder, name, AvatarImportConfig.SuffixDiffuse);
            string tNormal = AvatarPaths.Texture(folder, name, AvatarImportConfig.SuffixNormal);
            string matPath = AvatarPaths.Material(folder, name);

            if (File.Exists(tSpec) && File.Exists(tGloss) && NeedsSpecGlossRebuild(tSpec, tGloss, tSpecGloss, force))
            {
                CombineSpecGloss(tSpec, tGloss, tSpecGloss);
                AssetDatabase.ImportAsset(tSpecGloss, ImportAssetOptions.ForceUpdate);
            }

            bool matExists = File.Exists(matPath);
            CreateOrUpdateMaterial(matPath, tDiffuse, tNormal, tSpecGloss, updateAll: !matExists || force);
            if (!matExists) Debug.Log($"{LOG} material created: {matPath}");
            return matPath;
        }

        private static bool NeedsSpecGlossRebuild(string tSpec, string tGloss, string tSpecGloss, bool force)
        {
            if (force || !File.Exists(tSpecGloss)) return true;
            var t = File.GetLastWriteTimeUtc(tSpecGloss);
            return t < File.GetLastWriteTimeUtc(tSpec) || t < File.GetLastWriteTimeUtc(tGloss);
        }

        // --- SpecGloss combine: RGB z specular, alpha z gloss.R -------------
        private static void CombineSpecGloss(string specPath, string glossPath, string outPath)
        {
            try
            {
                var spec = LoadPng(specPath);
                var gloss = LoadPng(glossPath);
                if (spec == null || gloss == null) return;

                var glossMatched = MatchSize(gloss, spec.width, spec.height);
                var combined = MergeRgbAndRedToAlpha(spec, glossMatched);
                File.WriteAllBytes(outPath, combined.EncodeToPNG());

                UnityEngine.Object.DestroyImmediate(spec);
                if (glossMatched != gloss) UnityEngine.Object.DestroyImmediate(gloss);
                UnityEngine.Object.DestroyImmediate(glossMatched);
                UnityEngine.Object.DestroyImmediate(combined);

                Debug.Log($"{LOG} SpecGloss combined: {combined.width}x{combined.height} -> {outPath}");
            }
            catch (Exception e)
            {
                Debug.LogError($"{LOG} SpecGloss combine failed: {e.Message}");
            }
        }

        private static Texture2D MatchSize(Texture2D tex, int w, int h)
            => (tex.width == w && tex.height == h) ? tex : Resize(tex, w, h);

        private static Texture2D MergeRgbAndRedToAlpha(Texture2D rgbSource, Texture2D redSource)
        {
            int w = rgbSource.width;
            int h = rgbSource.height;
            var rgbPx = rgbSource.GetPixels32();
            var redPx = redSource.GetPixels32();
            var outPx = new Color32[w * h];
            for (int i = 0; i < outPx.Length; i++)
                outPx[i] = new Color32(rgbPx[i].r, rgbPx[i].g, rgbPx[i].b, redPx[i].r);

            var merged = new Texture2D(w, h, TextureFormat.RGBA32, false);
            merged.SetPixels32(outPx);
            merged.Apply();
            return merged;
        }

        private static Texture2D LoadPng(string path)
        {
            byte[] bytes = File.ReadAllBytes(path);
            var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            if (tex.LoadImage(bytes)) return tex;
            UnityEngine.Object.DestroyImmediate(tex);
            return null;
        }

        private static Texture2D Resize(Texture2D src, int w, int h)
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

        // --- URP/Lit Specular material build --------------------------------
        private static void CreateOrUpdateMaterial(
            string matPath, string tDiffuse, string tNormal, string tSpecGloss, bool updateAll)
        {
            var shader = Shader.Find(UrpLit.ShaderName);
            if (shader == null)
            {
                Debug.LogError($"{LOG} {UrpLit.ShaderName} shader not found");
                return;
            }

            var mat = LoadOrCreateMaterial(matPath, shader, out bool created);
            if (created || updateAll)
                ApplySpecularWorkflowDefaults(mat, shader);

            BindTextures(mat, tDiffuse, tNormal, tSpecGloss);
            EditorUtility.SetDirty(mat);
        }

        private static Material LoadOrCreateMaterial(string matPath, Shader shader, out bool created)
        {
            var mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
            created = mat == null;
            if (!created) return mat;

            mat = new Material(shader);
            AssetDatabase.CreateAsset(mat, matPath);
            return mat;
        }

        private static void ApplySpecularWorkflowDefaults(Material mat, Shader shader)
        {
            mat.shader = shader;
            mat.SetFloat(UrpLit.WorkflowMode, UrpLit.WorkflowSpecular);
            mat.SetFloat(UrpLit.SmoothnessTextureChannel, UrpLit.SmoothnessFromSpecularAlpha);
            mat.SetFloat(UrpLit.Smoothness, 0.5f);
            mat.SetFloat(UrpLit.SpecularHighlights, 1f);
            mat.SetFloat(UrpLit.EnvironmentReflections, 1f);
            mat.SetFloat(UrpLit.BumpScale, 1f);
            mat.SetColor(UrpLit.BaseColor, Color.white);
            mat.SetColor(UrpLit.SpecColor, Color.white);
        }

        private static void BindTextures(Material mat, string tDiffuse, string tNormal, string tSpecGloss)
        {
            var diffuse = AssetDatabase.LoadAssetAtPath<Texture2D>(tDiffuse);
            var normal = AssetDatabase.LoadAssetAtPath<Texture2D>(tNormal);
            var specGloss = AssetDatabase.LoadAssetAtPath<Texture2D>(tSpecGloss);

            if (diffuse != null) mat.SetTexture(UrpLit.BaseMap, diffuse);
            if (normal != null)
            {
                mat.SetTexture(UrpLit.BumpMap, normal);
                mat.EnableKeyword(UrpLit.KeywordNormalMap);
            }
            if (specGloss != null)
            {
                mat.SetTexture(UrpLit.SpecGlossMap, specGloss);
                mat.EnableKeyword(UrpLit.KeywordSpecGlossMap);
                mat.EnableKeyword(UrpLit.KeywordMetallicSpecGlossMap);
            }
        }

        // --- Prefab variant --------------------------------------------------
        // matPath moze byc null -- wtedy prefab uzywa materialow embedded w modelu (FBX Mixamo).
        private static void CreateOrUpdatePrefab(string modelPath, string matPath, string prefabPath, bool force)
        {
            if (!force && IsPrefabUpToDate(prefabPath, modelPath, matPath)) return;

            var modelAsset = AssetDatabase.LoadAssetAtPath<GameObject>(modelPath);
            if (modelAsset == null)
            {
                Debug.LogWarning($"{LOG} cannot load model for prefab: {modelPath}");
                return;
            }
            var material = TryLoadMaterial(matPath);

            var instance = (GameObject)PrefabUtility.InstantiatePrefab(modelAsset);
            if (instance == null)
            {
                Debug.LogError($"{LOG} InstantiatePrefab failed: {modelPath}");
                return;
            }
            try
            {
                if (material != null) AssignMaterialToAllRenderers(instance, material);
                SavePrefabInstance(instance, prefabPath);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(instance);
            }
        }

        private static bool IsPrefabUpToDate(string prefabPath, string modelPath, string matPath)
        {
            if (!File.Exists(prefabPath)) return false;
            var prefabTime = File.GetLastWriteTimeUtc(prefabPath);
            if (prefabTime < File.GetLastWriteTimeUtc(modelPath)) return false;
            if (!string.IsNullOrEmpty(matPath) && File.Exists(matPath)
                && prefabTime < File.GetLastWriteTimeUtc(matPath)) return false;
            return true;
        }

        private static Material TryLoadMaterial(string matPath)
        {
            if (string.IsNullOrEmpty(matPath)) return null;
            var mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
            if (mat == null)
                Debug.LogWarning($"{LOG} material missing for prefab (will keep embedded): {matPath}");
            return mat;
        }

        private static void AssignMaterialToAllRenderers(GameObject instance, Material material)
        {
            foreach (var r in instance.GetComponentsInChildren<Renderer>(includeInactive: true))
            {
                var mats = r.sharedMaterials;
                for (int i = 0; i < mats.Length; i++) mats[i] = material;
                r.sharedMaterials = mats;
            }
        }

        private static void SavePrefabInstance(GameObject instance, string prefabPath)
        {
            PrefabUtility.SaveAsPrefabAsset(instance, prefabPath, out bool ok);
            if (ok) Debug.Log($"{LOG} prefab saved: {prefabPath}");
            else Debug.LogError($"{LOG} SaveAsPrefabAsset failed: {prefabPath}");
        }
    }
}
#endif
