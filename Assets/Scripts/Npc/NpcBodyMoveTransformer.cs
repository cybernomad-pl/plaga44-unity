// =============================================================================
// NpcBodyMoveTransformer.cs
// CYBERNOMAD -- Chwyt TULOWIA przenosi CALEGO NPC (translacja root, BEZ obrotu).
//
// Region tulowia (aktywny tylko gdy NPC nieprzytomny/trup -- brama NpcGrabRegions)
// przy chwycie przesuwa caly root NPC za reka gracza (GrabPoints[0]), zachowujac
// offset z momentu chwytu. To "dowolne repozycjonowanie calego ciala". Bez obrotu
// w stawie, bez ragdolla.
//
// ZERO FALLBACK: brak root NPC / grabbable / punktu chwytu -> LogError/return.
// =============================================================================

using UnityEngine;
using Oculus.Interaction;

namespace Plaga44.Npc
{
    [DisallowMultipleComponent]
    public sealed class NpcBodyMoveTransformer : MonoBehaviour, ITransformer
    {
        private const string LOG = "[PLAGA44][NpcBodyMove]";

        [SerializeField] private Transform _npcRoot;

        private IGrabbable _grabbable;
        private Vector3 _offset;

        public void Initialize(IGrabbable grabbable)
        {
            _grabbable = grabbable;
            if (_npcRoot == null)
                Debug.LogError($"{LOG} brak _npcRoot -- przenoszenie ciala nieaktywne");
        }

        public void BeginTransform()
        {
            if (_npcRoot == null || _grabbable == null) return;
            if (_grabbable.GrabPoints.Count == 0) return;
            _offset = _npcRoot.position - _grabbable.GrabPoints[0].position;
        }

        public void UpdateTransform()
        {
            if (_npcRoot == null || _grabbable == null) return;
            if (_grabbable.GrabPoints.Count == 0) return;
            _npcRoot.position = _grabbable.GrabPoints[0].position + _offset; // translacja calego NPC
        }

        public void EndTransform() { } // cialo zostaje w nowej pozycji
    }
}
