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
        public float logIntervalSec = 5f;  // co ile sekund logowac stan (0 = nigdy)

        private Material _skyMat;
        private float _lastLogTime;
        private float _lastLoggedRot;

        void Start()
        {
            _skyMat = RenderSettings.skybox;
            if (_skyMat != null)
                Debug.Log($"[PLAGA44][SkyRotator] Start: mat={_skyMat.name}, speed={rotationSpeed}");
            else
                Debug.LogWarning("[PLAGA44][SkyRotator] Brak skybox materialu");

            _lastLogTime = Time.time;
            if (_skyMat != null && _skyMat.HasFloat("_Rotation"))
                _lastLoggedRot = _skyMat.GetFloat("_Rotation");
        }

        void Update()
        {
            if (_skyMat == null || !_skyMat.HasFloat("_Rotation")) return;

            float rot = _skyMat.GetFloat("_Rotation");
            float newRot = rot + rotationSpeed * Time.deltaTime;
            if (newRot > 360f) newRot -= 360f;
            if (newRot < 0f) newRot += 360f;
            _skyMat.SetFloat("_Rotation", newRot);

            // Throttled log -- raz na logIntervalSec
            if (logIntervalSec > 0 && Time.time - _lastLogTime >= logIntervalSec)
            {
                float deltaRot = newRot - _lastLoggedRot;
                // Kompensuj owrap 0/360
                if (deltaRot < -180f) deltaRot += 360f;
                if (deltaRot >  180f) deltaRot -= 360f;
                Debug.Log(
                    $"[PLAGA44][SkyRotator] Rotation: {_lastLoggedRot:F1} -> {newRot:F1} " +
                    $"(delta={deltaRot:+0.0;-0.0;0.0} over {logIntervalSec}s, speed={rotationSpeed:F2}dps)"
                );
                _lastLogTime = Time.time;
                _lastLoggedRot = newRot;
            }
        }
    }
}
