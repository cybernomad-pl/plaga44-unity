// =============================================================================
// MixamoMaterialExtractor.cs
// CYBERNOMAD -- Post-import processing dla Mixamo FBX w Assets/PLAGA44/Avatars/.
// Odpowiedzialnosc: tylko texture extraction + URP/Lit material conversion.
//
// Import settings (Humanoid, NO-anim, NO-optimize, External materials) ustawia
// MixamoAvatarImporter (AssetPostprocessor, OnPreprocessModel) -- NIE powiela
// sie tutaj. Jedyne zrodlo prawdy = MixamoAvatarImporter.
//
// Proces per avatar FBX:
//   1. ExtractTextures -> <Folder>/Textures/ (Unity API)
//   2. Znajdz .mat assety w <Folder>/ (stworzone przez Unity z
//      materialLocation=External) i przestaw shader na URP/Lit + re-bind
//      _BaseMap / _BumpMap / _MetallicGlossMap (gdy mat ma te property).
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
        private const string LOG         = "[PLAGA44][MixamoExtractor]";
        private const string AvatarsRoot = "Assets/PLAGA44/Avatars";
        private const string UrpLitName  = "Universal Render Pipeline/Lit";

        public static int ExtractAll()
        {
            if (!AssetDatabase.IsValidFolder(AvatarsRoot))
            {
                Debug.LogWarning($"{LOG} Folder missing: {AvatarsRoot}");
                return 0;
            }

            int processed = 0;
            string[] guids = AssetDatabase.FindAssets("t:Model", new[] { AvatarsRoot });
            foreach (var guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (!path.EndsWith(".fbx", System.StringComparison.OrdinalIgnoreCase)) continue;
                if (ProcessOne(path)) processed++;
            }

            if (processed > 0)
            {
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
            }
            Debug.Log($"{LOG} Processed {processed} FBX files (textures extracted + URP materials).");
            return processed;
        }

        // Per-FBX: extract embedded textures + ensure External materials + remap
        // istniejace .mat-y do FBX po nazwie + convert materials do URP/Lit.
        //
        // KLUCZOWE: gdy .meta FBXa ma materialLocation=Embedded (stan z repo po
        // zerowaniu testbedu), wyekstrraktowane .mat-y w Materials/ sa orphaned
        // -- FBX uzywa embedded BiRP materials (rozowe w URP). Dlatego:
        //   1. Wymus materialLocation=External (naprawa .meta w repo)
        //   2. SearchAndRemapMaterials(BasedOnMaterialName, Everywhere)
        //      -> Unity znajduje .mat-y po nazwie materialu i remapuje FBX
        //   3. SaveAndReimport -> zapis .meta + zastosowanie bindingu
        private static bool ProcessOne(string fbxPath)
        {
            var importer = AssetImporter.GetAtPath(fbxPath) as ModelImporter;
            if (importer == null)
            {
                Debug.LogWarning($"{LOG} [SKIP] Not a ModelImporter: {fbxPath}");
                return false;
            }

            string folder    = Path.GetDirectoryName(fbxPath);
            string texFolder = Path.Combine(folder, "Textures").Replace('\\', '/');

            EnsureFolder(texFolder);
            if (importer.ExtractTextures(texFolder))
                Debug.Log($"{LOG} [TEX] Extracted -> {texFolder}");

            // --- Naprawa materialLocation + remap -----------------------------
            // Wymus materialLocation=External (zapisane do .meta przez Save).
            if (importer.materialLocation != ModelImporterMaterialLocation.External)
            {
                importer.materialLocation = ModelImporterMaterialLocation.External;
                Debug.Log($"{LOG} [META] {fbxPath}: Embedded -> External");
            }

            // KLUCZOWE: externalObjects moga wskazywac na NIEISTNIEJACE .mat-y
            // (ghost GUIDs z poprzedniej generacji Unity reimport-u). Liczenie
            // "mam mapping" po entries count klamie -- musimy sprawdzac
            // kv.Value != null (target asseta realnie istnieje).
            int validRemaps = 0;
            int ghostRemaps = 0;
            var ghostKeys = new System.Collections.Generic.List<AssetImporter.SourceAssetIdentifier>();
            foreach (var kv in importer.GetExternalObjectMap())
            {
                if (kv.Key.type != typeof(Material)) continue;
                if (kv.Value != null) { validRemaps++; }
                else { ghostRemaps++; ghostKeys.Add(kv.Key); }
            }

            // Usun ghost mappings -- wskazuja na skasowane .mat-y, Unity
            // fallback do embedded BiRP (rozowe w URP).
            foreach (var ghost in ghostKeys)
            {
                importer.RemoveRemap(ghost);
                Debug.Log($"{LOG} [REMAP-GHOST] {fbxPath}: removed {ghost.name} (null target)");
            }

            // SearchAndRemap zawsze po cleanup -- szuka .mat-y po nazwie
            // materialu w FBX, dodaje valid mapping do externalObjects.
            // Idempotent: jesli mapping juz valid, nic nie zmienia.
            importer.SearchAndRemapMaterials(
                ModelImporterMaterialName.BasedOnMaterialName,
                ModelImporterMaterialSearch.Everywhere);

            // Zawsze SaveAndReimport -- zapisuje materialLocation + externalObjects
            // do .meta. Bez tego zmiany siedza tylko w pamieci importera.
            importer.SaveAndReimport();
            Debug.Log($"{LOG} [REIMPORT] {fbxPath} (valid={validRemaps}, ghost-cleaned={ghostRemaps})");

            // Import settings (Humanoid / no-anim / external-mat) sa ustawione
            // przez MixamoAvatarImporter.OnPreprocessModel -- nie duplikujemy.
            ConvertMaterialsToUrp(folder);
            return true;
        }

        // ---- URP material conversion ----------------------------------------

        private static void ConvertMaterialsToUrp(string avatarFolder)
        {
            var urp = Shader.Find(UrpLitName);
            if (urp == null)
            {
                Debug.LogError($"{LOG} Shader '{UrpLitName}' not found");
                return;
            }

            string[] matGuids = AssetDatabase.FindAssets("t:Material", new[] { avatarFolder });
            foreach (var g in matGuids)
            {
                string path = AssetDatabase.GUIDToAssetPath(g);
                var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (mat == null) continue;
                if (mat.shader != null && mat.shader.name == UrpLitName) continue; // already URP

                // Property reads sa NULL-safe -- mat.HasProperty() sprawdza shader slot.
                Texture diffuse   = ReadTexture(mat, "_MainTex", "_BaseMap");
                Texture normal    = ReadTexture(mat, "_BumpMap");
                Texture specGloss = ReadTexture(mat, "_SpecGlossMap", "_MetallicGlossMap");

                // ZERO FALLBACK (CLAUDE.md). Brak binding = LogWarning, nie zgadujemy
                // po nazwach plikow. Borys decyduje czy material ma byc renderowany bez textury.
                if (diffuse == null)
                    Debug.LogWarning($"{LOG} '{mat.name}': brak _MainTex/_BaseMap binding");

                mat.shader = urp;
                mat.SetFloat("_WorkflowMode", 0f);              // 0=Metallic workflow
                mat.SetFloat("_Smoothness",   0.5f);
                mat.SetColor("_BaseColor",    Color.white);

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
                Debug.Log($"{LOG} [URP] {mat.name} (diff={diffuse != null} nrm={normal != null} spec={specGloss != null})");
            }
        }

        private static Texture ReadTexture(Material mat, params string[] propertyNames)
        {
            foreach (var prop in propertyNames)
                if (mat.HasProperty(prop))
                {
                    var t = mat.GetTexture(prop);
                    if (t != null) return t;
                }
            return null;
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
