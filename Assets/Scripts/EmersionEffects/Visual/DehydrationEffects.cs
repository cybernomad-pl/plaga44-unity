// PLAGA '44 - Emersion Effects System
// DehydrationEffects.cs - Visual distortion, dizziness, and blackout from dehydration
// Progressive dehydration effects from mild haze to full blackout at critical levels

using System.Collections;
using UnityEngine;
using Plaga44.EmersionEffects.Core;

namespace Plaga44.EmersionEffects.Visual
{
    /// <summary>
    /// Controls dehydration-related effects that worsen as hydration drops:
    /// - Heat haze / visual distortion (chromatic aberration, wave distortion)
    /// - Dizziness (camera sway, double vision)
    /// - Blackouts at critical dehydration
    ///
    /// For Meta Quest 3/3S: uses lightweight shader effects suitable for mobile GPU.
    /// Attach to the VR camera rig.
    /// </summary>
    public class DehydrationEffects : MonoBehaviour
    {
        [Header("References")]
        public EmersionEffectsManager EmersionManager;
        [Tooltip("The VR camera transform.")]
        public Transform CameraTransform;

        [Header("Visual Distortion")]
        [Tooltip("Hydration level (0-100) below which distortion begins.")]
        public float DistortionOnsetHydration = 40f;
        [Tooltip("Hydration level at which distortion is maximum.")]
        public float DistortionMaxHydration = 10f;
        [Tooltip("Heat haze distortion intensity.")]
        public float HeatHazeIntensity = 0.03f;
        [Tooltip("Chromatic aberration intensity.")]
        public float ChromaticAberration = 0.005f;
        [Tooltip("Wave distortion amplitude.")]
        public float WaveDistortionAmplitude = 0.02f;
        [Tooltip("Wave distortion frequency.")]
        public float WaveDistortionFrequency = 1.5f;
        [Tooltip("Material with distortion shader (fullscreen overlay).")]
        public Material DistortionMaterial;
        [Tooltip("Renderer for distortion overlay.")]
        public MeshRenderer DistortionRenderer;

        [Header("Dizziness")]
        [Tooltip("Hydration level below which dizziness starts.")]
        public float DizzinessOnsetHydration = 30f;
        [Tooltip("Hydration level at which dizziness is maximum.")]
        public float DizzinessMaxHydration = 10f;
        [Tooltip("Camera sway amplitude for dizziness (radians).")]
        public float CameraSwayAmplitude = 0.015f;
        [Tooltip("Camera sway frequency (Hz).")]
        public float CameraSwayFrequency = 0.5f;
        [Tooltip("Hydration level below which double vision occurs.")]
        public float DoubleVisionThreshold = 15f;
        [Tooltip("Double vision offset amount.")]
        public float DoubleVisionOffset = 0.03f;
        [Tooltip("Material for double vision effect.")]
        public Material DoubleVisionMaterial;

        [Header("Blackout")]
        [Tooltip("Hydration level at which blackouts can occur.")]
        public float BlackoutHydrationThreshold = 5f;
        [Tooltip("Hydration level at which warning flickers start.")]
        public float WarningHydration = 12f;
        [Tooltip("Flicker rate for pre-blackout warning.")]
        public float WarningFlickerRate = 0.5f;
        [Tooltip("Duration of fade-to-black.")]
        public float BlackoutFadeDuration = 3f;
        [Tooltip("Duration of full blackout.")]
        public float BlackoutDuration = 5f;
        [Tooltip("Duration of fade-back-in recovery.")]
        public float RecoveryFadeDuration = 4f;
        [Tooltip("Minimum time between blackout events.")]
        public float MinTimeBetweenBlackouts = 30f;
        [Tooltip("Material for blackout overlay (solid black fade).")]
        public Material BlackoutMaterial;
        [Tooltip("Renderer for blackout overlay.")]
        public MeshRenderer BlackoutRenderer;

        // Runtime state - Distortion
        private float _currentDistortionIntensity;
        private float _targetDistortionIntensity;

