#if UNITY_EDITOR
// =============================================================================
// ItemGrabSetup.cs
// Konwertuje itemy PLAGA44 (Assets/Resources/Items) ze starego OVR/PlagaGrabbable
// na Meta Interaction SDK (ISDK) HandGrab, wg wzorca Mug (jednoreczny) i
// StonePolyhedron (dual GrabFreeTransformer w obu slotach Grabbable).
//
// Root itemu dostaje: Item + Grabbable(+GrabFreeTransformer w obu slotach).
// Grab pointy z Item.grabPoints -> dzieci GrabPoint_<label> z HandGrabInteractable
// + HandGrabPose. Brak grab pointow -> jeden HandGrabInteractable na root bez pozy
// (free grab -- jawny tryb ISDK, NIE fallback). PlagaGrabbable usuwany, HapticOnGrab
// zostaje. Idempotent (regeneruje wygenerowane komponenty). Edytuje ASSET prefabu.
//
// DISTANCE GRAB (wzorzec Meta DistanceGrabExamples, ISDK v83, kamien Stone-InteractableToHand):
// OBOK kazdego HandGrabInteractable (near), na TYM SAMYM GameObject, dokladamy
// DistanceHandGrabInteractable (przyciaganie z dystansu) + MoveTowardsTargetProvider
// (item leci do dloni). DHGI wspoldzieli te same referencje co near: _pointableElement
// -> Grabbable roota, _rigidbody -> Rigidbody roota, _handGrabPoses -> te same HandGrabPose.
// Rig (OVRComprehensiveInteractors) ma juz DistanceHandGrabInteractor per reka -- brakowalo
// tylko interactable po stronie itemu (rejestr distance-grab byl pusty).
//
// ZERO FALLBACKOW: brak Rigidbody -> LogError + skip. Brak collidera w hierarchii
// Rigidbody -> LogError + skip (assert HandGrabInteractable.Start w runtime).
// =============================================================================
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using Oculus.Interaction;                 // Grabbable, GrabFreeTransformer, ITransformer, IPointableElement
using Oculus.Interaction.HandGrab;        // HandGrabInteractable, HandGrabPose
using Oculus.Interaction.Grab;            // GrabTypeFlags
using Oculus.Interaction.GrabAPI;         // GrabbingRule
using Plaga44.Items;                      // Item, ItemGrabPoint, GrabHand

namespace Plaga44.Editor.Setup
{
    public static class ItemGrabSetup
    {
        private const string LOG = "[PLAGA44][ItemGrabSetup]";
        private const string ItemsFolder = "Assets/Resources/Items";
        private const string GrabPointPrefix = "GrabPoint_";

        // Whitelist itemow do konwersji. TYLKO Shotgun -- zaden inny item (Borys 2026-07-29).
        private static readonly string[] Whitelist = { "Shotgun" };

        // Itemy dwureczne (dual-grab). Katalog EXPLICIT -- nie zgadujemy z nazwy/ksztaltu.
        private static readonly HashSet<string> KnownDual = new HashSet<string> { "Shotgun" };

        [MenuItem("PLAGA44/Setup/Item Grab (ISDK)")]
        private static void MenuRun() => Run(null);

        // cfg nieuzywane -- sciezka itemow jest stala. Podpis zgodny z pozostalymi Setup.
        public static bool Run(BootstrapConfig cfg)
        {
            if (!AssetDatabase.IsValidFolder(ItemsFolder))
            {
                Debug.LogError($"{LOG} brak folderu {ItemsFolder}");
                return false;
            }

            int ok = 0;
            foreach (var name in Whitelist)
            {
                var path = $"{ItemsFolder}/{name}.prefab";
                if (AssetDatabase.LoadAssetAtPath<GameObject>(path) == null)
                {
                    Debug.LogError($"{LOG} [{name}] brak prefabu {path} -- pomijam");
                    continue;
                }
                if (ConfigurePrefab(path, name)) ok++;
            }

            AssetDatabase.SaveAssets();
            Debug.Log($"{LOG} [DONE] skonfigurowano {ok}/{Whitelist.Length} itemow");
            return ok > 0;
        }

