// =============================================================================
// AvatarRegistry.cs
// CYBERNOMAD -- ScriptableObject rejestrujacy wszystkie avatary w projekcie.
// Generowany przez Assets/Editor/AvatarImport.cs (AvatarRegistryBuilder).
// NIE edytuj recznie -- zostanie nadpisany przy nastepnym imporcie.
// =============================================================================
using System.Collections.Generic;
using UnityEngine;

namespace Plaga44
{
    /// <summary>
    /// Auto-generowana lista wszystkich avatarow zaimportowanych do
    /// Assets/PLAGA44/Avatars/. Aktualizowana przez Editor/AvatarImport.cs po
    /// kazdym (re)imporcie. Asset zyje w Assets/PLAGA44/Resources/AvatarRegistry.asset
    /// zeby Resources.Load dzialal w runtime.
    ///
    /// NIE edytuj recznie -- zostanie nadpisane.
    /// </summary>
    [CreateAssetMenu(fileName = "AvatarRegistry", menuName = "Plaga44/Avatar Registry")]
    public class AvatarRegistry : ScriptableObject
    {
        public const string ResourcesPath = "AvatarRegistry";

        [System.Serializable]
        public class Entry
        {
            public string name;
            public GameObject prefab;

            /// <summary>True jesli rig invalid / brak Humanoid avatar / inny blad importu.
            /// PlayerAvatar pomija takie wpisy, menu pokazuje "AVATAR_ERROR".</summary>
            public bool broken;
            public string errorMessage;
        }

        public List<Entry> avatars = new List<Entry>();

        public int Count => avatars != null ? avatars.Count : 0;

        public Entry Get(int index) =>
            (avatars != null && index >= 0 && index < avatars.Count) ? avatars[index] : null;
    }
}
