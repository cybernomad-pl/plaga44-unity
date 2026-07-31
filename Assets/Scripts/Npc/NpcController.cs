// =============================================================================
// NpcController.cs
// CYBERNOMAD -- Sterownik animacji na instancji NPC Pinea.
// Odtwarza dowolny klip z NpcAnimationLibrary przez Playables API
// (AnimationPlayableUtilities.PlayClip) -- BEZ AnimatorControllera z 200 stanami.
// Kazdy NPC gra swoj klip niezaleznie. Loop domyslnie ON.
//
// ZERO FALLBACKOW: brak library / klip poza zakresem -> LogError + return.
// =============================================================================

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Animations;

namespace Plaga44.Npc
{
    /// <summary>Stan zycia NPC. Bramkuje chwyt tulowia: Alive -> tylko konczyny,
    /// Unconscious/Dead -> tulow tez chwytalny (patrz NpcGrabRegions).</summary>
    public enum NpcLifeState { Alive, Unconscious, Dead }

    [RequireComponent(typeof(Animator))]
    public class NpcController : MonoBehaviour
    {
        private const string LOG = "[PLAGA44][NpcController]";

        [Tooltip("Biblioteka klipow -- wstrzykiwana przez NpcSpawner.")]
        public NpcAnimationLibrary library;

        private Animator _animator;
        private PlayableGraph _graph;
        private Playable _clipPlayable;
        private AnimationClip _currentClip;
        private int _currentIndex = -1;
        private bool _currentLoops = true;

        private List<string> _clipNames;

        // =====================================================================
        // Public API (kontrakt)
        // =====================================================================

        public int CurrentIndex => _currentIndex;

        [Tooltip("Stan zycia -- bramkuje chwyt tulowia. Ustawiaj przez SetLifeState().")]
        [SerializeField] private NpcLifeState _lifeState = NpcLifeState.Alive;

        /// <summary>Aktualny stan zycia (tylko odczyt -- zmiana przez SetLifeState).</summary>
        public NpcLifeState LifeState => _lifeState;

        /// <summary>Czy tulow (kregoslup/miednica) jest chwytalny -- tylko gdy NIE zywy.</summary>
        public bool TorsoGrabbable => _lifeState != NpcLifeState.Alive;

        /// <summary>Emitowane przy KAZDEJ realnej zmianie stanu zycia.</summary>
        public event System.Action<NpcLifeState> LifeStateChanged;

        /// <summary>Ustawia stan zycia. Idempotentne (ten sam stan -> no-op, brak eventu).</summary>
        public void SetLifeState(NpcLifeState state)
        {
            if (state == _lifeState) return;
            _lifeState = state;
            Debug.Log($"{LOG} LifeState -> {state}");
            LifeStateChanged?.Invoke(state);
        }

        private bool _posable;

        /// <summary>Czy NPC jest w trybie lalki (animacja zdjeta, poza recznie manipulowana).</summary>
        public bool IsPosable => _posable;

        /// <summary>Wchodzi w tryb posable: zdejmuje animacje, kosci zostaja na biezacej
        /// (baked) klatce -- dalej repozycjonowane grabem konczyn. Idempotent (drugi grab
        /// NIE resetuje pozy). Wolane z NpcLimbPoseTransformer przy pierwszym chwycie.</summary>
        public void EnterPosableMode()
        {
            if (_posable) return;
            _posable = true;
            DestroyGraph(); // zdejmij naped -> transformy kosci zostaja na ostatniej klatce (baked)
            if (_animator != null) _animator.enabled = false;
            Debug.Log($"{LOG} EnterPosableMode -- animacja zdjeta, poza zamrozona (baked)");
        }

        public IReadOnlyList<string> ClipNames
        {
            get
            {
                if (_clipNames != null) return _clipNames;
                _clipNames = new List<string>();
                if (library != null)
                {
                    for (int i = 0; i < library.Count; i++)
                        _clipNames.Add(library.Name(i));
                }
                return _clipNames;
            }
        }

        /// <summary>Odtwarza klip pod indeksem (loop). Buduje swiezy PlayableGraph, stary niszczy.</summary>
        public void Play(int index)
        {
            if (_posable)
            {
                Debug.LogWarning($"{LOG} Play({index}) zignorowane -- NPC w trybie posable (animacja zdjeta)");
                return;
            }
            if (library == null)
            {
                Debug.LogError($"{LOG} Play({index}) -- brak library");
                return;
            }

            AnimationClip clip = library.Get(index); // waliduje zakres + LogError
            if (clip == null) return;

            DestroyGraph();

            AnimationPlayableUtilities.PlayClip(_animator, clip, out _graph);
            // Zrodlowy playable outputu = AnimationClipPlayable dla klipu.
            _clipPlayable = _graph.GetOutput(0).GetSourcePlayable();
            _currentClip = clip;
            _currentIndex = index;
            _currentLoops = library.Loops(index);
            _clipPlayable.SetSpeed(1); // reset po ewentualnym freeze poprzedniego (no-loop) klipu

            Debug.Log($"{LOG} Play [{index}] '{clip.name}' (loop={_currentLoops})");
        }

        /// <summary>Odtwarza klip po nazwie (z ClipNames). Brak dopasowania -> LogError.</summary>
        public void Play(string clipName)
        {
            IReadOnlyList<string> names = ClipNames;
            for (int i = 0; i < names.Count; i++)
            {
                if (names[i] == clipName)
                {
                    Play(i);
                    return;
                }
            }
            Debug.LogError($"{LOG} Play('{clipName}') -- nazwa nie znaleziona");
        }

        /// <summary>Nastepny klip z zawijaniem.</summary>
        public void Next()
        {
            int count = library != null ? library.Count : 0;
            if (count == 0)
            {
                Debug.LogError($"{LOG} Next() -- pusta library");
                return;
            }
            int next = _currentIndex < 0 ? 0 : (_currentIndex + 1) % count;
            Play(next);
        }

        /// <summary>Poprzedni klip z zawijaniem.</summary>
        public void Prev()
        {
            int count = library != null ? library.Count : 0;
            if (count == 0)
            {
                Debug.LogError($"{LOG} Prev() -- pusta library");
                return;
            }
            int prev = _currentIndex < 0 ? count - 1 : (_currentIndex - 1 + count) % count;
            Play(prev);
        }

        // =====================================================================
        // Lifecycle
        // =====================================================================

        private void Awake()
        {
            _animator = GetComponent<Animator>();
        }

        private void Start()
        {
            // Domyslny stan NPC: pierwszy klip (inaczej T-poza). Explicit init, nie fallback.
            if (_currentIndex < 0 && library != null && library.Count > 0)
                Play(0);
        }

        private void Update()
        {
            if (_posable) return; // tryb lalki: brak napedu, poza sterowana grabem
            if (!_graph.IsValid() || _currentClip == null) return;
            float len = _currentClip.length;
            if (len <= 0f) return;
            double t = _clipPlayable.GetTime();
            if (t < len) return;

            if (_currentLoops)
            {
                // IDLE/WALK: zawijanie czasu playable niezaleznie od importu klipu.
                _clipPlayable.SetTime(t % len);
            }
            else
            {
                // DYING: freeze na ostatniej klatce -- trup nie wstaje.
                _clipPlayable.SetTime(len);
                _clipPlayable.SetSpeed(0);
            }
        }

        private void OnDestroy()
        {
            DestroyGraph();
        }

        // =====================================================================
        // Internal
        // =====================================================================

        private void DestroyGraph()
        {
            if (_graph.IsValid())
                _graph.Destroy();
        }
    }
}
