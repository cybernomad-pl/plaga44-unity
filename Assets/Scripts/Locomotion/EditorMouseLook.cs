// =============================================================================
// EditorMouseLook.cs
// CYBERNOMAD -- Mouse look w edytorze bez headsetu VR.
//
// W edytorze bez Questa nie ma head trackingu, wiec obrot kamery musi
// byc na myszce. Ruch myszy = obrot kamery (FPS style, kursor zablokowany).
//
// OBROT:
// - Yaw (lewo/prawo): obraca CALY RIG (zeby LocomotionController
//   mogl uzywac head forward do kierunku ruchu).
// - Pitch (gora/dol): obraca TYLKO kamere (CenterEyeAnchor).
//   Pitch jest clampowany do -80..+80 zeby nie przekrecic.
//
// Na Questcie komponent sie wylacza -- tracking robi swoje.
//
// SETUP:
// Dodawany automatycznie przez SceneSetup.LoadTestbed().
// Attach na OVRCameraRig root.
// =============================================================================

using UnityEngine;

namespace Plaga44.Locomotion
{
    [DisallowMultipleComponent]
    public class EditorMouseLook : MonoBehaviour
    {
        [Header("Czulosc")]
        [Tooltip("Czulosc obrotu myszka.")]
        public float sensitivity = 2f;

        [Header("Limity pitch")]
        [Tooltip("Maksymalny kat patrzenia w gore (stopnie).")]
        public float maxPitch = 80f;

        [Tooltip("Maksymalny kat patrzenia w dol (stopnie).")]
        public float minPitch = -80f;

        private Transform _cameraTransform;
        private float _pitch;
        private float _yaw;

        private void Start()
        {
            // Na urzadzeniu VR -- wylacz, tracking obraca kamere
            if (UnityEngine.XR.XRSettings.isDeviceActive)
            {
                enabled = false;
                return;
            }

            _cameraTransform = FindCameraTransform();

            if (_cameraTransform == null)
            {
                Debug.LogWarning("[EditorMouseLook] Nie znaleziono kamery.");
                enabled = false;
                return;
            }

            // Startowe katy z aktualnej rotacji
            _yaw = transform.eulerAngles.y;
            _pitch = _cameraTransform.localEulerAngles.x;
            if (_pitch > 180f) _pitch -= 360f;

            // Schowaj i zablokuj kursor w oknie gry
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        private void Update()
        {
            if (!GameState.CanMove) return;

            float mx = UnityEngine.Input.GetAxis("Mouse X") * sensitivity;
            float my = UnityEngine.Input.GetAxis("Mouse Y") * sensitivity;

            // Yaw -- obracamy caly rig (zeby head-relative movement dzialal)
            _yaw += mx;
            transform.rotation = Quaternion.Euler(0f, _yaw, 0f);

            // Pitch -- obracamy tylko kamere
            _pitch -= my;
            _pitch = Mathf.Clamp(_pitch, minPitch, maxPitch);
            _cameraTransform.localRotation = Quaternion.Euler(_pitch, 0f, 0f);
        }

        private Transform FindCameraTransform()
        {
            var tracking = transform.Find("TrackingSpace");
            if (tracking != null)
            {
                var eye = tracking.Find("CenterEyeAnchor");
                if (eye != null) return eye;
            }

            var cam = GetComponentInChildren<Camera>();
            return cam != null ? cam.transform : null;
        }
    }
}
