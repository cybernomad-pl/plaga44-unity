// =============================================================================
// PlagaGrabbable.cs
// CYBERNOMAD -- OVRGrabbable subclass with integrated haptic feedback and
// holster return-on-release. Auto-wires HapticOnGrab on the same GameObject.
//
// When grabbed: plays grab haptic (modulated by mass).
// When released outside a holster: normal physics drop + release haptic.
// When released inside a holster volume: snaps back to holster anchor.
//
// Continuous haptics while grabbed:
//   - Grip held down: gentle continuous buzz (feel the object weight)
//   - Trigger pressed: sharp pulse (interact/use feedback)
// =============================================================================

using UnityEngine;
using Plaga44.Feedback;

namespace Plaga44.Inventory
{
    [RequireComponent(typeof(Rigidbody))]
    [RequireComponent(typeof(Collider))]
    public class PlagaGrabbable : OVRGrabbable
    {
        private const string LOG = "[PLAGA44][Grabbable]";

        [Header("Holster Return")]
        [Tooltip("If assigned, item snaps to this anchor when released near it.")]
        public HolsterAnchor homeHolster;

        private HapticOnGrab _haptic;

        // Continuous haptic state
        private bool _gripHeldLastFrame;
        private OVRInput.Controller _holdingController = OVRInput.Controller.None;

        // Per-item grip calibration (loaded on GrabBegin, applied to transform)
        private ItemGripConfig _gripConfig = ItemGripConfig.Default;
        private Vector3 _originalScale;
        private bool _originalScaleCached;

        /// <summary>Base item name (prefab name without "(Clone)" suffix) -- key for ItemGripConfig.</summary>
        public string BaseName
        {
            get
            {
                string n = name;
                int paren = n.IndexOf(" (Clone)", System.StringComparison.Ordinal);
                if (paren >= 0) n = n.Substring(0, paren);
                // Strip "ItemPreview_" prefix from ItemBrowser spawn
                if (n.StartsWith("ItemPreview_", System.StringComparison.Ordinal))
                    n = n.Substring("ItemPreview_".Length);
                return n;
            }
        }

        /// <summary>Current grip config (live-tunable via SettingsRegistry).</summary>
        public ItemGripConfig GripConfig
        {
            get => _gripConfig;
            set { _gripConfig = value; ApplyGripConfig(_gripConfig); }
        }

        /// <summary>Apply grip offset + scale to this item's transform LOCAL values
        /// (relative to hand anchor parent after OVRGrabbable parented it).</summary>
        private void ApplyGripConfig(ItemGripConfig cfg)
        {
            if (!_originalScaleCached)
            {
                _originalScale = transform.localScale;
                _originalScaleCached = true;
            }
            // Only apply position/rotation when parented to hand (isGrabbed)
            if (isGrabbed)
            {
                transform.localPosition = cfg.offsetPos;
                transform.localRotation = Quaternion.Euler(cfg.offsetRotEuler);
            }
            transform.localScale = _originalScale * cfg.scale;
            _gripConfig = cfg;
        }

        protected override void Start()
        {
            base.Start();
            _haptic = GetComponent<HapticOnGrab>();
            if (_haptic == null)
                Debug.LogWarning($"{LOG} {name} missing HapticOnGrab component -- no haptic feedback.");
        }

        private void Update()
        {
            if (!isGrabbed || m_grabbedBy == null)
            {
                // Stop any lingering grip haptic
                if (_gripHeldLastFrame)
                {
                    StopGripHaptic();
                    _gripHeldLastFrame = false;
                }
                _holdingController = OVRInput.Controller.None;
                return;
            }

            // Resolve which controller is holding us
            if (_holdingController == OVRInput.Controller.None)
                _holdingController = ResolveController(m_grabbedBy);

            var ctrl = _holdingController;
            if (ctrl == OVRInput.Controller.None) return;

            var mgr = HapticManager.Instance;
            if (mgr == null) return;

            // --- Continuous grip haptic: buzz while grip physically held ---
            float gripFlex = OVRInput.Get(OVRInput.Axis1D.PrimaryHandTrigger, ctrl);
            bool gripHeld = gripFlex >= 0.55f;

            if (gripHeld && !_gripHeldLastFrame)
            {
                // Started holding grip
                mgr.StartGripHold(ctrl);
            }
            else if (!gripHeld && _gripHeldLastFrame)
            {
                // Released grip (but still holding object due to toggle)
                StopGripHaptic();
            }
            _gripHeldLastFrame = gripHeld;

            // --- Trigger haptic: pulse on trigger press ---
            if (OVRInput.GetDown(OVRInput.Button.PrimaryIndexTrigger, ctrl))
            {
                mgr.PlayTriggerPull(ctrl);
            }
        }

        private void StopGripHaptic()
        {
            var mgr = HapticManager.Instance;
            if (mgr != null && _holdingController != OVRInput.Controller.None)
                mgr.StopGripHold(_holdingController);
        }

        public override void GrabBegin(OVRGrabber hand, Collider grabPoint)
        {
            base.GrabBegin(hand, grabPoint);
            _holdingController = ResolveController(hand);
            _gripHeldLastFrame = false;
            Debug.Log($"{LOG} GrabBegin: {name} by {_holdingController} (base={BaseName})");
            if (_haptic != null) _haptic.OnGrab(_holdingController);

            // Load + apply saved grip offset (per-item PlayerPrefs)
            ApplyGripConfig(ItemGripConfig.Load(BaseName));

            // Freeze SDK hand fingers while holding -- lock at CURRENT pose (natural grip
            // from hand tracking at moment of grab). No artificial fist -- just stop animating.
            HandFingerFreezer.Freeze(_holdingController, fistPose: false);

            // Remove from holster if attached
            if (homeHolster != null && homeHolster.ContainedItem == gameObject)
                homeHolster.Release();
        }

        public override void GrabEnd(Vector3 linearVelocity, Vector3 angularVelocity)
        {
            var controller = _holdingController != OVRInput.Controller.None
                ? _holdingController
                : ResolveController(m_grabbedBy);

            Debug.Log($"{LOG} GrabEnd: {name} released, vel={linearVelocity.magnitude:F2}m/s");

            // Stop any ongoing grip haptic
            StopGripHaptic();
            _gripHeldLastFrame = false;

            // Release SDK hand fingers -- back to normal tracking
            HandFingerFreezer.Unfreeze(_holdingController);

            _holdingController = OVRInput.Controller.None;

            if (_haptic != null) _haptic.OnRelease(controller);

            try
            {
                base.GrabEnd(linearVelocity, angularVelocity);
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"{LOG} GrabEnd exception (non-fatal): {e.Message}");
                // OVRGrabbable.GrabEnd can throw if Rigidbody was destroyed or kinematic state changed
            }

            // Auto-return to holster if released near it
            if (homeHolster != null && homeHolster.IsInRange(transform.position))
            {
                Debug.Log($"{LOG} {name} snapping back to {homeHolster.name}");
                homeHolster.Holster(gameObject);
            }
        }

        private static OVRInput.Controller ResolveController(OVRGrabber hand)
        {
            if (hand == null) return OVRInput.Controller.None;
            // OVRGrabber exposes m_controller -- check by name convention as fallback
            string n = hand.gameObject.name.ToLowerInvariant();
            if (n.Contains("right")) return OVRInput.Controller.RTouch;
            if (n.Contains("left"))  return OVRInput.Controller.LTouch;
            return OVRInput.Controller.None;
        }
    }
}
