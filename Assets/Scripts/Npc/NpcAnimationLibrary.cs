// =============================================================================
// NpcAnimationLibrary.cs
// CYBERNOMAD -- ScriptableObject: katalog humanoid AnimationClipow dla NPC Pinea.
// Budowany przez Editor/Setup/NpcSystemSetup.cs, ladowany przez NpcSpawner z
// Resources/Npc/NpcAnimationLibrary.asset.
//
// ZERO FALLBACKOW: dostep poza zakresem -> LogError + null/sentinel, NIE zgadujemy.
// =============================================================================

using UnityEngine;

namespace Plaga44.Npc
{
    [CreateAssetMenu(fileName = "NpcAnimationLibrary", menuName = "PLAGA44/NPC/Animation Library")]
    public class NpcAnimationLibrary : ScriptableObject
    {
        private const string LOG = "[PLAGA44][NpcAnimationLibrary]";

        [Tooltip("Zaimportowane humanoid klipy (mixamo).")]
        public AnimationClip[] clips;

        [Tooltip("Czytelne nazwy -- nazwa pliku bez rozszerzenia. Rownolegle do clips[].")]
        public string[] displayNames;

        [Tooltip("Czy klip petli. Rownolegle do clips[]. IDLE/WALK = true, DYING = false (freeze na ostatniej klatce).")]
        public bool[] loops;

        public int Count => clips != null ? clips.Length : 0;

        /// <summary>Zwraca klip pod indeksem lub null (LogError) gdy poza zakresem.</summary>
        public AnimationClip Get(int index)
        {
            if (clips == null || index < 0 || index >= clips.Length)
            {
                Debug.LogError($"{LOG} Get({index}) poza zakresem (Count={Count})");
                return null;
            }
            return clips[index];
        }

        /// <summary>Zwraca czytelna nazwe pod indeksem lub null (LogError) gdy poza zakresem.</summary>
        public string Name(int index)
        {
            if (displayNames == null || index < 0 || index >= displayNames.Length)
            {
                Debug.LogError($"{LOG} Name({index}) poza zakresem (displayNames={(displayNames != null ? displayNames.Length : 0)})");
                return null;
            }
            return displayNames[index];
        }

        /// <summary>Czy klip petli. Poza zakresem -> LogError + false (defensywny freeze,
        /// NIE zgadywanie petli -- brak danych = nie zapetlaj w nieskonczonosc).</summary>
        public bool Loops(int index)
        {
            if (loops == null || index < 0 || index >= loops.Length)
            {
                Debug.LogError($"{LOG} Loops({index}) poza zakresem (loops={(loops != null ? loops.Length : 0)})");
                return false;
            }
            return loops[index];
        }
    }
}
