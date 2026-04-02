// AUTO-DISABLED: PLAGA44_FULL_SDK
#if PLAGA44_FULL_SDK
// NetworkPlayer.cs
// CYBERNOMAD -- PLAGA '44
// Represents one player in the network session.
// Local player: serializes head + hand transforms into byte[] and sends each FixedUpdate.
// Remote player: receives byte[], deserializes, feeds into NetworkInterpolator.

using System;
using UnityEngine;

namespace Plaga44.Networking
{
    /// <summary>
    /// Snapshot of a player's tracked body poses at a given timestamp.
    /// </summary>
    [Serializable]
    public struct PlayerPoseSnapshot
    {
        public double Timestamp;        // Time.timeAsDouble when captured

        public Vector3    HeadPos;
        public Quaternion HeadRot;

        public Vector3    LeftHandPos;
        public Quaternion LeftHandRot;

        public Vector3    RightHandPos;
        public Quaternion RightHandRot;

        // Packet ID -- wraps around at 255. Used to detect out-of-order packets.
        public byte PacketId;

        // ---- Serialization ----

        // Binary layout (bytes):
        //  1   PacketId
        //  8   Timestamp (double)
        //  12  HeadPos        (3 x float)
        //  16  HeadRot        (4 x float)
        //  12  LeftHandPos
        //  16  LeftHandRot
        //  12  RightHandPos
        //  16  RightHandRot
        // Total: 93 bytes

        public const int PacketSize = 1 + 8 + (12 + 16) * 3;  // = 93

        public byte[] Serialize()
        {
            byte[] buf = new byte[PacketSize];
            int offset = 0;

            buf[offset++] = PacketId;

            WriteDouble(buf, ref offset, Timestamp);

            WriteVector3(buf, ref offset, HeadPos);
            WriteQuaternion(buf, ref offset, HeadRot);

            WriteVector3(buf, ref offset, LeftHandPos);
            WriteQuaternion(buf, ref offset, LeftHandRot);

            WriteVector3(buf, ref offset, RightHandPos);
            WriteQuaternion(buf, ref offset, RightHandRot);

            return buf;
        }

        public static bool TryDeserialize(byte[] buf, out PlayerPoseSnapshot snap)
        {
            snap = default;
            if (buf == null || buf.Length < PacketSize) return false;

            int offset = 0;

            snap.PacketId = buf[offset++];
            snap.Timestamp = ReadDouble(buf, ref offset);

            snap.HeadPos      = ReadVector3(buf, ref offset);
            snap.HeadRot      = ReadQuaternion(buf, ref offset);

            snap.LeftHandPos  = ReadVector3(buf, ref offset);
            snap.LeftHandRot  = ReadQuaternion(buf, ref offset);

            snap.RightHandPos = ReadVector3(buf, ref offset);
            snap.RightHandRot = ReadQuaternion(buf, ref offset);

            return true;
        }

        // ---- Low-level helpers ----
        private static void WriteFloat(byte[] buf, ref int offset, float v)
        {
            byte[] bytes = BitConverter.GetBytes(v);
            Buffer.BlockCopy(bytes, 0, buf, offset, 4);
            offset += 4;
        }

        private static float ReadFloat(byte[] buf, ref int offset)
        {
            float v = BitConverter.ToSingle(buf, offset);
            offset += 4;
            return v;
        }

        private static void WriteDouble(byte[] buf, ref int offset, double v)
        {
            byte[] bytes = BitConverter.GetBytes(v);
            Buffer.BlockCopy(bytes, 0, buf, offset, 8);
            offset += 8;
        }

        private static double ReadDouble(byte[] buf, ref int offset)
        {
            double v = BitConverter.ToDouble(buf, offset);
            offset += 8;
            return v;
        }

        private static void WriteVector3(byte[] buf, ref int offset, Vector3 v)
        {
            WriteFloat(buf, ref offset, v.x);
            WriteFloat(buf, ref offset, v.y);
            WriteFloat(buf, ref offset, v.z);
        }

        private static Vector3 ReadVector3(byte[] buf, ref int offset)
        {
            float x = ReadFloat(buf, ref offset);
            float y = ReadFloat(buf, ref offset);
            float z = ReadFloat(buf, ref offset);
            return new Vector3(x, y, z);
        }

        private static void WriteQuaternion(byte[] buf, ref int offset, Quaternion q)
        {
            WriteFloat(buf, ref offset, q.x);
            WriteFloat(buf, ref offset, q.y);
            WriteFloat(buf, ref offset, q.z);
            WriteFloat(buf, ref offset, q.w);
        }

        private static Quaternion ReadQuaternion(byte[] buf, ref int offset)
        {
            float x = ReadFloat(buf, ref offset);
            float y = ReadFloat(buf, ref offset);
            float z = ReadFloat(buf, ref offset);
            float w = ReadFloat(buf, ref offset);
            return new Quaternion(x, y, z, w);
        }
    }

    /// <summary>
    /// Represents a networked player. One instance per player in the session.
    ///
    /// Local player (IsLocal == true):
    ///   - Reads transforms from OVRCameraRig anchors each FixedUpdate
    ///   - Serializes into PlayerPoseSnapshot and sends via NetworkManager
    ///
    /// Remote player (IsLocal == false):
    ///   - Receives snapshots from NetworkManager
    ///   - Pushes them into NetworkInterpolator for smooth playback
    /// </summary>
    public sealed class NetworkPlayer : MonoBehaviour
    {
        // ---- Inspector ----
        [Header("Identity")]
        public int PlayerId = -1;

