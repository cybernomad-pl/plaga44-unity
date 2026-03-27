// AUTO-DISABLED: not needed for demo
#if PLAGA44_FULL_SDK
#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using Plaga44.Gameplay;

namespace Plaga44.Editor
{
    /// <summary>
    /// Editor utility: CYBERNOMAD / Scene Setup / Add Test Stones
    ///
    /// Creates three ThrowableStone GameObjects (small / medium / large) in the
    /// active scene, each with:
    ///   - SphereCollider (radius proportional to size)
    ///   - Rigidbody (continuous collision detection)
    ///   - ThrowableStone component
    ///   - URP Lit grey material (roughness 0.85)
    ///
    /// Stones are placed at eye level (~1.4 m) in front of the origin,
    /// spaced 0.4 m apart so they are immediately visible in the scene view.
    /// </summary>
    public static class StoneFactory
    {
        private const string LOG      = "[PLAGA44]";
        private const string MENU     = "CYBERNOMAD/Scene Setup/Add Test Stones";
        private const int    PRIORITY = 102;

        // Stone definitions: (name, scale, mass in kg)
        private static readonly (string name, float scale, float mass)[] StonePresets =
        {
            ("Stone_Small",  0.05f, 0.15f),
            ("Stone_Medium", 0.10f, 0.40f),
            ("Stone_Large",  0.18f, 1.00f),
        };

        [MenuItem(MENU, false, PRIORITY)]
        public static void AddTestStones()
        {
            Debug.Log($"{LOG} === Add Test Stones ===");

            // Parent container keeps the hierarchy clean
            var parent = new GameObject("TestStones");
            parent.transform.position = Vector3.zero;
            Undo.RegisterCreatedObjectUndo(parent, "Create Test Stones Parent");

            // Shared grey material (created once, reused across all stones)
            Material stoneMat = CreateStoneMaterial();

            float spacing = 0.40f;
            float startX  = -(StonePresets.Length - 1) * spacing * 0.5f;

            for (int i = 0; i < StonePresets.Length; i++)
            {
                var (stoneName, scale, mass) = StonePresets[i];

                Vector3 pos = new Vector3(
                    startX + i * spacing,
                    1.40f,   // eye level (~1.4 m above origin)
                    1.00f    // 1 m in front
                );

                GameObject stone = CreateStone(stoneName, pos, scale, mass, stoneMat, parent.transform);
                Undo.RegisterCreatedObjectUndo(stone, $"Create {stoneName}");

                Debug.Log($"{LOG} Created {stoneName}: scale={scale} mass={mass} kg at {pos}");
            }

            Selection.activeGameObject = parent;

            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(
                UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene());

            Debug.Log($"{LOG} === Test Stones ready. Select TestStones in hierarchy. ===");
        }

        // ------------------------------------------------------------------ //
        //  Private helpers
        // ------------------------------------------------------------------ //

        private static GameObject CreateStone(
            string stoneName,
            Vector3 worldPos,
            float   scale,
            float   mass,
            Material mat,
            Transform parent)
        {
            // Sphere primitive gives us MeshFilter + MeshRenderer + SphereCollider
            var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            go.name = stoneName;
            go.transform.SetParent(parent, worldPositionStays: false);
            go.transform.position   = worldPos;
            go.transform.localScale = Vector3.one * scale;

            // Apply shared material
            var renderer = go.GetComponent<Renderer>();
            if (renderer != null)
                renderer.sharedMaterial = mat;

            // Rigidbody -- ThrowableStone.Awake() will also configure it,
            // but we set sensible defaults here so the inspector shows them.
            var rb = go.AddComponent<Rigidbody>();
            rb.mass = mass;
            rb.collisionDetectionMode = CollisionDetectionMode.Continuous;
            rb.isKinematic = true; // ThrowableStone starts kinematic

            // ThrowableStone -- use SerializedObject so the _mass field in the
            // inspector reflects the preset value immediately.
            var stone = go.AddComponent<ThrowableStone>();
            var so    = new SerializedObject(stone);
            var prop  = so.FindProperty("_mass");
            if (prop != null)
            {
                prop.floatValue = mass;
                so.ApplyModifiedProperties();
            }

            return go;
        }

        /// <summary>
        /// Creates a URP Lit material with grey albedo and high roughness.
        /// Saves it as an asset so it persists across domain reloads.
        /// If the asset already exists it is reused.
        /// </summary>
        private static Material CreateStoneMaterial()
        {
            const string assetPath = "Assets/Materials/Stone_Grey.mat";

            // Reuse existing asset if present
            var existing = AssetDatabase.LoadAssetAtPath<Material>(assetPath);
            if (existing != null)
            {
                Debug.Log($"{LOG} Reusing existing material: {assetPath}");
                return existing;
            }

            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null)
            {
                Debug.LogWarning($"{LOG} URP Lit shader not found -- falling back to Standard.");
                shader = Shader.Find("Standard");
            }

            var mat = new Material(shader);
            mat.name = "Stone_Grey";

            // Albedo: mid-grey with a very slight warm tint (stone-like)
            mat.color = new Color(0.45f, 0.43f, 0.40f, 1f);

            // URP Lit property names
            // Smoothness 0 = fully rough; Metallic 0 = non-metal
            if (mat.HasProperty("_Smoothness"))
                mat.SetFloat("_Smoothness", 0.15f);    // rough surface

            if (mat.HasProperty("_Metallic"))
                mat.SetFloat("_Metallic", 0.0f);

            // Ensure Materials folder exists
            if (!System.IO.Directory.Exists(Application.dataPath + "/Materials"))
                System.IO.Directory.CreateDirectory(Application.dataPath + "/Materials");

            AssetDatabase.CreateAsset(mat, assetPath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"{LOG} Created material: {assetPath}");
            return mat;
        }
    }
}
#endif
#endif
