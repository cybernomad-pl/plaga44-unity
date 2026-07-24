// =============================================================================
// PlagaGrabber.cs
// CYBERNOMAD -- OVRGrabber subclass: TOGGLE grab (press grip to grab, press
// again to release). Replaces default hold-to-grab behaviour.
//
// How it works:
//   - grabEnd is set to -1 in Awake, making the base class release condition
//     impossible (flex can never reach -1). This disables hold-to-release.
//   - GrabBegin() override implements toggle: if already holding, release;
//     if not holding, grab nearest candidate.
//
// BACK-HOLSTER DRAW (issue: M249 z plecow):
//   - Gdy grip wcisniety, reka PUSTA, brak kandydata w zasiegu, a reka jest
//     ZA PLECAMI na wysokosci headsetu -> spawn M249 wprost do tej dloni.
//   - Lewy grabber -> lewa reka, prawy -> prawa (ten grabber JEST ta reka).
//   - Spawn + auto-grab przez normalny pipeline OVRGrabber (offsety, teleport,
//     kinematic). Grab deferowany o 1 klatke, zeby OVRGrabbable.Start()
//     zdazyl zlapac poprawny baseline kinematic (inaczej release zostawia
//     bron zamrozona w powietrzu).
// =============================================================================

using System.Collections;
using UnityEngine;

namespace Plaga44.Inventory
{
    [RequireComponent(typeof(Rigidbody))]
    public class PlagaGrabber : OVRGrabber
    {
        private const string LOG = "[PLAGA44][PlagaGrabber]";
        private const string OvrRigName = "OVRCameraRig";

        [Header("Magnetic grab")]
        [Tooltip("Max zasieg z ktorego item przylatuje do reki na grip (m). 0 = wylaczone.")]
        public float magneticRange = 8f;

        [Tooltip("Czas lotu itemu do reki (s). Mniej = szybciej 'przyskakuje'.")]
        public float magneticFlyTime = 0.18f;

        [Header("Back-holster draw")]
        [Tooltip("Resources path itemu dobywanego zza plecow (grip za glowa na wysokosci headsetu).")]
        public string backHolsterResource = "Items/M249";

        [Tooltip("Jak daleko ZA plaszczyzna glowy musi byc reka (m). Wieksze = trzeba siegnac glebiej.")]
        public float behindThreshold = 0.1f;

        [Tooltip("Max |reka.y - glowa.y| zeby liczyc jako 'wysokosc headsetu' (m).")]
        public float heightTolerance = 0.4f;

        [Tooltip("Max pozioma odleglosc reka-glowa zeby liczyc jako 'siegniecie' (m).")]
        public float reachRadius = 0.7f;

        private Transform _head;

        protected override void Awake()
        {
            base.Awake();
            // Disable hold-to-release: set grabEnd to impossible value.
            // Base CheckForGrabOrRelease needs m_prevFlex <= grabEnd which
            // can never happen when grabEnd is negative (flex range is 0..1).
            grabEnd = -1f;
            Debug.Log($"{LOG} Toggle-grab mode active (grabEnd=-1)");
        }

        protected override void GrabBegin()
        {
            // Already holding -- toggle release.
            if (m_grabbedObj != null)
            {
                Debug.Log($"{LOG} Toggle RELEASE: {m_grabbedObj.name} from {m_controller}");
                GrabEnd();
                return;
            }

            // Empty hand, no grabbable in reach, hand behind back at head height -> draw M249.
            if (m_grabCandidates.Count == 0 && IsReachingBehindBack())
            {
                DrawFromBackHolster();
                return;
            }

            // MAGNETYCZNY GRAB (priorytet): nic w zasiegu collidera -> najblizszy item
            // w scenie FRUNIE do tej reki. Bez kucania/siegania.
            if (m_grabCandidates.Count == 0 && TryMagneticGrab())
                return;

            // Not holding -- grab nearest candidate.
            Debug.Log($"{LOG} Toggle GRAB via {m_controller}");
            base.GrabBegin();
        }

        // =====================================================================
        // TRIGGER SPAWN -- index trigger spawnuje item WYBRANY w galerii
        // (HamburgerMenu -> ItemBrowser) wprost do TEJ dloni.
        // Lewy grabber -> lewa reka, prawy -> prawa (ten grabber JEST ta reka).
        // Ponowny trigger gdy cos trzymam -> toggle release (spojne z gripem).
        // =====================================================================
        public override void Update()
        {
            base.Update();
            if (OVRInput.GetDown(OVRInput.Button.PrimaryIndexTrigger, m_controller))
                OnIndexTriggerPressed();
        }

        private void OnIndexTriggerPressed()
        {
            // MENU OTWARTE -> trigger nalezy do UI (HamburgerMenu: zmiana wartosci +/-).
            // Bez tego jeden pull robilby jednoczesnie zmiane ustawienia I spawn itemu.
            if (Plaga44.UI.HamburgerMenu.MenuOpen) return;

            // Toggle: cos trzymam -> puszczam.
            if (m_grabbedObj != null)
            {
                Debug.Log($"{LOG} Trigger RELEASE: {m_grabbedObj.name} from {m_controller}");
                GrabEnd();
                return;
            }

            var browser = Plaga44.ItemBrowser.Instance;
            if (browser == null)
            {
                Debug.LogError($"{LOG} Trigger spawn: brak ItemBrowser w scenie -- nie wiem co spawnowac.");
                return;
            }

            var prefab = browser.SelectedPrefab;
            if (prefab == null)
            {
                Debug.LogWarning($"{LOG} Trigger spawn: zaden item nie wybrany w galerii (None). "
                    + "Wybierz item w menu (sekcja ITEMS).");
                return;
            }

            Debug.Log($"{LOG} Trigger SPAWN '{prefab.name}' -> {m_controller}");
            StartCoroutine(SpawnAndGrab(prefab, browser.SelectedResourcePath));
        }