        [Tooltip("True = this is the local player (send mode). False = remote (receive mode).")]
        public bool IsLocal = false;

        [Header("Send rate")]
        [Tooltip("How many pose packets to send per second.")]
        [Range(10, 90)]
        public int SendRateHz = 30;

        [Header("Transform references (local player)")]
        [Tooltip("CenterEyeAnchor transform. Auto-detected from OVRCameraRig if null.")]
        public Transform HeadAnchor;

        [Tooltip("LeftHandAnchor transform. Auto-detected from OVRCameraRig if null.")]
        public Transform LeftHandAnchor;

        [Tooltip("RightHandAnchor transform. Auto-detected from OVRCameraRig if null.")]
        public Transform RightHandAnchor;

        [Header("Avatar bones (remote player -- apply received poses here)")]
        public Transform RemoteHeadBone;
        public Transform RemoteLeftHandBone;
        public Transform RemoteRightHandBone;

        // ---- Events ----
        /// <summary>Fired when a new snapshot is received (remote player only).</summary>
        public event Action<PlayerPoseSnapshot> OnSnapshotReceived;

        // ---- Private ----
        private NetworkInterpolator _interpolator;
        private float _sendTimer;
        private float _sendInterval;
        private byte  _packetId;

        // ---- Lifecycle ----
        private void Awake()
        {
            _interpolator = GetComponent<NetworkInterpolator>();
            if (_interpolator == null && !IsLocal)
                _interpolator = gameObject.AddComponent<NetworkInterpolator>();
        }

        private void Start()
        {
            _sendInterval = 1f / Mathf.Max(1, SendRateHz);

            if (IsLocal)
                AutoDetectAnchors();

            if (!IsLocal && NetworkManager.Instance != null)
                NetworkManager.Instance.OnDataReceived += HandleRawData;
        }

        private void OnDestroy()
        {
            if (!IsLocal && NetworkManager.Instance != null)
                NetworkManager.Instance.OnDataReceived -= HandleRawData;
        }

        private void FixedUpdate()
        {
            if (!IsLocal) return;
            if (NetworkManager.Instance == null || !NetworkManager.Instance.IsConnected) return;

            _sendTimer += Time.fixedDeltaTime;
            if (_sendTimer < _sendInterval) return;
            _sendTimer -= _sendInterval;

            SendSnapshot();
        }

        // ---- Local: capture + send ----
        private void SendSnapshot()
        {
            var snap = CaptureLocalPose();
            byte[] data = snap.Serialize();
            NetworkManager.Instance.SendUnreliable(data);
        }

        private PlayerPoseSnapshot CaptureLocalPose()
        {
            unchecked { _packetId++; }

            return new PlayerPoseSnapshot
            {
                PacketId       = _packetId,
                Timestamp      = Time.timeAsDouble,

                HeadPos        = HeadAnchor      != null ? HeadAnchor.position      : Vector3.zero,
                HeadRot        = HeadAnchor      != null ? HeadAnchor.rotation      : Quaternion.identity,

                LeftHandPos    = LeftHandAnchor  != null ? LeftHandAnchor.position  : Vector3.zero,
                LeftHandRot    = LeftHandAnchor  != null ? LeftHandAnchor.rotation  : Quaternion.identity,

                RightHandPos   = RightHandAnchor != null ? RightHandAnchor.position : Vector3.zero,
                RightHandRot   = RightHandAnchor != null ? RightHandAnchor.rotation : Quaternion.identity,
            };
        }

        // ---- Remote: receive + push to interpolator ----
        private void HandleRawData(int senderId, byte[] data)
        {
            // Only process packets that belong to this player's sender id
            if (senderId != PlayerId) return;
            if (data == null || data.Length < PlayerPoseSnapshot.PacketSize) return;

            if (!PlayerPoseSnapshot.TryDeserialize(data, out var snap)) return;

            OnSnapshotReceived?.Invoke(snap);

            if (_interpolator != null)
                _interpolator.PushSnapshot(snap);
        }

        // ---- Helpers ----
        private void AutoDetectAnchors()
        {
#if HAS_META_XR
            var rig = FindFirstObjectByType<OVRCameraRig>();
            if (rig != null)
            {
                if (HeadAnchor      == null) HeadAnchor      = rig.centerEyeAnchor;
                if (LeftHandAnchor  == null) LeftHandAnchor  = rig.leftHandAnchor;
                if (RightHandAnchor == null) RightHandAnchor = rig.rightHandAnchor;
                return;
            }
#endif
            // Fallback: use main camera as head
            if (HeadAnchor == null && Camera.main != null)
                HeadAnchor = Camera.main.transform;
        }

        // ---- Public API ----
        /// <summary>
        /// Called by external systems (e.g. LobbyUI) to apply a received snapshot
        /// directly to the avatar bones without using the interpolator.
        /// </summary>
        public void ApplySnapshot(PlayerPoseSnapshot snap)
        {
            if (RemoteHeadBone      != null) { RemoteHeadBone.position      = snap.HeadPos;      RemoteHeadBone.rotation      = snap.HeadRot; }
            if (RemoteLeftHandBone  != null) { RemoteLeftHandBone.position  = snap.LeftHandPos;  RemoteLeftHandBone.rotation  = snap.LeftHandRot; }
            if (RemoteRightHandBone != null) { RemoteRightHandBone.position = snap.RightHandPos; RemoteRightHandBone.rotation = snap.RightHandRot; }
        }
    }
}
#endif // PLAGA44_FULL_SDK
