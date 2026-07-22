// =============================================================================
// SkyFlipByAltitude.cs
// CYBERNOMAD -- Odwracanie skyboxa wraz z wysokoscia gracza.
// Im wyzej gracz (blizej "stratosfery"), tym bardziej niebo sie odwraca:
// ciemny GROUND wedruje na gore, chmury pod nogi. Steruje _FlipAmount
// w materiale skyboxa (shader Flooded_Grounds/Skybox_Rotating).
// =============================================================================
using UnityEngine;

namespace Plaga44
{
    public class SkyFlipByAltitude : MonoBehaviour
    {
        private const string LOG = "[PLAGA44][SkyFlip]";
        private const string FlipProperty = "_FlipAmount";

        [Header("Zrodlo wysokosci")]
        [Tooltip("Transform gracza / glowy (VR camera rig). Wymagane -- brak = skrypt sie wylacza.")]
        public Transform player;

        [Header("Przedzial wysokosci (world Y, jednostki Unity)")]
        [Tooltip("Ponizej tej wysokosci niebo jest normalne (flip = 0).")]
        public float groundY = 0f;
        [Tooltip("Na tej wysokosci niebo jest w pelni odwrocone (flip = 1) -- 'stratosfera'.")]
        public float stratosphereY = 3000f;

        [Header("Krzywa reakcji")]
        [Tooltip("Mapuje znormalizowana wysokosc 0..1 na sile flipa 0..1. " +
                 "Domyslnie liniowa; wygnij zeby 'im blizej tym bardziej'.")]
        public AnimationCurve response = AnimationCurve.Linear(0f, 0f, 1f, 1f);

        [Header("Diagnostyka")]
        [Tooltip("Co ile sekund logowac stan (0 = nigdy).")]
        public float logIntervalSec = 5f;

        private Material _skyMat;
        private float _initialFlip;
        private float _lastLogTime;

        private void Start()
        {
            if (player == null)
            {
                Debug.LogError($"{LOG} Pole 'player' nie przypisane -- przypnij Transform gracza w Inspectorze. Wylaczam.");
                enabled = false;
                return;
            }

            _skyMat = RenderSettings.skybox;
            if (_skyMat == null)
            {
                Debug.LogError($"{LOG} Brak skybox materialu (RenderSettings.skybox == null). Wylaczam.");
                enabled = false;
                return;
            }
            if (!_skyMat.HasFloat(FlipProperty))
            {
                Debug.LogError($"{LOG} Material '{_skyMat.name}' nie ma property {FlipProperty} " +
                               $"-- czy to shader Skybox_Rotating? Wylaczam.");
                enabled = false;
                return;
            }
            if (Mathf.Approximately(stratosphereY, groundY))
            {
                Debug.LogError($"{LOG} stratosphereY == groundY ({groundY}) -- dzielenie przez zero w mapowaniu. Wylaczam.");
                enabled = false;
                return;
            }

            _initialFlip = _skyMat.GetFloat(FlipProperty);
            _lastLogTime = Time.time;
            Debug.Log($"{LOG} Start: mat={_skyMat.name}, ground={groundY}, strato={stratosphereY}, initFlip={_initialFlip:F2}");
        }

        private void Update()
        {
            float worldY = player.position.y;
            float t = Mathf.Clamp01((worldY - groundY) / (stratosphereY - groundY));
            float flip = Mathf.Clamp01(response.Evaluate(t));
            _skyMat.SetFloat(FlipProperty, flip);
            LogOccasionally(worldY, t, flip);
        }

        // Przywraca material do stanu sprzed Play -- RenderSettings.skybox to shared
        // asset, bez tego flip zostaje zapisany w pliku .mat po zatrzymaniu edytora.
        private void OnDisable()
        {
            if (_skyMat != null && _skyMat.HasFloat(FlipProperty))
                _skyMat.SetFloat(FlipProperty, _initialFlip);
        }

        private void LogOccasionally(float worldY, float t, float flip)
        {
            if (logIntervalSec <= 0 || Time.time - _lastLogTime < logIntervalSec) return;
            Debug.Log($"{LOG} y={worldY:F0} -> t={t:F2} -> flip={flip:F2}");
            _lastLogTime = Time.time;
        }
    }
}
