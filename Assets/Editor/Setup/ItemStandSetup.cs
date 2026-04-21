// =============================================================================
// ItemStandSetup.cs
// CYBERNOMAD -- Dodaje fizyczny stolik (ItemStand) do sceny, na ktorym
// spawnuje sie item preview. Bez tego item ladowalby na ziemie po release
// -- trudno podniesc bo crouch w VR slaby.
//
// ItemStand = prosty Cube (~40x80x40 cm) przed graczem, na poziomie ~0.7m.
// Fizyczny collider (bez Rigidbody -- static) zatrzymuje spadajacy item.
// Idempotent: jesli GO "ItemStand" juz istnieje, skip.
// =============================================================================
#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace Plaga44.Editor.Setup
{
    public static class ItemStandSetup
    {
        private const string LOG       = "[PLAGA44][ItemStandSetup]";
        private const string StandName = "ItemStand";

        // Pozycja przed graczem -- testbed player spawn ~ (512, 15, 512) yaw=0
        // stand na 1.2m przed spawn, na poziomie ~0.7m (poziom stolu VR).
        private static readonly Vector3 StandPosition = new Vector3(512f, 15.7f, 513.2f);
        private static readonly Vector3 StandScale    = new Vector3(0.4f, 0.8f, 0.4f);

        public static void Run()
        {
            var existing = GameObject.Find(StandName);
            if (existing != null)
            {
                Debug.Log($"{LOG} [OK] {StandName} already in scene at {existing.transform.position:F2}");
                return;
            }

            var stand = GameObject.CreatePrimitive(PrimitiveType.Cube);
            stand.name = StandName;
            stand.transform.position   = StandPosition;
            stand.transform.localScale = StandScale;

            // Static collider (brak Rigidbody) -- zatrzymuje spadajacy item.
            // BoxCollider jest auto-dodany przez CreatePrimitive.
            // Renderer widoczny -- latwo zobaczyc gdzie stolik.

            Undo.RegisterCreatedObjectUndo(stand, "Create ItemStand");
            EditorUtility.SetDirty(stand);
            Debug.Log($"{LOG} Created {StandName} at {StandPosition:F2} scale={StandScale:F2}");
        }
    }
}
#endif