        // Runtime state - Dizziness
        private float _currentDizzinessIntensity;
        private float _targetDizzinessIntensity;
        private Vector3 _cameraSwayOffset;
        private Quaternion _camerSwayRotation = Quaternion.identity;

        // Runtime state - Blackout
        private float _blackoutAlpha;
        private float _lastBlackoutTime = -999f;
        private bool _isBlackingOut;
        private Coroutine _blackoutCoroutine;
        private float _warningFlickerTimer;

        // Runtime state - Double vision
        private float _currentDoubleVisionIntensity;

        /// <summary>Current distortion intensity [0..1].</summary>
        public float CurrentDistortionIntensity => _currentDistortionIntensity;

        /// <summary>Current dizziness intensity [0..1].</summary>
        public float CurrentDizzinessIntensity => _currentDizzinessIntensity;

        /// <summary>Whether a blackout is currently in progress.</summary>
        public bool IsBlackingOut => _isBlackingOut;

        private void Start()
        {
            if (EmersionManager == null)
                EmersionManager = EmersionEffectsManager.Instance;

            if (EmersionManager == null)
            {
                Debug.LogError("[DehydrationEffects] EmersionEffectsManager not found.");
                enabled = false;
                return;
            }

            if (CameraTransform == null && Camera.main != null)
                CameraTransform = Camera.main.transform;

            // Initialize materials
            if (DistortionMaterial != null)
            {
                DistortionMaterial.SetFloat("_HeatHaze", 0f);
                DistortionMaterial.SetFloat("_ChromaticAberration", 0f);
                DistortionMaterial.SetFloat("_WaveAmplitude", 0f);
            }

            if (BlackoutMaterial != null)
            {
                SetMaterialAlpha(BlackoutMaterial, 0f);
            }

            if (DoubleVisionMaterial != null)
            {
                DoubleVisionMaterial.SetFloat("_Offset", 0f);
            }

            Debug.Log("[DehydrationEffects] Dehydration effects system initialized.");
        }

        private void Update()
        {
            if (EmersionManager == null || !EmersionManager.EffectsEnabled) return;

            var state = EmersionManager.PlayerState;
            float globalMult = EmersionManager.GlobalIntensityMultiplier;

            UpdateDistortion(state, globalMult);
            UpdateDizziness(state, globalMult);
            UpdateDoubleVision(state, globalMult);
            UpdateBlackout(state, globalMult);
            UpdateWarningFlicker(state, globalMult);
        }

        private void LateUpdate()
        {
            // Apply camera sway after all other camera updates
            if (CameraTransform != null && _currentDizzinessIntensity > 0.001f)
            {
                ApplyCameraSway();
            }
        }

        #region Visual Distortion

        private void UpdateDistortion(PlayerPhysiologyState state, float globalMult)
        {
            if (state.Hydration < DistortionOnsetHydration)
            {
                float t = Mathf.InverseLerp(DistortionOnsetHydration, DistortionMaxHydration, state.Hydration);
                _targetDistortionIntensity = t;
            }
            else
            {
                _targetDistortionIntensity = 0f;
            }

            _currentDistortionIntensity = Mathf.Lerp(_currentDistortionIntensity, _targetDistortionIntensity,
                Time.deltaTime * 1.5f);

            float effective = _currentDistortionIntensity * globalMult;

            if (DistortionMaterial != null)
            {
                DistortionMaterial.SetFloat("_HeatHaze", effective * HeatHazeIntensity);
                DistortionMaterial.SetFloat("_ChromaticAberration", effective * ChromaticAberration);
                DistortionMaterial.SetFloat("_WaveAmplitude", effective * WaveDistortionAmplitude);
                DistortionMaterial.SetFloat("_WaveFrequency", WaveDistortionFrequency);
                DistortionMaterial.SetFloat("_Time", Time.time);
            }

            if (DistortionRenderer != null)
            {
                DistortionRenderer.enabled = effective > 0.01f;
            }
        }

        #endregion

        #region Dizziness

