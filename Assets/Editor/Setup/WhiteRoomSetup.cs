// =============================================================================
// WhiteRoomSetup.cs
// CYBERNOMAD -- "konstruktor z Matrixa": ogromny bialy zamkniety pokoj do
// whiteboxingu. Zastepuje CALE environment (woda/teren/skybox/bounce -- precz).
//
// Zawartosc:
//   WhiteRoom (root, 0,0,0)
//     Floor    -- Size x Size m, TOP na Y=0, BoxCollider
//     Wall_N/S/E/W + Ceiling -- zamkniety szescian (nic nie wypada)
//   Directional Light -- z configa (sun*), soft shadows
//   RenderSettings: skybox=null, ambient FLAT bialy, fog OFF
//
// SAMONAPRAWA przy KAZDYM runie (nie tylko przy tworzeniu):
//   - zle wymiary (stary pokoj po zmianie Size/Height) -> ZBURZ i postaw od nowa
//   - material re-aplikowany na wszystkie plyty ZAWSZE (zadnej magenty po rerunie)
// Shader z AKTYWNEGO render pipeline (GraphicsSettings.defaultShader), nie
// Shader.Find po nazwie -- kuloodporne na zmiany URP. ZERO FALLBACK: brak
// pipeline/shadera -> LogError i stop.
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
        private const float Size = 300f;      // szer/glab -- OGROMNY (konstruktor)
        private const float Height = 60f;     // wysokosc scian
        private const float Thickness = 1f;   // grubosc plyt

        public static bool Run(BootstrapConfig cfg)
        {
            var mat = GetOrCreateWhiteMaterial();
            if (mat == null) return false; // LogError w helperze

            bool changed = false;
            changed |= EnsureRoom(mat);
            changed |= EnsureSun(cfg);
            changed |= ApplyRenderSettings();
            return changed;
        }

        private static bool EnsureRoom(Material mat)
        {
            var existing = GameObject.Find(RootName);

            // Istniejacy pokoj o ZLYCH wymiarach (np. po zmianie Size) -> zburz, postaw nowy.
            if (existing != null && !HasCurrentDimensions(existing))
            {
                Undo.DestroyObjectImmediate(existing);
                existing = null;
                Debug.Log($"{LOG} [REBUILD] stary pokoj mial inne wymiary -- burze i stawiam {Size}x{Height}x{Size}.");
            }

            if (existing != null)
            {
                // Wymiary OK -- tylko wymus material (samonaprawa po ew. magencie).
                return ReapplyMaterial(existing, mat);
            }

            var root = new GameObject(RootName);
            Undo.RegisterCreatedObjectUndo(root, "Create WhiteRoom");

            float half = Size / 2f;
            float ht = Thickness / 2f;

            Slab(root, mat, "Floor",   new Vector3(0, -ht, 0),             new Vector3(Size, Thickness, Size));
            Slab(root, mat, "Ceiling", new Vector3(0, Height + ht, 0),     new Vector3(Size, Thickness, Size));
            Slab(root, mat, "Wall_N",  new Vector3(0, Height / 2f, half),  new Vector3(Size, Height, Thickness));
            Slab(root, mat, "Wall_S",  new Vector3(0, Height / 2f, -half), new Vector3(Size, Height, Thickness));
            Slab(root, mat, "Wall_E",  new Vector3(half, Height / 2f, 0),  new Vector3(Thickness, Height, Size));
            Slab(root, mat, "Wall_W",  new Vector3(-half, Height / 2f, 0), new Vector3(Thickness, Height, Size));

            Debug.Log($"{LOG} [CREATED] {RootName} {Size}x{Height}x{Size} m (6 plyt, collidery).");
            return true;
        }

        // Pokoj "aktualny" = Floor istnieje i ma skale zgodna z Size (marker wersji geometrii).
        private static bool HasCurrentDimensions(GameObject root)
        {
            var floor = root.transform.Find("Floor");
            return floor != null && Mathf.Approximately(floor.localScale.x, Size);
        }

        private static bool ReapplyMaterial(GameObject root, Material mat)
        {
            bool changed = false;
            foreach (var mr in root.GetComponentsInChildren<MeshRenderer>(true))
            {
                if (mr.sharedMaterial == mat) continue;
                mr.sharedMaterial = mat;
                changed = true;
            }
            if (changed) Debug.Log($"{LOG} [FIX] material '{mat.name}' wymuszony na plytach pokoju.");
            else Debug.Log($"{LOG} [OK] {RootName} (wymiary i material aktualne).");
            return changed;
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

        // Shader Lit z AKTYWNEGO pipeline (URP) -- zrodlo prawdy, nie string-lookup.
        private static Material GetOrCreateWhiteMaterial()
        {
            var rp = GraphicsSettings.currentRenderPipeline;
            var lit = rp != null ? rp.defaultShader : null;
            if (lit == null)
            {
                Debug.LogError($"{LOG} brak aktywnego render pipeline / defaultShader -- material nie utworzony (sprawdz GraphicsSettings).");
                return null;
            }

            var mat = AssetDatabase.LoadAssetAtPath<Material>(MatPath);
            if (mat == null)
            {
                BootstrapUtils.EnsureFolder("Assets/PLAGA44", "Materials");
                mat = new Material(lit) { name = "WhiteRoom" };
                AssetDatabase.CreateAsset(mat, MatPath);
                Debug.Log($"{LOG} [CREATED] {MatPath} (shader: {lit.name}).");
            }
            else if (mat.shader != lit)
            {
                mat.shader = lit;
                Debug.Log($"{LOG} [FIX] shader materialu -> {lit.name} (byl inny/uszkodzony).");
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
