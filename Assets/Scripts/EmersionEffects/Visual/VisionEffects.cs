// PLAGA '44 - Emersion Effects System
// VisionEffects.cs - FOV narrowing (tunnel vision), blur, color desaturation, blood overlay
// Visual feedback reflecting the player's declining health and injury state

using UnityEngine;
using UnityEngine.Rendering;
using Plaga44.EmersionEffects.Core;

namespace Plaga44.EmersionEffects.Visual
{
    /// <summary>
    /// Controls health-responsive visual effects:
    /// - Tunnel vision (vignette/FOV narrowing) at low health
    /// - Blur effect at critical health and after concussion
    /// - Color desaturation as health drops
    /// - Blood splatter overlay that pulses with heartbeat
    ///
    /// For Meta Quest 3/3S: uses URP post-processing volume overrides.
    /// Attach to the XR camera rig or main camera.
    /// </summary>
    public class VisionEffects : MonoBehaviour
    {
        [Header("References")]
        public EmersionEffectsManager EmersionManager;
        [Tooltip("Reference to HeartbeatController for pulse-synced blood overlay.")]
        public Audio.HeartbeatController HeartbeatController;

        [Header("Post-Processing")]
        [Tooltip("The post-processing Volume used for vision effects.")]
        public Volume PostProcessVolume;

        [Header("Camera Reference")]
        [Tooltip("The VR camera (usually the center eye camera).")]
        public Camera VRCamera;

        [Header("Tunnel Vision")]
        [Tooltip("Health % below which tunnel vision begins.")]
        public float TunnelVisionOnsetHealth = 40f;
        [Tooltip("Health % at which tunnel vision reaches maximum.")]
        public float TunnelVisionMaxHealth = 10f;
        [Tooltip("Minimum FOV multiplier at max tunnel vision.")]
        [Range(0.3f, 1f)] public float MinFOVMultiplier = 0.55f;
        [Tooltip("Maximum vignette intensity.")]
        [Range(0f, 1f)] public float VignetteIntensityMax = 0.8f;
        public float TunnelVisionTransitionSpeed = 2f;

        [Header("Vignette Material")]
        [Tooltip("Material for the vignette overlay (unlit, alpha-blended).")]
        public Material VignetteMaterial;
        [Tooltip("Mesh renderer for the vignette overlay quad.")]
        public MeshRenderer VignetteRenderer;

        [Header("Blur")]
        [Tooltip("Health % below which blur begins.")]
        public float BlurOnsetHealth = 35f;
        public float MaxBlurRadius = 4f;
        public float BloodLossBlurMultiplier = 1.5f;
        public float ConcussionBlurDuration = 8f;
        public float BlurTransitionSpeed = 1.5f;

        [Header("Color Desaturation")]
        [Tooltip("Health % below which colors start fading.")]
        public float DesaturationOnsetHealth = 50f;
        [Tooltip("Maximum desaturation amount (0 = full color, 1 = grayscale).")]
        [Range(0f, 1f)] public float MaxDesaturation = 0.85f;
        [Tooltip("Near-death desaturation (almost grayscale).")]
        [Range(0f, 1f)] public float NearDeathDesaturation = 0.95f;
        public float NearDeathThreshold = 10f;
        public float DesaturationTransitionSpeed = 1f;

        [Header("Blood Splatter Overlay")]
        [Tooltip("Health % below which blood overlay appears.")]
        public float BloodOnsetHealth = 70f;
        [Tooltip("Maximum blood overlay alpha.")]
        [Range(0f, 1f)] public float MaxBloodAlpha = 0.6f;
        [Tooltip("Pulse the blood overlay with heartbeat.")]
        public bool PulseWithHeartbeat = true;
        [Tooltip("Rate at which blood overlay clears (per second).")]
        public float BloodClearSpeed = 0.1f;
        [Tooltip("Material for the blood splatter overlay.")]
        public Material BloodOverlayMaterial;
        [Tooltip("Renderer for the blood overlay quad.")]
        public MeshRenderer BloodOverlayRenderer;

        // Runtime state
        private float _currentVignetteIntensity;
        private float _currentBlurAmount;
        private float _currentDesaturation;
        private float _currentBloodAlpha;
        private float _concussionTimer;
        private float _targetVignetteIntensity;
        private float _targetBlurAmount;
        private float _targetDesaturation;
        private float _targetBloodAlpha;
        private float _originalFOV;

        /// <summary>Current vignette intensity [0..1].</summary>
        public float CurrentVignetteIntensity => _currentVignetteIntensity;

