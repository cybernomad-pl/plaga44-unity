// =============================================================================
// PlayerPhysicsLayersRuntime.cs
// CYBERNOMAD -- Runtime configuration collision matrix dla warstw
// PlayerBody (6) / Item (7). Physics.IgnoreLayerCollision nie persystuje
// do DynamicsManager.asset -- zawsze trzeba ustawic runtime.
//
// Editor-time PlayerPhysicsLayers.cs tworzy NAZWY warstw w TagManager
// (persistent w ProjectSettings/TagManager.asset). Ta klasa ustawia
// COLLISION MATRIX (runtime only).
//
// Logika (musi byc spojna z PlayerPhysicsLayers.cs):
//   PlayerBody x {Default, TransparentFX, IgnoreRaycast, Water, UI, PlayerBody} = OFF
//   PlayerBody x Item = ON
//   Item x Default = ON (default, nie ruszamy)
// =============================================================================

using UnityEngine;

namespace Plaga44
{
    public static class PlayerPhysicsLayersRuntime
    {
        private const string LOG = "[PLAGA44][PhysicsLayers][Runtime]";

        // Musi zgadzac sie z PlayerPhysicsLayers.cs (editor-side)
        public const int PlayerBodyLayer = 6;
        public const int ItemLayer       = 7;

        // Built-in Unity layers
        private const int DefaultLayer       = 0;
        private const int TransparentFXLayer = 1;
        private const int IgnoreRaycastLayer = 2;
        private const int WaterLayer         = 4;
        private const int UILayer            = 5;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void ConfigureCollisionMatrix()
        {
            // PlayerBody NIE koliduje ze srodowiskiem -> capsule nog nie odbijaja
            // od terenu -> gracz nie fruwa.
            SetIgnore(PlayerBodyLayer, DefaultLayer,       true);
            SetIgnore(PlayerBodyLayer, TransparentFXLayer, true);
            SetIgnore(PlayerBodyLayer, IgnoreRaycastLayer, true);
            SetIgnore(PlayerBodyLayer, WaterLayer,         true);
            SetIgnore(PlayerBodyLayer, UILayer,            true);

            // PlayerBody vs PlayerBody -- wlasne konczyny nie odbijaja, dwa
            // awatary obok tez nie.
            SetIgnore(PlayerBodyLayer, PlayerBodyLayer, true);

            // PlayerBody vs Item -- cel body physics, reka/tors blokuje item.
            SetIgnore(PlayerBodyLayer, ItemLayer, false);

            Debug.Log($"{LOG} Collision matrix set: PlayerBody vs env = OFF, PlayerBody vs Item = ON");
        }

        private static void SetIgnore(int a, int b, bool ignore)
        {
            if (Physics.GetIgnoreLayerCollision(a, b) != ignore)
                Physics.IgnoreLayerCollision(a, b, ignore);
        }
    }
}
