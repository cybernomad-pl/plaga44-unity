// =============================================================================
// EditorCameraHeight.cs
// CYBERNOMAD -- Wymusza wysokosc kamery w edytorze bez headsetu VR.
//
// PROBLEM:
// OVRCameraRig co klatke ustawia CenterEyeAnchor na podstawie trackingu.
// W edytorze bez headsetu tracking zwraca (0,0,0) wiec kamera laduje
// na pozycji riga = poziom ziemi = kamera wtapia sie w teren.
//
// ROZWIAZANIE:
// LateUpdate (po OVRCameraRig.Update) wymusza CenterEyeAnchor.localPosition.y
// na wysokosc oczu (1.664m -- zmierzone z PLAYER.obj Eyes group).
// Dziala TYLKO w edytorze bez XR -- na Questcie
// tracking nadpisuje pozycje wiec ten komponent nic nie robi.
//
// SETUP:
// Dodawany automatycznie przez SceneSetup.LoadTestbed().
// Attach na OVRCameraRig root (ten sam GO co LocomotionController).
// =============================================================================

using UnityEngine;

namespace Plaga44.Locomotion
{
    /// <summary>
    /// Wymusza wysokosc kamery na 1.664m w edytorze bez headsetu.
    /// Na urzadzeniu VR (Quest) nie robi nic -- tracking nadpisuje pozycje.
    /// Uzywa LateUpdate zeby dzialac PO OVRCameraRig.Update().
    /// </summary>
    [DisallowMultipleComponent]
    public class EditorCameraHeight : MonoBehaviour
    {
        [Tooltip("Wysokosc oczu gracza w metrach.")]
        public float eyeHeight = 1.664f;  // z PLAYER.obj Eyes group

        private Transform _cameraTransform;
        private bool _isVRActive;

        private const string LOG = "[PLAGA44][CamHeight]";

        private void Start()
        {
            _isVRActive = UnityEngine.XR.XRSettings.isDeviceActive;
            Debug.Log($"{LOG} Start: XR active={_isVRActive}, eyeHeight={eyeHeight}");

            if (_isVRActive)
            {
                Debug.Log($"{LOG} VR tracking active -- wylaczam komponent");
                enabled = false;
                return;
            }

            _cameraTransform = FindCameraTransform();

            if (_cameraTransform == null)
            {
                Debug.LogError($"{LOG} BRAK KAMERY -- wylaczam komponent");
                enabled = false;
            }
            else
            {
                Debug.Log($"{LOG} Kamera: {_cameraTransform.name}, localPos={_cameraTransform.localPosition}");
            }
        }

        private void LateUpdate()
        {
            // LateUpdate bo OVRCameraRig ustawia pozycje w Update.
            // My nadpisujemy PO nim.
            if (_cameraTransform == null) return;

            var pos = _cameraTransform.localPosition;
            // Zawsze ustawiaj -- eyeHeight moze sie zmieniac dynamicznie (crouch)
            if (Mathf.Abs(pos.y - eyeHeight) > 0.001f)
            {
                _cameraTransform.localPosition = new Vector3(pos.x, eyeHeight, pos.z);
            }
        }

        private Transform FindCameraTransform()
        {
            // Szukaj CenterEyeAnchor (OVRCameraRig hierarchy)
            var tracking = transform.Find("TrackingSpace");
            if (tracking != null)
            {
                var eye = tracking.Find("CenterEyeAnchor");
                if (eye != null) return eye;
            }

            // Fallback
            var cam = GetComponentInChildren<Camera>();
            return cam != null ? cam.transform : null;
        }
    }
}
