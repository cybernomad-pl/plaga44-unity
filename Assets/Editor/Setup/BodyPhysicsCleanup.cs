// =============================================================================
// BodyPhysicsCleanup.cs
// CYBERNOMAD -- Usuwa body physics capsule (*_PhysCol) z defaultRig avatara.
// Body physics zostalo OLANE przez Borysa -- collision matrix PlayerBody x Item=ON
// powodowalo konflikt: item w rece zderza sie z body capsule -> gracz leci.
//
// Cleanup:
//   1. Znajdz wszystkie GO "<bone>_PhysCol" pod defaultRig -> Destroy
//   2. Usun Rigidbody z defaultRig root (jesli Kinematic, dodany przez
//      stare BodyPhysicsSetup)
//
// Layery "PlayerBody" i "Item" w TagManager.asset ZOSTAJA -- nie szkodza,
// w przyszlosci mozna do nich wrocic. Collision matrix reset runtime.
// =============================================================================
#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace Plaga44.Editor.Setup
{
    public static class BodyPhysicsCleanup
    {
        private const string LOG              = "[PLAGA44][BodyPhysicsCleanup]";
        private const string ColliderGoSuffix = "_PhysCol";

        public static void Run()
        {
            var avatar = Object.FindAnyObjectByType<Plaga44.PlayerAvatar>();
            GameObject rig = avatar != null ? avatar.defaultRig : null;
            if (rig == null)
            {
                Debug.Log($"{LOG} [SKIP] PlayerAvatar.defaultRig null");
                return;
            }

            int removedCaps = 0;
            var transforms = rig.GetComponentsInChildren<Transform>(true);
            for (int i = transforms.Length - 1; i >= 0; i--)
            {
                var t = transforms[i];
                if (t == null) continue;
                if (t.name.EndsWith(ColliderGoSuffix))
                {
                    Undo.DestroyObjectImmediate(t.gameObject);
                    removedCaps++;
                }
            }

            // Usun Kinematic Rigidbody z rigu root (poprzednia wersja BodyPhysicsSetup)
            bool removedRB = false;
            var rb = rig.GetComponent<Rigidbody>();
            if (rb != null && rb.isKinematic)
            {
                Undo.DestroyObjectImmediate(rb);
                removedRB = true;
            }

            if (removedCaps > 0 || removedRB)
            {
                EditorUtility.SetDirty(rig);
                UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(rig.scene);
                Debug.Log($"{LOG} Removed {removedCaps} *_PhysCol GO + rigidbody={removedRB} from {rig.name}");
            }
            else
            {
                Debug.Log($"{LOG} [OK] No body physics to clean on {rig.name}");
            }
        }
    }
}
#endif