        /// <summary>Current desaturation level [0..1].</summary>
        public float CurrentDesaturation => _currentDesaturation;

        private void Start()
        {
            if (EmersionManager == null)
                EmersionManager = EmersionEffectsManager.Instance;

            if (EmersionManager == null)
            {
                Debug.LogError("[VisionEffects] EmersionEffectsManager not found.");
                enabled = false;
                return;
            }

            if (VRCamera == null)
                VRCamera = Camera.main;

            if (VRCamera != null)
                _originalFOV = VRCamera.fieldOfView;

            // Initialize overlay materials
            InitializeOverlays();

            Debug.Log("[VisionEffects] Vision effects system initialized.");
        }

        private void Update()
        {
            if (EmersionManager == null || !EmersionManager.EffectsEnabled) return;

            var state = EmersionManager.PlayerState;

            // Update concussion timer
            if (state.HasConcussion && _concussionTimer <= 0f)
            {
                _concussionTimer = ConcussionBlurDuration;
            }
            if (_concussionTimer > 0f)
            {
                _concussionTimer -= Time.deltaTime;
            }

            CalculateTargetValues(state);
            ApplySmoothedValues();
            RenderEffects();
        }

        private void CalculateTargetValues(PlayerPhysiologyState state)
        {
            float health = state.Health;

            // ---- Tunnel Vision / Vignette ----
            if (health < TunnelVisionOnsetHealth)
            {
                float t = Mathf.InverseLerp(TunnelVisionOnsetHealth, TunnelVisionMaxHealth, health);
                _targetVignetteIntensity = Mathf.Lerp(0f, VignetteIntensityMax, t);
            }
            else
            {
                _targetVignetteIntensity = 0f;
            }

            // Combat adds slight vignette for focus effect
            if (state.IsInCombat)
                _targetVignetteIntensity = Mathf.Max(_targetVignetteIntensity, 0.15f);

            // Comfort vignette during locomotion (VR sickness prevention)
            if (EmersionManager.EnableComfortVignette && state.IsSprinting)
                _targetVignetteIntensity = Mathf.Max(_targetVignetteIntensity, 0.2f);

            // ---- Blur ----
            _targetBlurAmount = 0f;
            if (health < BlurOnsetHealth)
            {
                float t = Mathf.InverseLerp(BlurOnsetHealth, 0f, health);
                _targetBlurAmount = t * MaxBlurRadius;
            }

            // Blood loss amplifies blur
            _targetBlurAmount += (state.BloodLoss / 100f) * MaxBlurRadius * BloodLossBlurMultiplier;

            // Concussion blur
            if (_concussionTimer > 0f)
            {
                float concussionT = _concussionTimer / ConcussionBlurDuration;
                _targetBlurAmount = Mathf.Max(_targetBlurAmount, MaxBlurRadius * concussionT);
            }

            _targetBlurAmount = Mathf.Clamp(_targetBlurAmount, 0f, MaxBlurRadius);

            // ---- Desaturation ----
            if (health < DesaturationOnsetHealth)
            {
                float t = Mathf.InverseLerp(DesaturationOnsetHealth, 0f, health);
                _targetDesaturation = t * MaxDesaturation;

                // Near-death extra desaturation
                if (health < NearDeathThreshold)
                {
                    float nearDeathT = Mathf.InverseLerp(NearDeathThreshold, 0f, health);
                    _targetDesaturation = Mathf.Lerp(_targetDesaturation, NearDeathDesaturation, nearDeathT);
                }
            }
            else
            {
                _targetDesaturation = 0f;
            }

            // ---- Blood Overlay ----
            if (health < BloodOnsetHealth)
            {
                float t = Mathf.InverseLerp(BloodOnsetHealth, 0f, health);
                _targetBloodAlpha = t * MaxBloodAlpha;
            }
            else
            {
                // Gradually clear blood overlay when health recovers
                _targetBloodAlpha = Mathf.Max(0f, _targetBloodAlpha - BloodClearSpeed * Time.deltaTime);
            }
        }

