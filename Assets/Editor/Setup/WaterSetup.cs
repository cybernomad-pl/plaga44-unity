#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Plaga44.Editor.Setup
{
    public static class WaterSetup
    {
        private const string LOG = "[PLAGA44][WaterSetup]";
        private const string SourceScene = "Assets/PLAGA44/TESTBED_V6.unity";
        private const string PrefabPath = "Assets/PLAGA44/Prefabs/Environment.prefab";
        private const string Name = "Environment";
        private const string WaterPlaneName = "WaterPlane 2";
        private const string WaterMatPath = "Assets/PLAGA44/Materials/WaterTransparent.mat";

        public static bool Run(BootstrapConfig cfg)
        {
            var env = GameObject.Find(Name);
            bool created = false;
            if (env == null)
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath) ?? ExtractFromV6();
                if (prefab == null)
                {
                    Debug.LogError($"{LOG} brak {PrefabPath} i nie sklonowano {Name} z V6");
                    return false;
                }

                env = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
                env.name = Name;
                SceneManager.MoveGameObjectToScene(env, SceneManager.GetActiveScene());
                created = true;
            }

            // ZAWSZE (takze gdy Environment juz istnial): wymus collider + material wody.
            // Inaczej re-run Bootstrap na istniejacym Environment zostawilby WaterPlane 2
            // na magenta FG_PBR_Water (built-in surface shader). "Wszystko przez Bootstrap"
            // = po kazdym uruchomieniu woda ma poprawny URP/Lit Transparent.
            //
            // Prefab nie ma MeshCollidera na tafli wody -- dogrywamy (gracz stoi na wodzie).
            AddWaterPlaneCollider(env);
            // BGR_Water = FG_PBR_Water (built-in surface shader) -> magenta pod URP.
            // Podmieniamy na URP/Lit Transparent -> polprzezroczysta woda (widac dno).
            ApplyTransparentWaterMaterial(env);
            return created;
        }

        // Jedyny renderer wody = 'WaterPlane 2' (potwierdzone: '3D_Water (N)' to
        // AudioSource'y, nie meshe). Oryginalny material BGR_Water uzywa FG_PBR_Water
        // (built-in surface shader, CGPROGRAM) -> MAGENTA pod URP. Podmieniamy na
        // URP/Lit Transparent. Zero fallback: brak WaterPlane 2 -> LogError, nie zgaduj.
        private static void ApplyTransparentWaterMaterial(GameObject root)
        {
            var t = FindByNameDeep(root.transform, WaterPlaneName);
            if (t == null)
            {
                Debug.LogError($"{LOG} brak '{WaterPlaneName}' w {Name} -- material wody nie podmieniony.");
                return;
            }

            var mr = t.GetComponent<MeshRenderer>();
            if (mr == null)
            {
                Debug.LogError($"{LOG} '{WaterPlaneName}' bez MeshRenderer -- material wody nie podmieniony.");
                return;
            }

            var mat = GetOrCreateTransparentWaterMaterial();
            if (mat == null) return; // LogError juz w helperze

            mr.sharedMaterial = mat;
            Debug.Log($"{LOG} [OK] material wody -> URP/Lit Transparent ('{mat.name}').");
        }

        private static Material GetOrCreateTransparentWaterMaterial()
        {
            var urp = Shader.Find("Universal Render Pipeline/Lit");
            if (urp == null)
            {
                Debug.LogError($"{LOG} shader 'Universal Render Pipeline/Lit' nie znaleziony -- material wody nie utworzony.");
                return null;
            }

            var mat = AssetDatabase.LoadAssetAtPath<Material>(WaterMatPath);
            if (mat == null)
            {
                BootstrapUtils.EnsureFolder("Assets/PLAGA44", "Materials");
                mat = new Material(urp) { name = "WaterTransparent" };
                AssetDatabase.CreateAsset(mat, WaterMatPath);
            }
            else if (mat.shader != urp)
            {
                mat.shader = urp;
            }

            // URP Lit: Surface Type = Transparent, Alpha blend.
            mat.SetFloat("_Surface", 1f);   // 0 = Opaque, 1 = Transparent
            mat.SetFloat("_Blend", 0f);     // 0 = Alpha
            mat.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            mat.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            mat.SetFloat("_ZWrite", 0f);
            mat.SetFloat("_Metallic", 0f);
            mat.SetFloat("_Smoothness", 0.9f);
            mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            mat.DisableKeyword("_ALPHATEST_ON");
            mat.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            mat.SetOverrideTag("RenderType", "Transparent");
            mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;

            // Jasniejszy niebiesko-zielony, polprzezroczysty (alpha 0.6).
            var col = new Color(0.30f, 0.60f, 0.62f, 0.6f);
            mat.SetColor("_BaseColor", col);
            if (mat.HasProperty("_Color")) mat.SetColor("_Color", col);

            EditorUtility.SetDirty(mat);
            AssetDatabase.SaveAssets();
            return mat;
        }

        private static void AddWaterPlaneCollider(GameObject root)
        {
            var t = FindByNameDeep(root.transform, WaterPlaneName);
            if (t == null)
            {
                Debug.LogError($"{LOG} brak '{WaterPlaneName}' w {Name} -- MeshCollider nie dodany.");
                return;
            }

            var mf = t.GetComponent<MeshFilter>();
            if (mf == null || mf.sharedMesh == null)
            {
                Debug.LogError($"{LOG} '{WaterPlaneName}' bez MeshFilter/mesh -- MeshCollider nie dodany.");
                return;
            }

            var mc = t.GetComponent<MeshCollider>();
            if (mc == null) mc = t.gameObject.AddComponent<MeshCollider>();
            mc.sharedMesh = mf.sharedMesh;
            Debug.Log($"{LOG} [OK] MeshCollider na '{WaterPlaneName}'.");
        }

        private static Transform FindByNameDeep(Transform root, string name)
        {
            if (root.name == name) return root;
            for (int i = 0; i < root.childCount; i++)
            {
                var found = FindByNameDeep(root.GetChild(i), name);
                if (found != null) return found;
            }
            return null;
        }

        private static GameObject ExtractFromV6()
        {
            if (!System.IO.File.Exists(SourceScene)) return null;

            Scene src = EditorSceneManager.OpenScene(SourceScene, OpenSceneMode.Additive);
            try
            {
                GameObject envSrc = null;
                foreach (var root in src.GetRootGameObjects())
                    if (root.name == Name) { envSrc = root; break; }
                if (envSrc == null) return null;

                var clone = Object.Instantiate(envSrc);
                clone.name = Name;

                BootstrapUtils.EnsureFolder("Assets/PLAGA44", "Prefabs");
                var prefab = PrefabUtility.SaveAsPrefabAsset(clone, PrefabPath, out bool ok);
                Object.DestroyImmediate(clone);
                return ok ? prefab : null;
            }
            finally { EditorSceneManager.CloseScene(src, true); }
        }
    }
}
#endif