        private static bool ConfigurePrefab(string path, string name)
        {
            var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                // (a) Item na root.
                var item = root.GetComponent<Item>();
                if (item == null)
                {
                    item = root.AddComponent<Item>();
                    Debug.Log($"{LOG} [{name}] [ADDED] Item");
                }
                if (string.IsNullOrEmpty(item.itemName)) item.itemName = name;

                // (f) Usun legacy PlagaGrabbable (konflikt dwoch systemow grab). HapticOnGrab zostaje.
                RemoveLegacyGrab(root, name);

                // ZERO FALLBACK: Rigidbody musi juz byc (nie dodajemy, nie zgadujemy fizyki).
                var rb = root.GetComponent<Rigidbody>();
                if (rb == null)
                {
                    Debug.LogError($"{LOG} [{name}] brak Rigidbody na root -- SKIP (nie dodaje, nie zgaduje)");
                    return false;
                }
                // ZERO FALLBACK: HandGrabInteractable.Start robi assert na >=1 collider w hierarchii Rigidbody.
                if (rb.GetComponentsInChildren<Collider>(true).Length == 0)
                {
                    Debug.LogError($"{LOG} [{name}] brak collidera w hierarchii Rigidbody -- HandGrabInteractable " +
                                   $"assert fail w runtime. SKIP.");
                    return false;
                }

                // (b) Grabbable na root + GrabFreeTransformer w obu slotach.
                var grabbable = EnsureComponent<Grabbable>(root, name, "Grabbable");
                grabbable.InjectOptionalRigidbody(rb);
                grabbable.InjectOptionalKinematicWhileSelected(true);

                var transformer = EnsureComponent<GrabFreeTransformer>(root, name, "GrabFreeTransformer");
                grabbable.InjectOptionalOneGrabTransformer(transformer);
                grabbable.InjectOptionalTwoGrabTransformer(transformer);

                // (e) Dual-grab.
                bool isDual = KnownDual.Contains(name) || item.dualWield;
                item.dualWield = isDual;
                grabbable.MaxGrabPoints = isDual ? 2 : -1; // -1 = bez limitu (ISDK default)
                EditorUtility.SetDirty(grabbable);
                EditorUtility.SetDirty(item);

                // Idempotencja: skasuj wczesniej wygenerowane GrabPoint_* dzieci oraz interactable/pose
                // dodane przez nas na ROOT (sample'owe interactable siedza na innych dzieciach -- nie ruszamy).
                CleanupGenerated(root);

                // (c)/(d) Grab pointy albo free grab.
                var points = item.grabPoints;
                if (points == null || points.Length == 0)
                {
                    // (d) Free grab -- jeden interactable na root, BEZ HandGrabPose. Jawny tryb ISDK.
                    var hgi = root.AddComponent<HandGrabInteractable>();
                    ConfigureInteractable(hgi, rb, grabbable, null);
                    // Distance grab na tym samym root GO (bez poz -- position-based distance grab).
                    AddDistanceInteractable(root, rb, grabbable, null, name, "root");
                    Debug.LogWarning($"{LOG} [{name}] brak zdefiniowanych grab pointow (Item.grabPoints puste) " +
                                     $"-- free grab (chwyt gdziekolwiek na colliderze). Dostroj punkty w Item.");
                }
                else
                {
                    // (c) Po jednym HandGrabInteractable + HandGrabPose na kazdy grab point.
                    int idx = 0;
                    foreach (var gp in points)
                    {
                        if (gp == null) { Debug.LogError($"{LOG} [{name}] grabPoints[{idx}] == null -- SKIP tego punktu"); idx++; continue; }
                        CreateGrabPoint(root, rb, grabbable, gp, name, idx);
                        idx++;
                    }
                }

                PrefabUtility.SaveAsPrefabAsset(root, path);
                Debug.Log($"{LOG} [{name}] [OK] dual={isDual}, grabPoints={(points == null ? 0 : points.Length)}");
                return true;
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static void CreateGrabPoint(GameObject root, Rigidbody rb, Grabbable grabbable,
                                            ItemGrabPoint gp, string name, int idx)
        {
            string label = string.IsNullOrEmpty(gp.label) ? idx.ToString() : gp.label;
            var go = new GameObject(GrabPointPrefix + label);
            go.transform.SetParent(root.transform, false);
            go.transform.localPosition = gp.localPosition;
            go.transform.localRotation = gp.LocalRotation;

            var pose = go.AddComponent<HandGrabPose>();
            pose.InjectRelativeTo(root.transform);
            SetBool(pose, "_usesHandPose", false); // sam punkt chwytu, bez pozy palcow (start)
            EditorUtility.SetDirty(pose);

            var hgi = go.AddComponent<HandGrabInteractable>();
            ConfigureInteractable(hgi, rb, grabbable, new List<HandGrabPose> { pose });

            // Distance grab na tym samym GO co near -- wspoldzieli te sama poze (wzorzec Meta).
            AddDistanceInteractable(go, rb, grabbable, new List<HandGrabPose> { pose }, name, GrabPointPrefix + label);

            // GrabHand (LeftOnly/RightOnly) NIE jest tu egzekwowane -- ISDK per-hand filtr nie jest
            // w potwierdzonym API. Zostawiamy Any; ograniczenie dloni = osobny etap (nie zgadujemy).
            if (gp.hand != GrabHand.Any)
                Debug.LogWarning($"{LOG} [{name}] GrabPoint_{label} hand={gp.hand} NIE wyegzekwowane " +
                                 $"(brak potwierdzonego per-hand API) -- do dostrojenia recznie.");

            Debug.Log($"{LOG} [{name}] [ADDED] {GrabPointPrefix}{label} (HandGrabInteractable+HandGrabPose)");
        }

        private static void ConfigureInteractable(HandGrabInteractable hgi, Rigidbody rb,
                                                  Grabbable grabbable, List<HandGrabPose> poses)
        {
            hgi.InjectRigidbody(rb);
            hgi.InjectOptionalPointableElement(grabbable); // Grabbable : IPointableElement
            hgi.InjectSupportedGrabTypes(GrabTypeFlags.All); // Pinch | Palm
            hgi.InjectPinchGrabRules(GrabbingRule.DefaultPinchRule);
            hgi.InjectPalmGrabRules(GrabbingRule.DefaultPalmRule);
            if (poses != null && poses.Count > 0) hgi.InjectOptionalHandGrabPoses(poses);
            EditorUtility.SetDirty(hgi);
        }

        // Dokłada DistanceHandGrabInteractable + MoveTowardsTargetProvider na tym samym GO co
        // near HandGrabInteractable (wzorzec Meta DistanceGrabExamples: near+distance+provider na
        // jednym obiekcie). Distance grab wspoldzieli Grabbable/Rigidbody/poses z near -- rozni sie
        // tylko movement providerem. MoveTowardsTargetProvider = item leci do dloni (najnaturalniejszy
        // dla broni/itemow; to tez domyslny provider jaki DHGI tworzy sam w Start(), tu jawnie katalogowany).
        // _handAligment zostawiamy domyslne (AlignOnGrab=1) -- zgodne ze scena sampla.
        // supportedGrabTypes/rules = te same co near (spojnosc chwytu near i distance), NIE zgadywane.
        private static void AddDistanceInteractable(GameObject go, Rigidbody rb, Grabbable grabbable,
                                                    List<HandGrabPose> poses, string name, string label)
        {
            // Movement provider na tym samym GO co DHGI (jak w scenie: GO 10865400 ma oba).
            var provider = go.AddComponent<MoveTowardsTargetProvider>();
            EditorUtility.SetDirty(provider);

            var dhgi = go.AddComponent<DistanceHandGrabInteractable>();
            dhgi.InjectRigidbody(rb);                              // _rigidbody -> Rigidbody roota
            dhgi.InjectOptionalPointableElement(grabbable);        // _pointableElement -> Grabbable roota
            dhgi.InjectSupportedGrabTypes(GrabTypeFlags.All);      // Pinch | Palm (jak near)
            dhgi.InjectPinchGrabRules(GrabbingRule.DefaultPinchRule);
            dhgi.InjectPalmGrabRules(GrabbingRule.DefaultPalmRule);
            if (poses != null && poses.Count > 0) dhgi.InjectOptionalHandGrabPoses(poses); // te same pozy co near
            dhgi.InjectOptionalMovementProvider(provider);         // _movementProvider -> MoveTowardsTargetProvider
            EditorUtility.SetDirty(dhgi);

            // ReticleData* (Ghost/Mesh/Icon) POMINIETE -- to tylko wizualne podswietlenie celowania
            // na dystans; distance grab dziala bez nich (interactor selekcjonuje po InteractableRegistry +
            // ConicalFrustum, nie po reticle). Zglaszam jawnie, nie po cichu.
            Debug.Log($"{LOG} [{name}] [ADDED] DistanceHandGrabInteractable+MoveTowardsTargetProvider @ {label} " +
                      $"(ReticleData pominieto -- wizualny bajer, niewymagany do dzialania)");
        }

        // Kasuje wygenerowane wczesniej dzieci GrabPoint_* oraz interactable/pose dodane na ROOT.
        private static void CleanupGenerated(GameObject root)
        {
            // Dzieci GrabPoint_*.
            var toKill = new List<GameObject>();
            for (int i = 0; i < root.transform.childCount; i++)
            {
                var c = root.transform.GetChild(i);
                if (c.name.StartsWith(GrabPointPrefix)) toKill.Add(c.gameObject);
            }
            foreach (var go in toKill) Object.DestroyImmediate(go, true);

            // Interactable/pose na samym root (sample'owe siedza na dzieciach -- nie ruszamy).
            // Kolejnosc: najpierw DHGI (referuje providera), potem provider, potem near HGI/pose.
            foreach (var c in root.GetComponents<DistanceHandGrabInteractable>()) Object.DestroyImmediate(c, true);
            foreach (var c in root.GetComponents<MoveTowardsTargetProvider>()) Object.DestroyImmediate(c, true);
            foreach (var c in root.GetComponents<HandGrabInteractable>()) Object.DestroyImmediate(c, true);
            foreach (var c in root.GetComponents<HandGrabPose>()) Object.DestroyImmediate(c, true);
        }

        private static void RemoveLegacyGrab(GameObject root, string name)
        {
            var legacy = root.GetComponent<Plaga44.Inventory.PlagaGrabbable>();
            if (legacy == null) return;
            Object.DestroyImmediate(legacy, true);
            Debug.Log($"{LOG} [{name}] [REMOVED] PlagaGrabbable (legacy OVRGrabbable, konflikt z ISDK)");
        }

        private static T EnsureComponent<T>(GameObject root, string name, string label) where T : Component
        {
            var c = root.GetComponent<T>();
            if (c != null) return c;
            c = root.AddComponent<T>();
            Debug.Log($"{LOG} [{name}] [ADDED] {label}");
            return c;
        }

        // Ustawia serializowane pole bool (brak metody Inject dla _usesHandPose).
        private static void SetBool(Component target, string field, bool value)
        {
            var so = new SerializedObject(target);
            var prop = so.FindProperty(field);
            if (prop == null)
            {
                Debug.LogError($"{LOG} pole '{field}' nie istnieje na {target.GetType().Name} -- pominieto (bez zgadywania)");
                return;
            }
            prop.boolValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
#endif
