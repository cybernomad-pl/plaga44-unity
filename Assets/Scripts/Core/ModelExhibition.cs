using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// ModelExhibition -- spawnuje WSZYSTKIE modele w linii przed graczem.
/// Wystawa -- mozna chodzic, ogladac, lapac.
/// Auto-creates on scene load.
/// </summary>
public class ModelExhibition : MonoBehaviour
{
    public float spacing = 2.5f;
    public float distanceFromPlayer = 5f;

    private static readonly string[] MODEL_PATHS = {
        "PLAGA44/Characters/PINEA/PINEA_rigged",
        "PLAGA44/Characters/PINEA-NEO/PINEA-NEO_rigged",
    };

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void AutoCreate()
    {
        var go = new GameObject("_ModelExhibition");
        var ex = go.AddComponent<ModelExhibition>();
        DontDestroyOnLoad(go);

        // Delay spawn by 2s to let scene settle
        ex.Invoke(nameof(SpawnExhibition), 2f);
    }

    void SpawnExhibition()
    {
        var cam = Camera.main;
        if (cam == null) return;

        Vector3 center = cam.transform.position + cam.transform.forward * distanceFromPlayer;
        center.y = cam.transform.position.y - 1.5f; // ground level estimate

        // Raycast for actual ground
        if (Physics.Raycast(center + Vector3.up * 10f, Vector3.down, out RaycastHit hit, 50f))
            center.y = hit.point.y;

        int count = 0;
        float totalWidth = (MODEL_PATHS.Length - 1) * spacing;
        Vector3 right = cam.transform.right;
        right.y = 0;
        right.Normalize();

        foreach (var path in MODEL_PATHS)
        {
            var prefab = Resources.Load<GameObject>(path);
            if (prefab == null)
            {
                Debug.LogWarning($"[EXHIBITION] Model not found: {path}");
                continue;
            }

            Vector3 pos = center + right * (count * spacing - totalWidth * 0.5f);

            var instance = Instantiate(prefab, pos, Quaternion.LookRotation(-cam.transform.forward));
            instance.name = prefab.name + "_exhibit";

            // Grabbable
            var rb = instance.GetComponent<Rigidbody>();
            if (rb == null) rb = instance.AddComponent<Rigidbody>();
            rb.isKinematic = true;
            rb.useGravity = false;

            if (instance.GetComponent<Collider>() == null)
            {
                var renderers = instance.GetComponentsInChildren<Renderer>();
                if (renderers.Length > 0)
                {
                    var bounds = renderers[0].bounds;
                    foreach (var r in renderers) bounds.Encapsulate(r.bounds);
                    var box = instance.AddComponent<BoxCollider>();
                    box.center = instance.transform.InverseTransformPoint(bounds.center);
                    box.size = instance.transform.InverseTransformVector(bounds.size);
                }
            }

            var grabbable = instance.GetComponent<OVRGrabbable>();
            if (grabbable == null) instance.AddComponent<OVRGrabbable>();

            // Label
            CreateLabel(instance.transform, prefab.name, pos + Vector3.up * 2f);

            count++;
            Debug.Log($"[EXHIBITION] Spawned: {prefab.name} at {pos}");
        }

        Debug.Log($"[EXHIBITION] {count} models displayed");
    }

    void CreateLabel(Transform parent, string text, Vector3 worldPos)
    {
        var labelGO = new GameObject("Label_" + text);
        labelGO.transform.position = worldPos;

        var tm = labelGO.AddComponent<TextMesh>();
        tm.text = text;
        tm.fontSize = 48;
        tm.characterSize = 0.05f;
        tm.anchor = TextAnchor.MiddleCenter;
        tm.alignment = TextAlignment.Center;
        tm.color = Color.white;

        // Face camera
        labelGO.AddComponent<Billboard>();
    }
}

/// <summary>
/// Billboard -- always faces camera.
/// </summary>
public class Billboard : MonoBehaviour
{
    void LateUpdate()
    {
        var cam = Camera.main;
        if (cam == null) return;
        transform.LookAt(cam.transform);
        transform.Rotate(0, 180, 0);
    }
}
