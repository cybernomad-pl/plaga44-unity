// =============================================================================
// NpcMenuSection.cs
// CYBERNOMAD -- pomost miedzy sekcja "NPC" w HamburgerMenu/SettingsRegistry a
// runtime NpcSpawner/NpcController. Trzyma "aktywnego" NPC (ostatnio zespawniony)
// i wystawia akcje menu: Spawn Pinea / przewijanie animacji / Despawn wszystkie.
//
// Wzor UI: ItemBrowser (CurrentLabel + absolutny indeks sterowany przez menu).
// Notyfikacje: Plaga44.UI.MenuNotifier (banner w canvasie menu).
//
// ZERO FALLBACKOW: brak NpcSpawner.Instance / brak aktywnego NPC -> nota przez
// MenuNotifier + return. Nie zgadujemy, nie tworzymy "zastepczego" NPC.
// =============================================================================

using UnityEngine;

namespace Plaga44.Npc
{
    /// <summary>
    /// Statyczny stan sekcji NPC menu. SettingsRegistry rejestruje SettingDef-y,
    /// ktore wolaja te metody; HamburgerMenu czyta CurrentAnimLabel do etykiety wiersza.
    /// </summary>
    public static class NpcMenuSection
    {
        private const string LOG = "[PLAGA44][NpcMenu]";
        private const string LibraryResourcePath = "Npc/NpcAnimationLibrary";

        // Ostatnio zespawniony NPC = cel akcji animacji. Unity fake-null obsluguje despawn.
        private static NpcController _active;

        // Wybrany (jeszcze nie zespawniony) NPC z rejestru -- indeks w NpcRegistry.
        private static int _selectedNpc;

        /// <summary>Aktywny NPC lub null (gdy nie zespawniono / zniszczony).</summary>
        private static NpcController ResolveActive()
        {
            return _active != null ? _active : null;
        }

        // =====================================================================
        // Akcje menu
        // =====================================================================

        /// <summary>Spawnuje Pinee i zapamietuje ja jako aktywnego NPC.</summary>
        public static void SpawnPinea()
        {
            var spawner = NpcSpawner.Instance;
            if (spawner == null) { Notify("NPC: brak NpcSpawner.Instance", false); return; }

            var npc = spawner.SpawnPinea();
            if (npc == null) { Notify("NPC: spawn nieudany (patrz konsola)", false); return; }

            _active = npc;
            Notify($"NPC: Pinea zespawniona ({AnimCount} animacji)", true);
        }

        /// <summary>Liczba NPC w rejestrze.</summary>
        public static int NpcCount => NpcSpawner.Instance != null ? NpcSpawner.Instance.NpcCount : 0;

        /// <summary>Wybrany indeks NPC z rejestru (sklampowany do zakresu).</summary>
        public static int SelectedNpc
        {
            get
            {
                int max = NpcCount - 1;
                return max < 0 ? 0 : Mathf.Clamp(_selectedNpc, 0, max);
            }
            set
            {
                int max = NpcCount - 1;
                _selectedNpc = max < 0 ? 0 : Mathf.Clamp(value, 0, max);
            }
        }

        /// <summary>Nazwa wybranego NPC -- etykieta wiersza w menu.</summary>
        public static string SelectedNpcLabel
        {
            get
            {
                var spawner = NpcSpawner.Instance;
                if (spawner == null || NpcCount == 0) return "(brak NPC w rejestrze)";
                return spawner.NpcName(SelectedNpc) ?? "(brak)";
            }
        }

        /// <summary>Spawnuje aktualnie wybranego NPC z rejestru; zapamietuje jako aktywnego.</summary>
        public static void SpawnSelected()
        {
            var spawner = NpcSpawner.Instance;
            if (spawner == null) { Notify("NPC: brak NpcSpawner.Instance", false); return; }
            if (NpcCount == 0) { Notify("NPC: rejestr pusty (odpal CYBERNOMAD/Setup/NPC Registry)", false); return; }

            var npc = spawner.SpawnNpc(SelectedNpc);
            if (npc == null) { Notify("NPC: spawn nieudany (patrz konsola)", false); return; }

            _active = npc;
            Notify($"NPC: {SelectedNpcLabel} zespawniony ({AnimCount} animacji)", true);
        }

        /// <summary>Niszczy wszystkie NPC i czysci aktywnego.</summary>
        public static void DespawnAll()
        {
            var spawner = NpcSpawner.Instance;
            if (spawner == null) { Notify("NPC: brak NpcSpawner.Instance", false); return; }

            spawner.DespawnAll();
            _active = null;
            Notify("NPC: despawn wszystkich", true);
        }

        /// <summary>Przewija animacje aktywnego NPC. Menu podaje absolutny (sklampowany)
        /// indeks docelowy; kierunek wzgledem biezacego -> Next()/Prev() na kontrolerze.</summary>
        public static void ScrollAnim(int requestedIndex)
        {
            var npc = ResolveActive();
            if (npc == null) { Notify("NPC: brak aktywnego NPC (najpierw Spawn Pinea)", false); return; }

            int cur = npc.CurrentIndex;
            if (requestedIndex > cur) npc.Next();
            else if (requestedIndex < cur) npc.Prev();
            // requestedIndex == cur -> boundary/clamp, brak zmiany
        }

        // =====================================================================
        // Query (dla SettingDef.get + HamburgerMenu etykieta)
        // =====================================================================

        /// <summary>Biezacy indeks animacji aktywnego NPC (0 gdy brak/nieustawiony).</summary>
        public static int CurrentAnimIndex
        {
            get
            {
                var npc = ResolveActive();
                if (npc == null) return 0;
                int i = npc.CurrentIndex;
                return i < 0 ? 0 : i;
            }
        }

        /// <summary>Liczba dostepnych animacji (z aktywnego NPC lub z library w Resources).</summary>
        public static int AnimCount
        {
            get
            {
                var npc = ResolveActive();
                if (npc != null && npc.library != null) return npc.library.Count;
                var lib = Resources.Load<NpcAnimationLibrary>(LibraryResourcePath);
                return lib != null ? lib.Count : 0;
            }
        }

        /// <summary>Etykieta animacji do wyswietlenia w wierszu menu.</summary>
        public static string CurrentAnimLabel
        {
            get
            {
                var npc = ResolveActive();
                if (npc == null) return "(brak NPC -- Spawn Pinea)";

                var names = npc.ClipNames;
                int i = npc.CurrentIndex;
                if (names == null || i < 0 || i >= names.Count) return "(brak klipu)";
                return names[i];
            }
        }

        // =====================================================================
        // Notyfikacje
        // =====================================================================

        private static void Notify(string msg, bool success)
        {
            Debug.Log($"{LOG} {msg}");
            var notifier = Plaga44.UI.MenuNotifier.Instance;
            if (notifier != null) notifier.Show(msg, success);
        }
    }
}
