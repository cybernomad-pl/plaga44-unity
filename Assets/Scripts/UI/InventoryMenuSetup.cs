// =============================================================================
// InventoryMenuSetup.cs
// CYBERNOMAD -- Dodaje przycisk "INVENTORY" do HamburgerMenu.
//
// Oddzielny skrypt (SRP) -- laczy InventoryScreen z HamburgerMenu
// bez tworzenia twardej zaleznosci miedzy nimi.
//
// Dodawany na scene przez SceneSetup. Czeka az HamburgerMenu bedzie gotowy
// (w Start, po Awake wszystkich singletonow) i dodaje przycisk.
// =============================================================================

using UnityEngine;

namespace Plaga44.UI
{
    /// <summary>
    /// Hooks the "INVENTORY" button into HamburgerMenu.
    /// Placed on scene by SceneSetup or manually.
    /// </summary>
    public class InventoryMenuSetup : MonoBehaviour
    {
        private const string LOG = "[PLAGA44][Inventory]";

        private void Start()
        {
            // Wait a frame to ensure all singletons are initialized
            // (HamburgerMenu.Awake may not have run yet if spawned in same frame)
            StartCoroutine(SetupNextFrame());
        }

        private System.Collections.IEnumerator SetupNextFrame()
        {
            yield return null; // wait one frame

            if (HamburgerMenu.Instance == null)
            {
                Debug.LogWarning($"{LOG} HamburgerMenu.Instance == null -- nie moge dodac przycisku INVENTORY");
                yield break;
            }

            HamburgerMenu.Instance.AddSeparator();

            HamburgerMenu.Instance.AddButton("INVENTORY", () =>
            {
                if (InventoryScreen.Instance != null)
                    InventoryScreen.Instance.Show();
                else
                    Debug.LogWarning($"{LOG} InventoryScreen.Instance == null");
            });

            // Debug HUD toggle (if available)
            if (DebugHUD.Instance != null)
            {
                HamburgerMenu.Instance.AddToggle("DEBUG HUD", false, (val) =>
                {
                    if (val) DebugHUD.Instance.Show();
                    else DebugHUD.Instance.Hide();
                });
            }

            Debug.Log($"{LOG} Menu buttons added to HamburgerMenu");
        }
    }
}
