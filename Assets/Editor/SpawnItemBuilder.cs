#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

/// <summary>
/// Creates SpawnItem prefabs from FBX models.
/// Each prefab gets: Rigidbody, BoxCollider (auto-fit), OVRGrabbable.
/// Run: CYBERNOMAD > Tools > Build Spawn Items
/// </summary>
public static class SpawnItemBuilder
{
    private const string OUTPUT_DIR = "Assets/Resources/SpawnItems";

    struct ItemDef
    {
        public string name;
        public string fbxPath;
        public float mass;
        public bool stripAnimator;
    }

    private static readonly ItemDef[] Items = new[]
    {
        new ItemDef
        {
            name = "M249",
            fbxPath = "Assets/PLAGA44/Weapons/Models/M249/M249_low.fbx",
            mass = 7.5f,
            stripAnimator = true
        },
        new ItemDef
        {
            name = "Sword",
            fbxPath = "Assets/PLAGA44/Weapons/Models/Sword.FBX",
            mass = 1.5f,
            stripAnimator = true
        },
        new ItemDef
        {
            name = "Pistol",
            fbxPath = "Assets/PLAGA44/Weapons/Models/Pistol/Gun.fbx",
            mass = 1.2f,
            stripAnimator = true
        },
    };

    [MenuItem("CYBERNOMAD/Tools/Build Spawn Items", false, 210)]
    public static void BuildAll()
    {
        if (!AssetDatabase.IsValidFolder(OUTPUT_DIR))
        {
            // Create folder path recursively
            if (!AssetDatabase.IsValidFolder("Assets/Resources"))
                AssetDatabase.CreateFolder("Assets", "Resources");
            AssetDatabase.CreateFolder("Assets/Resources", "SpawnItems");
        }

        int created = 0;
        foreach (var item in Items)
        {
            if (BuildItem(item))
                created++;
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log($"[SpawnItemBuilder] Built {created}/{Items.Length} spawn item prefabs in {OUTPUT_DIR}");
    }

    // Auto-run on first import
    [InitializeOnLoadMethod]
    static void AutoBuildIfMissing()
    {
        // Check if any prefabs are missing
        bool anyMissing = false;
        foreach (var item in Items)
        {
            string path = $"{OUTPUT_DIR}/{item.name}.prefab";
            if (AssetDatabase.LoadAssetAtPath<GameObject>(path) == null)
            {
                anyMissing = true;
                break;
            }
        }

        if (anyMissing)
        {
            // Delay to avoid running during import
            EditorApplication.delayCall += BuildAll;
        }
    }

    static bool BuildItem(ItemDef item)
    {
        string prefabPath = $"{OUTPUT_DIR}/{item.name}.prefab";

        // Load FBX
        var fbx = AssetDatabase.LoadAssetAtPath<GameObject>(item.fbxPath);
        if (fbx == null)
        {
            Debug.LogWarning($"[SpawnItemBuilder] FBX not found: {item.fbxPath}");
            return false;
        }

        // Instantiate
        var instance = Object.Instantiate(fbx);
        instance.name = item.name;

        // Strip Animator if requested (no animations for spawn items)
        if (item.stripAnimator)
        {
            var animator = instance.GetComponent<Animator>();
            if (animator != null)
                Object.DestroyImmediate(animator);
            var animation = instance.GetComponent<Animation>();
            if (animation != null)
                Object.DestroyImmediate(animation);
        }

        // Add Rigidbody
        var rb = instance.GetComponent<Rigidbody>();
        if (rb == null)
            rb = instance.AddComponent<Rigidbody>();
        rb.mass = item.mass;
        rb.useGravity = true;
        rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;

        // Auto-fit BoxCollider from mesh bounds
        if (instance.GetComponent<Collider>() == null &&
            instance.GetComponentInChildren<Collider>() == null)
        {
            var renderers = instance.GetComponentsInChildren<Renderer>();
            if (renderers.Length > 0)
            {
                var bounds = renderers[0].bounds;
                for (int i = 1; i < renderers.Length; i++)
                    bounds.Encapsulate(renderers[i].bounds);

                var box = instance.AddComponent<BoxCollider>();
                box.center = instance.transform.InverseTransformPoint(bounds.center);
                box.size = new Vector3(
                    Mathf.Abs(instance.transform.InverseTransformVector(bounds.size).x),
                    Mathf.Abs(instance.transform.InverseTransformVector(bounds.size).y),
                    Mathf.Abs(instance.transform.InverseTransformVector(bounds.size).z));
            }
        }

        // Add OVRGrabbable
        if (instance.GetComponent<OVRGrabbable>() == null)
            instance.AddComponent<OVRGrabbable>();

        // Save as prefab
        var prefab = PrefabUtility.SaveAsPrefabAsset(instance, prefabPath);
        Object.DestroyImmediate(instance);

        if (prefab != null)
        {
            Debug.Log($"[SpawnItemBuilder] Created: {prefabPath} (mass: {item.mass}kg)");
            return true;
        }

        Debug.LogError($"[SpawnItemBuilder] Failed to save: {prefabPath}");
        return false;
    }
}
#endif
