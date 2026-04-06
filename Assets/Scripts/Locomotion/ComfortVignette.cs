// =============================================================================
// ComfortVignette.cs
// CYBERNOMAD -- Winieta komfortu przeciw chorobie lokomocyjnej (motion sickness).
//
// JAK DZIALA:
// Podczas ruchu gracza (smooth locomotion) krawedzie pola widzenia sa
// stopniowo przyciemniane. Im szybciej gracz sie rusza, tym mocniejszy efekt.
// To redukuje "vection" -- iluzje ruchu ktora powoduje nudnosci w VR.
//
// TECHNICZNIE:
// Tworzy World-space Canvas przytwierdzony do kamery VR z tekstura radialna
// (gradient: przezroczysty srodek, czarne krawedzie). Intensywnosc jest
// sterowana przez alpha kanał obrazka.
//
// INTENSYWNOSC BRANA Z:
// LocomotionController.NormalisedSpeed (0 = stoi, 1 = pelna predkosc).
//
// UWAGA:
// Komponent jest wlaczany/wylaczany automatycznie przez LocomotionManager.
// Podczas teleportacji i room-scale winieta jest wylaczona (niepotrzebna).
//
// REFERENCJA: Skopiowany z reference-branch, zmieniony z SmoothLocomotion na
// LocomotionController.
// =============================================================================

using UnityEngine;
using UnityEngine.UI;

namespace Plaga44.Locomotion
{
    /// <summary>
    /// Winieta komfortu -- przyciemnia krawedzie widoku podczas ruchu
    /// zeby zmniejszyc motion sickness. Intensywnosc skaluje z predkoscia gracza.
    /// </summary>
    [DisallowMultipleComponent]
    public class ComfortVignette : MonoBehaviour
    {
        // =====================================================================
        // Pola inspektora
        // =====================================================================

        [Header("Mapowanie intensywnosci")]
        [Tooltip("Intensywnosc winiety przy maksymalnej predkosci (0 = niewidoczna, 1 = pelna czern).")]
        [Range(0f, 1f)]
        public float maxIntensity = 0.6f;

        [Tooltip("Prog predkosci ponizej ktorego winieta zaczyna zanikac.")]
        [Range(0f, 1f)]
        public float fadeInThreshold = 0.05f;

        [Tooltip("Predkosc lerpu intensywnosci. Wyzsza = szybsza reakcja.")]
        public float lerpSpeed = 8f;

        [Header("Kolor")]
        [Tooltip("Kolor winiety. Zwykle czarny.")]
        public Color vignetteColor = Color.black;

        [Header("Reczne referencje (opcjonalne)")]
        [Tooltip("RawImage lub Image uzywany jako overlay winiety. Auto-tworzony jesli null.")]
        [SerializeField] private Graphic vignetteImage;

        [Tooltip("Nazwa parametru shader intensity (jesli uzywasz custom materialu).")]
        [SerializeField] private string vignetteIntensityParam = "_Intensity";

        // =====================================================================
        // Stan runtime
        // =====================================================================

        /// <summary>Referencja do LocomotionController -- zrodlo NormalisedSpeed.</summary>
        private LocomotionController _locomotion;

        /// <summary>Aktualna interpolowana intensywnosc.</summary>
        private float _currentIntensity;

        /// <summary>Czy uzywamy parametru materialu (custom shader).</summary>
        private bool _useMaterialParam;

        /// <summary>Czy uzywamy alpha kanalu obrazka (fallback).</summary>
        private bool _useAlpha;

        /// <summary>Programatycznie wygenerowana tekstura radialna.</summary>
        private Texture2D _vignetteTexture;

        // =====================================================================
        // Unity lifecycle
        // =====================================================================

        private void Awake()
        {
            // Szukamy LocomotionController w rodzicach
            // (winieta jest dzieckiem rig root, LocomotionController tez).
            _locomotion = GetComponentInParent<LocomotionController>(includeInactive: true);

            if (vignetteImage == null)
                CreateFallbackOverlay();
            else
                DetermineRenderMode();
        }

        private void OnEnable()
        {
            // Reset wizualny przy ponownym wlaczeniu.
            ApplyIntensityImmediate(0f);
        }

        private void OnDisable()
        {
            // Wyzeruj winiete przy wylaczeniu (zeby nie zostala "zamrozona").
            ApplyIntensityImmediate(0f);
        }

        private void Update()
        {
            float targetIntensity = 0f;

            // Oblicz docelowa intensywnosc na podstawie predkosci gracza.
            if (_locomotion != null)
            {
                float speed = _locomotion.NormalisedSpeed;
                if (speed > fadeInThreshold)
                {
                    // InverseLerp: mapuje speed z zakresu [fadeInThreshold, 1] na [0, 1],
                    // potem mnozymy przez maxIntensity.
                    targetIntensity = Mathf.InverseLerp(fadeInThreshold, 1f, speed) * maxIntensity;
                }
            }

            // Plynny lerp do docelowej intensywnosci (nie skacze nagle).
            _currentIntensity = Mathf.Lerp(_currentIntensity, targetIntensity, Time.deltaTime * lerpSpeed);
            ApplyIntensityImmediate(_currentIntensity);
        }

        // =====================================================================
        // Publiczne API
        // =====================================================================

