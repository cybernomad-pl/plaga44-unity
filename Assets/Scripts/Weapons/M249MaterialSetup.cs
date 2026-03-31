// M249MaterialSetup.cs
// CYBERNOMAD -- Creates and applies proper material to M249 at runtime.
// Uses AO texture from Assets + dark metallic gun material.

using UnityEngine;

public class M249MaterialSetup : MonoBehaviour
{
    public static Color gunColor = new Color(0.08f, 0.08f, 0.09f); // dark gunmetal
    public static float gunMetallic = 0.85f;
    public static float gunSmoothness = 0.45f;
    public static float aoStrength = 1.0f;

    private static Material _gunMat;

    public static Material GetGunMaterial()
    {
        if (_gunMat != null) return _gunMat;

        var shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null) shader = Shader.Find("Standard");
        _gunMat = new Material(shader);
        _gunMat.name = "M249_Gun_Runtime";

        _gunMat.SetColor("_BaseColor", gunColor);
        _gunMat.SetFloat("_Metallic", gunMetallic);
        _gunMat.SetFloat("_Smoothness", gunSmoothness);

        // Try to load AO texture
        var ao = Resources.Load<Texture2D>("M249_AO");
        if (ao == null)
        {
            // Try direct path
            var allTextures = Resources.FindObjectsOfTypeAll<Texture2D>();
            foreach (var t in allTextures)
            {
                if (t.name.Contains("M249") && t.name.Contains("AO"))
                {
                    ao = t;
                    break;
                }
            }
        }

        if (ao != null && _gunMat.HasTexture("_OcclusionMap"))
        {
            _gunMat.SetTexture("_OcclusionMap", ao);
            _gunMat.SetFloat("_OcclusionStrength", aoStrength);
            Debug.Log("[M249] AO texture applied");
        }

        Debug.Log($"[M249] Material created: color={gunColor} metallic={gunMetallic} smooth={gunSmoothness}");
        return _gunMat;
    }

    public static void ApplyToWeapon(GameObject weapon)
    {
        var mat = GetGunMaterial();
        var renderers = weapon.GetComponentsInChildren<Renderer>();
        foreach (var r in renderers)
        {
            var mats = new Material[r.sharedMaterials.Length];
            for (int i = 0; i < mats.Length; i++)
                mats[i] = mat;
            r.sharedMaterials = mats;
        }
    }
}
