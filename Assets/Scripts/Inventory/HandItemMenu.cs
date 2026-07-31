// =============================================================================
// HandItemMenu.cs
// CYBERNOMAD -- pomost miedzy sekcjami "LEFT HAND" / "RIGHT HAND" w
// HamburgerMenu/SettingsRegistry a runtime spawnem do dloni (GripSpawnToHand).
//
// MODEL (kontrola calkowita, per-reka): gracz wchodzi w LEFT HAND, stickiem
// wybiera grabbable z katalogu (ItemBrowser), ENTEREM na "Grab" spawnuje go
// PROSTO DO LEWEJ DLONI. RIGHT HAND analogicznie do prawej. ZERO preview-na-stole.
//
// Wzor UI: NpcMenuSection (absolutny indeks sterowany stickiem + akcja Spawn).
// Katalog grabbable: ItemBrowser (jedno zrodlo prawdy, whitelist=Shotgun).
//
// ZERO FALLBACKOW: brak ItemBrowser / brak grabbable pod indeksem / brak
// GripSpawnToHand na rigu -> nota + return. Nie zgadujemy, nie spawnujemy
// "zastepczego" itemu.
// =============================================================================

using UnityEngine;
using Oculus.Interaction.Input; // Handedness

namespace Plaga44.Inventory
{
    /// <summary>
    /// Statyczny stan sekcji LEFT HAND / RIGHT HAND menu. SettingsRegistry rejestruje
    /// SettingDef-y wolajace te metody; HamburgerMenu czyta LeftLabel/RightLabel do
    /// etykiety wiersza. Wybor per-reka jest niezalezny (osobne indeksy).
    /// </summary>
    public static class HandItemMenu
    {
        private const string LOG = "[PLAGA44][HandItemMenu]";

        // Wybor grabbable per reka (indeks 0-based w katalogu ItemBrowser).
        private static int _selectedLeft;
        private static int _selectedRight;

        // =====================================================================
        // Katalog (delegowany do ItemBrowser -- jedno zrodlo prawdy)
        // =====================================================================

        /// <summary>Liczba grabbable w katalogu, albo 0 gdy brak ItemBrowser.</summary>
        public static int GrabbableCount =>
            Plaga44.ItemBrowser.Instance != null ? Plaga44.ItemBrowser.Instance.GrabbableCount : 0;

        // =====================================================================
        // Selekcja per reka (get/set z SettingDef, clamp do zakresu katalogu)
        // =====================================================================

        public static int SelectedLeft
        {
            get { int max = GrabbableCount - 1; return max < 0 ? 0 : Mathf.Clamp(_selectedLeft, 0, max); }
            set { int max = GrabbableCount - 1; _selectedLeft = max < 0 ? 0 : Mathf.Clamp(value, 0, max); }
        }

        public static int SelectedRight
        {
            get { int max = GrabbableCount - 1; return max < 0 ? 0 : Mathf.Clamp(_selectedRight, 0, max); }
            set { int max = GrabbableCount - 1; _selectedRight = max < 0 ? 0 : Mathf.Clamp(value, 0, max); }
        }

        /// <summary>Nazwa wybranego grabbable danej reki -- etykieta wiersza menu.</summary>
        public static string LeftLabel => LabelFor(SelectedLeft);
        public static string RightLabel => LabelFor(SelectedRight);

        private static string LabelFor(int zeroBased)
        {
            var ib = Plaga44.ItemBrowser.Instance;
            if (ib == null || GrabbableCount == 0) return "(brak grabbable)";
            return ib.GrabbableName(zeroBased) ?? "(brak)";
        }

        // =====================================================================
        // Spawn do dloni (akcja "Grab" w menu)
        // =====================================================================

        /// <summary>Grabbable prefab wybrany dla wskazanej reki, albo NULL. Zrodlo dla
        /// grip-pusta-reka (GripSpawnToHand) -- spawn odpowiada wyborowi w menu tej reki.</summary>
        public static GameObject PrefabFor(Handedness hand)
        {
            var ib = Plaga44.ItemBrowser.Instance;
            if (ib == null) return null;
            int sel = hand == Handedness.Right ? SelectedRight : SelectedLeft;
            return ib.GrabbablePrefab(sel);
        }

        /// <summary>Spawn wybranego grabbable do LEWEJ dloni (akcja menu LEFT HAND).</summary>
        public static void SpawnLeft() => SpawnTo(Handedness.Left);

        /// <summary>Spawn wybranego grabbable do PRAWEJ dloni (akcja menu RIGHT HAND).</summary>
        public static void SpawnRight() => SpawnTo(Handedness.Right);

        private static void SpawnTo(Handedness hand)
        {
            var prefab = PrefabFor(hand);
            if (prefab == null)
            {
                Notify($"{hand} HAND: brak grabbable do spawnu (katalog pusty?)", false);
                return;
            }
            var grip = Object.FindFirstObjectByType<GripSpawnToHand>();
            if (grip == null)
            {
                Notify($"{hand} HAND: brak GripSpawnToHand na rigu -- nie moge spawnowac", false);
                return;
            }
            grip.SpawnToHand(prefab, hand);
            Notify($"{hand} HAND: '{prefab.name}' -> dlon", true);
        }

        // =====================================================================
        // Notyfikacje (banner w canvasie menu)
        // =====================================================================

        private static void Notify(string msg, bool success)
        {
            Debug.Log($"{LOG} {msg}");
            var notifier = Plaga44.UI.MenuNotifier.Instance;
            if (notifier != null) notifier.Show(msg, success);
        }
    }
}
