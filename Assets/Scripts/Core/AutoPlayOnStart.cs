// AutoPlayOnStart.cs -- startuje rozgrywke automatycznie po zaladowaniu sceny.
// Dodawany przez SceneSetup.BuildLocomotionTestbed().
// Bez tego GameState zostaje w Splash i lokomocja nie dziala (guard CanMove).

using UnityEngine;

namespace Plaga44
{
    public class AutoPlayOnStart : MonoBehaviour
    {
        private void Start()
        {
            GameState.Play();
            Debug.Log("[PLAGA44] AutoPlay: GameState -> Playing");
        }
    }
}
