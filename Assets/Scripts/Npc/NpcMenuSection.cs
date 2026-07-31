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

using System.Collections.Generic;
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

        // Galeria NPC pokazuje WYLACZNIE czyste IDLE (samo stanie w miejscu).
        // ALLOWLIST EXPLICIT (nie blacklist, nie substring) -- dokladne nazwy klipow
        // zweryfikowane na NpcAnimationLibrary.asset. Klip spoza tej listy jest ODRZUCONY
        // i zalogowany. ZERO FALLBACK: nie zgadujemy po fragmencie nazwy.
        // ODRZUCONE swiadomie (mimo "idle" w nazwie): falling idle, rifle aiming idle,
        // breakdance footwork to idle (tranzycja), sword and shield (block/crouch/*) idle
        // (warianty bojowe/akcyjne). To NIE jest czyste stanie.
        // UWAGA: filtr jest LOKALNY dla galerii -- library NIE jest okrajana, bo dzieli ja
        // AkslopeWanderAI (potrzebuje walk/run do locomotion). Zawezenie library zlamaloby AI.
        // Porownanie po nazwie w lowercase (klucze ponizej sa lowercase).
        private static readonly HashSet<string> IdleAllowlist = new HashSet<string>
        {
            "idle",             // ActionAdventure/idle + FemaleLocomotion/idle (obie nazwane "idle")
            "idle (2)",
            "idle (3)",
            "idle (4)",
            "idle (5)",
            "standing idle 01", // LongbowLocomotion -- czyste stanie
        };

        // Ostatnio zespawniony NPC = cel akcji animacji. Unity fake-null obsluguje despawn.
        private static NpcController _active;

        // Wybrany (jeszcze nie zespawniony) NPC z rejestru -- indeks w NpcRegistry.
        private static int _selectedNpc;

        // Cache mapy idle: idle-index (pozycja na sliderze galerii) -> realny indeks w library.
        // Klucz = referencja library; menu odpytuje co klatke, wiec (re)budowa + log tylko przy zmianie.
        private static NpcAnimationLibrary _idleCacheLib;
        private static List<int> _idleIndices;

        /// <summary>Aktywny NPC lub null (gdy nie zespawniono / zniszczony).</summary>
        private static NpcController ResolveActive()
        {
            return _active != null ? _active : null;
        }

        /// <summary>Library aktywnego NPC, albo z Resources gdy nic nie zespawniono. Null gdy brak.</summary>
        private static NpcAnimationLibrary ActiveLibrary()
        {
            var npc = ResolveActive();
            if (npc != null && npc.library != null) return npc.library;
            return Resources.Load<NpcAnimationLibrary>(LibraryResourcePath);
        }

        /// <summary>Indeksy czystych klipow IDLE w library -- galeria NPC pokazuje WYLACZNIE te (idle-space).
        /// Dopuszczenie = dokladne dopasowanie nazwy do IdleAllowlist (nie substring). ZERO FALLBACK:
        /// klip spoza allowlisty NIE trafia na liste i jest logowany jako odrzucony; nie zgadujemy kategorii.
        /// Cache per-library -> (re)budowa i logi tylko przy zmianie library.</summary>
        private static List<int> IdleIndices()
        {
            var lib = ActiveLibrary();
            if (lib == null) { _idleCacheLib = null; _idleIndices = null; return null; }

            if (!ReferenceEquals(lib, _idleCacheLib) || _idleIndices == null)
            {
                _idleCacheLib = lib;
                _idleIndices = new List<int>();
                int rejected = 0;
                for (int i = 0; i < lib.Count; i++)
                {
                    string n = lib.Name(i);
                    if (n != null && IdleAllowlist.Contains(n.ToLowerInvariant()))
                    {
                        _idleIndices.Add(i);
                    }
                    else
                    {
                        rejected++;
                        Debug.Log($"{LOG} [idle-filter] ODRZUCONO '{n}' -- nie na allowliscie czystego idle, ukryte w galerii NPC");
                    }
                }
                Debug.Log($"{LOG} [idle-filter] galeria NPC: {_idleIndices.Count} idle / {lib.Count} klipow ({rejected} odrzuconych)");
            }
            return _idleIndices;
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

        /// <summary>Ustawia animacje aktywnego NPC na wybrany klip IDLE. Menu podaje absolutny
        /// indeks w PRZESTRZENI IDLE (0..AnimCount-1); mapujemy go na realny indeks w library
        /// i gramy bezposrednio (Play), zamiast Next/Prev po pelnej library. Dzieki temu galeria
        /// nigdy nie wejdzie na klip non-idle.</summary>
        public static void ScrollAnim(int requestedIdleIndex)
        {
            var npc = ResolveActive();
            if (npc == null) { Notify("NPC: brak aktywnego NPC (najpierw Spawn Pinea)", false); return; }

            var idle = IdleIndices();
            if (idle == null || idle.Count == 0) { Notify("NPC: brak klipow idle w library", false); return; }

            int clamped = Mathf.Clamp(requestedIdleIndex, 0, idle.Count - 1);
            npc.Play(idle[clamped]);
        }

        // =====================================================================
        // Query (dla SettingDef.get + HamburgerMenu etykieta)
        // =====================================================================

        /// <summary>Biezacy indeks animacji w PRZESTRZENI IDLE (pozycja slidera galerii).
        /// Gdy aktualny klip NPC nie jest idle (np. AI gra locomotion / domyslny klip po spawnie)
        /// -> 0 (pierwszy idle). To clamp pozycji slidera do przestrzeni idle, nie zgadywanie zachowania:
        /// realnie grany klip pokazuje CurrentAnimLabel (prawda), tu chodzi tylko o pozycje na liscie.</summary>
        public static int CurrentAnimIndex
        {
            get
            {
                var npc = ResolveActive();
                if (npc == null) return 0;
                var idle = IdleIndices();
                if (idle == null) return 0;
                int pos = idle.IndexOf(npc.CurrentIndex);
                return pos < 0 ? 0 : pos;
            }
        }

        /// <summary>Liczba animacji IDLE dostepnych w galerii (idle-space). Galeria nie pokazuje
        /// non-idle, wiec to liczba przefiltrowana, NIE library.Count.</summary>
        public static int AnimCount
        {
            get
            {
                var idle = IdleIndices();
                return idle != null ? idle.Count : 0;
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
