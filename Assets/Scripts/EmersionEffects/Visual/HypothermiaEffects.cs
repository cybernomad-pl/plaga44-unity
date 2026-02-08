// PLAGA '44 - Emersion Effects System
// HypothermiaEffects.cs - Screen frost, movement slowdown, shivering, cold color shift
// Simulates progressive hypothermia with VR-appropriate visual and movement effects

using UnityEngine;
using Plaga44.EmersionEffects.Core;

namespace Plaga44.EmersionEffects.Visual
{
    /// <summary>
    /// Controls hypothermia-related effects that intensify as body temperature drops:
    /// - Screen frost overlay (ice crystal formation on VR lens edges)
    /// - Camera/hand shivering (micro-shake)
    /// - Movement speed reduction
    /// - Blue color shift and desaturation
    /// - Breath fog on the "VR lens"
    ///
    /// For Meta Quest 3/3S: optimized for mobile GPU with simple overlay shaders.
    /// Attach to the player rig or camera.
    /// </summary>
    public class HypothermiaEffects : MonoBehaviour
    {
        [Header("References")]
        public EmersionEffectsManager EmersionManager;
        [Tooltip("Player character controller for movement speed modification.")]
        public CharacterController CharacterController;
        [Tooltip("VR camera for shiver shake.")]
        public Transform CameraTransform;

        [Header("Screen Frost")]
        [Tooltip("Material for the frost overlay on the VR lens.")]
        public Material FrostMaterial;
        [Tooltip("Renderer for the frost overlay quad.")]
        public MeshRenderer FrostRenderer;
        [Tooltip("Body temperature at which frost begins forming.")]
        public float FrostOnsetTemperature = 35.5f;
        [Tooltip("Body temperature at which frost is at maximum.")]
        public float FrostMaxTemperature = 31f;
        [Tooltip("Maximum frost coverage (0 = none, 1 = fully frosted).")]
        [Range(0f, 1f)] public float FrostCoverageMax = 0.7f;
        [Tooltip("Initial frost appears at edges. This controls where frost starts (0 = center, 1 = edges).")]
        [Range(0f, 1f)] public float FrostEdgeStart = 0.3f;
        [Tooltip("Speed at which frost crystals form/dissolve.")]
        public float CrystalFormationSpeed = 0.5f;

        [Header("Breath Fog on Lens")]
        [Tooltip("Enable simulated breath fog on the VR lens in cold.")]
        public bool BreathFogOnLens = true;
        [Tooltip("Material for breath fog overlay.")]
        public Material BreathFogMaterial;
        [Tooltip("Renderer for breath fog quad.")]
        public MeshRenderer BreathFogRenderer;
        [Range(0f, 1f)] public float BreathFogAlpha = 0.3f;
        [Tooltip("Seconds between breath fog events.")]
        public float BreathFogInterval = 4f;

        [Header("Shivering")]
        [Tooltip("Body temperature at which shivering starts.")]
        public float ShiverOnsetTemperature = 35f;
        [Tooltip("Body temperature at which shivering is at maximum.")]
        public float ShiverMaxTemperature = 32f;
        [Tooltip("Camera shake amplitude for shivering (meters).")]
        public float CameraShakeAmplitude = 0.003f;
        [Tooltip("Camera shake frequency (Hz).")]
        public float CameraShakeFrequency = 8f;
        [Tooltip("Additional hand shake amplitude.")]
        public float HandShakeAmplitude = 0.005f;
        public float HandShakeFrequency = 10f;
        [Tooltip("Maximum intensity multiplier for severe hypothermia.")]
        public float IntensityMultiplierMax = 3f;

        [Header("Movement Slowdown")]
        [Tooltip("Body temperature at which slowdown begins.")]
        public float SlowdownOnsetTemperature = 35f;
        public float SlowdownMaxTemperature = 32f;
        [Tooltip("Minimum movement speed multiplier at max hypothermia.")]
        [Range(0.1f, 1f)] public float MinSpeedMultiplier = 0.4f;
        [Range(0.1f, 1f)] public float TurnSpeedMultiplier = 0.6f;
        [Tooltip("Chance to stumble per check.")]
        [Range(0f, 0.5f)] public float StumblingChance = 0.1f;
        [Tooltip("Seconds between stumble checks.")]
        public float StumblingInterval = 5f;

        [Header("Cold Color Shift")]
        [Tooltip("Body temperature at which blue shift starts.")]
        public float ColorShiftOnsetTemperature = 35f;
        [Tooltip("Maximum blue tint strength.")]
        [Range(0f, 1f)] public float BlueShiftMax = 0.3f;
        [Tooltip("Saturation reduction from cold.")]
        [Range(0f, 1f)] public float SaturationReduction = 0.4f;
        [Tooltip("Material with color grading shader (fullscreen overlay or post-process).")]
        public Material ColorGradingMaterial;

        // Runtime state
        private float _currentFrostCoverage;
        private float _currentShiverIntensity;
        private float _currentSpeedMultiplier = 1f;
        private float _currentBlueShift;
        private float _breathFogTimer;
        private float _breathFogAlphaCurrent;
        private float _stumbleTimer;
        private float _originalMoveSpeed;
        private Vector3 _cameraShakeOffset;
        private bool _isStumbling;