        private void UpdateDizziness(PlayerPhysiologyState state, float globalMult)
        {
            if (state.Hydration < DizzinessOnsetHydration)
            {
                float t = Mathf.InverseLerp(DizzinessOnsetHydration, DizzinessMaxHydration, state.Hydration);
                _targetDizzinessIntensity = t;
            }
            else
            {
                _targetDizzinessIntensity = 0f;
            }

            _currentDizzinessIntensity = Mathf.Lerp(_currentDizzinessIntensity, _targetDizzinessIntensity,
                Time.deltaTime * 1.0f);
        }

        private void ApplyCameraSway()
        {
            if (CameraTransform == null) return;

            // Remove previous frame's sway
            CameraTransform.localRotation *= Quaternion.Inverse(_camerSwayRotation);

            float intensity = _currentDizzinessIntensity * EmersionManager.GlobalIntensityMultiplier;
            float amplitude = CameraSwayAmplitude * intensity;

            // Slow, organic sway using multiple sine waves
            float time = Time.time * CameraSwayFrequency;
            float swayX = Mathf.Sin(time * 1.0f) * amplitude;
            float swayY = Mathf.Sin(time * 0.7f + 1.2f) * amplitude * 0.5f;
            float swayZ = Mathf.Sin(time * 0.4f + 2.8f) * amplitude * 0.3f;

            // Add irregular perturbation
            float perlinX = (Mathf.PerlinNoise(time * 0.3f, 0f) * 2f - 1f) * amplitude * 0.3f;
            float perlinY = (Mathf.PerlinNoise(0f, time * 0.3f + 42f) * 2f - 1f) * amplitude * 0.2f;

            _camerSwayRotation = Quaternion.Euler(
                (swayX + perlinX) * Mathf.Rad2Deg,
                (swayY + perlinY) * Mathf.Rad2Deg,
                swayZ * Mathf.Rad2Deg
            );

            CameraTransform.localRotation *= _camerSwayRotation;
        }

        #endregion

        #region Double Vision

        private void UpdateDoubleVision(PlayerPhysiologyState state, float globalMult)
        {
            float targetDoubleVision = 0f;

            if (state.Hydration < DoubleVisionThreshold)
            {
                float t = Mathf.InverseLerp(DoubleVisionThreshold, 0f, state.Hydration);
                targetDoubleVision = t;
            }

            _currentDoubleVisionIntensity = Mathf.Lerp(_currentDoubleVisionIntensity, targetDoubleVision,
                Time.deltaTime * 1.0f);

            float effective = _currentDoubleVisionIntensity * globalMult;

            if (DoubleVisionMaterial != null)
            {
                // Pulsating double vision offset
                float pulse = Mathf.Sin(Time.time * 1.5f) * 0.5f + 0.5f;
                float offset = effective * DoubleVisionOffset * (0.5f + pulse * 0.5f);

                DoubleVisionMaterial.SetFloat("_Offset", offset);
                DoubleVisionMaterial.SetFloat("_Intensity", effective);
            }
        }

        #endregion

        #region Blackout

        private void UpdateBlackout(PlayerPhysiologyState state, float globalMult)
        {
            if (_isBlackingOut) return; // Already blacking out, managed by coroutine

            if (state.Hydration <= BlackoutHydrationThreshold &&
                Time.time - _lastBlackoutTime >= MinTimeBetweenBlackouts)
            {
                _blackoutCoroutine = StartCoroutine(BlackoutRoutine());
            }
        }

