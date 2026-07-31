// =============================================================================
// GripSpawnToHand.cs
// CYBERNOMAD -- spawn-do-reki na ISDK. Nacisniecie GRIPA pusta reka spawnuje
// AKTUALNIE WYBRANY item galerii i NATYCHMIAST wklada go do TEJ dloni w stanie
// GRAB (trzymany) przez oficjalne API Meta ISDK: HandGrabInteractor.ForceSelect.
//
// HANDEDNESS (sedno): grip PRAWY -> OVRInput.Controller.RTouch -> interactor z
// Hand.Handedness == Right -> PRAWA reka. Lewy analogicznie. Interactory
// rozwiazywane PO Hand.Handedness (nie po nazwie GameObjectu -- nazwy zmienne
// miedzy wersjami prefabu rigu).
//
// NIE KOLIDUJE z normalnym near-grabem: spawn tylko gdy interactor tej reki
// NIC nie trzyma i NIE MA kandydata (realny item w zasiegu -> zostawiamy ISDK).
//
// FIZYKA (nie spada gdy trzymasz / spada gdy pusto) -- ZERO kodu tutaj:
//   - prefab itemu ma Rigidbody isKinematic=false, useGravity=true,
//   - Grabbable._kinematicWhileSelected=true (ItemGrabSetup),
//   -> trzymany: RB kinematic (wisi); puszczenie gripa -> ForceRelease ->
//      HandGrabAPI Unselect -> RB wraca do isKinematic=false + grawitacja -> spada.
//
// RETENCJA CHWYTU (dlaczego allowManualRelease=false, nie true):
//   ForceSelect wymusza TRANSYCJE Hover->Select, ale gdy allowManualRelease=true
//   NIE instaluje overrideu ShouldUnselect -> dalsze utrzymanie chwytu zalezy od
//   natywnej detekcji palcow ISDK (HandGrabInteractor.ComputeShouldUnselect ->
//   HandGrabInteraction.ComputeShouldUnselect: IsSustainingGrab / IsHandUnselect*Changed).
//   Dla itemu ZESPAWNOWANEGO w dloni (reka nigdy nie "podeszla" do niego, grip byl
//   wcisniety ZANIM item powstal) ta detekcja NIE potwierdza aktywnego chwytu:
//   _currentGrabType startuje =None (brak edge'a "select"), a pierwszy select-update
//   wykrywa unselect -> _handGrabShouldUnselect=true -> NATYCHMIASTOWY unselect ->
//   item wypada i spada na ziemie. To jest przyczyna buga "item u stop".
//   allowManualRelease=false instaluje override ShouldUnselect ()=>false (item ==
//   SelectedInteractable) -> item NIE MOZE byc puszczony przez detekcje palcow,
//   trzyma sie DETERMINISTYCZNIE do jawnego ForceRelease() (grip pusc = ForceRelease).
//
// ZERO FALLBACKOW: brak wybranego itemu -> log + no-op. Brak HandGrabInteractable
// na spawnie -> LogError + Destroy (nie zgaduje czego chwycic). Brak interactora
// danej reki -> LogError + STOP.
// =============================================================================

using System.Collections;
using UnityEngine;
using Oculus.Interaction;               // InteractorState
using Oculus.Interaction.HandGrab;      // HandGrabInteractor, HandGrabInteractable
using Oculus.Interaction.Input;         // IHand, Handedness

namespace Plaga44.Inventory
{
    [DisallowMultipleComponent]
    public class GripSpawnToHand : MonoBehaviour
    {
        private const string LOG = "[PLAGA44][GripSpawnToHand]";

        [Tooltip("Prog wcisniecia gripa (0..1) liczony jako 'grip wcisniety'. 0.55 = jak legacy PlagaGrabbable.")]
        [Range(0.1f, 1f)] public float gripThreshold = 0.55f;

