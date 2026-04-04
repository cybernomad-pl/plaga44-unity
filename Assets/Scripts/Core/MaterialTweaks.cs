// MaterialTweaks.cs
// CYBERNOMAD -- Runtime material corrections for FloodedGrounds assets.
// Fixes: tree trunks too white, terrain normal intensity too strong.

using UnityEngine;

public class MaterialTweaks : MonoBehaviour
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
#if LOCOMOTION_ONLY
        return;
#endif
    static void AutoCreate()
    {
        var go = new GameObject("_MaterialTweaks");
        go.AddComponent<MaterialTweaks>();
        DontDestroyOnLoad(go);
    }

    void Start()
    {
        FixTreeTrunks();
        FixTerrainNormals();
    }

    void FixTreeTrunks()
    {
        int fixed_ = 0;

        // 1. Fix ALL renderer materials (instances)
        var renderers = FindObjectsByType<Renderer>(FindObjectsSortMode.None);
        foreach (var r in renderers)
        {
            var mats = r.materials;
            bool changed = false;
            for (int i = 0; i < mats.Length; i++)
            {
                var m = mats[i];
                if (m == null) continue;

                string name = m.name.ToLower();
                string shaderName = m.shader != null ? m.shader.name.ToLower() : "";

                bool isBark = name.Contains("bark") || name.Contains("trunk") ||
                              name.Contains("branch") ||
                              (shaderName.Contains("treecreator") && shaderName.Contains("bark"));

                if (isBark)
                {
                    if (m.HasColor("_Color"))
                    { m.SetColor("_Color", new Color(0.45f, 0.35f, 0.25f, 1f)); changed = true; }
                    if (m.HasColor("_BaseColor"))
                    { m.SetColor("_BaseColor", new Color(0.45f, 0.35f, 0.25f, 1f)); changed = true; }
                    if (m.HasFloat("_Smoothness")) { m.SetFloat("_Smoothness", 0.05f); changed = true; }
                    if (m.HasFloat("_Glossiness")) { m.SetFloat("_Glossiness", 0.05f); changed = true; }
                    if (m.HasColor("_SpecColor")) { m.SetColor("_SpecColor", new Color(0.1f, 0.1f, 0.1f, 1f)); changed = true; }
                    fixed_++;
                }
            }
            if (changed) r.materials = mats;
        }

        // 2. Fix shared materials (affects ALL instances including embedded prefab mats)
        foreach (var r in renderers)
        {
            foreach (var m in r.sharedMaterials)
            {
                if (m == null) continue;
                string name = m.name.ToLower();
                string shaderName = m.shader != null ? m.shader.name.ToLower() : "";

                bool isBark = name.Contains("bark") || name.Contains("trunk") ||
                              name.Contains("branch") ||
                              (shaderName.Contains("tree") && shaderName.Contains("bark"));

                if (isBark)
                {
                    // URP Lit uses _BaseColor, TreeCreator uses _Color
                    if (m.HasColor("_BaseColor"))
                        m.SetColor("_BaseColor", new Color(0.45f, 0.35f, 0.25f, 1f));
                    if (m.HasColor("_Color"))
                        m.SetColor("_Color", new Color(0.45f, 0.35f, 0.25f, 1f));
                    if (m.HasFloat("_Glossiness"))
                        m.SetFloat("_Glossiness", 0.05f);
                    if (m.HasFloat("_Smoothness"))
                        m.SetFloat("_Smoothness", 0.05f);
                    if (m.HasColor("_SpecColor"))
                        m.SetColor("_SpecColor", new Color(0.1f, 0.1f, 0.1f, 1f));
                }
            }
        }

        // 3. Force terrain tree refresh
        var terrain = FindAnyObjectByType<Terrain>();
        if (terrain != null)
        {
            terrain.treeDistance = terrain.treeDistance; // force refresh
        }

        if (fixed_ > 0) Debug.Log($"[PLAGA44] MaterialTweaks: tinted {fixed_} bark materials to brown");
    }

    void FixTerrainNormals()
    {
        var terrains = FindObjectsByType<Terrain>(FindObjectsSortMode.None);
        foreach (var t in terrains)
        {
            if (t.materialTemplate == null) continue;

            var mat = t.materialTemplate;

            // Reduce normal intensity
            if (mat.HasFloat("_BumpScale"))
            {
                float current = mat.GetFloat("_BumpScale");
                if (current > 0.5f)
                {
                    mat.SetFloat("_BumpScale", 0.3f);
                    Debug.Log($"[PLAGA44] MaterialTweaks: terrain normal {current:F1} -> 0.3");
                }
            }
            // Kill terrain shininess
            if (mat.HasFloat("_Smoothness"))
            {
                mat.SetFloat("_Smoothness", 0.05f);
                Debug.Log("[PLAGA44] MaterialTweaks: terrain smoothness -> 0.05");
            }
            if (mat.HasFloat("_Glossiness"))
                mat.SetFloat("_Glossiness", 0.05f);

            // Also check terrain layers
            var layers = t.terrainData?.terrainLayers;
            if (layers != null)
            {
                foreach (var layer in layers)
                {
                    if (layer != null && layer.normalScale > 0.5f)
                    {
                        layer.normalScale = 0.3f;
                    }
                }
                Debug.Log($"[PLAGA44] MaterialTweaks: terrain layers normal scale -> 0.3");
            }
        }
    }
}
