using UnityEngine;
using UnityEngine.UI;

namespace Plaga44.Locomotion
{
    /// <summary>
    /// Screen-space vignette overlay that reduces motion sickness during smooth locomotion.
    ///
    /// Renders a fullscreen Canvas Image with a radial gradient material.
    /// Intensity (vignette radius) scales with the player's current movement speed
    /// sourced from <see cref="SmoothLocomotion.NormalisedSpeed"/>.
    ///
    /// Setup options:
    ///   A) Auto-setup: leave all serialized references null -- the component will
    ///      create a Canvas + RawImage at runtime using a programmatic texture.
    ///   B) Manual: provide your own Canvas and a Material based on a vignette shader.
    ///      Set <see cref="vignetteImage"/> and optionally <see cref="vignetteIntensityParam"/>.
    ///
    /// Attach to the same GameObject as (or a child of) the VR rig root.
    /// <see cref="LocomotionManager"/> enables/disables this component automatically.
    /// </summary>
    [DisallowMultipleComponent]
    public class ComfortVignette : MonoBehaviour
    {
        // -------------------------------------------------------------------------
        // Inspector fields
        // -------------------------------------------------------------------------

        [Header("Intensity Mapping")]
        [Tooltip("Vignette intensity at maximum movement speed (0 = invisible, 1 = full black).")]
        [Range(0f, 1f)]
        public float maxIntensity = 0.6f;

        [Tooltip("Threshold of normalised speed below which the vignette starts fading in.")]
        [Range(0f, 1f)]
        public float fadeInThreshold = 0.05f;

        [Tooltip("Speed of intensity lerp. Higher = faster response.")]
        public float lerpSpeed = 8f;

        [Header("Colour")]
        [Tooltip("Vignette colour. Typically black.")]
        public Color vignetteColor = Color.black;

        [Header("Manual References (optional)")]
        [Tooltip("RawImage or Image used as the vignette overlay. Auto-created if null.")]
        [SerializeField] private Graphic vignetteImage;

        [Tooltip("If using a custom Material with an intensity parameter, enter the shader property name here.")]
        [SerializeField] private string vignetteIntensityParam = "_Intensity";

        // -------------------------------------------------------------------------
        // Runtime state
        // -------------------------------------------------------------------------

        private SmoothLocomotion _locomotion;
        private float _currentIntensity;
        private bool _useMaterialParam;
        private bool _useAlpha;          // fallback: drive intensity via image alpha
        private Texture2D _vignetteTexture;

        // -------------------------------------------------------------------------
        // Unity lifecycle
        // -------------------------------------------------------------------------

        private void Awake()
        {
            _locomotion = GetComponentInParent<SmoothLocomotion>(includeInactive: true);

            if (vignetteImage == null)
                CreateFallbackOverlay();
            else
                DetermineRenderMode();
        }

        private void OnEnable()
        {
            // Reset intensity visually when re-enabled.
            ApplyIntensityImmediate(0f);
        }

        private void OnDisable()
        {
            ApplyIntensityImmediate(0f);
        }

        private void Update()
        {
            float targetIntensity = 0f;

            if (_locomotion != null)
            {
                float speed = _locomotion.NormalisedSpeed;
                if (speed > fadeInThreshold)
                    targetIntensity = Mathf.InverseLerp(fadeInThreshold, 1f, speed) * maxIntensity;
            }

            _currentIntensity = Mathf.Lerp(_currentIntensity, targetIntensity, Time.deltaTime * lerpSpeed);
            ApplyIntensityImmediate(_currentIntensity);
        }

        // -------------------------------------------------------------------------
        // Public API
        // -------------------------------------------------------------------------

        /// <summary>Directly set vignette intensity (used by LocomotionManager to clear on mode switch).</summary>
        public void SetIntensity(float intensity)
        {
            _currentIntensity = intensity;
            ApplyIntensityImmediate(intensity);
        }

        // -------------------------------------------------------------------------
        // Internal
        // -------------------------------------------------------------------------

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
        /// Creates a fullscreen Canvas with a programmatic radial gradient texture
        /// when no manual references are provided.
        /// </summary>
        private void CreateFallbackOverlay()
        {
            // Find or create a ScreenSpaceOverlay Canvas that lives under VR camera.
            // Note: in VR ScreenSpaceOverlay is not correct; we need World Space or Camera Space.
            // We use Camera Space here so it moves with the HMD.

            Transform headTransform = null;

#if HAS_META_XR
            var tracking = transform.root.Find("TrackingSpace");
            if (tracking != null)
                headTransform = tracking.Find("CenterEyeAnchor");
#endif
            if (headTransform == null && Camera.main != null)
                headTransform = Camera.main.transform;

            // Build Canvas.
            var canvasGO = new GameObject("ComfortVignetteCanvas");
            canvasGO.transform.SetParent(headTransform != null ? headTransform : transform, false);

            var canvas = canvasGO.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;

            // Position just in front of the camera -- distance chosen so it fills view.
            canvasGO.transform.localPosition = new Vector3(0f, 0f, 0.31f);
            canvasGO.transform.localRotation = Quaternion.identity;

            // Size to fill ~110 deg FOV at 0.31 m distance (Quest 3 FOV approx).
            float halfSize = Mathf.Tan(55f * Mathf.Deg2Rad) * 0.31f;
            var rt = canvasGO.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(halfSize * 2f, halfSize * 2f);

            canvasGO.AddComponent<CanvasGroup>().blocksRaycasts = false;

            // Build Image.
            var imgGO = new GameObject("VignetteImage");
            imgGO.transform.SetParent(canvasGO.transform, false);

            var imgRt = imgGO.AddComponent<RectTransform>();
            imgRt.anchorMin = Vector2.zero;
            imgRt.anchorMax = Vector2.one;
            imgRt.offsetMin = Vector2.zero;
            imgRt.offsetMax = Vector2.zero;

            var rawImg = imgGO.AddComponent<RawImage>();
            rawImg.raycastTarget = false;

            // Generate radial gradient texture.
            _vignetteTexture = GenerateVignetteTexture(128);
            rawImg.texture = _vignetteTexture;

            Color c = vignetteColor;
            c.a = 0f;
            rawImg.color = c;

            vignetteImage = rawImg;
            _useAlpha = true;
        }

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

                    // Smooth step: transparent in centre, opaque at edges.
                    float alpha = Mathf.SmoothStep(0.4f, 1.0f, dist);
                    tex.SetPixel(x, y, new Color(0f, 0f, 0f, alpha));
                }
            }

            tex.Apply();
            return tex;
        }

        private void OnDestroy()
        {
            if (_vignetteTexture != null)
                Destroy(_vignetteTexture);
        }
    }
}
