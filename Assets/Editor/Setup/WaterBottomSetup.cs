#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Plaga44.Editor.Setup
{
    // Plaskie DNO pod tafla wody, z MeshColliderem. Gracz brodzi (do klatki
    // piersiowej) i staje na dnie -- glowa nigdy pod woda.
    // NIE implementuje plywania (mechanike poda Borys osobno) -- tu tylko podloze + collider.
    //
    // Poziom dna = poziom wody - WadeDepth. Poziom i zasieg wody czytane z REALNEGO
    // WaterPlane 2 w scenie (transform.position.y + renderer.bounds) -- bez zgadywania.
    // Faza Bootstrap "2b-WaterBottom", uruchamiana PO "2-Water".
    public static class WaterBottomSetup
    {
        private const string LOG = "[PLAGA44][WaterBottomSetup]";
        private const string BottomName = "WaterBottom";
        private const string WaterPlaneName = "WaterPlane 2";
        private const string MatPath = "Assets/PLAGA44/Materials/WaterBottom.mat";

        // Glebokosc brodzenia (m): odleglosc od tafli wody do dna. Gracz stoi na dnie,
        // woda siega ~klatki piersiowej. DO POTWIERDZENIA z Borysem (wartosc designowa).
        private const float WadeDepth = 1.2f;

        // Zapas na footprint dna wzgledem tafli wody (dno lekko szersze niz woda).
        private const float FootprintMargin = 1.15f;

        public static bool Run(BootstrapConfig cfg)
        {
            // Idempotencja: skasuj poprzednie dno przy ponownym Bootstrap.
            var existing = GameObject.Find(BottomName);
            if (existing != null) Undo.DestroyObjectImmediate(existing);

            var water = GameObject.Find(WaterPlaneName);
            if (water == null)
            {
                Debug.LogError($"{LOG} brak '{WaterPlaneName}' w scenie -- dno nie utworzone (uruchom po 2-Water).");
                return false;
            }

            var wr = water.GetComponent<MeshRenderer>();
            if (wr == null)
            {
                Debug.LogError($"{LOG} '{WaterPlaneName}' bez MeshRenderer -- nie ustale zasiegu tafli wody.");
                return false;
            }

            Bounds b = wr.bounds; // world-space AABB tafli wody
            float surfaceY = water.transform.position.y;
            float bottomY = surfaceY - WadeDepth;

            var plane = GameObject.CreatePrimitive(PrimitiveType.Plane);
            plane.name = BottomName;

            // Primitive Plane = 10x10 world units przy scale 1 -> skalujemy do footprintu wody.
            float sx = (b.size.x * FootprintMargin) / 10f;
            float sz = (b.size.z * FootprintMargin) / 10f;
            plane.transform.localScale = new Vector3(sx, 1f, sz);
            plane.transform.position = new Vector3(b.center.x, bottomY, b.center.z);

            // MeshCollider -- gracz stoi na dnie. Primitive Plane dostaje MeshCollider
            // domyslnie, ale wymuszamy pewnosc.
            var mc = plane.GetComponent<MeshCollider>();
            if (mc == null) mc = plane.AddComponent<MeshCollider>();

            var mat = GetOrCreateBottomMaterial();
            if (mat != null) plane.GetComponent<MeshRenderer>().sharedMaterial = mat;

            SceneManager.MoveGameObjectToScene(plane, SceneManager.GetActiveScene());
            Undo.RegisterCreatedObjectUndo(plane, "Add WaterBottom");

            Debug.Log($"{LOG} [OK] '{BottomName}' Y={bottomY:0.###} (woda Y={surfaceY:0.###}, glebokosc {WadeDepth}m), " +
                      $"footprint {b.size.x * FootprintMargin:0.#}x{b.size.z * FootprintMargin:0.#}, center=({b.center.x:0.#},{b.center.z:0.#}), MeshCollider={(mc != null)}.");
            return true;
        }

        private static Material GetOrCreateBottomMaterial()
        {
            var urp = Shader.Find("Universal Render Pipeline/Lit");
            if (urp == null)
            {
                Debug.LogError($"{LOG} shader 'Universal Render Pipeline/Lit' nie znaleziony -- dno bez materialu.");
                return null;
            }

            var mat = AssetDatabase.LoadAssetAtPath<Material>(MatPath);
            if (mat == null)
            {
                BootstrapUtils.EnsureFolder("Assets/PLAGA44", "Materials");
                mat = new Material(urp) { name = "WaterBottom" };
                AssetDatabase.CreateAsset(mat, MatPath);
            }
            else if (mat.shader != urp)
            {
                mat.shader = urp;
            }

            // Ciemny mul/piasek -- prosty, matowy.
            mat.SetColor("_BaseColor", new Color(0.12f, 0.11f, 0.09f, 1f));
            mat.SetFloat("_Smoothness", 0.15f);
            mat.SetFloat("_Metallic", 0f);

            EditorUtility.SetDirty(mat);
            AssetDatabase.SaveAssets();
            return mat;
        }
    }
}
#endif
