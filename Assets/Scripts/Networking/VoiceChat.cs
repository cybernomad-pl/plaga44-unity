// AUTO-DISABLED: requires newer Meta XR SDK APIs
#if PLAGA44_FULL_SDK
// VoiceChat.cs
// CYBERNOMAD -- PLAGA '44
// Voice chat placeholder. Stubbed API ready for Photon Voice / Vivox integration.
// Optionally hooks into OVRLipSync (Meta XR) if the package is present.
// Compile-time guard: HAS_META_XR  -- OVR types available
//                     HAS_PHOTON_VOICE -- Photon Voice available (not yet)

using UnityEngine;

namespace Plaga44.Networking
{
    /// <summary>
    /// Voice mode: push-to-talk requires holding a button; always-on streams continuously.
    /// </summary>
    public enum VoiceChatMode
    {
        AlwaysOn,
        PushToTalk
    }

    /// <summary>
    /// Voice chat placeholder MonoBehaviour.
    /// Attach to the local player GameObject.
    ///
    /// Current state: API stubs only. No audio data is actually transmitted.
    /// Integration points are marked with TODO comments.
    ///
    /// OVRLipSync integration:
    ///   - If HAS_META_XR is defined and OVRLipSyncContext is found on the avatar,
    ///     captured mic audio is fed into it each frame to drive lip animation.
    /// </summary>
    public sealed class VoiceChat : MonoBehaviour
    {
        // ---- Inspector ----
        [Header("Mode")]
        public VoiceChatMode Mode = VoiceChatMode.AlwaysOn;

        [Tooltip("OVR button used for push-to-talk (right primary = A button).")]
        public OVRInput.Button PushToTalkButton = OVRInput.Button.One;

        [Header("Audio")]
        [Tooltip("Microphone device name. Leave empty to use default device.")]
        public string MicDeviceName = "";

        [Tooltip("Sample rate for captured audio.")]
        public int SampleRate = 22050;

        [Tooltip("Capture buffer length in seconds.")]
        [Range(0.1f, 2f)]
        public float BufferLengthSeconds = 0.5f;

        [Header("Playback")]
        [Tooltip("AudioSource used to play back received voice from this player.")]
        public AudioSource PlaybackSource;

        [Tooltip("Spatial blend for voice playback (0 = 2D, 1 = 3D).")]
        [Range(0f, 1f)]
        public float SpatialBlend = 1f;

        [Header("OVRLipSync")]
        [Tooltip("Enable feeding mic audio into OVRLipSync on the local avatar.")]
        public bool EnableLipSync = true;

        [Tooltip("LipSync context on this player's avatar mesh. Auto-detected if null.")]
        public Component LipSyncContext;  // typed as Component to avoid hard dependency

        // ---- Public state ----
        public bool IsMuted      { get; private set; } = false;
        public bool IsTransmitting { get; private set; } = false;
        public bool IsCaptureRunning { get; private set; } = false;

        // ---- Private ----
        private AudioClip _micClip;
        private int       _lastMicPosition;
        private float[]   _sampleBuffer;

        // ---- Lifecycle ----
        private void Start()
        {
            if (PlaybackSource == null)
            {
                PlaybackSource = gameObject.AddComponent<AudioSource>();
                PlaybackSource.spatialBlend = SpatialBlend;
                PlaybackSource.loop = false;
                PlaybackSource.playOnAwake = false;
            }

            AutoDetectLipSync();
            StartCapture();
        }

        private void OnDestroy()
        {
            StopCapture();
        }

        private void Update()
        {
            if (!IsCaptureRunning) return;

            bool shouldTransmit = ShouldTransmit();
            IsTransmitting = shouldTransmit;

            if (shouldTransmit)
                ProcessMicInput();
        }

        // ---- Public API ----
        /// <summary>Start capturing microphone input.</summary>
        public void StartCapture()
        {
            if (IsCaptureRunning) return;

            string device = string.IsNullOrEmpty(MicDeviceName) ? null : MicDeviceName;

            // TODO: replace with actual microphone capture when transport is implemented
            // _micClip = Microphone.Start(device, true, Mathf.CeilToInt(BufferLengthSeconds), SampleRate);

            int bufferSamples = Mathf.CeilToInt(SampleRate * BufferLengthSeconds);
            _sampleBuffer = new float[bufferSamples];
            _lastMicPosition = 0;
            IsCaptureRunning = true;

            Debug.Log($"[VoiceChat] Capture started. Device: '{device ?? "default"}' Rate: {SampleRate}Hz");
        }