        private void ApplySmoothedValues()
        {
            float dt = Time.deltaTime;

            _currentVignetteIntensity = Mathf.MoveTowards(_currentVignetteIntensity, _targetVignetteIntensity,
                dt * TunnelVisionTransitionSpeed);

            _currentBlurAmount = Mathf.MoveTowards(_currentBlurAmount, _targetBlurAmount,
                dt * BlurTransitionSpeed);

            _currentDesaturation = Mathf.MoveTowards(_currentDesaturation, _targetDesaturation,
                dt * DesaturationTransitionSpeed);

            _currentBloodAlpha = Mathf.MoveTowards(_currentBloodAlpha, _targetBloodAlpha,
                dt * 2f);

            // Apply global intensity multiplier
            float globalMult = EmersionManager.GlobalIntensityMultiplier;
            float effectiveVignette = _currentVignetteIntensity * globalMult;
            float effectiveBlur = _currentBlurAmount * globalMult;
            float effectiveDesat = _currentDesaturation * globalMult;
            float effectiveBlood = _currentBloodAlpha * globalMult;

            // Apply to materials and post-processing
            ApplyVignette(effectiveVignette);
            ApplyBlur(effectiveBlur);
            ApplyDesaturation(effectiveDesat);
            ApplyBloodOverlay(effectiveBlood);
        }

        private void RenderEffects()
        {
            // FOV adjustment for tunnel vision (in non-VR mode; VR uses vignette instead)
            // In VR, FOV is hardware-locked, so we use vignette darkening only
            // This FOV code path is for flat-screen testing/fallback
            if (VRCamera != null && !UnityEngine.XR.XRSettings.enabled)
            {
                float fovMult = Mathf.Lerp(1f, MinFOVMultiplier, _currentVignetteIntensity / VignetteIntensityMax);
                VRCamera.fieldOfView = _originalFOV * fovMult;
            }
        }

        private void ApplyVignette(float intensity)
        {
            if (VignetteMaterial != null)
            {
                VignetteMaterial.SetFloat("_Intensity", intensity);
            }

            if (VignetteRenderer != null)
            {
                VignetteRenderer.enabled = intensity > 0.01f;
            }
        }

        private void ApplyBlur(float blurAmount)
        {
            // Blur is typically applied via post-processing volume override
            // Set the blur parameter on the volume profile if available
            if (PostProcessVolume != null && PostProcessVolume.profile != null)
            {
                // This integrates with URP post-processing
                // Actual implementation depends on the blur effect used (e.g., Gaussian Blur)
                // For Quest 3 performance, consider using a lightweight fullscreen blur shader
            }
        }

        private void ApplyDesaturation(float desaturation)
        {
            // Applied via color grading or a fullscreen shader
            if (PostProcessVolume != null && PostProcessVolume.profile != null)
            {
                // URP Color Adjustments - saturation control
                // Saturation range: -100 (grayscale) to 100 (vivid)
                // Map our 0..1 to 0..-100
            }
        }

        private void ApplyBloodOverlay(float alpha)
        {
            if (BloodOverlayMaterial == null) return;

            float finalAlpha = alpha;

            // Pulse with heartbeat if enabled
            if (PulseWithHeartbeat && HeartbeatController != null && HeartbeatController.IsAudible)
            {
                float pulse = HeartbeatController.GetBeatPulse();
                finalAlpha *= (0.7f + pulse * 0.3f); // 70% base + 30% pulsing
            }

            Color overlayColor = BloodOverlayMaterial.color;
            overlayColor.a = finalAlpha;
            BloodOverlayMaterial.color = overlayColor;

            if (BloodOverlayRenderer != null)
            {
                BloodOverlayRenderer.enabled = finalAlpha > 0.01f;
            }
        }

        private void InitializeOverlays()
        {
            // Overlay quads should be set up in the scene/prefab.
            // They are typically child quads of the camera at a very close distance,
            // rendered with an overlay/UI shader.
            if (VignetteMaterial != null)
            {
                VignetteMaterial.SetFloat("_Intensity", 0f);
            }

            if (BloodOverlayMaterial != null)
            {
                Color c = BloodOverlayMaterial.color;
                c.a = 0f;
                BloodOverlayMaterial.color = c;
            }
        }

        /// <summary>
        /// Trigger a sudden damage flash (screen goes red briefly then fades).
        /// Called on hit/injury events.
        /// </summary>
        public void TriggerDamageFlash()
        {
            if (BloodOverlayMaterial == null) return;

            // Instant spike
            _currentBloodAlpha = Mathf.Min(MaxBloodAlpha, _currentBloodAlpha + 0.3f);
        }

        private void OnDisable()
        {
            // Reset visuals
            if (VRCamera != null)
                VRCamera.fieldOfView = _originalFOV;

            if (VignetteMaterial != null)
                VignetteMaterial.SetFloat("_Intensity", 0f);

            if (BloodOverlayMaterial != null)
            {
                Color c = BloodOverlayMaterial.color;
                c.a = 0f;
                BloodOverlayMaterial.color = c;
            }
        }
    }
}
