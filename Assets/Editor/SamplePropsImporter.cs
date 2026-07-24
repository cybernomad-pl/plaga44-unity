// =============================================================================
// SamplePropsImporter.cs
// CYBERNOMAD -- Szabruje grab-propsy z Meta Interaction SDK sample do ITEM
// SPAWNER GALLERY. Buduje czyste item-prefaby w Assets/Resources/Items/ z
// mesh+material zrodlowego prefaba (REFERENCJA przez GUID pakietu -- NIC nie
// kopiujemy, bo V7 ma pakiet com.meta.xr.sdk.interaction).
//
// Kazdy zbudowany prop: MeshFilter/MeshRenderer (+ SkinnedMeshRenderer /
// ParticleSystem jesli prop ich uzywa) z oryginalnymi materialami, Rigidbody,
// BoxCollider (fit do bounds), Plaga44.Inventory.PlagaGrabbable +
// Plaga44.Feedback.HapticOnGrab. NIE uzywa Meta Interaction Grabbable --
// V7 grab = OVRGrabber/PlagaGrabbable.
//
// Zrodlowe prefaby laduje przez stabilna sciezke "Packages/<pkg>/..." (nie
// przez hash PackageCache), zeby przetrwac update pakietu.
//
// Idempotentny: [InitializeOnLoadMethod] buduje tylko brakujace prefaby.
// Menu: CYBERNOMAD/Inventory/Build Sample Props (force rebuild wszystkich).
// Wzorzec: M249PrefabBuilder.cs.
// =============================================================================
#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using Plaga44.Feedback;
using Plaga44.Inventory;

namespace Plaga44.Editor
{
    public static class SamplePropsImporter
    {
        private const string LOG = "[PLAGA44][SamplePropsImporter]";

        // Stabilna sciezka pakietu (Unity resolvuje niezaleznie od hashu PackageCache).
        private const string SourceBase =
            "Packages/com.meta.xr.sdk.interaction/Runtime/Sample/Objects/Props";

        private const string ResourcesRoot = "Assets/Resources";
        private const string ItemsFolder = "Assets/Resources/Items";

        private const float LinearDamping = 0f;
        private const float AngularDamping = 0.05f;

        // Fallback tylko dla GEOMETRII collidera gdy renderer bounds pusty (log warning).
        private static readonly Vector3 FallbackBoundsSize = new Vector3(0.15f, 0.15f, 0.15f);

        // Komponenty ktore ZOSTAWIAMY (wizualne). Reszta jest zdejmowana ze zrodla.
        private static readonly HashSet<Type> RenderWhitelist = new HashSet<Type>
        {
            typeof(Transform),
            typeof(RectTransform),
            typeof(MeshFilter),
            typeof(MeshRenderer),
            typeof(SkinnedMeshRenderer),
            typeof(ParticleSystem),
            typeof(ParticleSystemRenderer),
        };

        private struct PropDef
        {
            public string SourceRelPath; // relative to SourceBase
            public string TargetName;    // prefab name in Resources/Items
            public float MassKg;
            public PropDef(string src, string name, float mass)
            {
                SourceRelPath = src; TargetName = name; MassKg = mass;
            }
        }

        // Explicit katalog propsow (kazdy przypadek wymieniony -- zero zgadywania).
        private static readonly PropDef[] Props =
        {
            new PropDef("BigRedButton/BigRedButton.prefab", "BigRedButton", 1.5f),
            new PropDef("BigStone/BigStone.prefab",         "BigStone",     3.0f),
            new PropDef("Box/Box.prefab",                   "Box",          1.0f),
            new PropDef("ChessPiece/ChessPiece.prefab",     "ChessPiece",   0.3f),
            new PropDef("Doll/Doll.prefab",                 "Doll",         0.5f),
            new PropDef("PingPong/PingPongBall.prefab",     "PingPongBall", 0.05f),
            new PropDef("Torch/Torch.prefab",               "Torch",        1.0f),
            new PropDef("StonePolyhedra/StoneCube.prefab",         "StoneCube",         2.0f),
            new PropDef("StonePolyhedra/StoneDodecahedron.prefab", "StoneDodecahedron", 2.0f),
            new PropDef("StonePolyhedra/StoneIcosahedron.prefab",  "StoneIcosahedron",  2.0f),
            new PropDef("StonePolyhedra/StoneOctahedron.prefab",   "StoneOctahedron",   2.0f),
            new PropDef("StonePolyhedra/StoneTetrahedron.prefab",  "StoneTetrahedron",  2.0f),
            new PropDef("StonePolyhedra/StonePolyhedron.prefab",   "StonePolyhedron",   2.0f),
        };

        // =====================================================================
        // Entry points
        // =====================================================================

        [MenuItem("CYBERNOMAD/Inventory/Build Sample Props", false, 302)]
        public static void BuildMenu()
        {
            int built = BuildAll(force: true);
            Debug.Log($"{LOG} Menu build done: {built}/{Props.Length} prefabs built.");
        }

        // Auto-build once on editor load if any prop prefab missing. delayCall
        // defers until AssetDatabase ready (pakiet zaimportowany).
        [InitializeOnLoadMethod]
        private static void AutoBuildOnLoad()
        {
            EditorApplication.delayCall += () =>
            {
                if (!AnyMissing()) return;
                int built = BuildAll(force: false);
                if (built > 0) Debug.Log($"{LOG} Auto-built {built} missing sample props on load.");
            };
        }

        private static bool AnyMissing()
        {
            foreach (var p in Props)
                if (!File.Exists(TargetPath(p.TargetName))) return true;
            return false;
        }

