// =============================================================================
// WeaponPrefabBuilder.cs
// CYBERNOMAD -- Buduje grywalne prefaby broni z surowych FBX do Resources/Items/.
// Powiela wzorzec RevolverPrefabBuilder, uogolniony na jawny katalog broni.
//
// ZERO zgadywania: kazda bron ma EXPLICIT wpis (nazwa + sciezka FBX + masa).
// Nowa bron = nowy wpis w Catalog. Auto-scan celowo NIE uzyty.
//
// BEZ [DidReloadScripts] -- auto-build przy kazdej rekompilacji podejrzany
// o ubicie setupu menu. Budowanie odpala sie TYLKO:
//   - recznie: CYBERNOMAD/Inventory/Rebuild ALL Weapons
//   - z InventorySetup.Run() (Bootstrap), w izolacji try/catch
//
// Material fix: FBX z built-in Standard shaderem = MAGENTA w URP. Builder
// przelacza .mat asset na URP/Lit zachowujac tekstury. Material wbudowany
// w FBX NIE jest ruszany -- LogWarning zamiast zgadywania.
// =============================================================================
#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using Plaga44.Feedback;
using Plaga44.Inventory;

namespace Plaga44.Editor
{
    public static class WeaponPrefabBuilder
    {
        private const string LOG = "[PLAGA44][WeaponBuilder]";
        private const string ResourcesRoot = "Assets/Resources";
        private const string ItemsFolder = "Assets/Resources/Items";

        // Physics -- spojne z RevolverPrefabBuilder.
        private const float LinearDamping = 0f;
        private const float AngularDamping = 0.05f;
        private static readonly Vector3 FallbackBoundsSize = new Vector3(0.2f, 0.15f, 0.05f);

        private static readonly int P_MainTex = Shader.PropertyToID("_MainTex");
        private static readonly int P_BaseMap = Shader.PropertyToID("_BaseMap");
        private static readonly int P_BumpMap = Shader.PropertyToID("_BumpMap");
        private static readonly int P_MetallicGlossMap = Shader.PropertyToID("_MetallicGlossMap");
        private static readonly int P_Color = Shader.PropertyToID("_Color");
        private static readonly int P_BaseColor = Shader.PropertyToID("_BaseColor");

        private struct WeaponDef
        {
            public readonly string Name;
            public readonly string SourceFbx;
            public readonly float MassKg;
            public WeaponDef(string name, string fbx, float mass) { Name = name; SourceFbx = fbx; MassKg = mass; }
        }

        // JAWNY KATALOG. Revolver ma wlasny RevolverPrefabBuilder -- nie dublujemy.
        private static readonly WeaponDef[] Catalog =
        {
            new WeaponDef("Shotgun", "Assets/PLAGA44/Weapons/Shotgun_Double_Barrel_01/FBX/Low.fbx", 3.2f),
            new WeaponDef("M249",    "Assets/PLAGA44/Weapons/Models/M249/M249_low.fbx",             7.5f),
        };

        [MenuItem("CYBERNOMAD/Inventory/Rebuild ALL Weapons", false, 301)]
        public static void RebuildAllMenu() => BuildAll(force: true);

        /// <summary>Buduje brakujace prefaby broni. Wolane z InventorySetup.Run() w try/catch.</summary>
        public static void EnsureAllWeapons() => BuildAll(force: false);

        private static void BuildAll(bool force)
        {
            EnsureResourcesItemsFolder();
            int built = 0, skipped = 0, failed = 0;

            foreach (var def in Catalog)
            {
                string prefabPath = $"{ItemsFolder}/{def.Name}.prefab";
                if (!force && File.Exists(prefabPath)) { skipped++; continue; }
                if (BuildOne(def, prefabPath)) built++; else failed++;
            }

            if (built > 0) { AssetDatabase.SaveAssets(); AssetDatabase.Refresh(); }
            if (built > 0 || failed > 0)
                Debug.Log($"{LOG} Weapons: built={built}, skipped={skipped}, failed={failed}");
        }

        private static bool BuildOne(WeaponDef def, string prefabPath)
        {
            var fbx = AssetDatabase.LoadAssetAtPath<GameObject>(def.SourceFbx);
            if (fbx == null)
            {
                Debug.LogError($"{LOG} [{def.Name}] Source FBX not found: {def.SourceFbx}");
                return false;
            }

            var instance = Object.Instantiate(fbx);
            instance.name = def.Name;

            var bounds = ComputeRendererBounds(instance);
            AttachPhysics(instance, def.MassKg);
            AttachCollider(instance, bounds);
            AttachFeedbackAndGrab(instance);
            FixMaterialsToURP(instance, def.Name);

            var prefab = PrefabUtility.SaveAsPrefabAsset(instance, prefabPath, out bool success);
            Object.DestroyImmediate(instance);

            if (!success || prefab == null)
            {
                Debug.LogError($"{LOG} [{def.Name}] Failed to save prefab: {prefabPath}");
                return false;
            }
            Debug.Log($"{LOG} [{def.Name}] Built: {prefabPath} (bounds={bounds.size}, mass={def.MassKg}kg)");
            return true;
        }

