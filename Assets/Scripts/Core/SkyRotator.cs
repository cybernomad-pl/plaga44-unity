// =============================================================================
// SkyRotator.cs
// CYBERNOMAD -- Powolna rotacja skyboxa (symulacja wiatru/chmur).
// Speed sterowalny z HamburgerMenu przez SettingsRegistry.
// =============================================================================
using UnityEngine;

namespace Plaga44
{
    public class SkyRotator : MonoBehaviour
    {
        public float rotationSpeed = 0.5f; // stopnie na sekunde

        private Material _skyMat;

        void Start()
        {
            _skyMat = RenderSettings.skybox;
            if (_skyMat != null)
                Debug.Log($"[PLAGA44][SkyRotator] Start: mat={_skyMat.name}, speed={rotationSpeed}");
            else
                Debug.LogWarning("[PLAGA44][SkyRotator] Brak skybox materialu");
        }

        void Update()
        {
            if (_skyMat == null || !_skyMat.HasFloat("_Rotation")) return;

            float rot = _skyMat.GetFloat("_Rotation");
            rot += rotationSpeed * Time.deltaTime;
            if (rot > 360f) rot -= 360f;
            if (rot < 0f) rot += 360f;
            _skyMat.SetFloat("_Rotation", rot);
        }
    }
}
