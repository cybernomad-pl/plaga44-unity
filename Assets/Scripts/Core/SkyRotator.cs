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
        private const string LOG = "[PLAGA44][SkyRotator]";
        private const string RotationProperty = "_Rotation";
        private const float FullRotationDeg = 360f;
        private const float HalfRotationDeg = 180f;

        public float rotationSpeed = 0.5f; // stopnie na sekunde
        public float logIntervalSec = 0f;  // co ile sekund logowac stan (0 = nigdy). Borys: spam, OFF.

        private Material _skyMat;
        private float _lastLogTime;
        private float _lastLoggedRot;

        private void Start()
        {
            _skyMat = RenderSettings.skybox;
            if (_skyMat == null) { Debug.LogWarning($"{LOG} Brak skybox materialu"); return; }

            Debug.Log($"{LOG} Start: mat={_skyMat.name}, speed={rotationSpeed}");
            _lastLogTime = Time.time;
            if (_skyMat.HasFloat(RotationProperty))
                _lastLoggedRot = _skyMat.GetFloat(RotationProperty);
        }

        private void Update()
        {
            if (_skyMat == null || !_skyMat.HasFloat(RotationProperty)) return;

            float newRot = AdvanceRotation(_skyMat.GetFloat(RotationProperty));
            _skyMat.SetFloat(RotationProperty, newRot);
            LogRotationOccasionally(newRot);
        }

        private float AdvanceRotation(float current)
        {
            float next = current + rotationSpeed * Time.deltaTime;
            if (next >= FullRotationDeg) next -= FullRotationDeg;
            if (next < 0f) next += FullRotationDeg;
            return next;
        }

        private void LogRotationOccasionally(float currentRot)
        {
            if (logIntervalSec <= 0 || Time.time - _lastLogTime < logIntervalSec) return;

            float delta = WrapDelta(currentRot - _lastLoggedRot);
            Debug.Log(
                $"{LOG} Rotation: {_lastLoggedRot:F1} -> {currentRot:F1} " +
                $"(delta={delta:+0.0;-0.0;0.0} over {logIntervalSec}s, speed={rotationSpeed:F2}dps)");
            _lastLogTime = Time.time;
            _lastLoggedRot = currentRot;
        }

        // Kompensuje wrap 0/360 zeby delta pokazywalo sensowny kierunek
        private static float WrapDelta(float delta)
        {
            if (delta < -HalfRotationDeg) return delta + FullRotationDeg;
            if (delta > HalfRotationDeg) return delta - FullRotationDeg;
            return delta;
        }
    }
}
