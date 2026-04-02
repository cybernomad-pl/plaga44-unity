// AUTO-DISABLED: PLAGA44_FULL_SDK
#if PLAGA44_FULL_SDK
// NetworkInterpolator.cs
// CYBERNOMAD -- PLAGA '44
// Smooths out remote player movement by maintaining a small snapshot buffer
// and rendering at (now - interpolationDelay). Lerp between two bracketing
// snapshots each frame so the avatar looks smooth even with jitter / packet loss.

using System.Collections.Generic;
using UnityEngine;

namespace Plaga44.Networking
{
    /// <summary>
    /// Attach to a remote player GameObject.
    /// Receives PlayerPoseSnapshots from NetworkPlayer and applies interpolated
    /// transforms to the avatar bones each Update.
    /// </summary>
    public sealed class NetworkInterpolator : MonoBehaviour
    {
        // ---- Inspector ----
        [Header("Interpolation")]
        [Tooltip("How many milliseconds behind real-time to render. Increase to hide more jitter.")]
        [Range(20f, 300f)]
        public float InterpolationDelayMs = 50f;

        [Tooltip("Maximum snapshots to keep in buffer. Old snapshots beyond this are dropped.")]
        [Range(8, 64)]
        public int BufferSize = 32;

        [Header("Avatar bones")]
        [Tooltip("Receives interpolated head transform.")]
        public Transform HeadBone;

        [Tooltip("Receives interpolated left hand transform.")]
        public Transform LeftHandBone;

        [Tooltip("Receives interpolated right hand transform.")]
        public Transform RightHandBone;

        [Header("Debug")]
        public bool ShowDebugGizmos = false;

        // ---- Private ----
        // Buffer is kept sorted ascending by Timestamp.
        private readonly List<PlayerPoseSnapshot> _buffer = new List<PlayerPoseSnapshot>(64);

        private double _delaySeconds;

        // ---- Lifecycle ----
        private void OnEnable()
        {
            _buffer.Clear();
        }

        private void Update()
        {
            _delaySeconds = InterpolationDelayMs / 1000.0;
            double renderTime = Time.timeAsDouble - _delaySeconds;
            ApplyInterpolated(renderTime);
        }

        // ---- Public API ----
        /// <summary>
        /// Push a newly received snapshot into the buffer.
        /// Called by NetworkPlayer when a packet arrives.
        /// </summary>
        public void PushSnapshot(PlayerPoseSnapshot snap)
        {
            // Insert in sorted order (ascending Timestamp).
            // In practice packets usually arrive in order, so insertion at the end is common.
            int insertAt = _buffer.Count;
            for (int i = _buffer.Count - 1; i >= 0; i--)
            {
                if (_buffer[i].Timestamp <= snap.Timestamp)
                {
                    insertAt = i + 1;
                    break;
                }
                insertAt = i;
            }
            _buffer.Insert(insertAt, snap);

            // Trim old snapshots that are way behind the render window.
            // Keep at least 2 so we can always interpolate.
            double renderTime = Time.timeAsDouble - _delaySeconds;
            while (_buffer.Count > 2 && _buffer[0].Timestamp < renderTime - 1.0)
                _buffer.RemoveAt(0);

            // Hard cap on buffer size
            while (_buffer.Count > BufferSize)
                _buffer.RemoveAt(0);
        }

        /// <summary>
        /// Clear the snapshot buffer (e.g. on teleport / respawn).
        /// </summary>
        public void ClearBuffer()
        {
            _buffer.Clear();
        }

        // ---- Interpolation logic ----
        private void ApplyInterpolated(double renderTime)
        {
            if (_buffer.Count == 0) return;

            // Find the two snapshots that bracket renderTime.
            // bufferA.Timestamp <= renderTime < bufferB.Timestamp
            int idxB = -1;
            for (int i = 0; i < _buffer.Count; i++)
            {
                if (_buffer[i].Timestamp >= renderTime)
                {
                    idxB = i;
                    break;
                }
            }

            PlayerPoseSnapshot snapA, snapB;

            if (idxB < 0)
            {
                // renderTime is ahead of all snapshots -- use the latest snapshot as-is.
                snapB = _buffer[_buffer.Count - 1];
                ApplyExact(ref snapB);
                return;
            }

            if (idxB == 0)
            {
                // renderTime is before any snapshot -- use the earliest.
                snapA = _buffer[0];
                ApplyExact(ref snapA);
                return;
            }

            snapA = _buffer[idxB - 1];
            snapB = _buffer[idxB];

            double span = snapB.Timestamp - snapA.Timestamp;
            float t = (span > 0.0001) ? (float)((renderTime - snapA.Timestamp) / span) : 1f;
            t = Mathf.Clamp01(t);

            ApplyLerp(ref snapA, ref snapB, t);
        }

        private void ApplyExact(ref PlayerPoseSnapshot snap)
        {
            if (HeadBone      != null) { HeadBone.position      = snap.HeadPos;      HeadBone.rotation      = snap.HeadRot; }
            if (LeftHandBone  != null) { LeftHandBone.position  = snap.LeftHandPos;  LeftHandBone.rotation  = snap.LeftHandRot; }
            if (RightHandBone != null) { RightHandBone.position = snap.RightHandPos; RightHandBone.rotation = snap.RightHandRot; }
        }

        private void ApplyLerp(ref PlayerPoseSnapshot a, ref PlayerPoseSnapshot b, float t)
        {
            if (HeadBone != null)
            {
                HeadBone.position = Vector3.Lerp(a.HeadPos, b.HeadPos, t);
                HeadBone.rotation = Quaternion.Slerp(a.HeadRot, b.HeadRot, t);
            }
            if (LeftHandBone != null)
            {
                LeftHandBone.position = Vector3.Lerp(a.LeftHandPos, b.LeftHandPos, t);
                LeftHandBone.rotation = Quaternion.Slerp(a.LeftHandRot, b.LeftHandRot, t);
            }
            if (RightHandBone != null)
            {
                RightHandBone.position = Vector3.Lerp(a.RightHandPos, b.RightHandPos, t);
                RightHandBone.rotation = Quaternion.Slerp(a.RightHandRot, b.RightHandRot, t);
            }
        }

        // ---- Gizmos ----
#if UNITY_EDITOR
        private void OnDrawGizmos()
        {
            if (!ShowDebugGizmos) return;

            // Draw interpolation buffer snapshots as small spheres for head position.
            double renderTime = Time.timeAsDouble - InterpolationDelayMs / 1000.0;

            Gizmos.color = Color.grey;
            foreach (var snap in _buffer)
                Gizmos.DrawWireSphere(snap.HeadPos, 0.04f);

            // Show render cursor
            if (_buffer.Count >= 2)
            {
                Gizmos.color = Color.cyan;
                // Approximate render head position
                int idxB = -1;
                for (int i = 0; i < _buffer.Count; i++)
                {
                    if (_buffer[i].Timestamp >= renderTime) { idxB = i; break; }
                }
                if (idxB > 0)
                {
                    var a = _buffer[idxB - 1];
                    var b = _buffer[idxB];
                    double span = b.Timestamp - a.Timestamp;
                    float t = (span > 0) ? (float)((renderTime - a.Timestamp) / span) : 1f;
                    Vector3 pos = Vector3.Lerp(a.HeadPos, b.HeadPos, Mathf.Clamp01(t));
                    Gizmos.DrawSphere(pos, 0.06f);
                }
            }
        }
#endif
    }
}
#endif // PLAGA44_FULL_SDK
