// AutoPlayOnStart.cs -- startuje rozgrywke automatycznie po zaladowaniu sceny.
// Dodawany przez SceneSetup.BuildLocomotionTestbed().
// Bez tego GameState zostaje w Splash i lokomocja nie dziala (guard CanMove).

using UnityEngine;

namespace Plaga44
{
    public class AutoPlayOnStart : MonoBehaviour
    {
        private void Awake()
        {
            Debug.Log($"[PLAGA44][AutoPlay] Awake: scene={gameObject.scene.name}, GO={gameObject.name}");
        }

        private void Start()
        {
            Debug.Log($"[PLAGA44][AutoPlay] Start: setting GameState -> Playing");
            GameState.Play();
            Debug.Log($"[PLAGA44][AutoPlay] GameState.Current={GameState.Current}, CanMove={GameState.CanMove}");
        }
    }
}
