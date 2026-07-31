// =============================================================================
// NpcGrabRegions.cs
// CYBERNOMAD -- Bramka chwytu tulowia NPC wg stanu zycia.
//
// Konczyny (barki/ramiona, uda/nogi, glowa+szyja) sa ZAWSZE chwytalne -- to
// osobne GameObjecty "GrabRegion_*" i ich tu NIE ruszamy.
// Tulow (Hips/Spine -> region "GrabRegion_Torso") jest chwytalny TYLKO gdy NPC
// jest nieprzytomny albo trup (NpcController.TorsoGrabbable). Wlaczamy/wylaczamy
// region tulowia calym GameObjectem (SetActive) -- gasi collider+Grabbable+
// HandGrabInteractable jednym ruchem.
//
// Referencje wpina NpcGrabSetup (editor). ZERO FALLBACKOW: brak controllera ->
// LogError + gate nieaktywny; null w tablicy regionow -> LogError + skip.
// =============================================================================

using UnityEngine;

namespace Plaga44.Npc
{
    [DisallowMultipleComponent]
    public sealed class NpcGrabRegions : MonoBehaviour
    {
        private const string LOG = "[PLAGA44][NpcGrabRegions]";

        [SerializeField] private NpcController _controller;

        [Tooltip("Regiony chwytu TULOWIA -- aktywne tylko gdy NPC nieprzytomny/trup.")]
        [SerializeField] private GameObject[] _torsoRegions;

        private void Awake()
        {
            if (_controller == null) _controller = GetComponent<NpcController>();
        }

        private void OnEnable()
        {
            if (_controller == null)
            {
                Debug.LogError($"{LOG} brak NpcController -- gate tulowia nieaktywny");
                return;
            }
            _controller.LifeStateChanged += OnLifeStateChanged;
            Apply(_controller.LifeState);
        }

        private void OnDisable()
        {
            if (_controller != null) _controller.LifeStateChanged -= OnLifeStateChanged;
        }

        private void OnLifeStateChanged(NpcLifeState state) => Apply(state);

        private void Apply(NpcLifeState state)
        {
            bool torsoGrabbable = state != NpcLifeState.Alive;
            if (_torsoRegions == null) return;
            for (int i = 0; i < _torsoRegions.Length; i++)
            {
                GameObject go = _torsoRegions[i];
                if (go == null)
                {
                    Debug.LogError($"{LOG} _torsoRegions[{i}] == null -- sprawdz NpcGrabSetup");
                    continue;
                }
                if (go.activeSelf != torsoGrabbable) go.SetActive(torsoGrabbable);
            }
        }
    }
}