        /// <summary>Stop microphone capture and release device.</summary>
        public void StopCapture()
        {
            if (!IsCaptureRunning) return;

            // TODO: Microphone.End(MicDeviceName);
            _micClip = null;
            IsCaptureRunning = false;
            IsTransmitting = false;

            Debug.Log("[VoiceChat] Capture stopped.");
        }

        /// <summary>Mute/unmute local microphone (other players will hear silence).</summary>
        public void SetMute(bool muted)
        {
            IsMuted = muted;
            Debug.Log($"[VoiceChat] {(muted ? "Muted" : "Unmuted")}");
        }

        public void ToggleMute() => SetMute(!IsMuted);

        /// <summary>
        /// Feed received encoded audio bytes to the playback pipeline.
        /// Call this from NetworkManager.OnDataReceived when the packet type
        /// indicates voice data.
        /// TODO: decode and schedule on PlaybackSource.
        /// </summary>
        public void ReceiveVoicePacket(byte[] encodedData)
        {
            if (PlaybackSource == null) return;
            // TODO: decode audio (Opus or raw PCM) and play via PlaybackSource
            Debug.Log($"[VoiceChat] Received voice packet: {encodedData?.Length ?? 0} bytes (not yet decoded)");
        }

        // ---- Private helpers ----
        private bool ShouldTransmit()
        {
            if (IsMuted) return false;
            if (Mode == VoiceChatMode.AlwaysOn) return true;

            // Push-to-talk
#if HAS_META_XR
            return OVRInput.Get(PushToTalkButton);
#else
            return UnityEngine.Input.GetKey(KeyCode.V);  // fallback: V key in editor
#endif
        }

        private void ProcessMicInput()
        {
            if (_micClip == null) return;

            int micPos = Microphone.GetPosition(
                string.IsNullOrEmpty(MicDeviceName) ? null : MicDeviceName);

            if (micPos == _lastMicPosition) return;

            // Wrap-around read
            int samples;
            if (micPos > _lastMicPosition)
            {
                samples = micPos - _lastMicPosition;
            }
            else
            {
                samples = _micClip.samples - _lastMicPosition + micPos;
            }

            if (samples <= 0) return;

            if (_sampleBuffer == null || _sampleBuffer.Length < samples)
                _sampleBuffer = new float[samples];

            _micClip.GetData(_sampleBuffer, _lastMicPosition);
            _lastMicPosition = micPos;

            FeedLipSync(_sampleBuffer, samples);
            TransmitAudio(_sampleBuffer, samples);
        }

        private void FeedLipSync(float[] samples, int count)
        {
            if (!EnableLipSync || LipSyncContext == null) return;

#if HAS_META_XR
            // OVRLipSyncContext.ProcessAudioSamplesRaw exists in Meta XR Audio SDK.
            // We use reflection to avoid a hard compile dependency.
            var method = LipSyncContext.GetType().GetMethod("ProcessAudioSamplesRaw");
            if (method != null)
            {
                // Trim array if needed
                float[] chunk = samples;
                if (samples.Length != count)
                {
                    chunk = new float[count];
                    System.Array.Copy(samples, chunk, count);
                }
                method.Invoke(LipSyncContext, new object[] { chunk, 1 });
            }
#endif
        }

        private void TransmitAudio(float[] samples, int count)
        {
            // TODO: encode samples (Opus recommended) and send via NetworkManager.
            // Stub: just log once every ~5 seconds to show the pipeline is running.
            // NetworkManager.Instance?.SendUnreliable(encodedBytes, -1);
        }

        private void AutoDetectLipSync()
        {
            if (!EnableLipSync || LipSyncContext != null) return;

#if HAS_META_XR
            // Try to find OVRLipSyncContext anywhere on this GameObject or children
            var ctx = GetComponentInChildren<OVRLipSyncContext>();
            if (ctx != null)
            {
                LipSyncContext = ctx;
                Debug.Log("[VoiceChat] OVRLipSyncContext auto-detected.");
            }
#endif
        }
    }
}
#endif // PLAGA44_FULL_SDK
