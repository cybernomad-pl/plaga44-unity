// HandGrabInteractorPlaceholder.cs
// CYBERNOMAD -- Temporary placeholder for HandGrabInteractor setup.
//
// Created by HandGrabSetup editor tool when com.meta.xr.sdk.interaction types
// are not yet resolvable (packages still downloading).
//
// Once SDK resolves: remove this component, add HandGrabInteractor manually,
// or re-run CYBERNOMAD > Scene Setup > Add Hand Grab Interactors.
//
// Namespace: Plaga44.Interaction

using UnityEngine;

namespace Plaga44.Interaction
{
    /// <summary>
    /// Placeholder component placed on controller anchor GameObjects when
    /// HandGrabInteractor type cannot be resolved at editor setup time.
    ///
    /// Replace with Oculus.Interaction.HandGrab.HandGrabInteractor once
    /// com.meta.xr.sdk.interaction package is fully imported by Unity.
    /// </summary>
    [AddComponentMenu("PLAGA44/Hand Grab Interactor Placeholder (Temp)")]
    public class HandGrabInteractorPlaceholder : MonoBehaviour
    {
        [Tooltip("Which hand this interactor is for.")]
        public string handSide = "Left";

        [Multiline]
        public string setupInstructions =
            "This is a placeholder for HandGrabInteractor.\n" +
            "1. Ensure com.meta.xr.sdk.interaction is installed.\n" +
            "2. Remove this component.\n" +
            "3. Add: Oculus.Interaction.HandGrab.HandGrabInteractor\n" +
            "4. Wire OVRControllerRef / OVRHandRef references.\n" +
            "OR: re-run CYBERNOMAD > Scene Setup > Add Hand Grab Interactors.";

        private void Awake()
        {
            Debug.LogWarning(
                $"[PLAGA44] HandGrabInteractorPlaceholder is present on '{name}' ({handSide}). " +
                "This is a setup placeholder -- replace with HandGrabInteractor " +
                "from com.meta.xr.sdk.interaction.");
        }
    }
}
