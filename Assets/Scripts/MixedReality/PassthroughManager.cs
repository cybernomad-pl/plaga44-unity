#if HAS_META_XR
using System.Collections;
using UnityEngine;

namespace Plaga44.MixedReality
{
    /// <summary>
    /// Manages the Meta Quest passthrough layer.
    /// Attach to the same GameObject as OVRManager (or a dedicated MR manager object).
    /// Exposes VR/MR toggle, opacity, edge rendering, and a colour LUT texture.
    /// </summary>
    [RequireComponent(typeof(OVRPassthroughLayer))]
    public class PassthroughManager : MonoBehaviour
    {
        // ------------------------------------------------------------------ //
        //  Inspector fields                                                   //
        // ------------------------------------------------------------------ //

        [Header("Passthrough layer")]
        [Tooltip("Reference to the OVRPassthroughLayer added to this GameObject.")]
        [SerializeField] private OVRPassthroughLayer _passthroughLayer;

        [Header("Opacity")]
        [Range(0f, 1f)]
        [SerializeField] private float _opacity = 1f;

        [Header("Edge rendering")]
        [SerializeField] private bool _edgeRenderingEnabled = false;
        [SerializeField] private Color _edgeColor = new Color(1f, 0.5f, 0f, 1f); // post-apo orange

        [Header("Colour LUT")]
        [Tooltip("Optional colour LUT texture (256x1 or 16x16x16). Leave null to skip.")]
        [SerializeField] private Texture2D _colorLUT;
        [Range(0f, 1f)]
        [SerializeField] private float _lutWeight = 1f;

        [Header("Fade")]
        [Tooltip("Duration of VR<->MR crossfade in seconds.")]
        [SerializeField] private float _fadeDuration = 1.0f;

        // ------------------------------------------------------------------ //
        //  Runtime state                                                      //
        // ------------------------------------------------------------------ //

        private bool _isMRActive = false;
        private Coroutine _fadeCoroutine;

        // ------------------------------------------------------------------ //
        //  Unity lifecycle                                                    //
        // ------------------------------------------------------------------ //

        private void Awake()
        {
            if (_passthroughLayer == null)
                _passthroughLayer = GetComponent<OVRPassthroughLayer>();

            // Start in VR mode -- passthrough hidden
            ApplyPassthroughSettings(visible: false, opacity: 0f);
        }

        private void OnValidate()
        {
            if (_passthroughLayer == null) return;
            ApplyPassthroughSettings(_isMRActive, _opacity);
        }

        // ------------------------------------------------------------------ //
        //  Public API                                                         //
        // ------------------------------------------------------------------ //

        /// <summary>Returns true when passthrough (MR mode) is currently active.</summary>
        public bool IsMRActive => _isMRActive;

        /// <summary>Toggles between VR (no passthrough) and MR (passthrough visible).</summary>
        public void ToggleMode()
        {
            SetMRMode(!_isMRActive);
        }

        /// <summary>
        /// Switches to MR mode (passthrough on) or VR mode (passthrough off).
        /// Uses a coroutine fade so the transition is smooth.
        /// </summary>
        public void SetMRMode(bool enable)
        {
            if (_fadeCoroutine != null)
                StopCoroutine(_fadeCoroutine);

            _isMRActive = enable;
            _fadeCoroutine = StartCoroutine(FadeOpacity(enable ? _opacity : 0f));
        }

        /// <summary>Sets passthrough opacity (0=transparent, 1=fully opaque).</summary>
        public void SetOpacity(float opacity)
        {
            _opacity = Mathf.Clamp01(opacity);
            if (_isMRActive)
                _passthroughLayer.textureOpacity = _opacity;
        }

        /// <summary>Enables or disables edge rendering overlay.</summary>
        public void SetEdgeRendering(bool enabled, Color? color = null)
        {
            _edgeRenderingEnabled = enabled;
            if (color.HasValue) _edgeColor = color.Value;
            ApplyEdgeRendering();
        }

        /// <summary>Applies a colour LUT to the passthrough layer.</summary>
        public void SetColorLUT(Texture2D lut, float weight = 1f)
        {
            _colorLUT = lut;
            _lutWeight = Mathf.Clamp01(weight);
            ApplyColorLUT();
        }

        // ------------------------------------------------------------------ //
        //  Internal helpers                                                   //
        // ------------------------------------------------------------------ //

        private void ApplyPassthroughSettings(bool visible, float opacity)
        {
            if (_passthroughLayer == null) return;

            _passthroughLayer.hidden = !visible;
            _passthroughLayer.textureOpacity = opacity;

            if (visible)
            {
                ApplyEdgeRendering();
                ApplyColorLUT();
            }
        }

        private void ApplyEdgeRendering()
        {
            if (_passthroughLayer == null) return;

            _passthroughLayer.edgeRenderingEnabled = _edgeRenderingEnabled;
            if (_edgeRenderingEnabled)
                _passthroughLayer.edgeColor = _edgeColor;
        }

        private void ApplyColorLUT()
        {
            if (_passthroughLayer == null || _colorLUT == null) return;

            // OVRPassthroughLayer.SetColorLut is available in Meta XR SDK >= v60
            _passthroughLayer.SetColorLut(_colorLUT, _lutWeight);
        }

        private IEnumerator FadeOpacity(float targetOpacity)
        {
            float startOpacity = _passthroughLayer.textureOpacity;

            // Make layer visible before fading in
            if (targetOpacity > 0f)
                _passthroughLayer.hidden = false;

            float elapsed = 0f;
            while (elapsed < _fadeDuration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / _fadeDuration);
                _passthroughLayer.textureOpacity = Mathf.Lerp(startOpacity, targetOpacity, t);
                yield return null;
            }

            _passthroughLayer.textureOpacity = targetOpacity;

            // Hide layer after fading out to save GPU cost
            if (targetOpacity <= 0f)
            {
                _passthroughLayer.hidden = true;
                ApplyEdgeRendering(); // resets edge state
            }
            else
            {
                ApplyEdgeRendering();
                ApplyColorLUT();
            }

            _fadeCoroutine = null;
        }
    }
}
#endif // HAS_META_XR