        /// <summary>Current frost coverage [0..1].</summary>
        public float CurrentFrostCoverage => _currentFrostCoverage;

        /// <summary>Current speed multiplier from hypothermia [0..1].</summary>
        public float CurrentSpeedMultiplier => _currentSpeedMultiplier;

        /// <summary>Current shiver intensity [0..1].</summary>
        public float CurrentShiverIntensity => _currentShiverIntensity;

        private void Start()
        {
            if (EmersionManager == null)
                EmersionManager = EmersionEffectsManager.Instance;

            if (EmersionManager == null)
            {
                Debug.LogError("[HypothermiaEffects] EmersionEffectsManager not found.");
                enabled = false;
                return;
            }

            if (CameraTransform == null && Camera.main != null)
                CameraTransform = Camera.main.transform;

            // Initialize materials
            if (FrostMaterial != null)
            {
                FrostMaterial.SetFloat("_Coverage", 0f);
                FrostMaterial.SetFloat("_EdgeStart", FrostEdgeStart);
            }

            if (BreathFogMaterial != null)
            {
                SetMaterialAlpha(BreathFogMaterial, 0f);
            }

            _currentSpeedMultiplier = 1f;
            _stumbleTimer = StumblingInterval;

            Debug.Log("[HypothermiaEffects] Hypothermia effects system initialized.");
        }

        private void Update()
        {
            if (EmersionManager == null || !EmersionManager.EffectsEnabled) return;

            var state = EmersionManager.PlayerState;
            float severity = state.HypothermiaSeverity;
            float globalMult = EmersionManager.GlobalIntensityMultiplier;

            UpdateFrost(state, severity, globalMult);
            UpdateShivering(state, severity, globalMult);
            UpdateMovementSlowdown(state, severity, globalMult);
            UpdateColorShift(state, severity, globalMult);
            UpdateBreathFog(state, severity, globalMult);
        }

        private void LateUpdate()
        {
            // Apply camera shake from shivering (after all other camera updates)
            if (CameraTransform != null && _currentShiverIntensity > 0.001f)
            {
                ApplyCameraShiver();
            }
        }

        #region Screen Frost

        private void UpdateFrost(PlayerPhysiologyState state, float severity, float globalMult)
        {
            float targetCoverage = 0f;

            if (state.BodyTemperature < FrostOnsetTemperature)
            {
                float t = Mathf.InverseLerp(FrostOnsetTemperature, FrostMaxTemperature, state.BodyTemperature);
                targetCoverage = t * FrostCoverageMax;
            }

            // Smooth crystal formation/dissolution
            _currentFrostCoverage = Mathf.MoveTowards(_currentFrostCoverage, targetCoverage,
                Time.deltaTime * CrystalFormationSpeed);

            float effectiveCoverage = _currentFrostCoverage * globalMult;

            if (FrostMaterial != null)
            {
                FrostMaterial.SetFloat("_Coverage", effectiveCoverage);
                FrostMaterial.SetFloat("_Time", Time.time); // For animated frost patterns
            }

            if (FrostRenderer != null)
            {
                FrostRenderer.enabled = effectiveCoverage > 0.01f;
            }
        }

        #endregion

        #region Shivering

        private void UpdateShivering(PlayerPhysiologyState state, float severity, float globalMult)
        {
            float targetShiver = 0f;

            if (state.BodyTemperature < ShiverOnsetTemperature)
            {
                float t = Mathf.InverseLerp(ShiverOnsetTemperature, ShiverMaxTemperature, state.BodyTemperature);
                targetShiver = t;
            }

            _currentShiverIntensity = Mathf.Lerp(_currentShiverIntensity, targetShiver, Time.deltaTime * 2f);
            _currentShiverIntensity *= globalMult * EmersionManager.CameraShakeScale;
        }

        private void ApplyCameraShiver()
        {
            // Remove previous frame's shake
            CameraTransform.localPosition -= _cameraShakeOffset;

            float intensity = _currentShiverIntensity;
            float amplitude = CameraShakeAmplitude * Mathf.Lerp(1f, IntensityMultiplierMax, intensity);
            float time = Time.time * CameraShakeFrequency;

            // Multi-frequency noise for organic shiver
            float x = (Mathf.PerlinNoise(time, 0f) * 2f - 1f) * amplitude;
            float y = (Mathf.PerlinNoise(0f, time + 50f) * 2f - 1f) * amplitude;
            float z = (Mathf.PerlinNoise(time + 100f, time) * 2f - 1f) * amplitude * 0.3f;

            // Intermittent shiver bursts (shivering is not constant)
            float shiverWave = Mathf.Sin(Time.time * 0.8f) * 0.5f + 0.5f;
            float burstMask = shiverWave > 0.3f ? 1f : shiverWave / 0.3f;

            _cameraShakeOffset = new Vector3(x, y, z) * burstMask * intensity;
            CameraTransform.localPosition += _cameraShakeOffset;
        }

