// MakeGrabbable.cs
// CYBERNOMAD -- Adds Rigidbody + OVRGrabbable + materials to scene items.
// Targets: M249 parts, Sword, Gun_Fire, MixingSet -- everything the designer
// placed in PLAGA44-BASE that should be interactive.

using UnityEngine;
using System.Collections.Generic;

public class MakeGrabbable : MonoBehaviour
{
    // Items to make grabbable (name contains any of these)
    private static readonly string[] GrabbableNames = {
        "m249", "sword", "gun_fire", "gun fire", "mixingset", "mixing",
        "receiver", "magazine", "handguard", "stock", "grip_trigger",
        "knife", "axe", "weapon", "item", "prop", "pickup",
    };

    // Mass estimates (kg)
    private static readonly Dictionary<string, float> MassMap = new Dictionary<string, float>() {
        { "m249",       7.5f },    // M249 SAW full
        { "receiver",   3.0f },
        { "magazine",   1.5f },
        { "handguard",  0.8f },
        { "stock",      0.6f },
        { "grip",       0.4f },
        { "sword",      1.2f },
        { "gun_fire",   3.5f },
        { "gun fire",   3.5f },
        { "mixing",     5.0f },
    };

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void AutoRun()
    {
        var go = new GameObject("_MakeGrabbable");
        go.AddComponent<MakeGrabbable>();
        DontDestroyOnLoad(go);
    }

    void Start()
    {
        Invoke(nameof(Process), 1.5f);
    }

    void Process()
    {
        int count = 0;
        foreach (var r in FindObjectsByType<Renderer>(FindObjectsSortMode.None))
        {
            var go = r.gameObject;
            string name = go.name.ToLower();

            bool isGrabbable = false;
            foreach (var gn in GrabbableNames)
            {
                if (name.Contains(gn)) { isGrabbable = true; break; }
            }

            // Also check parent name (prefab instances often have child meshes)
            if (!isGrabbable && go.transform.parent != null)
            {
                string parentName = go.transform.parent.name.ToLower();
                foreach (var gn in GrabbableNames)
                {
                    if (parentName.Contains(gn)) { isGrabbable = true; break; }
                }
            }

            if (!isGrabbable) continue;
            if (go.GetComponent<OVRGrabbable>() != null) continue;

            // Find root of this item (walk up until parent is scene root or null)
            Transform root = go.transform;
            while (root.parent != null)
            {
                string pn = root.parent.name.ToLower();
                bool parentIsItem = false;
                foreach (var gn in GrabbableNames)
                    if (pn.Contains(gn)) { parentIsItem = true; break; }
                if (parentIsItem) root = root.parent;
                else break;
            }
            var rootGo = root.gameObject;

            // Skip if already processed
            if (rootGo.GetComponent<OVRGrabbable>() != null) continue;

            // --- COLLIDER ---
            if (rootGo.GetComponentInChildren<Collider>() == null)
            {
                var mf = rootGo.GetComponentInChildren<MeshFilter>();
                if (mf != null && mf.sharedMesh != null)
                {
                    var mc = mf.gameObject.AddComponent<MeshCollider>();
                    mc.convex = true;
                }
                else
                {
                    rootGo.AddComponent<BoxCollider>();
                }
            }
            else
            {
                // Ensure mesh colliders are convex
                foreach (var mc in rootGo.GetComponentsInChildren<MeshCollider>())
                    mc.convex = true;
            }

            // --- RIGIDBODY ---
            if (rootGo.GetComponent<Rigidbody>() == null)
            {
                var rb = rootGo.AddComponent<Rigidbody>();
                rb.mass = EstimateMass(rootGo.name);
                rb.linearDamping = 0.5f;
                rb.angularDamping = 0.5f;
                rb.interpolation = RigidbodyInterpolation.Interpolate;
                rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            }

            // --- MATERIAL (fix missing/pink) ---
            FixMaterials(rootGo);

            // --- GRABBABLE ---
            rootGo.AddComponent<OVRGrabbable>();
            count++;

            Debug.Log($"[PLAGA44] MakeGrabbable: {rootGo.name} ({rootGo.GetComponent<Rigidbody>().mass:F1}kg)");
        }

        Debug.Log($"[PLAGA44] MakeGrabbable: {count} items ready");
        Destroy(gameObject);
    }

    static float EstimateMass(string name)
    {
        string n = name.ToLower();
        foreach (var kv in MassMap)
            if (n.Contains(kv.Key)) return kv.Value;
        return 1f;
    }

    static void FixMaterials(GameObject go)
    {
        var shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null) shader = Shader.Find("Standard");

        foreach (var r in go.GetComponentsInChildren<Renderer>())
        {
            bool needsFix = false;
            foreach (var m in r.sharedMaterials)
            {
                if (m == null || m.shader == null || m.shader.name.Contains("Error") ||
                    m.shader.name.Contains("Hidden/InternalErrorShader"))
                {
                    needsFix = true;
                    break;
                }
            }

            if (needsFix)
            {
                string n = go.name.ToLower();
                var mat = new Material(shader);

                if (n.Contains("sword") || n.Contains("knife"))
                {
                    mat.name = "Blade_Runtime";
                    mat.SetColor("_BaseColor", new Color(0.7f, 0.72f, 0.75f));
                    mat.SetFloat("_Metallic", 0.95f);
                    mat.SetFloat("_Smoothness", 0.7f);
                }
                else if (n.Contains("m249") || n.Contains("gun") || n.Contains("receiver") ||
                         n.Contains("magazine") || n.Contains("handguard") || n.Contains("stock") ||
                         n.Contains("grip"))
                {
                    mat.name = "Gun_Runtime";
                    mat.SetColor("_BaseColor", new Color(0.08f, 0.08f, 0.09f));
                    mat.SetFloat("_Metallic", 0.85f);
                    mat.SetFloat("_Smoothness", 0.45f);
                }
                else if (n.Contains("mixing"))
                {
                    mat.name = "Metal_Runtime";
                    mat.SetColor("_BaseColor", new Color(0.15f, 0.15f, 0.17f));
                    mat.SetFloat("_Metallic", 0.9f);
                    mat.SetFloat("_Smoothness", 0.6f);
                }
                else
                {
                    mat.name = "Default_Runtime";
                    mat.SetColor("_BaseColor", new Color(0.3f, 0.3f, 0.3f));
                    mat.SetFloat("_Metallic", 0.3f);
                    mat.SetFloat("_Smoothness", 0.3f);
                }

                var mats = new Material[r.sharedMaterials.Length];
                for (int i = 0; i < mats.Length; i++) mats[i] = mat;
                r.sharedMaterials = mats;
                Debug.Log($"[PLAGA44] Fixed material on: {r.gameObject.name} -> {mat.name}");
            }
        }
    }
}
