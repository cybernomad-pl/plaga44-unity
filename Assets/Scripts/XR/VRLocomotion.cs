// PLAGA '44 - VR Locomotion
// VR movement system for Meta Quest 3 with mode-dependent behavior.
// Part of issue #23: Unity VR project structure

using UnityEngine;

namespace Plaga44.XR
{
    /// <summary>
    /// VR locomotion system adapted for Meta Quest 3.
    ///
    /// Mode A (Edu-Tourist): Free teleport + smooth locomotion, debug camera
    /// Mode B (Hardcore Survival): Smooth locomotion only, affected by physiology
    ///   - Movement speed affected by: backpack weight, terrain, weather, injuries
    ///   - Stamina system: breaks every 2-3 hours per scenario docs
    ///   - Terrain effects: slippery in rain, slow in snow, risky on limestone
    /// </summary>
    public class VRLocomotion : MonoBehaviour
    {
        [Header("Movement")]
        [SerializeField] private float baseWalkSpeed = 1.4f;   // m/s - average walk
        [SerializeField] private float baseRunSpeed = 3.0f;    // m/s - jog speed
        [SerializeField] private float rotationSpeed = 45f;    // Snap turn degrees

        [Header("Teleport (Mode A only)")]
        [SerializeField] private float maxTeleportDistance = 20f;
        [SerializeField] private LineRenderer teleportLine;
        [SerializeField] private GameObject teleportMarker;

        [Header("Physiology Integration (Mode B)")]
        [SerializeField] private float weightSpeedPenalty = 0.3f;   // 30% slower at max weight
        [SerializeField] private float injurySpeedPenalty = 0.5f;   // 50% slower when injured
        [SerializeField] private float terrainSlipChance = 0.02f;   // 2% per step in rain

        [Header("References")]
        [SerializeField] private CharacterController characterController;
        [SerializeField] private Transform vrCamera;

        // State
        private float currentSpeedMultiplier = 1f;
        private float backpackWeight = 0f;      // kg
        private float maxCarryWeight = 25f;     // Scenario: max 25kg
        private bool isInjured = false;
        private bool isTeleportMode = false;
        private bool canRun = true;

        // Terrain modifiers
        private float terrainSpeedModifier = 1f;
        private float weatherSpeedModifier = 1f;

        private void Update()
        {
            // Check game mode for available movement types
            bool isEduTourist = DualMode.DualModeController.Instance != null &&
                DualMode.DualModeController.Instance.IsFeatureEnabled("FreeTeleport");

            if (isEduTourist && isTeleportMode)
            {
                HandleTeleport();
            }
            else
            {
                HandleSmoothLocomotion();
            }

            HandleSnapTurn();
        }

        private void HandleSmoothLocomotion()
        {
            if (characterController == null || vrCamera == null) return;

            // Get input from Quest 3 thumbstick
            Vector2 input = OVRInput.Get(OVRInput.Axis2D.PrimaryThumbstick);

            if (input.magnitude < 0.1f) return;

            // Determine speed
            bool wantsToRun = OVRInput.Get(OVRInput.Button.PrimaryThumbstick);
            float baseSpeed = (wantsToRun && canRun) ? baseRunSpeed : baseWalkSpeed;

            // Apply modifiers (Mode B only)
            float finalSpeed = baseSpeed * currentSpeedMultiplier * terrainSpeedModifier * weatherSpeedModifier;

            // Weight penalty: linear from 0kg to 25kg
            float weightRatio = Mathf.Clamp01(backpackWeight / maxCarryWeight);
            finalSpeed *= (1f - weightRatio * weightSpeedPenalty);

            // Injury penalty
            if (isInjured)
            {
                finalSpeed *= (1f - injurySpeedPenalty);
            }

            // Direction based on camera facing
            Vector3 forward = vrCamera.forward;
            forward.y = 0;
            forward.Normalize();

            Vector3 right = vrCamera.right;
            right.y = 0;
            right.Normalize();

            Vector3 moveDir = (forward * input.y + right * input.x).normalized;

            // Apply gravity
            Vector3 movement = moveDir * finalSpeed;
            movement.y = -9.81f; // Gravity

            characterController.Move(movement * Time.deltaTime);
        }

        private void HandleTeleport()
        {
            // Teleport arc visualization
            if (OVRInput.Get(OVRInput.Button.PrimaryIndexTrigger))
            {
                // Show teleport arc
                if (teleportLine != null)
                    teleportLine.enabled = true;
                if (teleportMarker != null)
                    teleportMarker.SetActive(true);

                // Calculate teleport target (parabolic arc)
                Ray ray = new Ray(vrCamera.position, vrCamera.forward);
                if (Physics.Raycast(ray, out RaycastHit hit, maxTeleportDistance, LayerMask.GetMask("Ground")))
                {
                    if (teleportMarker != null)
                        teleportMarker.transform.position = hit.point;
                }
            }

            // Execute teleport
            if (OVRInput.GetUp(OVRInput.Button.PrimaryIndexTrigger))
            {
                if (teleportMarker != null && teleportMarker.activeSelf)
                {
                    transform.position = teleportMarker.transform.position;
                }

                if (teleportLine != null)
                    teleportLine.enabled = false;
                if (teleportMarker != null)
                    teleportMarker.SetActive(false);
            }
        }

        private void HandleSnapTurn()
        {
            Vector2 rightStick = OVRInput.Get(OVRInput.Axis2D.SecondaryThumbstick);

            if (Mathf.Abs(rightStick.x) > 0.7f)
            {
                float turnAmount = Mathf.Sign(rightStick.x) * rotationSpeed;
                transform.Rotate(0, turnAmount, 0);
            }
        }

        /// <summary>
        /// Set backpack weight affecting movement speed.
        /// Scenario: max 25kg for 90L packs, 15kg for 60L/20L.
        /// </summary>
        public void SetBackpackWeight(float weightKg)
        {
            backpackWeight = Mathf.Clamp(weightKg, 0f, maxCarryWeight);
        }

        /// <summary>
        /// Set terrain speed modifier (slippery, muddy, deep snow, etc.)
        /// Scenario: wet limestone very slippery, deep snow exhausting.
        /// </summary>
        public void SetTerrainModifier(float modifier)
        {
            terrainSpeedModifier = Mathf.Clamp(modifier, 0.2f, 1f);
        }

        /// <summary>
        /// Set weather speed modifier (rain, snow, storm).
        /// </summary>
        public void SetWeatherModifier(float modifier)
        {
            weatherSpeedModifier = Mathf.Clamp(modifier, 0.3f, 1f);
        }

        /// <summary>
        /// Set injury state affecting movement.
        /// </summary>
        public void SetInjured(bool injured)
        {
            isInjured = injured;
        }

        /// <summary>
        /// Set overall speed multiplier (from hypothermia, fatigue, etc.)
        /// </summary>
        public void SetSpeedMultiplier(float multiplier)
        {
            currentSpeedMultiplier = Mathf.Clamp(multiplier, 0.1f, 1.5f);
        }

        /// <summary>
        /// Toggle teleport mode (Mode A only).
        /// </summary>
        public void SetTeleportMode(bool enabled)
        {
            isTeleportMode = enabled;
        }

        /// <summary>
        /// Set whether player can run (disabled when too fatigued/injured).
        /// </summary>
        public void SetCanRun(bool canRunNow)
        {
            canRun = canRunNow;
        }
    }
}