        #endregion

        #region Movement Slowdown

        private void UpdateMovementSlowdown(PlayerPhysiologyState state, float severity, float globalMult)
        {
            float targetSpeed = 1f;

            if (state.BodyTemperature < SlowdownOnsetTemperature)
            {
                float t = Mathf.InverseLerp(SlowdownOnsetTemperature, SlowdownMaxTemperature, state.BodyTemperature);
                targetSpeed = Mathf.Lerp(1f, MinSpeedMultiplier, t * globalMult);
            }

            _currentSpeedMultiplier = Mathf.Lerp(_currentSpeedMultiplier, targetSpeed, Time.deltaTime * 1.5f);

            // Stumbling at severe hypothermia
            if (severity > 0.5f)
            {
                _stumbleTimer -= Time.deltaTime;
                if (_stumbleTimer <= 0f)
                {
                    _stumbleTimer = StumblingInterval;
                    if (Random.value < StumblingChance * severity * globalMult)
                    {
                        TriggerStumble();
                    }
                }
            }
        }

        private void TriggerStumble()
        {
            if (_isStumbling) return;

            Debug.Log("[HypothermiaEffects] Player stumbled from hypothermia.");

            // Brief camera dip and lateral offset to simulate stumble
            // In a full implementation, this would also affect the character controller
            _isStumbling = true;
            Invoke(nameof(EndStumble), 0.5f);

            // Apply a brief camera offset
            if (CameraTransform != null)
            {
                Vector3 stumbleDir = new Vector3(
                    Random.Range(-1f, 1f),
                    -0.1f,
                    Random.Range(-0.5f, 0.5f)
                ).normalized * 0.05f;

                CameraTransform.localPosition += stumbleDir;
            }
        }

        private void EndStumble()
        {
            _isStumbling = false;
        }

        #endregion

        #region Color Shift

        private void UpdateColorShift(PlayerPhysiologyState state, float severity, float globalMult)
        {
            float targetBlueShift = 0f;

            if (state.BodyTemperature < ColorShiftOnsetTemperature)
            {
                float t = severity;
                targetBlueShift = t * BlueShiftMax;
            }

            _currentBlueShift = Mathf.Lerp(_currentBlueShift, targetBlueShift, Time.deltaTime);

            if (ColorGradingMaterial != null)
            {
                float effectiveBlue = _currentBlueShift * globalMult;
                float effectiveDesat = Mathf.Lerp(0f, SaturationReduction, severity) * globalMult;

                ColorGradingMaterial.SetFloat("_BlueShift", effectiveBlue);
                ColorGradingMaterial.SetFloat("_Desaturation", effectiveDesat);
            }
        }

        #endregion

        #region Breath Fog

        private void UpdateBreathFog(PlayerPhysiologyState state, float severity, float globalMult)
        {
            if (!BreathFogOnLens || BreathFogMaterial == null) return;

            // Only show breath fog when cold
            if (state.BodyTemperature >= FrostOnsetTemperature || state.IsIndoors)
            {
                _breathFogAlphaCurrent = Mathf.MoveTowards(_breathFogAlphaCurrent, 0f, Time.deltaTime * 2f);
                SetMaterialAlpha(BreathFogMaterial, _breathFogAlphaCurrent);
                if (BreathFogRenderer != null) BreathFogRenderer.enabled = _breathFogAlphaCurrent > 0.01f;
                return;
            }

            _breathFogTimer += Time.deltaTime;

            // Breath fog appears periodically (simulating each exhale fogging the lens)
            float adjustedInterval = BreathFogInterval / (1f + severity * 0.5f); // Faster breathing in cold
            if (_breathFogTimer >= adjustedInterval)
            {
                _breathFogTimer = 0f;
                // Trigger fog-up
                _breathFogAlphaCurrent = BreathFogAlpha * (0.5f + severity * 0.5f) * globalMult;
            }
            else
            {
                // Fog dissipates over time
                float dissipateSpeed = 1f / (adjustedInterval * 0.6f);
                _breathFogAlphaCurrent = Mathf.MoveTowards(_breathFogAlphaCurrent, 0f, Time.deltaTime * dissipateSpeed);
            }

            SetMaterialAlpha(BreathFogMaterial, _breathFogAlphaCurrent);
            if (BreathFogRenderer != null)
                BreathFogRenderer.enabled = _breathFogAlphaCurrent > 0.01f;
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
            // Clean up: remove camera shake offset
            if (CameraTransform != null)
            {
                CameraTransform.localPosition -= _cameraShakeOffset;
                _cameraShakeOffset = Vector3.zero;
            }

            // Reset materials
            if (FrostMaterial != null)
                FrostMaterial.SetFloat("_Coverage", 0f);

            if (BreathFogMaterial != null)
                SetMaterialAlpha(BreathFogMaterial, 0f);

            if (ColorGradingMaterial != null)
            {
                ColorGradingMaterial.SetFloat("_BlueShift", 0f);
                ColorGradingMaterial.SetFloat("_Desaturation", 0f);
            }

            _currentSpeedMultiplier = 1f;
        }
    }
}
