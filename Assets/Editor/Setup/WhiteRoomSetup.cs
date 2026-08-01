// =============================================================================
// WhiteRoomSetup.cs
// CYBERNOMAD -- "konstruktor z Matrixa": ogromny bialy zamkniety pokoj do
// whiteboxingu. Zastepuje CALE environment (woda/teren/skybox/bounce -- precz).
//
// Zawartosc:
//   WhiteRoom (root)
//     Floor    -- 100x100 m, BoxCollider (gracz stoi, itemy leza)
//     Wall_N/S/E/W -- 4 sciany z colliderami (nic nie wypada z pokoju)
//     Ceiling  -- sufit 30 m nad podloga
//   Directional Light -- z configa (sun*), soft shadows na bialej podlodze
//   RenderSettings: skybox=null, ambient FLAT bialy, fog OFF
//
// Material: jeden WhiteRoom.mat (URP/Lit, bialy, matowy) na wszystkich 6 plytach.
// Podloga TOP na Y=0 -- PlayerRigSetup sadza rig na (0, 0, 0).
// Idempotentne: WhiteRoom w scenie -> nic nie robi.
// =============================================================================
#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace Plaga44.Editor.Setup
{
    public static class WhiteRoomSetup
    {
        private const string LOG = "[PLAGA44][WhiteRoom]";
        private const string RootName = "WhiteRoom";
        private const string MatPath = "Assets/PLAGA44/Materials/WhiteRoom.mat";

        // Wymiary pokoju (m). Podloga TOP na Y=0, sufit na Y=Height.
        private const float Size = 100f;      // szer/glab
        private const float Height = 30f;     // wysokosc scian
        private const float Thickness = 1f;   // grubosc plyt

        public static bool Run(BootstrapConfig cfg)
        {
            bool changed = false;
            changed |= EnsureRoom();
            changed |= EnsureSun(cfg);
            changed |= ApplyRenderSettings();
            return changed;
        }

        private static bool EnsureRoom()
        {
            if (GameObject.Find(RootName) != null) return false;

            var mat = GetOrCreateWhiteMaterial();
            if (mat == null) return false; // LogError w helperze

            var root = new GameObject(RootName);
            Undo.RegisterCreatedObjectUndo(root, "Create WhiteRoom");

            float half = Size / 2f;
            float ht = Thickness / 2f;

            // Plyty: pozycja srodka + skala. Podloga TOP=0 -> srodek na -ht.
            Slab(root, mat, "Floor",   new Vector3(0, -ht, 0),            new Vector3(Size, Thickness, Size));
            Slab(root, mat, "Ceiling", new Vector3(0, Height + ht, 0),    new Vector3(Size, Thickness, Size));
            Slab(root, mat, "Wall_N",  new Vector3(0, Height / 2f, half), new Vector3(Size, Height, Thickness));
            Slab(root, mat, "Wall_S",  new Vector3(0, Height / 2f, -half),new Vector3(Size, Height, Thickness));
            Slab(root, mat, "Wall_E",  new Vector3(half, Height / 2f, 0), new Vector3(Thickness, Height, Size));
            Slab(root, mat, "Wall_W",  new Vector3(-half, Height / 2f, 0),new Vector3(Thickness, Height, Size));

            Debug.Log($"{LOG} [CREATED] {RootName} {Size}x{Height}x{Size} m (6 plyt, collidery).");
            return true;
        }

        private static void Slab(GameObject parent, Material mat, string name, Vector3 pos, Vector3 scale)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube); // ma BoxCollider
            go.name = name;
            go.transform.SetParent(parent.transform, false);
            go.transform.localPosition = pos;
            go.transform.localScale = scale;
            go.GetComponent<MeshRenderer>().sharedMaterial = mat;
            go.isStatic = true;
        }

        private static Material GetOrCreateWhiteMaterial()
        {
            var urp = Shader.Find("Universal Render Pipeline/Lit");
            if (urp == null)
            {
                Debug.LogError($"{LOG} brak shadera 'Universal Render Pipeline/Lit' -- material nie utworzony.");
                return null;
            }

            var mat = AssetDatabase.LoadAssetAtPath<Material>(MatPath);
            if (mat == null)
            {
                BootstrapUtils.EnsureFolder("Assets/PLAGA44", "Materials");
                mat = new Material(urp) { name = "WhiteRoom" };
                AssetDatabase.CreateAsset(mat, MatPath);
            }
            else if (mat.shader != urp)
            {
                mat.shader = urp;
            }

            mat.SetColor("_BaseColor", Color.white);
            mat.SetFloat("_Metallic", 0f);
            mat.SetFloat("_Smoothness", 0.05f); // matowy -- zero odblyskow
            EditorUtility.SetDirty(mat);
            AssetDatabase.SaveAssets();
            return mat;
        }

        // Slonce z configa (sun*). Bez skyboxa -- swiatlo kierunkowe + cienie
        // daja bryle avatara/itemow czytelnosc na bialym tle.
        private static bool EnsureSun(BootstrapConfig cfg)
        {
            foreach (var light in Object.FindObjectsByType<Light>(FindObjectsSortMode.None))
                if (light.type == LightType.Directional && light.gameObject.name == "Directional Light")
                {
                    Debug.Log($"{LOG} [OK] Directional Light juz jest.");
                    return false;
                }

            var go = new GameObject("Directional Light");
            var l = go.AddComponent<Light>();
            l.type = LightType.Directional;
            l.color = cfg.sunColor;
            l.intensity = cfg.sunIntensity;
            l.shadows = cfg.sunShadows;
            go.transform.rotation = Quaternion.Euler(cfg.sunRotation);
            Undo.RegisterCreatedObjectUndo(go, "WhiteRoom: Directional Light");
            Debug.Log($"{LOG} [ADDED] Directional Light (kolor/intensywnosc/cienie z configa).");
            return true;
        }

        // Konstruktor: zadnego nieba, plaski bialy ambient, zero mgly.
        private static bool ApplyRenderSettings()
        {
            bool changed = RenderSettings.skybox != null
                        || RenderSettings.ambientMode != AmbientMode.Flat
                        || RenderSettings.fog;

            RenderSettings.skybox = null;
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.85f, 0.85f, 0.85f);
            RenderSettings.fog = false;

            if (changed) Debug.Log($"{LOG} [SET] skybox=null, ambient flat bialy, fog off.");
            return changed;
        }
    }
}
#endif
