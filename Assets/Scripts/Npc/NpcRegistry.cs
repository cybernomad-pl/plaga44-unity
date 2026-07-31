// =============================================================================
// NpcRegistry.cs
// Rejestr NPC dostepnych w NPC gallery. Budowany przez editor tool
// (DefaultMaleNpcSetup) skanem Resources/Npc/*_NPC.prefab -- dodanie NPC = wrzucenie
// prefaba do Resources/Npc/, zero recznej edycji listy.
// Asset zyje w Assets/Resources/Npc/NpcRegistry.asset -> Resources.Load w runtime.
// Wzor: Plaga44.AvatarRegistry.
// =============================================================================
using System.Collections.Generic;
using UnityEngine;

namespace Plaga44.Npc
{
    public class NpcRegistry : ScriptableObject
    {
        public const string ResourcesPath = "Npc/NpcRegistry";

        [System.Serializable]
        public class Entry
        {
            public string name;      // czytelna nazwa (np. "Pinea", "DefaultMale")
            public GameObject prefab; // prefab NPC z Resources/Npc/
        }

        public List<Entry> npcs = new List<Entry>();

        public int Count => npcs != null ? npcs.Count : 0;

        public Entry Get(int index) =>
            (npcs != null && index >= 0 && index < npcs.Count) ? npcs[index] : null;
    }
}