        private static void EnsureResourcesItemsFolder()
        {
            if (!AssetDatabase.IsValidFolder(ResourcesRoot))
                AssetDatabase.CreateFolder("Assets", "Resources");
            if (!AssetDatabase.IsValidFolder(ItemsFolder))
                AssetDatabase.CreateFolder(ResourcesRoot, "Items");
        }

        private static void AttachPhysics(GameObject instance, float massKg)
        {
            var rb = instance.GetComponent<Rigidbody>();
            if (rb == null) rb = instance.AddComponent<Rigidbody>();
            rb.mass = massKg;
            rb.linearDamping = LinearDamping;
            rb.angularDamping = AngularDamping;
            rb.interpolation = RigidbodyInterpolation.Interpolate;
            rb.collisionDetectionMode = CollisionDetectionMode.Continuous;
        }

        private static void AttachCollider(GameObject instance, Bounds bounds)
        {
            var col = instance.GetComponent<BoxCollider>();
            if (col == null) col = instance.AddComponent<BoxCollider>();
            col.center = instance.transform.InverseTransformPoint(bounds.center);
            col.size = bounds.size;
        }

        private static void AttachFeedbackAndGrab(GameObject instance)
        {
            if (instance.GetComponent<HapticOnGrab>() == null)
                instance.AddComponent<HapticOnGrab>();
            if (instance.GetComponent<PlagaGrabbable>() == null)
                instance.AddComponent<PlagaGrabbable>();
        }

        /// <summary>Built-in Standard .mat -> URP/Lit (magenta fix), zachowuje tekstury.
        /// Material embedded w FBX NIE ruszany -- LogWarning, bez zgadywania.</summary>
        private static void FixMaterialsToURP(GameObject instance, string weaponName)
        {
            var urpLit = Shader.Find("Universal Render Pipeline/Lit");
            if (urpLit == null)
            {
                Debug.LogError($"{LOG} [{weaponName}] URP/Lit shader not found -- material fix skipped");
                return;
            }

            var seen = new HashSet<Material>();
            foreach (var r in instance.GetComponentsInChildren<Renderer>(true))
            {
                foreach (var mat in r.sharedMaterials)
                {
                    if (mat == null || seen.Contains(mat)) continue;
                    seen.Add(mat);
                    if (mat.shader == urpLit) continue;

                    string matPath = AssetDatabase.GetAssetPath(mat);
                    if (string.IsNullOrEmpty(matPath) || matPath.EndsWith(".fbx"))
                    {
                        Debug.LogWarning($"{LOG} [{weaponName}] Material '{mat.name}' shader='{mat.shader.name}' " +
                                         $"nie jest osobnym .mat (path='{matPath}') -- NIE zmieniam. " +
                                         $"Jesli magenta w grze: wyekstrahuj material z FBX i przelacz na URP/Lit.");
                        continue;
                    }

                    Texture albedo = mat.HasProperty(P_MainTex) ? mat.GetTexture(P_MainTex) : null;
                    Texture bump = mat.HasProperty(P_BumpMap) ? mat.GetTexture(P_BumpMap) : null;
                    Texture metal = mat.HasProperty(P_MetallicGlossMap) ? mat.GetTexture(P_MetallicGlossMap) : null;
                    Color col = mat.HasProperty(P_Color) ? mat.GetColor(P_Color) : Color.white;

                    mat.shader = urpLit;
                    if (albedo != null) mat.SetTexture(P_BaseMap, albedo);
                    if (bump != null) { mat.SetTexture(P_BumpMap, bump); mat.EnableKeyword("_NORMALMAP"); }
                    if (metal != null) { mat.SetTexture(P_MetallicGlossMap, metal); mat.EnableKeyword("_METALLICSPECGLOSSMAP"); }
                    if (mat.HasProperty(P_BaseColor)) mat.SetColor(P_BaseColor, col);

                    EditorUtility.SetDirty(mat);
                    Debug.Log($"{LOG} [{weaponName}] Material '{mat.name}' -> URP/Lit");
                }
            }
        }

        private static Bounds ComputeRendererBounds(GameObject root)
        {
            // Renderer (nie MeshRenderer) -- lapie tez SkinnedMeshRenderer.
            var renderers = root.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0)
                return new Bounds(root.transform.position, FallbackBoundsSize);

            Bounds b = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++) b.Encapsulate(renderers[i].bounds);
            return b;
        }
    }
}
#endif