        /// <summary>
        /// Bezposrednio ustawia intensywnosc winiety.
        /// Uzywane przez LocomotionManager do zerowania przy zmianie trybu.
        /// </summary>
        /// <param name="intensity">Intensywnosc 0-1.</param>
        public void SetIntensity(float intensity)
        {
            _currentIntensity = intensity;
            ApplyIntensityImmediate(intensity);
        }

        // =====================================================================
        // Logika wewnetrzna
        // =====================================================================

        /// <summary>
        /// Aplikuje intensywnosc na grafike -- albo przez parametr materialu,
        /// albo przez alpha kanał koloru.
        /// </summary>
        private void ApplyIntensityImmediate(float intensity)
        {
            if (vignetteImage == null) return;

            if (_useMaterialParam && vignetteImage.material != null)
            {
                vignetteImage.material.SetFloat(vignetteIntensityParam, intensity);
            }
            else if (_useAlpha)
            {
                Color c = vignetteColor;
                c.a = intensity;
                vignetteImage.color = c;
            }
        }

        /// <summary>
        /// Sprawdza jaki tryb renderowania winiety jest dostepny
        /// (parametr materialu vs alpha kanał).
        /// </summary>
        private void DetermineRenderMode()
        {
            if (vignetteImage != null
                && vignetteImage.material != null
                && vignetteImage.material.HasProperty(vignetteIntensityParam))
            {
                _useMaterialParam = true;
            }
            else
            {
                _useAlpha = true;
            }
        }

        /// <summary>
        /// Tworzy fallback overlay gdy nie podano recznych referencji.
        /// World-space Canvas z programatyczna tekstura radialna.
        /// Przypiety do kamery VR zeby poruszal sie z glowa gracza.
        /// </summary>
        private void CreateFallbackOverlay()
        {
            // Szukamy kamery VR do ktorej przypienimy canvas.
            Transform headTransform = null;

#if HAS_META_XR
            // Meta XR: TrackingSpace/CenterEyeAnchor
            var tracking = transform.root.Find("TrackingSpace");
            if (tracking != null)
                headTransform = tracking.Find("CenterEyeAnchor");
#endif
            if (headTransform == null && Camera.main != null)
                headTransform = Camera.main.transform;

            // Tworzymy Canvas w trybie World Space.
            // ScreenSpaceOverlay nie dziala w VR -- obraz jest renderowany per-oko,
            // wiec potrzebujemy World Space zeby winieta byla widoczna w obu oczach.
            var canvasGO = new GameObject("ComfortVignetteCanvas");
            canvasGO.transform.SetParent(headTransform != null ? headTransform : transform, false);

            var canvas = canvasGO.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;

            // Pozycja 31cm przed kamera -- blisko zeby wypelniac pole widzenia.
            canvasGO.transform.localPosition = new Vector3(0f, 0f, 0.31f);
            canvasGO.transform.localRotation = Quaternion.identity;

            // Rozmiar dopasowany do ~110 stopni FOV Quest 3 przy 0.31m odleglosci.
            float halfSize = Mathf.Tan(55f * Mathf.Deg2Rad) * 0.31f;
            var rt = canvasGO.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(halfSize * 2f, halfSize * 2f);

            // Winieta nie powinna blokowac raycastow UI.
            canvasGO.AddComponent<CanvasGroup>().blocksRaycasts = false;

            // Tworzymy RawImage z tekstura radialna.
            var imgGO = new GameObject("VignetteImage");
            imgGO.transform.SetParent(canvasGO.transform, false);

            var imgRt = imgGO.AddComponent<RectTransform>();
            imgRt.anchorMin = Vector2.zero;
            imgRt.anchorMax = Vector2.one;
            imgRt.offsetMin = Vector2.zero;
            imgRt.offsetMax = Vector2.zero;

            var rawImg = imgGO.AddComponent<RawImage>();
            rawImg.raycastTarget = false;

            // Generujemy teksture radialna 128x128 px.
            _vignetteTexture = GenerateVignetteTexture(128);
            rawImg.texture = _vignetteTexture;

            Color c = vignetteColor;
            c.a = 0f;
            rawImg.color = c;

            vignetteImage = rawImg;
            _useAlpha = true;
        }

        /// <summary>
        /// Generuje teksture z radialnym gradientem:
        /// przezroczysty srodek, czarne krawedzie.
        /// SmoothStep daje naturalny, nieostrny gradient.
        /// </summary>
        /// <param name="size">Rozmiar tekstury w pikselach (kwadratowa).</param>
        private Texture2D GenerateVignetteTexture(int size)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, mipChain: false);
            tex.wrapMode = TextureWrapMode.Clamp;

            float center = size * 0.5f;
            float radius = center;

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = (x - center) / radius;
                    float dy = (y - center) / radius;
                    float dist = Mathf.Sqrt(dx * dx + dy * dy);

                    // SmoothStep: przejscie od przezroczystego (srodek) do czarnego (krawedzie).
                    // 0.4 = poczatek gradientu (40% promienia), 1.0 = pełna czern.
                    float alpha = Mathf.SmoothStep(0.4f, 1.0f, dist);
                    tex.SetPixel(x, y, new Color(0f, 0f, 0f, alpha));
                }
            }

            tex.Apply();
            return tex;
        }

        private void OnDestroy()
        {
            // Sprzatamy programatycznie wygenerowana teksture.
            if (_vignetteTexture != null)
                Destroy(_vignetteTexture);
        }
    }
}
