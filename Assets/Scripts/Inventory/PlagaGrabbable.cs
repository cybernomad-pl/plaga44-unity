// =============================================================================
// PlagaGrabbable.cs
// CYBERNOMAD -- OVRGrabbable subclass z DEBUG TOGGLES.
//
// Wszystkie dodatkowe features DOMYSLNIE WYLACZONE. Borys runtime w
// Inspectorze (Play Mode) wlacza po kolei aby zobaczyc ktory feature
// przeszkadza w grab. Kazdy feature ma one-shot log przy pierwszym
// aktywowaniu.
//
// FEATURES:
//   enableApplyGripConfig  -- ustawia transform.localPos/Rot z ItemGripConfig
//   enableSyncGrabberOffset -- nadpisuje OVRGrabber.m_grabbedObjectPosOff
//   enableHaptic            -- HapticOnGrab (grab/release + grip/trigger buzz)
//   enableFingerFreeze      -- HandFingerFreezer (lock palcow podczas grip)
//   enableHolsterReturn     -- snap do holster na release
//   enableHeldPositionLog   -- periodic log pozycji gdy trzymany (diagnostyka)
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

        [Header("Debug Toggles -- RUNTIME togglable w Inspectorze")]
        [Tooltip("Apply ItemGripConfig offset/rot na transform.localPosition w GrabBegin.")]
        public bool enableApplyGripConfig = false;

        [Tooltip("Nadpisuje OVRGrabber.m_grabbedObjectPosOff zeby MoveGrabbedObject uzywal naszych offsetow.")]
        public bool enableSyncGrabberOffset = false;

        [Tooltip("HapticOnGrab feedback (grab pulse, release pulse, grip continuous buzz, trigger pulse).")]
        public bool enableHaptic = false;

        [Tooltip("Freeze SDK hand fingers podczas trzymania itemu (lock na natural grip pose).")]
        public bool enableFingerFreeze = false;

        [Tooltip("Auto-return do holster jesli item zwolniony w poblizu.")]
        public bool enableHolsterReturn = false;

        [Tooltip("LateUpdate log pozycji itemu co 1s gdy trzymany -- diagnostyka teleportacji.")]
        public bool enableHeldPositionLog = false;

        [Header("Holster Return (jesli enableHolsterReturn)")]
        public HolsterAnchor homeHolster;

        // --- Runtime state -------------------------------------------------
        private HapticOnGrab _haptic;
        private bool _gripHeldLastFrame;
        private OVRInput.Controller _holdingController = OVRInput.Controller.None;

        private ItemGripConfig _gripConfig = ItemGripConfig.Default;
        private Vector3 _originalScale;
        private bool _originalScaleCached;

        // One-shot log flags
        private bool _logApplyGripOnce;
        private bool _logSyncOffsetOnce;

        // --- Public API (SettingsRegistry) ---------------------------------
        public string BaseName
        {
            get
            {
                string n = name;
                int paren = n.IndexOf(" (Clone)", System.StringComparison.Ordinal);
                if (paren >= 0) n = n.Substring(0, paren);
                if (n.StartsWith("ItemPreview_", System.StringComparison.Ordinal))
                    n = n.Substring("ItemPreview_".Length);
                return n;
            }
        }

        public ItemGripConfig GripConfig
        {
            get => _gripConfig;
            set { _gripConfig = value; ApplyGripConfig(_gripConfig); }
        }

        // --- ApplyGripConfig (GUARDED by toggle) ---------------------------
        private void ApplyGripConfig(ItemGripConfig cfg)
        {
            if (!_originalScaleCached)
            {
                _originalScale = transform.localScale;
                _originalScaleCached = true;
            }

            if (!enableApplyGripConfig)
            {
                // Skip pos/rot mutate. Tylko scale (zachowane dla backward compat).
                transform.localScale = _originalScale * cfg.scale;
                _gripConfig = cfg;
                return;
            }

            if (isGrabbed && transform.parent != null)
            {
                transform.localPosition = cfg.offsetPos;
                transform.localRotation = Quaternion.Euler(cfg.offsetRotEuler);

                if (!_logApplyGripOnce)
                {
                    Debug.Log($"{LOG} [ApplyGripConfig-ACTIVE] first time: cfg offsetPos={cfg.offsetPos:F3} rot={cfg.offsetRotEuler:F1}");
                    _logApplyGripOnce = true;
                }

                if (enableSyncGrabberOffset && m_grabbedBy is PlagaGrabber pg)
                {
                    pg.UpdateGrabbedOffset(cfg.offsetPos, Quaternion.Euler(cfg.offsetRotEuler));
                    if (!_logSyncOffsetOnce)
                    {
                        Debug.Log($"{LOG} [SyncGrabberOffset-ACTIVE] first time: nadpisuje m_grabbedObjectPosOff");
                        _logSyncOffsetOnce = true;
                    }
                }
            }

            transform.localScale = _originalScale * cfg.scale;
            _gripConfig = cfg;
        }

        // --- Lifecycle -----------------------------------------------------
        protected override void Start()
        {
            base.Start();
            _haptic = GetComponent<HapticOnGrab>();
            Debug.Log($"{LOG} Start: {name} toggles: ApplyGrip={enableApplyGripConfig} SyncOff={enableSyncGrabberOffset} " +
                $"Haptic={enableHaptic} FingerFreeze={enableFingerFreeze} Holster={enableHolsterReturn} HeldLog={enableHeldPositionLog}");
        }

        private void Update()
        {
            if (!isGrabbed || m_grabbedBy == null)
            {
                if (_gripHeldLastFrame) { StopGripHaptic(); _gripHeldLastFrame = false; }
                _holdingController = OVRInput.Controller.None;
                return;
            }

            if (_holdingController == OVRInput.Controller.None)
                _holdingController = ResolveController(m_grabbedBy);
            var ctrl = _holdingController;
            if (ctrl == OVRInput.Controller.None) return;

            if (!enableHaptic) return;
            var mgr = HapticManager.Instance;
            if (mgr == null) return;

            float gripFlex = OVRInput.Get(OVRInput.Axis1D.PrimaryHandTrigger, ctrl);
            bool gripHeld = gripFlex >= 0.55f;
            if (gripHeld && !_gripHeldLastFrame) mgr.StartGripHold(ctrl);
            else if (!gripHeld && _gripHeldLastFrame) StopGripHaptic();
            _gripHeldLastFrame = gripHeld;

            if (OVRInput.GetDown(OVRInput.Button.PrimaryIndexTrigger, ctrl))
                mgr.PlayTriggerPull(ctrl);
        }

        private void StopGripHaptic()
        {
            var mgr = HapticManager.Instance;
            if (mgr != null && _holdingController != OVRInput.Controller.None)
                mgr.StopGripHold(_holdingController);
        }

        // --- GRAB ---------------------------------------------------------
        public override void GrabBegin(OVRGrabber hand, Collider grabPoint)
        {
            Debug.Log($"{LOG} GRAB: {name} by {ResolveController(hand)} " +
                $"pos={transform.position:F2} parent-before={(transform.parent != null ? transform.parent.name : "<null>")}");

            base.GrabBegin(hand, grabPoint);

            _holdingController = ResolveController(hand);
            _gripHeldLastFrame = false;

            if (enableHaptic && _haptic != null) _haptic.OnGrab(_holdingController);

            ApplyGripConfig(ItemGripConfig.Load(BaseName));

            if (enableFingerFreeze)
                HandFingerFreezer.Freeze(_holdingController, fistPose: false);

            if (enableHolsterReturn && homeHolster != null && homeHolster.ContainedItem == gameObject)
                homeHolster.Release();
        }

        public override void GrabEnd(Vector3 linearVelocity, Vector3 angularVelocity)
        {
            var controller = _holdingController != OVRInput.Controller.None
                ? _holdingController : ResolveController(m_grabbedBy);

            Debug.Log($"{LOG} RELEASE: {name} ctrl={controller} " +
                $"pos={transform.position:F2} vel={linearVelocity.magnitude:F2}m/s");

            StopGripHaptic();
            _gripHeldLastFrame = false;

            if (enableFingerFreeze)
                HandFingerFreezer.Unfreeze(_holdingController);

            _holdingController = OVRInput.Controller.None;

            if (enableHaptic && _haptic != null) _haptic.OnRelease(controller);

            try { base.GrabEnd(linearVelocity, angularVelocity); }
            catch (System.Exception e) { Debug.LogWarning($"{LOG} RELEASE exception: {e.Message}"); }

            if (enableHolsterReturn && homeHolster != null && homeHolster.IsInRange(transform.position))
                homeHolster.Holster(gameObject);
        }

        // --- HELD log (GUARDED, domyslnie OFF) -----------------------------
        private float _nextHeldLog;
        private void LateUpdate()
        {
            if (!enableHeldPositionLog) return;
            if (!isGrabbed) return;
            if (Time.unscaledTime < _nextHeldLog) return;
            _nextHeldLog = Time.unscaledTime + 1f;
            Debug.Log($"{LOG} HELD: {name} pos={transform.position:F2} parent={(transform.parent != null ? transform.parent.name : "<null>")}");
        }

        private static OVRInput.Controller ResolveController(OVRGrabber hand)
        {
            if (hand == null) return OVRInput.Controller.None;
            string n = hand.gameObject.name.ToLowerInvariant();
            if (n.Contains("right")) return OVRInput.Controller.RTouch;
            if (n.Contains("left"))  return OVRInput.Controller.LTouch;
            return OVRInput.Controller.None;
        }
    }
}