        private HandGrabInteractor _left;
        private HandGrabInteractor _right;
        private bool _leftGripPrev;
        private bool _rightGripPrev;
        // Reka trzyma item ZESPAWNOWANY przez nas (ForceSelect, allowManualRelease=false).
        // Tylko taki item puszczamy jawnym ForceRelease na zwolnieniu gripa -- normalnego
        // near-graba ISDK NIE ruszamy (nie force-selectowalismy go, wiec forced=false).
        private bool _leftForced;
        private bool _rightForced;
        private bool _resolved;

        private void Start() => ResolveInteractors();

        // Rozwiazanie interactorow PO Hand.Handedness. Hand moze byc null zanim
        // interactor sie zainicjalizuje -> proba ponawiana w Update dopoki brak obu.
        private void ResolveInteractors()
        {
            var all = GetComponentsInChildren<HandGrabInteractor>(true);
            _left = null;
            _right = null;
            foreach (var i in all)
            {
                if (i.Hand == null) continue;
                if (i.Hand.Handedness == Handedness.Right) _right = i;
                else if (i.Hand.Handedness == Handedness.Left) _left = i;
            }
            _resolved = _left != null && _right != null;
            if (_resolved)
                Debug.Log($"{LOG} interactory OK: LEWY='{_left.name}', PRAWY='{_right.name}'");
            else
                Debug.LogWarning($"{LOG} nie rozwiazano obu HandGrabInteractor (L={_left != null}, R={_right != null}) " +
                                 $"z {all.Length} znalezionych -- ponawiam. Sprawdz warstwe OVRInteractionComprehensive na rigu.");
        }

        private void Update()
        {
            if (!_resolved)
            {
                ResolveInteractors();
                if (!_resolved) return;
            }

            HandleHand(OVRInput.Controller.RTouch, _right, ref _rightGripPrev, ref _rightForced, Handedness.Right);
            HandleHand(OVRInput.Controller.LTouch, _left, ref _leftGripPrev, ref _leftForced, Handedness.Left);
        }

        // Edge-detekcja gripa danej reki. DOWN-edge -> ewentualny spawn-do-reki.
        // UP-edge -> jesli reka trzyma nasz force-item, ForceRelease (spada).
        private void HandleHand(OVRInput.Controller ctrl, HandGrabInteractor interactor,
                                ref bool prev, ref bool forced, Handedness hand)
        {
            float grip = OVRInput.Get(OVRInput.Axis1D.PrimaryHandTrigger, ctrl);
            bool down = grip >= gripThreshold;
            bool downEdge = down && !prev;
            bool upEdge = !down && prev;
            prev = down;

            // Zwolnienie gripa: puszczamy TYLKO item ktory sami wymusilismy (forced).
            // allowManualRelease=false -> detekcja palcow nie puszcza, wiec MY musimy.
            if (upEdge)
            {
                if (forced)
                {
                    interactor.ForceRelease();
                    forced = false;
                    Debug.Log($"{LOG} [RELEASE] reka {hand}: grip zwolniony -> ForceRelease (item spada)");
                }
                return;
            }

            if (!downEdge) return;

            // Reka cos trzyma -> zostaw ISDK (nie podmieniaj tego co w dloni).
            if (interactor.State == InteractorState.Select || interactor.HasSelectedInteractable)
                return;

            // Realny item w zasiegu near-grab -> to normalny chwyt, nie spawn.
            if (interactor.HasCandidate)
                return;

            // Zrodlo "wybranego itemu" = wybor TEJ reki w menu (LEFT/RIGHT HAND).
            // NULL = nic nie wybrano / katalog pusty (zero fallback).
            var prefab = HandItemMenu.PrefabFor(hand);
            if (prefab == null)
            {
                Debug.Log($"{LOG} grip {hand}: brak wybranego grabbable dla tej reki (HandItemMenu.PrefabFor=null) -- nie spawnuje");
                return;
            }
            SpawnToHand(prefab, hand);
        }

