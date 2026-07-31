// =============================================================================
// NpcLimbPoseTransformer.cs
// CYBERNOMAD -- Manipulacja pozy NPC przez chwyt konczyny (FK, BEZ IK).
//
// Region chwytu jest dzieckiem kosci-korzenia (LeftArm/UpLeg/Neck...). Grab obraca
// TE kosc w jej stawie (pivot = origin kosci = staw z jej rodzicem); dzieci
// (przedramie->dlon, lydka->stopa) ida za parentem NATURALNIE przez hierarchie
// transformow -- nie liczymy ich osobno. Kierunek staw->rece gracza (GrabPoints[0])
// wyznacza obrot (aim). Obrot ograniczony do _maxSwingAngle wzgledem pozy BAKED.
//
// Pierwszy grab DOWOLNEJ konczyny -> NpcController.EnterPosableMode() (zdejmuje
// animacje, zamraza kosci na biezacej klatce = baked). Idempotent.
//
// ZERO FALLBACK: brak kosci-rodzica / grabbable / punktu chwytu -> LogError/return.
// =============================================================================

using UnityEngine;
using Oculus.Interaction;

namespace Plaga44.Npc
{
    [DisallowMultipleComponent]
    public sealed class NpcLimbPoseTransformer : MonoBehaviour, ITransformer
    {
        private const string LOG = "[PLAGA44][NpcLimbPose]";

        [SerializeField] private NpcController _controller;
        [Tooltip("Maks. odchylenie kosci od pozy baked (stopnie) -- limit stawu.")]
        [SerializeField] private float _maxSwingAngle = 90f;

        private IGrabbable _grabbable;
        private Transform _bone;          // kosc-korzen regionu (= transform.parent)
        private bool _bakedCaptured;
        private Quaternion _bakedLocalRot;
        private Vector3 _startDir;
        private Quaternion _startBoneRot;

        public void Initialize(IGrabbable grabbable)
        {
            _grabbable = grabbable;
            _bone = transform.parent;
            if (_bone == null)
                Debug.LogError($"{LOG} region bez kosci-rodzica -- manipulacja nieaktywna");
        }

        public void BeginTransform()
        {
            if (_bone == null || _grabbable == null) return;
            if (_grabbable.GrabPoints.Count == 0) return;

            if (_controller != null) _controller.EnterPosableMode(); // idempotent
            else Debug.LogError($"{LOG} brak NpcController -- animacja nie zdjeta, poza bedzie nadpisywana");

            if (!_bakedCaptured)
            {
                _bakedLocalRot = _bone.localRotation; // referencja limitu = poza baked (raz)
                _bakedCaptured = true;
            }

            Vector3 hand = _grabbable.GrabPoints[0].position;
            _startDir = hand - _bone.position;
            _startBoneRot = _bone.rotation;
        }

        public void UpdateTransform()
        {
            if (_bone == null || _grabbable == null) return;
            if (_grabbable.GrabPoints.Count == 0) return;

            Vector3 curDir = _grabbable.GrabPoints[0].position - _bone.position;
            if (curDir.sqrMagnitude < 1e-6f || _startDir.sqrMagnitude < 1e-6f) return;

            Quaternion swing = Quaternion.FromToRotation(_startDir, curDir);
            _bone.rotation = swing * _startBoneRot;
            ClampToParent();
        }

        public void EndTransform() { } // poza zostaje w nowym stanie -- kolejny grab manipuluje dalej

        // Ogranicza swing kosci od pozy baked do _maxSwingAngle (limit anatomiczny stawu).
        private void ClampToParent()
        {
            Transform parent = _bone.parent;
            if (parent == null) return;

            Quaternion local = Quaternion.Inverse(parent.rotation) * _bone.rotation;
            Quaternion delta = local * Quaternion.Inverse(_bakedLocalRot);
            delta.ToAngleAxis(out float angle, out Vector3 axis);
            if (angle > 180f) { angle = 360f - angle; axis = -axis; }

            if (angle > _maxSwingAngle)
            {
                delta = Quaternion.AngleAxis(_maxSwingAngle, axis);
                local = delta * _bakedLocalRot;
                _bone.rotation = parent.rotation * local;
            }
        }
    }
}
