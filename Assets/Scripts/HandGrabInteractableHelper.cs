// HandGrabInteractableHelper.cs
// CYBERNOMAD -- Marks a GameObject as grabbable via Meta XR Interaction SDK.
//
// Usage:
//   Add this component to any object you want the player to grab.
//   It auto-adds Rigidbody and (when Interaction SDK is present) HandGrabInteractable.
//
// Requires: com.meta.xr.sdk.interaction (auto-detected via HAS_META_XR define)
// Namespace: Plaga44.Interaction

using UnityEngine;

#if HAS_META_XR
using System;
#endif

namespace Plaga44.Interaction
{
    /// <summary>
    /// Marks a GameObject as grabbable via controller-driven hand grab.
    /// Add this component to objects the player should be able to pick up.
    ///
    /// At runtime (on Quest), if com.meta.xr.sdk.interaction is installed,
    /// HandGrabInteractable will be resolved and added automatically.
    /// If the SDK is not present, the component logs a warning and skips.
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    [AddComponentMenu("PLAGA44/Hand Grab Interactable Helper")]
    public class HandGrabInteractableHelper : MonoBehaviour
    {
        [Header("Grab Settings")]
        [Tooltip("Mass of the object when held (kg). Reverts on release.")]
        public float heldMass = 0.3f;

        [Tooltip("If true, gravity is disabled while the object is grabbed.")]
        public bool disableGravityWhileHeld = true;

        [Header("Physics")]
        [Tooltip("Mass of the object at rest.")]
        public float restMass = 0.5f;

        [Tooltip("Collision detection mode. Continuous is safer for fast throws.")]
        public CollisionDetectionMode collisionMode = CollisionDetectionMode.ContinuousDynamic;

        // Runtime state
        private Rigidbody _rb;
        private bool _isGrabbed;

#if HAS_META_XR
        private const string TYPE_HAND_GRAB_INTERACTABLE =
            "Oculus.Interaction.HandGrab.HandGrabInteractable";

        private const string TYPE_GRABBABLE =
            "Oculus.Interaction.Grabbable";

        private const string TYPE_RIGIDBODY_POSE_UPDATER =
            "Oculus.Interaction.RigidbodyPoseUpdater";
#endif

        private void Awake()
        {
            _rb = GetComponent<Rigidbody>();
            ConfigureRigidbody();

#if HAS_META_XR
            EnsureInteractionComponents();
#else
            Debug.LogWarning(
                $"[PLAGA44] HandGrabInteractableHelper on '{name}': " +
                "HAS_META_XR not defined. Grab will not work. " +
                "Make sure build target is Android with Meta XR SDK installed.");
#endif
        }

        private void ConfigureRigidbody()
        {
            _rb.mass = restMass;
            _rb.collisionDetectionMode = collisionMode;
        }

#if HAS_META_XR
        /// <summary>
        /// Adds HandGrabInteractable, Grabbable, and RigidbodyPoseUpdater via reflection
        /// so this script compiles without a hard assembly reference to the Interaction SDK.
        /// </summary>
        private void EnsureInteractionComponents()
        {
            TryAddComponent(TYPE_GRABBABLE, "Grabbable");
            TryAddComponent(TYPE_RIGIDBODY_POSE_UPDATER, "RigidbodyPoseUpdater");
            TryAddComponent(TYPE_HAND_GRAB_INTERACTABLE, "HandGrabInteractable");
        }

        private void TryAddComponent(string typeName, string friendlyName)
        {
            // Search all loaded assemblies for the type
            Type t = null;
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                t = asm.GetType(typeName);
                if (t != null) break;
            }

            if (t == null)
            {
                Debug.LogWarning(
                    $"[PLAGA44] HandGrabInteractableHelper: " +
                    $"Type '{typeName}' not found. " +
                    $"Is com.meta.xr.sdk.interaction installed and compiled?");
                return;
            }

            if (GetComponent(t) == null)
            {
                gameObject.AddComponent(t);
                Debug.Log($"[PLAGA44] Added {friendlyName} to '{name}'.");
            }
        }
#endif

        // ── Public API for grab state (called by grab events if wired manually) ──

        /// <summary>
        /// Call this when the object is grabbed (e.g. from HandGrabInteractable events).
        /// Adjusts physics for held state.
        /// </summary>
        public void OnGrabbed()
        {
            if (_isGrabbed) return;
            _isGrabbed = true;

            _rb.mass = heldMass;
            if (disableGravityWhileHeld)
                _rb.useGravity = false;
        }

        /// <summary>
        /// Call this when the object is released.
        /// Restores physics for free-fall state.
        /// </summary>
        public void OnReleased()
        {
            if (!_isGrabbed) return;
            _isGrabbed = false;

            _rb.mass = restMass;
            _rb.useGravity = true;
        }

        public bool IsGrabbed => _isGrabbed;
    }
}