        /// <summary>Czysty punkt wejscia: spawn prefabu przy dloni wskazanej reki i
        /// natychmiastowy ISDK grab (ForceSelect, allowManualRelease=true -> grip pusc = spada).
        /// hand == Handedness.Right -> PRAWY interactor -> PRAWA reka.</summary>
        public void SpawnToHand(GameObject prefab, Handedness hand)
        {
            if (prefab == null)
            {
                Debug.LogError($"{LOG} SpawnToHand: prefab == null -- STOP");
                return;
            }
            var interactor = hand == Handedness.Right ? _right : _left;
            if (interactor == null)
            {
                Debug.LogError($"{LOG} SpawnToHand: brak HandGrabInteractor dla reki {hand} -- STOP (rig niekompletny)");
                return;
            }

            // WristPoint = poza chwytu w dloni; spawn tutaj to KOSMETYKA (MoveTowardsTargetProvider
            // itemu i tak dociagnie item do dloni). Gdy pole nierozwiazane -> pozycja interactora.
            Transform wrist = interactor.WristPoint;
            Vector3 pos = wrist != null ? wrist.position : interactor.transform.position;
            Quaternion rot = wrist != null ? wrist.rotation : interactor.transform.rotation;
            if (wrist == null)
                Debug.LogWarning($"{LOG} interactor {hand} nie ma WristPoint -- spawn na pozycji interactora (kosmetyka, grab i tak zadziala)");

            var go = Instantiate(prefab, pos, rot);
            go.name = prefab.name;
            Plaga44.Rendering.TestShaderApplier.Apply(go); // Custom/Test Shader (spojnie z galeria)

            // Optymistycznie oznacz reke jako trzymajaca nasz force-item JUZ TERAZ (grip
            // wcisniety). Gdyby grab zawiodl (brak HandGrabInteractable) -> cofniete w coroutine.
            SetForced(hand, true);
            StartCoroutine(GrabNextFrame(go, interactor, hand));
        }

        // 1 klatka opoznienia: Start() itemu (HandGrabInteractable/Grabbable, rejestr,
        // movement provider) musi sie wykonac ZANIM ForceSelect (inaczej za wczesnie).
        private IEnumerator GrabNextFrame(GameObject go, HandGrabInteractor interactor, Handedness hand)
        {
            yield return null;
            if (go == null) { SetForced(hand, false); yield break; }

            var hgi = go.GetComponentInChildren<HandGrabInteractable>(true);
            if (hgi == null)
            {
                Debug.LogError($"{LOG} spawniony '{go.name}' NIE ma HandGrabInteractable -- ForceSelect nie ma czego chwycic. " +
                               $"Niszcze. (item musi przejsc przez ItemGrabSetup)");
                SetForced(hand, false); // grab nie doszedl -> nie ma czego zwalniac
                Destroy(go);
                yield break;
            }

            // allowManualRelease=false: chwyt trzyma sie DETERMINISTYCZNIE (override ShouldUnselect
            // ()=>false), NIE zalezy od detekcji palcow ISDK -> item NIE spada zaraz po spawnie.
            // Zwolnienie = jawny ForceRelease na UP-edge gripa (HandleHand). Patrz naglowek pliku.
            interactor.ForceSelect(hgi, allowManualRelease: false);
            Debug.Log($"{LOG} [GRAB] '{go.name}' -> reka {hand} (ForceSelect, allowManualRelease=false; grip pusc = ForceRelease -> spada)");
        }

        // Ustawia flage 'reka trzyma nasz force-item' dla wskazanej reki.
        // EXPLICIT Right/Left -- inna wartosc = blad wywolania (LogError, nie zgaduj reki).
        private void SetForced(Handedness hand, bool value)
        {
            if (hand == Handedness.Right) _rightForced = value;
            else if (hand == Handedness.Left) _leftForced = value;
            else Debug.LogError($"{LOG} SetForced: nieoczekiwana reka '{hand}' -- flaga NIE ustawiona (spodziewane Left/Right)");
        }
    }
}
