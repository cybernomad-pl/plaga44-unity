using UnityEngine;

namespace Plaga44.Core
{
    /// <summary>
    /// BoneTouchHaptics -- kontroler wibruje gdy dotyka rigidbody NPC.
    /// Dodaj na kazdy obiekt z OVRGrabber (lewy/prawy kontroler).
    /// Albo: auto-setup przez BoneTouchHapticsBootstrap.
    ///
    /// Wibracja proporcjonalna do predkosci dotyku -- lekki dotyk = delikatna wibracja,
    /// mocne wejscie = silna wibracja.
    /// </summary>
    public class BoneTouchHaptics : MonoBehaviour
    {
        [Header("Haptics")]
        public OVRInput.Controller controller = OVRInput.Controller.RTouch;
        public float baseFrequency = 0.8f;
        public float baseAmplitude = 0.8f;
        public float maxAmplitude = 1.0f;
        public float velocityScale = 0.5f;

        [Header("State")]
        public bool isTouching;

        private float _hapticCooldown;

        void OnTriggerStay(Collider other)
        {
            var bone = other.GetComponentInParent<PoseableBone>();
            if (bone == null) return;

            isTouching = true;

            // Wibracja -- stala, mocna, zawsze
            // Guard: skip when controller not connected (hand tracking mode)
            float amplitude = baseAmplitude;

            ControllerModeHelper.SafeVibration(baseFrequency, amplitude, controller);
            _hapticCooldown = 0.05f; // trzymaj wibracje jeszcze 50ms po utracie kontaktu
        }

        void OnTriggerExit(Collider other)
        {
            var bone = other.GetComponentInParent<PoseableBone>();
            if (bone == null) return;

            isTouching = false;
        }

        void Update()
        {
            if (!isTouching)
            {
                _hapticCooldown -= Time.deltaTime;
                if (_hapticCooldown <= 0f)
                {
                    ControllerModeHelper.SafeVibration(0, 0, controller);
                }
            }
        }

        void OnDisable()
        {
            ControllerModeHelper.SafeVibration(0, 0, controller);
        }
    }

    /// <summary>
    /// Auto-setup: znajduje OVRGrabbery w scenie i dodaje BoneTouchHaptics.
    /// </summary>
    public class BoneTouchHapticsBootstrap : MonoBehaviour
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void AutoSetup()
        {
            var grabbers = FindObjectsByType<OVRGrabber>(FindObjectsSortMode.None);
            foreach (var grabber in grabbers)
            {
                if (grabber.GetComponent<BoneTouchHaptics>() != null) continue;

                var haptics = grabber.gameObject.AddComponent<BoneTouchHaptics>();

                // Rozpoznaj ktory kontroler
                if (grabber.name.ToLower().Contains("left") ||
                    grabber.name.ToLower().Contains("lhand"))
                {
                    haptics.controller = OVRInput.Controller.LTouch;
                }
                else
                {
                    haptics.controller = OVRInput.Controller.RTouch;
                }

                Debug.Log($"[PLAGA44] BoneTouchHaptics added to {grabber.name} ({haptics.controller})");
            }
        }
    }
}