        // =====================================================================
        // MAGNETYCZNY GRAB -- najblizszy PlagaGrabbable w scenie leci do reki.
        // =====================================================================
        private bool TryMagneticGrab()
        {
            if (magneticRange <= 0f) return false;

            Vector3 hand = m_gripTransform != null ? m_gripTransform.position : transform.position;
            PlagaGrabbable nearest = null;
            float best = magneticRange;

            foreach (var g in FindObjectsByType<PlagaGrabbable>(FindObjectsSortMode.None))
            {
                if (g == null || g.isGrabbed) continue; // nie kradnij z drugiej reki
                float d = Vector3.Distance(hand, g.transform.position);
                if (d < best) { best = d; nearest = g; }
            }

            if (nearest == null) return false;
            Debug.Log($"{LOG} MAGNETIC grab '{nearest.name}' (d={best:F1}m) -> {m_controller}");
            StartCoroutine(MagneticPull(nearest));
            return true;
        }

        private IEnumerator MagneticPull(PlagaGrabbable target)
        {
            var rb = target.GetComponent<Rigidbody>();
            bool prevKinematic = rb != null && rb.isKinematic;
            if (rb != null) rb.isKinematic = true; // leci sterowany, bez fizyki po drodze

            float t = 0f;
            Vector3 start = target.transform.position;
            float dur = Mathf.Max(0.01f, magneticFlyTime);

            while (t < dur)
            {
                if (m_grabbedObj != null || target == null)
                {
                    if (rb != null) rb.isKinematic = prevKinematic;
                    yield break; // chwycilem cos innego / item zniknal
                }
                Vector3 hand = m_gripTransform != null ? m_gripTransform.position : transform.position;
                t += Time.deltaTime;
                target.transform.position = Vector3.Lerp(start, hand, t / dur);
                yield return null;
            }

            if (m_grabbedObj != null || target == null) yield break;

            // Item w rece -> normalny pipeline grab (offsety, kinematic baseline, parent).
            m_grabCandidates[target] = 1;
            base.GrabBegin();
        }

        // Hand is behind the head plane, near headset height, within arm's reach.
        private bool IsReachingBehindBack()
        {
            var head = ResolveHead();
            if (head == null) return false;

            Vector3 handPos = m_gripTransform != null ? m_gripTransform.position : transform.position;
            Vector3 toHand = handPos - head.position;

            // Behind the head plane (dot with forward is negative beyond threshold).
            if (Vector3.Dot(head.forward, toHand) > -behindThreshold) return false;

            // Near headset height.
            if (Mathf.Abs(toHand.y) > heightTolerance) return false;

            // Within reach horizontally.
            Vector3 flat = toHand; flat.y = 0f;
            if (flat.magnitude > reachRadius) return false;

            return true;
        }

        private void DrawFromBackHolster()
        {
            var prefab = Resources.Load<GameObject>(backHolsterResource);
            if (prefab == null)
            {
                Debug.LogError($"{LOG} Back-holster draw FAILED: Resources/{backHolsterResource} nie znaleziony. "
                    + "Zbuduj prefab (CYBERNOMAD > Inventory > Rebuild M249 Prefab).");
                return;
            }
            StartCoroutine(SpawnAndGrab(prefab, backHolsterResource));
        }

        /// <summary>Spawnuje prefab w dloni i chwyta go normalnym pipeline'em OVRGrabber.
        /// resourcePath -- sciezka Resources do world-save tagowania (NIE zgadywana z nazwy).</summary>
        private IEnumerator SpawnAndGrab(GameObject prefab, string resourcePath)
        {
            Vector3 pos = m_gripTransform != null ? m_gripTransform.position : transform.position;
            Quaternion rot = m_gripTransform != null ? m_gripTransform.rotation : transform.rotation;

            var go = Instantiate(prefab, pos, rot);
            go.name = prefab.name;

            // One frame so OVRGrabbable.Awake (grabPoints) + Start (kinematic baseline) run
            // BEFORE we grab -- otherwise release would leave the item kinematic (frozen).
            yield return null;

            if (m_grabbedObj != null) yield break; // grabbed something else meanwhile

            var grab = go.GetComponent<PlagaGrabbable>();
            if (grab == null)
            {
                Debug.LogError($"{LOG} Back-holster item '{go.name}' bez PlagaGrabbable -- nie moge przypiac.");
                Destroy(go);
                yield break;
            }

            // Inject as sole candidate and run the normal grab pipeline
            // (computes offsets, teleports to hand, sets kinematic).
            m_grabCandidates[grab] = 1;
            Debug.Log($"{LOG} DRAW '{go.name}' into {m_controller}");
            base.GrabBegin();

            // World-save (#196): explicit resourcePath + spawn itemu = trigger zapisu.
            Plaga44.SaveableObject.Tag(go, resourcePath);
            Plaga44.WorldSaveManager.Instance?.Save("item-spawn");
        }

        // CenterEyeAnchor via rig (same convention as LocomotionController/ItemBrowser).
        private Transform ResolveHead()
        {
            if (_head != null) return _head;
            var rig = GameObject.Find(OvrRigName);
            if (rig != null)
            {
                var eye = rig.transform.Find("TrackingSpace/CenterEyeAnchor");
                if (eye != null) { _head = eye; return _head; }
            }
            if (Camera.main != null) _head = Camera.main.transform;
            return _head;
        }
    }
}
