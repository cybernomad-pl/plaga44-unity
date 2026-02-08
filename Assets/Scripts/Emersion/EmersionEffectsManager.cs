// PLAGA '44 VR - EmersionEffectsManager
// Translates physiological state into VR controller and rendering effects.
//
// From IPK grant: "Emersja - celowe wyciaganie gracza na powierzchnie
// przez dyskomfort fizyczny. Tremor kontrolerow przy glodzie, lag po
// zatruciu grzybami, aberracje widzenia przy hipoksji."
//
// From SPARK 3.0: "Controlled discomfort as deliberate design methodology.
// Unlike typical VR that optimizes for comfort, we use calibrated
// physiological stress as educational tool."
//
// This is the bridge between the physiology simulation and the VR hardware.

using System;
using UnityEngine;

namespace Plaga44.Emersion
{
    using Plaga44.Physiology;

    /// <summary>
    /// Manages all emersion effects: controller tremor, visual distortions,
    /// audio hallucinations, input lag, and controller lockout.
    /// Reads PhysiologyState and applies effects to VR systems.
    /// </summary>
    public class EmersionEffectsManager : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private PhysiologyController physiologyController;

        [Header("Emersion Configuration")]
        [SerializeField] private EmersionConfig config;

        [Header("Effect State (Read Only)")]
        [SerializeField] private float currentTremorIntensity = 0f;
        [SerializeField] private float currentVisualDistortion = 0f;
        [SerializeField] private float currentInputLag = 0f;
        [SerializeField] private float currentFOVReduction = 0f;
        [SerializeField] private bool leftControllerLocked = false;
        [SerializeField] private bool rightControllerLocked = false;
        [SerializeField] private float hallucinationTimer = 0f;

        // VR system references (assigned at runtime from XR rig)
        private Transform leftController;
        private Transform rightController;
        private Camera vrCamera;

        // Input lag buffer
        private float[] inputLagBuffer;
        private int inputLagBufferIndex = 0;
        private float inputLagDuration = 0f;

        // Tremor
        private Vector3 leftTremorOffset;
        private Vector3 rightTremorOffset;
        private float tremorPhase = 0f;

        // Visual effects
        private float tunnelVisionAmount = 0f;
        private float colorDesaturation = 0f;
        private float chromaticAberration = 0f;
        private float blurAmount = 0f;

        // Hallucination
        private float nextHallucinationCheck = 0f;
        private const float HALLUCINATION_CHECK_INTERVAL = 5f; // seconds

        // Events for VR rendering systems
        public event Action<TremorData> OnTremorUpdate;
        public event Action<VisualEffectData> OnVisualEffectUpdate;
        public event Action<HallucinationEvent> OnHallucination;
        public event Action<bool, bool> OnControllerLockoutChanged;
        public event Action<float> OnInputLagChanged;

        /// <summary>
        /// Whether emersion effects are active.
        /// Disabled in Mode A (edu-tourist), enabled in Mode B (hardcore survival).
        /// </summary>
        public bool EffectsEnabled { get; set; } = true;

        private void Start()
        {
            inputLagBuffer = new float[120]; // 2 seconds at 60fps

            if (physiologyController != null)
            {
                physiologyController.OnStateChanged += HandlePhysiologyChanged;
                physiologyController.OnPlayerDeath += HandlePlayerDeath;
            }
        }

        private void OnDestroy()
        {
            if (physiologyController != null)
            {
                physiologyController.OnStateChanged -= HandlePhysiologyChanged;
                physiologyController.OnPlayerDeath -= HandlePlayerDeath;
            }
        }

        private void Update()
        {
            if (!EffectsEnabled) return;

            float dt = Time.deltaTime;

            UpdateTremorEffect(dt);
            UpdateVisualEffects(dt);
            UpdateInputLag(dt);
            UpdateControllerLockout();
            UpdateHallucinations(dt);
        }

        private void HandlePhysiologyChanged(PhysiologyState state)
        {
            // Smooth transitions for all effect intensities
            float lerpRate = config != null ? config.effectTransitionRate : 2f;
            float dt = Time.deltaTime;

            currentTremorIntensity = Mathf.Lerp(currentTremorIntensity,
                state.TremorIntensity, lerpRate * dt);

            currentVisualDistortion = Mathf.Lerp(currentVisualDistortion,
                state.VisualDistortionIntensity, lerpRate * dt);

            currentInputLag = Mathf.Lerp(currentInputLag,
                state.InputLagFactor, lerpRate * dt);

            // Controller lockout from limb injuries
            // From SPARK: "Controller lockout on limb injuries - Simulated arm injury
            // disables corresponding controller. Forces one-handed gameplay."
            bool newLeftLock = state.leftArmFunction < 0.1f;
            bool newRightLock = state.rightArmFunction < 0.1f;

            if (newLeftLock != leftControllerLocked || newRightLock != rightControllerLocked)
            {
                leftControllerLocked = newLeftLock;
                rightControllerLocked = newRightLock;
                OnControllerLockoutChanged?.Invoke(leftControllerLocked, rightControllerLocked);
            }
        }

