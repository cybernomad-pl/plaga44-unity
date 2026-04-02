// AUTO-DISABLED: depends on PassthroughManager (guarded by PLAGA44_FULL_SDK)
#if PLAGA44_FULL_SDK
#if HAS_META_XR
using System.Collections;
using UnityEngine;

namespace Plaga44.MixedReality
{
    /// <summary>
    /// Trigger collider portal that switches between VR and MR mode when the
    /// player enters. Requires a <see cref="PassthroughManager"/> in the scene.
    ///
    /// Gameplay meaning:
    ///   - Walking INTO the portal  --> switches to MR (your room = the bunker)
    ///   - Walking OUT of the portal --> switches back to VR (underground)
    ///
    /// Attach to a GameObject with a Collider set as Trigger.
    /// The OVRCameraRig (or its CenterEyeAnchor) must have a Rigidbody (kinematic)
    /// or you can tag the player with the playerTag field below.
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public class PassthroughPortal : MonoBehaviour
    {
        // ------------------------------------------------------------------ //
        //  Inspector fields                                                   //
        // ------------------------------------------------------------------ //

        [Header("References")]
        [Tooltip("PassthroughManager in the scene. Auto-located if left empty.")]
        [SerializeField] private PassthroughManager _passthroughManager;

        [Header("Portal behaviour")]
        [Tooltip("Tag used to identify the VR player object (OVRCameraRig or head).")]
        [SerializeField] private string _playerTag = "MainCamera";

        [Tooltip("When true, entering the portal enables MR; exiting disables it. " +
                 "When false, each entry toggles the current mode.")]
        [SerializeField] private bool _enterEnablesMR = true;

        [Header("Visual feedback")]
        [Tooltip("Optional renderer on the portal frame -- changes emissive on activation.")]
        [SerializeField] private Renderer _portalFrameRenderer;
        [SerializeField] private Color _vrModeFrameColor  = new Color(0.0f, 0.4f, 1.0f);
        [SerializeField] private Color _mrModeFrameColor  = new Color(1.0f, 0.5f, 0.0f);

        // ------------------------------------------------------------------ //
        //  Unity lifecycle                                                    //
        // ------------------------------------------------------------------ //

        private void Awake()
        {
            var col = GetComponent<Collider>();
            col.isTrigger = true;

            if (_passthroughManager == null)
                _passthroughManager = FindObjectOfType<PassthroughManager>();

            UpdateFrameColor();
        }

        // ------------------------------------------------------------------ //
        //  Trigger callbacks                                                  //
        // ------------------------------------------------------------------ //

        private void OnTriggerEnter(Collider other)
        {
            if (!IsPlayer(other)) return;

            if (_enterEnablesMR)
                _passthroughManager?.SetMRMode(true);
            else
                _passthroughManager?.ToggleMode();

            UpdateFrameColor();
        }

        private void OnTriggerExit(Collider other)
        {
            if (!IsPlayer(other)) return;

            if (_enterEnablesMR)
                _passthroughManager?.SetMRMode(false);
            // In toggle mode, exit does nothing -- next entry will toggle

            UpdateFrameColor();
        }

        // ------------------------------------------------------------------ //
        //  Helpers                                                            //
        // ------------------------------------------------------------------ //

        private bool IsPlayer(Collider other)
        {
            return other.CompareTag(_playerTag);
        }

        private void UpdateFrameColor()
        {
            if (_portalFrameRenderer == null) return;

            bool mrActive = _passthroughManager != null && _passthroughManager.IsMRActive;
            Color target = mrActive ? _mrModeFrameColor : _vrModeFrameColor;

            // Use MaterialPropertyBlock to avoid creating new material instances
            var mpb = new MaterialPropertyBlock();
            _portalFrameRenderer.GetPropertyBlock(mpb);
            mpb.SetColor("_EmissionColor", target);
            _portalFrameRenderer.SetPropertyBlock(mpb);
        }
    }
}
#endif // HAS_META_XR
#endif // PLAGA44_FULL_SDK
