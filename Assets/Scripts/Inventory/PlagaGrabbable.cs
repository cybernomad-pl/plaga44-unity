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
            // Apply gripconfig ZAWSZE gdy isGrabbed + parent przypisany.
            // Guard transform.parent != null -- OVRGrabber parent w GrabBegin
            // wywolywany PO grabbable.GrabBegin, wiec przy pierwszym call
            // moze nie byc ustawiony -- localPos=zero teleportuje do origin!
            // Zero-offset TEZ mutate (reset do hand origin) -- inaczej slider
            // dzialal niespojnie (non-zero ok, zero ignored).
            if (isGrabbed && transform.parent != null)
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
            // LOG PRE -- stan przed przekazaniem do OVRGrabbable base
            var rbPre = GetComponent<Rigidbody>();
            Debug.Log($"{LOG} GRAB[1/4] PRE: {name} pos={transform.position:F2} " +
                $"rb(kinem={rbPre?.isKinematic},grav={rbPre?.useGravity},vel={rbPre?.linearVelocity.magnitude:F2}) " +
                $"parent={(transform.parent != null ? transform.parent.name : "<null>")} " +
                $"grabPoint={grabPoint?.name} handCtrl={(hand != null ? ResolveController(hand).ToString() : "null")}");

            base.GrabBegin(hand, grabPoint);

            // LOG POST BASE -- base zmienilo parent + kinematic
            var rbPost = GetComponent<Rigidbody>();
            Debug.Log($"{LOG} GRAB[2/4] POST-BASE: {name} pos={transform.position:F2} " +
                $"rb(kinem={rbPost?.isKinematic},grav={rbPost?.useGravity}) " +
                $"parent={(transform.parent != null ? transform.parent.name : "<null>")} " +
                $"isGrabbed={isGrabbed} grabbedBy={(m_grabbedBy != null ? m_grabbedBy.name : "null")}");

            _holdingController = ResolveController(hand);
            _gripHeldLastFrame = false;
            if (_haptic != null) _haptic.OnGrab(_holdingController);

            // Load + apply saved grip offset (per-item PlayerPrefs)
            var cfg = ItemGripConfig.Load(BaseName);
            ApplyGripConfig(cfg);
            Debug.Log($"{LOG} GRAB[3/4] GripConfig applied: {name} " +
                $"offsetPos={cfg.offsetPos:F3} offsetRot={cfg.offsetRotEuler:F1} scale={cfg.scale:F3} " +
                $"-> localPos={transform.localPosition:F3} localRot={transform.localEulerAngles:F1}");

            // Freeze SDK hand fingers while holding -- lock at CURRENT pose (natural grip
            // from hand tracking at moment of grab). No artificial fist -- just stop animating.
            HandFingerFreezer.Freeze(_holdingController, fistPose: false);

            // Remove from holster if attached
            if (homeHolster != null && homeHolster.ContainedItem == gameObject)
            {
                Debug.Log($"{LOG} GRAB[4/4] Released from holster {homeHolster.name}");
                homeHolster.Release();
            }
            Debug.Log($"{LOG} GRAB[DONE]: {name} by {_holdingController}");
        }

        public override void GrabEnd(Vector3 linearVelocity, Vector3 angularVelocity)
        {
            var controller = _holdingController != OVRInput.Controller.None
                ? _holdingController
                : ResolveController(m_grabbedBy);

            var rbPre = GetComponent<Rigidbody>();
            Debug.Log($"{LOG} RELEASE[1/3] PRE: {name} pos={transform.position:F2} " +
                $"rb(kinem={rbPre?.isKinematic},grav={rbPre?.useGravity}) " +
                $"vel={linearVelocity.magnitude:F2}m/s angVel={angularVelocity.magnitude:F2} " +
                $"ctrl={controller}");

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
                Debug.LogWarning($"{LOG} RELEASE exception (non-fatal): {e.Message}");
            }

            // LOG POST -- base odparentowal + restore kinematic state
            var rbPost = GetComponent<Rigidbody>();
            Debug.Log($"{LOG} RELEASE[2/3] POST-BASE: {name} pos={transform.position:F2} " +
                $"rb(kinem={rbPost?.isKinematic},grav={rbPost?.useGravity},vel={rbPost?.linearVelocity.magnitude:F2}) " +
                $"parent={(transform.parent != null ? transform.parent.name : "<null>")}");

            // Auto-return to holster if released near it
            if (homeHolster != null && homeHolster.IsInRange(transform.position))
            {
                Debug.Log($"{LOG} RELEASE[3/3] snapping to holster {homeHolster.name}");
                homeHolster.Holster(gameObject);
            }
            else
            {
                Debug.Log($"{LOG} RELEASE[3/3] dropped to world (no holster)");
            }
        }

        // Periodic position log gdy trzymany -- wykrywa czy item znika albo teleportuje
        private float _nextHeldLog;
        private void LateUpdate()
        {
            if (!isGrabbed) return;
            if (Time.unscaledTime < _nextHeldLog) return;
            _nextHeldLog = Time.unscaledTime + 1f; // raz na sekunde
            Debug.Log($"{LOG} HELD: {name} pos={transform.position:F2} parent={(transform.parent != null ? transform.parent.name : "<null>")}");
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