        // ===== TREMOR SYSTEM =====
        // From IPK: "Niedobor weglowodanow awatara powoduje tremor miesni -
        // nasilajace sie drgania kontrolerow"
        // From SPARK: "Controller vibration and aim instability increase with stress.
        // Directly impacts combat effectiveness and fine motor tasks."

        private void UpdateTremorEffect(float dt)
        {
            if (currentTremorIntensity < 0.01f)
            {
                leftTremorOffset = Vector3.zero;
                rightTremorOffset = Vector3.zero;
                return;
            }

            tremorPhase += dt * (5f + currentTremorIntensity * 15f); // Frequency increases with intensity

            float maxAmplitude = config != null ? config.maxTremorAmplitude : 0.02f;
            float amplitude = currentTremorIntensity * maxAmplitude;

            // Multi-frequency tremor for realism (not a simple sine wave)
            // Combines low-frequency drift with high-frequency shaking
            float lowFreq = Mathf.Sin(tremorPhase * 1.3f) * 0.6f;
            float midFreq = Mathf.Sin(tremorPhase * 4.7f) * 0.3f;
            float highFreq = Mathf.Sin(tremorPhase * 11.2f) * 0.1f;

            float tremorX = (lowFreq + midFreq + highFreq) * amplitude;
            float tremorY = Mathf.Sin(tremorPhase * 2.1f + 0.7f) * amplitude * 0.7f;
            float tremorZ = Mathf.Sin(tremorPhase * 3.3f + 1.4f) * amplitude * 0.5f;

            leftTremorOffset = new Vector3(tremorX, tremorY, tremorZ);
            rightTremorOffset = new Vector3(
                tremorX * 0.8f + Mathf.Sin(tremorPhase * 5.1f) * amplitude * 0.2f,
                tremorY * 0.9f,
                tremorZ * 0.7f
            );

            // Haptic feedback intensity
            float hapticIntensity = currentTremorIntensity * (config != null ? config.hapticTremorMultiplier : 0.5f);

            OnTremorUpdate?.Invoke(new TremorData
            {
                leftOffset = leftControllerLocked ? Vector3.zero : leftTremorOffset,
                rightOffset = rightControllerLocked ? Vector3.zero : rightTremorOffset,
                intensity = currentTremorIntensity,
                hapticAmplitude = hapticIntensity,
                hapticFrequency = 50f + currentTremorIntensity * 200f
            });
        }

        // ===== VISUAL EFFECTS =====
        // From IPK: "aberracje pola widzenia", "delikatne, podprogowe modyfikacje"
        // From SPARK: "Progressive visual effects: blur, tunnel vision, color
        // desaturation. Creates urgency for shelter-seeking behavior."

        private void UpdateVisualEffects(float dt)
        {
            var state = physiologyController?.State;
            if (state == null) return;

            // Tunnel vision from hypothermia and blood loss
            // From SPARK: "Perception degradation in hypothermia"
            tunnelVisionAmount = Mathf.Lerp(tunnelVisionAmount,
                Mathf.Clamp01(
                    state.hypothermiaStage * 0.3f +
                    (1f - state.bloodVolume) * 0.5f +
                    Mathf.Max(0f, (100f - state.oxygenSaturation) / 30f)
                ),
                dt * 2f);

            // Color desaturation - world loses color as player deteriorates
            colorDesaturation = Mathf.Lerp(colorDesaturation,
                Mathf.Clamp01(
                    state.hypothermiaStage * 0.2f +
                    (1f - state.bloodVolume) * 0.4f +
                    state.mentalFatigue * 0.3f
                ),
                dt * 1f);

            // Chromatic aberration from toxins and concussion
            // From IPK: "znieksztalcenia pola widzenia imituja zaburzenia percepcji"
            chromaticAberration = Mathf.Lerp(chromaticAberration,
                Mathf.Clamp01(
                    state.toxinLevel * 0.6f +
                    state.concussionSeverity * 0.5f
                ),
                dt * 3f);

            // Blur from exhaustion and dehydration
            blurAmount = Mathf.Lerp(blurAmount,
                Mathf.Clamp01(
                    Mathf.Max(0f, state.mentalFatigue - 0.6f) * 2f +
                    Mathf.Max(0f, (1f - state.hydration) - 0.5f) * 1.5f +
                    state.concussionSeverity * 0.4f
                ),
                dt * 2f);

            // FOV reduction (tunnel vision for VR camera)
            currentFOVReduction = tunnelVisionAmount * (config != null ? config.maxFOVReduction : 30f);

            OnVisualEffectUpdate?.Invoke(new VisualEffectData
            {
                tunnelVision = tunnelVisionAmount,
                colorDesaturation = colorDesaturation,
                chromaticAberration = chromaticAberration,
                blur = blurAmount,
                fovReduction = currentFOVReduction,
                vignetteIntensity = tunnelVisionAmount * 0.8f,
                // Toxin-specific color shifts
                greenTint = state.activeToxin == ToxinType.MushroomHallucinogenic ? state.toxinLevel * 0.3f : 0f,
                timeDilation = state.activeToxin == ToxinType.Alkaloid ? state.toxinLevel * 0.5f : 0f
            });
        }

