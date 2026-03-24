#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace Plaga44.Editor
{
    /// <summary>
    /// Converts ALL Built-in materials in the active scene to URP.
    /// Scans every Renderer + Terrain in scene -- catches everything.
    /// </summary>
    public static class MaterialUpgrader
    {
        private const string LOG = "[PLAGA44]";

        public static void UpgradeMaterials()
        {
            Shader urpLit = Shader.Find("Universal Render Pipeline/Lit");
            Shader urpParticlesUnlit = Shader.Find("Universal Render Pipeline/Particles/Unlit");

            if (urpLit == null)
            {
                Debug.LogError($"{LOG} URP Lit shader not found!");
                return;
            }

            // Collect ALL unique materials from scene renderers + terrains
            var sceneMaterials = new HashSet<Material>();

            // From Renderers
            var renderers = Object.FindObjectsByType<Renderer>(
                FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (var r in renderers)
            {
                foreach (var mat in r.sharedMaterials)
                    if (mat != null) sceneMaterials.Add(mat);
            }

            // From Terrains
            var terrains = Object.FindObjectsByType<Terrain>(
                FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (var t in terrains)
            {
                if (t.materialTemplate != null)
                    sceneMaterials.Add(t.materialTemplate);
                // Tree prototypes on terrain
                if (t.terrainData != null)
                {
                    foreach (var tp in t.terrainData.treePrototypes)
                    {
                        if (tp.prefab == null) continue;
                        foreach (var r2 in tp.prefab.GetComponentsInChildren<Renderer>(true))
                            foreach (var m2 in r2.sharedMaterials)
                                if (m2 != null) sceneMaterials.Add(m2);
                    }
                    // Detail prototypes (grass etc)
                    foreach (var dp in t.terrainData.detailPrototypes)
                    {
                        if (dp.prototype == null) continue;
                        foreach (var r3 in dp.prototype.GetComponentsInChildren<Renderer>(true))
                            foreach (var m3 in r3.sharedMaterials)
                                if (m3 != null) sceneMaterials.Add(m3);
                    }
                }
            }

            // Also scan Assets/FloodedGrounds for any material not in scene
            var assetGuids = AssetDatabase.FindAssets("t:Material",
                new[] { "Assets/FloodedGrounds" });
            foreach (var guid in assetGuids)
            {
                var mat = AssetDatabase.LoadAssetAtPath<Material>(
                    AssetDatabase.GUIDToAssetPath(guid));
                if (mat != null) sceneMaterials.Add(mat);
            }

            Debug.Log($"{LOG} Found {sceneMaterials.Count} unique materials to check.");

            int upgraded = 0;
            foreach (var mat in sceneMaterials)
            {
                if (mat == null) continue;
                string shaderName = mat.shader != null ? mat.shader.name : "";

                bool needsUpgrade =
                    shaderName == "Standard" ||
                    shaderName == "Standard (Specular setup)" ||
                    shaderName == "Hidden/InternalErrorShader" ||
                    shaderName == "" ||
                    shaderName.Contains("Particles/Standard") ||
                    shaderName.Contains("Particles/Additive") ||
                    shaderName.Contains("Particles/Multiply") ||
                    shaderName.Contains("Particles/Alpha") ||
                    shaderName.StartsWith("Nature/") ||
                    shaderName.StartsWith("Legacy Shaders/") ||
                    shaderName == "Mobile/Diffuse" ||
                    shaderName == "Mobile/Bumped Diffuse" ||
                    shaderName == "Diffuse" ||
                    shaderName == "Bumped Diffuse" ||
                    shaderName == "Specular" ||
                    shaderName == "Bumped Specular" ||
                    shaderName == "Unlit/Texture" ||
                    shaderName == "Unlit/Color" ||
                    shaderName == "Unlit/Transparent" ||
                    shaderName == "Unlit/Transparent Cutout" ||
                    (mat.shader != null && !mat.shader.isSupported);

                if (!needsUpgrade) continue;

                // Particles
                if (shaderName.Contains("Particles/"))
                {
                    UpgradeParticles(mat, urpParticlesUnlit);
                }
                // Unlit
                else if (shaderName.StartsWith("Unlit/"))
                {
                    UpgradeUnlit(mat);
                }
                // Everything else -> URP Lit
                else
                {
                    UpgradeToURPLit(mat, urpLit);
                }

                EditorUtility.SetDirty(mat);
                upgraded++;
            }

            AssetDatabase.SaveAssets();
            Debug.Log($"{LOG} Upgraded {upgraded}/{sceneMaterials.Count} materials to URP.");
        }

        static void UpgradeToURPLit(Material mat, Shader urpLit)
        {
            // Save all properties before shader swap
            Texture mainTex = GetTex(mat, "_MainTex");
            Texture bumpMap = GetTex(mat, "_BumpMap");
            Texture metallicMap = GetTex(mat, "_MetallicGlossMap");
            Texture occlusionMap = GetTex(mat, "_OcclusionMap");
            Texture emissionMap = GetTex(mat, "_EmissionMap");
            Texture detailAlbedo = GetTex(mat, "_DetailAlbedoMap");
            Texture detailNormal = GetTex(mat, "_DetailNormalMap");
            Color color = GetColor(mat, "_Color", Color.white);
            float metallic = GetFloat(mat, "_Metallic", 0f);
            float glossiness = GetFloat(mat, "_Glossiness", 0.5f);
            float bumpScale = GetFloat(mat, "_BumpScale", 1f);
            Color emissionColor = GetColor(mat, "_EmissionColor", Color.black);
            float mode = GetFloat(mat, "_Mode", 0f);
            float cutoff = GetFloat(mat, "_Cutoff", 0.5f);
            Vector2 tiling = mat.HasProperty("_MainTex") ? mat.GetTextureScale("_MainTex") : Vector2.one;
            Vector2 offset = mat.HasProperty("_MainTex") ? mat.GetTextureOffset("_MainTex") : Vector2.zero;

            mat.shader = urpLit;

            // Restore
            SetTex(mat, "_BaseMap", mainTex);
            SetColor(mat, "_BaseColor", color);
            SetTex(mat, "_BumpMap", bumpMap);
            SetFloat(mat, "_BumpScale", bumpScale);
            SetTex(mat, "_MetallicGlossMap", metallicMap);
            SetFloat(mat, "_Metallic", metallic);
            SetFloat(mat, "_Smoothness", glossiness);
            SetTex(mat, "_OcclusionMap", occlusionMap);
            SetTex(mat, "_EmissionMap", emissionMap);
            SetColor(mat, "_EmissionColor", emissionColor);
            SetTex(mat, "_DetailAlbedoMap", detailAlbedo);
            SetTex(mat, "_DetailNormalMap", detailNormal);
            SetFloat(mat, "_Cutoff", cutoff);

            // Tiling/offset
            if (mat.HasProperty("_BaseMap"))
            {
                mat.SetTextureScale("_BaseMap", tiling);
                mat.SetTextureOffset("_BaseMap", offset);
            }

            // Transparency
            if (mode >= 2f) // Fade/Transparent
            {
                mat.SetFloat("_Surface", 1);
                mat.SetOverrideTag("RenderType", "Transparent");
                mat.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
                mat.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha);
                mat.SetInt("_ZWrite", 0);
                mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
                mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            }
            else if (mode == 1f) // Cutout
            {
                mat.SetFloat("_Surface", 0);
                mat.SetFloat("_AlphaClip", 1);
                mat.EnableKeyword("_ALPHATEST_ON");
                mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.AlphaTest;
            }

            // Emission
            if (emissionMap != null || emissionColor != Color.black)
                mat.EnableKeyword("_EMISSION");

            // Normal map keyword
            if (bumpMap != null)
                mat.EnableKeyword("_NORMALMAP");

            // Metallic map keyword
            if (metallicMap != null)
                mat.EnableKeyword("_METALLICSPECGLOSSMAP");
        }

        static void UpgradeParticles(Material mat, Shader urpParticles)
        {
            Texture mainTex = GetTex(mat, "_MainTex");
            Color tint = GetColor(mat, "_TintColor", Color.white);
            if (tint == Color.white)
                tint = GetColor(mat, "_Color", Color.white);

            mat.shader = urpParticles;
            SetTex(mat, "_BaseMap", mainTex);
            SetColor(mat, "_BaseColor", tint);
            mat.SetFloat("_Surface", 1);
            mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
        }

        static void UpgradeUnlit(Material mat)
        {
            Texture mainTex = GetTex(mat, "_MainTex");
            Color color = GetColor(mat, "_Color", Color.white);

            var urpUnlit = Shader.Find("Universal Render Pipeline/Unlit");
            if (urpUnlit == null) return;

            mat.shader = urpUnlit;
            SetTex(mat, "_BaseMap", mainTex);
            SetColor(mat, "_BaseColor", color);
        }

        // ---- Helpers ----

        static Texture GetTex(Material m, string prop)
            => m.HasProperty(prop) ? m.GetTexture(prop) : null;
        static Color GetColor(Material m, string prop, Color def)
            => m.HasProperty(prop) ? m.GetColor(prop) : def;
        static float GetFloat(Material m, string prop, float def)
            => m.HasProperty(prop) ? m.GetFloat(prop) : def;
        static void SetTex(Material m, string prop, Texture t)
        { if (t != null && m.HasProperty(prop)) m.SetTexture(prop, t); }
        static void SetColor(Material m, string prop, Color c)
        { if (m.HasProperty(prop)) m.SetColor(prop, c); }
        static void SetFloat(Material m, string prop, float v)
        { if (m.HasProperty(prop)) m.SetFloat(prop, v); }
    }
}
#endif
