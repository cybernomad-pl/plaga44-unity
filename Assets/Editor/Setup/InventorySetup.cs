// =============================================================================
// InventorySetup.cs
// Dodaje HapticManager, PlayerInventory, InventoryLoadout oraz GripSpawnToHand
// (spawn-do-reki na ISDK). AKTYWNIE usuwa legacy grab (PlagaGrabber + GrabVolume
// + kinematyczny Rigidbody) z anchorow rigu -- ISDK jest jedynym systemem grab.
// Wywolywany przez Bootstrap (faza 7, po BuildRig/PlayerRig).
// =============================================================================
#if UNITY_EDITOR
using System;
using UnityEditor;
using UnityEngine;
using Plaga44.Feedback;
using Plaga44.Inventory;

namespace Plaga44.Editor.Setup
{
    public static class InventorySetup
    {
        private const string LOG = "[PLAGA44][InventorySetup]";
        private const string OvrRigName = "OVRCameraRig";
        private const string GrabVolumeName = "GrabVolume";

        public static bool Run(BootstrapConfig cfg)
        {
            // Prefaby broni (Shotgun/M249) do Resources/Items. IZOLACJA: blad budowania
            // broni NIE moze przerwac setupu ponizej -- logujemy i lecimy dalej.
            try { WeaponPrefabBuilder.EnsureAllWeapons(); }
            catch (Exception e) { Debug.LogError($"{LOG} [WEAPONS] Build broni nie powiodl sie: {e.Message} -- setup gracza kontynuuje"); }

            var rig = GameObject.Find(OvrRigName);
            if (rig == null)
            {
                Debug.LogWarning($"{LOG} [MISSING] {OvrRigName} not found -- skipping");
                return false;
            }

            bool changed = false;
            changed |= AddIfMissing<HapticManager>(rig, "HapticManager");
            changed |= AddIfMissing<PlayerInventory>(rig, "PlayerInventory");
            changed |= AddIfMissing<InventoryLoadout>(rig, "InventoryLoadout");

            // ISDK = jedyny system grab. Zdejmij legacy grabber z rigu (patrz nizej).
            changed |= RemoveLegacyGrabbers(rig);

            // Spawn-do-reki (grip pusta reka -> wybrany item galerii do TEJ dloni, ISDK ForceSelect).
            changed |= AddIfMissing<GripSpawnToHand>(rig, "GripSpawnToHand");

            return changed;
        }

        private static bool AddIfMissing<T>(GameObject go, string label) where T : Component
        {
            if (go.GetComponent<T>() != null)
            {
                Debug.Log($"{LOG} [OK] {label}");
                return false;
            }
            Undo.AddComponent<T>(go);
            Debug.Log($"{LOG} [ADDED] {label}");
            return true;
        }

        // =====================================================================
        // LEGACY GRAB CLEANUP -- aktywne usuniecie PlagaGrabber (OVRGrabber) i jego
        // fizycznej obudowy (GrabVolume trigger + kinematyczny Rigidbody) z anchorow
        // rigu. Konieczne, bo PlagaGrabber jest ZAPIECZONY w PlayerRig.prefab i wraca
        // do sceny przy kazdym BuildRig -- ta faza (7) leci po BuildRig (5), wiec go zdejmuje.
        // Idempotentne: drugi przebieg nie znajduje grabberow, dosprzata ewentualne
        // osierocone GrabVolume.
        //
        // ZERO ZGADYWANIA fizyki: kinematyczny RB usuwam TYLKO gdy pasuje do stanu
        // nadawanego przez legacy EnsureKinematicRigidbody (isKinematic=true, useGravity=false).
        // Inny RB na anchorze -> NIE usuwam, LogWarning (zglaszam w raporcie).
        // =====================================================================
        private static bool RemoveLegacyGrabbers(GameObject rig)
        {
            bool changed = false;

            // OVRGrabber lapie tez PlagaGrabber (subclass). Anchory to GO na ktorych siedzi grabber.
            var grabbers = rig.GetComponentsInChildren<OVRGrabber>(true);
            foreach (var g in grabbers)
            {
                if (g == null) continue;
                var anchor = g.gameObject;
                string typeName = g.GetType().Name;

                // 1) GrabVolume (dziecko SphereCollider trigger).
                var vol = anchor.transform.Find(GrabVolumeName);
                if (vol != null)
                {
                    Debug.Log($"{LOG} [REMOVE] {GrabVolumeName} z '{anchor.name}'");
                    Undo.DestroyObjectImmediate(vol.gameObject);
                    changed = true;
                }

                // 2) sam grabber PRZED Rigidbody (PlagaGrabber ma [RequireComponent(Rigidbody)]).
                Debug.Log($"{LOG} [REMOVE] {typeName} z '{anchor.name}'");
                Undo.DestroyObjectImmediate(g);
                changed = true;

                // 3) kinematyczny RB nadany pod legacy grab -- tylko gdy pewny profil legacy.
                changed |= RemoveLegacyKinematicRigidbody(anchor);
            }

            // Idempotencja / dosprzatanie: osierocone GrabVolume na anchorach bez grabbera.
            changed |= SweepOrphanGrabVolumes(rig);

            if (!changed)
                Debug.Log($"{LOG} [OK] brak legacy grab (PlagaGrabber/GrabVolume) na rigu -- czysto");
            return changed;
        }

        private static bool RemoveLegacyKinematicRigidbody(GameObject anchor)
        {
            var rb = anchor.GetComponent<Rigidbody>();
            if (rb == null) return false;

            // Profil legacy (EnsureKinematicRigidbody): isKinematic=true, useGravity=false.
            if (rb.isKinematic && !rb.useGravity)
            {
                Debug.Log($"{LOG} [REMOVE] kinematyczny Rigidbody z '{anchor.name}' (legacy grab: isKinematic=1, useGravity=0)");
                Undo.DestroyObjectImmediate(rb);
                return true;
            }

            Debug.LogWarning($"{LOG} [KEEP] Rigidbody na '{anchor.name}' NIE pasuje do profilu legacy grab " +
                             $"(isKinematic={rb.isKinematic}, useGravity={rb.useGravity}) -- NIE usuwam, zglaszam. " +
                             $"Sprawdz recznie czy potrzebny ISDK.");
            return false;
        }

        // Usuwa dzieci nazwane GrabVolume z SphereCollider-trigger pod rigiem, gdy zostaly
        // bez grabbera (np. po czesciowym przebiegu). Celowane po nazwie + trigger, nie po zasiegu.
        private static bool SweepOrphanGrabVolumes(GameObject rig)
        {
            bool changed = false;
            foreach (var t in rig.GetComponentsInChildren<Transform>(true))
            {
                if (t == null || t.name != GrabVolumeName) continue;
                var sc = t.GetComponent<SphereCollider>();
                if (sc == null || !sc.isTrigger) continue;
                Debug.Log($"{LOG} [REMOVE] osierocone {GrabVolumeName} pod '{(t.parent != null ? t.parent.name : "?")}'");
                Undo.DestroyObjectImmediate(t.gameObject);
                changed = true;
            }
            return changed;
        }
    }
}
#endif