        // =====================================================================
        // Build
        // =====================================================================

        /// <summary>Build props. force=true rebuilds all, false only missing. Returns count built.</summary>
        public static int BuildAll(bool force)
        {
            EnsureResourcesItemsFolder();
            int built = 0;
            foreach (var p in Props)
            {
                string dst = TargetPath(p.TargetName);
                if (!force && File.Exists(dst)) continue;
                if (BuildOne(p)) built++;
            }
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            return built;
        }

        private static bool BuildOne(PropDef p)
        {
            string srcPath = SourceBase + "/" + p.SourceRelPath;
            var src = AssetDatabase.LoadAssetAtPath<GameObject>(srcPath);
            if (src == null)
            {
                Debug.LogError($"{LOG} Source prefab not found: {srcPath} -- czy pakiet Meta Interaction jest w projekcie?");
                return false;
            }

            var instance = (GameObject)PrefabUtility.InstantiatePrefab(src);
            if (instance == null)
            {
                Debug.LogError($"{LOG} InstantiatePrefab failed for {srcPath}");
                return false;
            }

            // Rozlacz od zrodlowego prefaba, zeby moc swobodnie zdejmowac komponenty.
            PrefabUtility.UnpackPrefabInstance(instance, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);

            instance.name = p.TargetName;
            instance.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);

            StripToRenderers(instance);

            if (!HasAnyRenderer(instance))
            {
                Debug.LogError($"{LOG} {p.TargetName}: brak rendererow po stripie -- pomijam (nie mesh-based?).");
                UnityEngine.Object.DestroyImmediate(instance);
                return false;
            }

            AttachPhysics(instance, p.MassKg);
            AttachCollider(instance, ComputeRendererBounds(instance, p.TargetName));
            AttachFeedbackAndGrab(instance);

            string dst = TargetPath(p.TargetName);
            var prefab = PrefabUtility.SaveAsPrefabAsset(instance, dst, out bool success);
            UnityEngine.Object.DestroyImmediate(instance);

            if (!success || prefab == null)
            {
                Debug.LogError($"{LOG} Failed to save prefab: {dst}");
                return false;
            }
            Debug.Log($"{LOG} Built {dst} (mass={p.MassKg}kg)");
            return true;
        }

        // =====================================================================
        // Strip -- zdejmij wszystko poza wizualnymi rendererami
        // =====================================================================

        private static void StripToRenderers(GameObject root)
        {
            // Kolejnosc kategorii = dependency-safe (RequireComponent).
            // MonoBehaviour (Meta grab/pose/audio scripts) -> Joint -> Collider -> Rigidbody -> reszta.
            DestroyByPredicate(root, c => c is MonoBehaviour);
            DestroyByPredicate(root, c => c is Joint);
            DestroyByPredicate(root, c => c is Collider);
            DestroyByPredicate(root, c => c is Rigidbody);
            DestroyByPredicate(root, c => !RenderWhitelist.Contains(c.GetType()));
        }

        // Multi-pass usuwanie: RequireComponent moze blokowac usuniecie w jednym
        // przebiegu, wiec powtarzamy dopoki cos ubywa.
        private static void DestroyByPredicate(GameObject root, Func<Component, bool> match)
        {
            const int maxPasses = 8;
            for (int pass = 0; pass < maxPasses; pass++)
            {
                var comps = root.GetComponentsInChildren<Component>(true);
                int destroyed = 0;
                foreach (var c in comps)
                {
                    if (c == null) continue;
                    if (c is Transform) continue;           // nigdy nie ruszamy Transform
                    if (RenderWhitelist.Contains(c.GetType())) continue;
                    if (!match(c)) continue;
                    try
                    {
                        UnityEngine.Object.DestroyImmediate(c, allowDestroyingAssets: false);
                        destroyed++;
                    }
                    catch (Exception)
                    {
                        // Zablokowane przez zaleznosc -- sprobujemy w nastepnym przebiegu.
                    }
                }
                if (destroyed == 0) break;
            }
        }

        // =====================================================================
        // Physics / collider / grab -- identyczne z M249 recipe
        // =====================================================================

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

        // =====================================================================
        // Helpers
        // =====================================================================

        private static bool HasAnyRenderer(GameObject root)
        {
            return root.GetComponentInChildren<MeshRenderer>(true) != null
                || root.GetComponentInChildren<SkinnedMeshRenderer>(true) != null;
        }

        private static Bounds ComputeRendererBounds(GameObject root, string label)
        {
            var renderers = root.GetComponentsInChildren<Renderer>(true);
            bool has = false;
            Bounds b = new Bounds(root.transform.position, Vector3.zero);
            foreach (var r in renderers)
            {
                if (r is ParticleSystemRenderer) continue; // particle bounds sa niewiarygodne
                if (!has) { b = r.bounds; has = true; }
                else b.Encapsulate(r.bounds);
            }

            if (!has || b.size.sqrMagnitude < 1e-8f)
            {
                Debug.LogWarning($"{LOG} {label}: renderer bounds puste -- collider {FallbackBoundsSize} (do recznej korekty).");
                return new Bounds(root.transform.position, FallbackBoundsSize);
            }
            return b;
        }

        private static string TargetPath(string name) => $"{ItemsFolder}/{name}.prefab";

        private static void EnsureResourcesItemsFolder()
        {
            if (!AssetDatabase.IsValidFolder(ResourcesRoot))
                AssetDatabase.CreateFolder("Assets", "Resources");
            if (!AssetDatabase.IsValidFolder(ItemsFolder))
                AssetDatabase.CreateFolder(ResourcesRoot, "Items");
        }
    }
}
#endif
