using UnityEngine;
using System;
using System.Collections.Generic;

namespace Plaga44.Core
{
    /// <summary>
    /// Poseable bone -- grabbable w VR, loguje kazda zmiane pozycji/rotacji.
    /// Kazda kosc ma min 2 grab pointy (collidery) do chwytania.
    ///
    /// Workflow:
    /// 1. Grip (lewy/prawy) -> chwytasz kosc
    /// 2. Przesuwasz/obracasz reka
    /// 3. Puszczasz grip -> kosc zostaje w nowej pozycji
    /// 4. Kazda zmiana AUTOMATYCZNIE logowana do AuditLog
    /// 5. Przycisk na Quecie -> manualny KEYFRAME calej pozy
    /// </summary>
    public class PoseableBone : MonoBehaviour
    {
        [Header("Bone Info")]
        public string boneName;
        public int boneIndex;

        [Header("State")]
        public bool isGrabbed;

        // Pozycja/rotacja sprzed grabniecia -- do delta logu
        private Vector3 _preGrabLocalPos;
        private Quaternion _preGrabLocalRot;

        // --- AUDYT LOG (automatyczny, po kazdym grab/release) ---
        public static List<AuditEntry> AuditLog = new List<AuditEntry>();
        public static event Action<AuditEntry> OnAuditEntry;

        [Serializable]
        public struct AuditEntry
        {
            public float timestamp;
            public string npcName;
            public string boneName;
            public int boneIndex;
            public Vector3 localPosition;
            public Quaternion localRotation;
            public Vector3 deltaPosition;
            public Vector3 deltaRotationEuler;
        }

        /// <summary>
        /// Wywolaj gdy kosc zostanie chwycona
        /// </summary>
        public void OnGrabBegin()
        {
            isGrabbed = true;
            _preGrabLocalPos = transform.localPosition;
            _preGrabLocalRot = transform.localRotation;
        }

        /// <summary>
        /// Wywolaj gdy kosc zostanie puszczona -- AUTOMATYCZNY AUDYT
        /// </summary>
        public void OnGrabEnd()
        {
            isGrabbed = false;

            // Rigidbody z powrotem kinematic
            var rb = GetComponent<Rigidbody>();
            if (rb != null)
            {
                rb.isKinematic = true;
                rb.linearVelocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
            }

            // Audyt -- automatyczny log zmiany
            var entry = new AuditEntry
            {
                timestamp = Time.time,
                npcName = GetNPCName(),
                boneName = boneName,
                boneIndex = boneIndex,
                localPosition = transform.localPosition,
                localRotation = transform.localRotation,
                deltaPosition = transform.localPosition - _preGrabLocalPos,
                deltaRotationEuler = (Quaternion.Inverse(_preGrabLocalRot) * transform.localRotation).eulerAngles,
            };

            AuditLog.Add(entry);
            OnAuditEntry?.Invoke(entry);

            Debug.Log($"[AUDIT] {entry.npcName}/{entry.boneName} " +
                      $"pos=({entry.localPosition.x:F3},{entry.localPosition.y:F3},{entry.localPosition.z:F3}) " +
                      $"rot=({entry.localRotation.eulerAngles.x:F1},{entry.localRotation.eulerAngles.y:F1},{entry.localRotation.eulerAngles.z:F1}) " +
                      $"delta=({entry.deltaPosition.x:F3},{entry.deltaPosition.y:F3},{entry.deltaPosition.z:F3}) " +
                      $"t={entry.timestamp:F2}");
        }

        private string GetNPCName()
        {
            Transform t = transform;
            while (t.parent != null)
                t = t.parent;
            return t.name;
        }
    }
}