        private void UpdateWarningFlicker(PlayerPhysiologyState state, float globalMult)
        {
            if (_isBlackingOut) return;

            if (state.Hydration <= WarningHydration && state.Hydration > BlackoutHydrationThreshold)
            {
                // Pre-blackout warning: intermittent darkening flickers
                if (EmersionManager.ReduceFlicker)
                {
                    // For photosensitive users: steady dim instead of flicker
                    float dimT = Mathf.InverseLerp(WarningHydration, BlackoutHydrationThreshold, state.Hydration);
                    _blackoutAlpha = dimT * 0.3f * globalMult;
                }
                else
                {
                    _warningFlickerTimer += Time.deltaTime;
                    float flicker = Mathf.Sin(_warningFlickerTimer * Mathf.PI * 2f * WarningFlickerRate);
                    float dimT = Mathf.InverseLerp(WarningHydration, BlackoutHydrationThreshold, state.Hydration);
                    _blackoutAlpha = Mathf.Max(0f, flicker) * dimT * 0.4f * globalMult;
                }

                if (BlackoutMaterial != null)
                    SetMaterialAlpha(BlackoutMaterial, _blackoutAlpha);
                if (BlackoutRenderer != null)
                    BlackoutRenderer.enabled = _blackoutAlpha > 0.01f;
            }
            else if (!_isBlackingOut)
            {
                // Clear any residual flicker
                _blackoutAlpha = Mathf.MoveTowards(_blackoutAlpha, 0f, Time.deltaTime * 3f);
                if (BlackoutMaterial != null)
                    SetMaterialAlpha(BlackoutMaterial, _blackoutAlpha);
                if (BlackoutRenderer != null)
                    BlackoutRenderer.enabled = _blackoutAlpha > 0.01f;
            }
        }

        /// <summary>
        /// Full blackout sequence: fade to black -> hold -> fade back.
        /// Uses fade-only approach for VR comfort (no sudden cuts).
        /// </summary>
        private IEnumerator BlackoutRoutine()
        {
            _isBlackingOut = true;
            _lastBlackoutTime = Time.time;

            Debug.Log("[DehydrationEffects] Blackout triggered from critical dehydration.");

            if (BlackoutRenderer != null)
                BlackoutRenderer.enabled = true;

            // Phase 1: Fade to black
            float elapsed = 0f;
            while (elapsed < BlackoutFadeDuration)
            {
                elapsed += Time.deltaTime;
                _blackoutAlpha = Mathf.Lerp(0f, 1f, elapsed / BlackoutFadeDuration);
                if (BlackoutMaterial != null)
                    SetMaterialAlpha(BlackoutMaterial, _blackoutAlpha);
                yield return null;
            }
            _blackoutAlpha = 1f;
            if (BlackoutMaterial != null)
                SetMaterialAlpha(BlackoutMaterial, 1f);

            // Phase 2: Hold black
            yield return new WaitForSeconds(BlackoutDuration);

            // Phase 3: Fade back in
            elapsed = 0f;
            while (elapsed < RecoveryFadeDuration)
            {
                elapsed += Time.deltaTime;
                _blackoutAlpha = Mathf.Lerp(1f, 0f, elapsed / RecoveryFadeDuration);
                if (BlackoutMaterial != null)
                    SetMaterialAlpha(BlackoutMaterial, _blackoutAlpha);
                yield return null;
            }

            _blackoutAlpha = 0f;
            if (BlackoutMaterial != null)
                SetMaterialAlpha(BlackoutMaterial, 0f);
            if (BlackoutRenderer != null)
                BlackoutRenderer.enabled = false;

            _isBlackingOut = false;

            Debug.Log("[DehydrationEffects] Blackout recovery complete.");
        }

        #endregion

        private void SetMaterialAlpha(Material mat, float alpha)
        {
            if (mat == null) return;
            Color c = mat.color;
            c.a = alpha;
            mat.color = c;
        }

        private void OnDisable()
        {
            // Clean up camera sway
            if (CameraTransform != null)
            {
                CameraTransform.localRotation *= Quaternion.Inverse(_camerSwayRotation);
                _camerSwayRotation = Quaternion.identity;
            }

            // Reset materials
            if (DistortionMaterial != null)
            {
                DistortionMaterial.SetFloat("_HeatHaze", 0f);
                DistortionMaterial.SetFloat("_ChromaticAberration", 0f);
                DistortionMaterial.SetFloat("_WaveAmplitude", 0f);
            }

            if (BlackoutMaterial != null)
                SetMaterialAlpha(BlackoutMaterial, 0f);

            if (DoubleVisionMaterial != null)
            {
                DoubleVisionMaterial.SetFloat("_Offset", 0f);
                DoubleVisionMaterial.SetFloat("_Intensity", 0f);
            }

            if (_blackoutCoroutine != null)
            {
                StopCoroutine(_blackoutCoroutine);
                _isBlackingOut = false;
            }
        }
    }
}