        // ===== INPUT LAG =====
        // From IPK: "celowy lag" - deliberate input delay after toxin ingestion.
        // "Zatrucie alkaloidami grzybow modyfikuje parametry silnika:
        // opoznienie reakcji symuluje spowolnienie"

        private void UpdateInputLag(float dt)
        {
            float maxLag = config != null ? config.maxInputLagSeconds : 0.3f;
            inputLagDuration = currentInputLag * maxLag;

            OnInputLagChanged?.Invoke(inputLagDuration);
        }

        // ===== CONTROLLER LOCKOUT =====
        // From SPARK: "Controller lockout on limb injuries - Simulated arm injury
        // disables corresponding controller. Forces one-handed gameplay.
        // Teaches adaptation under physical limitation."

        private void UpdateControllerLockout()
        {
            // Lockout state is updated in HandlePhysiologyChanged.
            // This method can add gradual degradation effects before full lockout.

            var state = physiologyController?.State;
            if (state == null) return;

            // Partial arm function = reduced tracking accuracy
            // (handled by adding extra tremor to the weakened arm)
            if (state.leftArmFunction < 1f && state.leftArmFunction > 0.1f)
            {
                float weakness = 1f - state.leftArmFunction;
                leftTremorOffset += UnityEngine.Random.insideUnitSphere * weakness * 0.01f;
            }
            if (state.rightArmFunction < 1f && state.rightArmFunction > 0.1f)
            {
                float weakness = 1f - state.rightArmFunction;
                rightTremorOffset += UnityEngine.Random.insideUnitSphere * weakness * 0.01f;
            }
        }

        // ===== HALLUCINATION SYSTEM =====
        // From SPARK: "Phantom sounds, voice distortion, shadow movement.
        // Creates psychological pressure mirroring real sleep deprivation."

        private void UpdateHallucinations(float dt)
        {
            var state = physiologyController?.State;
            if (state == null) return;

            hallucinationTimer += dt;
            if (hallucinationTimer < HALLUCINATION_CHECK_INTERVAL) return;
            hallucinationTimer = 0f;

            float probability = state.HallucinationProbability;
            if (probability <= 0f) return;

            if (UnityEngine.Random.value < probability)
            {
                TriggerHallucination(state);
            }
        }

        private void TriggerHallucination(PhysiologyState state)
        {
            // Determine hallucination type based on source
            HallucinationType type;

            if (state.sleepDebtHours > 48f)
            {
                // Severe sleep deprivation: visual + audio hallucinations
                type = UnityEngine.Random.value > 0.5f
                    ? HallucinationType.ShadowMovement
                    : HallucinationType.PhantomSound;
            }
            else if (state.activeToxin == ToxinType.MushroomHallucinogenic)
            {
                // Mushroom-induced: visual distortions
                type = HallucinationType.ColorShift;
            }
            else
            {
                // General fatigue: auditory hallucinations
                type = HallucinationType.VoiceDistortion;
            }

            float intensity = Mathf.Clamp01(state.HallucinationProbability * 2f);
            float duration = UnityEngine.Random.Range(1f, 5f) * intensity;

            OnHallucination?.Invoke(new HallucinationEvent
            {
                type = type,
                intensity = intensity,
                duration = duration,
                direction = UnityEngine.Random.insideUnitSphere
            });

            Debug.Log($"[Emersion] Hallucination triggered: {type} at intensity {intensity:F2}");
        }

        private void HandlePlayerDeath(string cause)
        {
            // Disable all effects on death - transition to noEZUS report screen
            EffectsEnabled = false;
            currentTremorIntensity = 0f;
            currentVisualDistortion = 0f;

            Debug.Log($"[Emersion] Effects disabled. Player death cause: {cause}");
        }
    }

    // ===== DATA STRUCTS =====

    [Serializable]
    public struct TremorData
    {
        public Vector3 leftOffset;
        public Vector3 rightOffset;
        public float intensity;
        public float hapticAmplitude;  // 0-1
        public float hapticFrequency;  // Hz
    }

    [Serializable]
    public struct VisualEffectData
    {
        public float tunnelVision;       // 0-1
        public float colorDesaturation;  // 0-1
        public float chromaticAberration; // 0-1
        public float blur;               // 0-1
        public float fovReduction;       // degrees
        public float vignetteIntensity;  // 0-1
        public float greenTint;          // 0-1 (mushroom specific)
        public float timeDilation;       // 0-1 (alkaloid specific)
    }

    [Serializable]
    public struct HallucinationEvent
    {
        public HallucinationType type;
        public float intensity;
        public float duration;
        public Vector3 direction; // For spatial audio positioning
    }

    public enum HallucinationType
    {
        PhantomSound,      // Hearing non-existent sounds (footsteps, voices, animal sounds)
        VoiceDistortion,   // Existing sounds become distorted, speech becomes unintelligible
        ShadowMovement,    // Seeing movement in peripheral vision where there is nothing
        ColorShift,        // Temporary color perception changes (from hallucinogenic toxins)
        ScaleDistortion,   // Objects appear larger/smaller (severe fatigue/toxins)
        TimeDistortion     // Subjective time perception changes
    }
}
