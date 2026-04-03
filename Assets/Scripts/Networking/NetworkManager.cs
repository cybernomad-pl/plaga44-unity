// NetworkManager.cs
// CYBERNOMAD -- PLAGA '44
// Singleton abstraction over network transport (Photon/Netcode-ready).
// Current implementation: local loopback placeholder.
// To swap transport: implement INetworkTransport and assign via SetTransport().

using System;
using UnityEngine;

namespace Plaga44.Networking
{
    /// <summary>
    /// Minimal transport interface -- implement this to plug in Photon, Unity Netcode, etc.
    /// </summary>
    public interface INetworkTransport
    {
        bool IsConnected { get; }
        bool IsHost { get; }
        void Connect(string roomId, Action<bool> onResult);
        void Disconnect();
        void SendReliable(byte[] data, int targetPlayerId);
        void SendUnreliable(byte[] data, int targetPlayerId);
        event Action<int, byte[]> OnDataReceived;   // (senderId, data)
        event Action<int> OnPlayerJoined;           // playerId
        event Action<int> OnPlayerLeft;             // playerId
    }

    /// <summary>
    /// Local loopback transport -- reflects all sends back to itself.
    /// Used for solo testing without a real server.
    /// </summary>
    internal sealed class LocalLoopbackTransport : INetworkTransport
    {
        private bool _connected;

        public bool IsConnected => _connected;
        public bool IsHost => _connected;  // loopback is always host

        public event Action<int, byte[]> OnDataReceived;
        public event Action<int> OnPlayerJoined;
        public event Action<int> OnPlayerLeft;

        public void Connect(string roomId, Action<bool> onResult)
        {
            _connected = true;
            Debug.Log($"[NetworkManager] LocalLoopback: connected to room '{roomId}'");
            onResult?.Invoke(true);
            OnPlayerJoined?.Invoke(0);  // local player id = 0
        }

        public void Disconnect()
        {
            if (!_connected) return;
            _connected = false;
            OnPlayerLeft?.Invoke(0);
            Debug.Log("[NetworkManager] LocalLoopback: disconnected");
        }

        public void SendReliable(byte[] data, int targetPlayerId)
        {
            // Loopback: reflect data back to caller immediately
            OnDataReceived?.Invoke(0, data);
        }

        public void SendUnreliable(byte[] data, int targetPlayerId)
        {
            OnDataReceived?.Invoke(0, data);
        }
    }

    /// <summary>
    /// MonoBehaviour singleton. Manages network lifecycle.
    /// Place on a persistent GameObject in the scene.
    /// </summary>
    public sealed class NetworkManager : MonoBehaviour
    {
        // ---- Singleton ----
        private static NetworkManager _instance;

        public static NetworkManager Instance
        {
            get
            {
                if (_instance == null)
                {
                    var go = new GameObject("[NetworkManager]");
                    DontDestroyOnLoad(go);
                    _instance = go.AddComponent<NetworkManager>();
                }
                return _instance;
            }
        }

        // ---- Inspector ----
        [Header("Connection")]
        [Tooltip("Room / lobby ID used when no explicit ID is provided to Connect().")]
        public string defaultRoomId = "plaga44-default";

        [Tooltip("Maximum players in a room.")]
        [Range(2, 8)]
        public int maxPlayers = 4;

        // ---- Public API ----
        public bool IsConnected => _transport != null && _transport.IsConnected;
        public bool IsHost     => _transport != null && _transport.IsHost;
        public int  LocalPlayerId { get; private set; } = -1;

        /// <summary>Fired after successful connect. Arg: local player id.</summary>
        public event Action<int> OnConnected;

        /// <summary>Fired after disconnect.</summary>
        public event Action OnDisconnected;

        /// <summary>Fired when remote player joins. Arg: their player id.</summary>
        public event Action<int> OnPlayerJoined;

        /// <summary>Fired when remote player leaves. Arg: their player id.</summary>
        public event Action<int> OnPlayerLeft;

        /// <summary>Fired when raw data arrives. Args: senderId, data.</summary>
        public event Action<int, byte[]> OnDataReceived;

        // ---- Private ----
        private INetworkTransport _transport;

        // ---- Lifecycle ----
        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }
            _instance = this;
            DontDestroyOnLoad(gameObject);

            // Default: loopback
            SetTransport(new LocalLoopbackTransport());
        }

        private void OnDestroy()
        {
            if (_instance == this) _instance = null;
            DetachTransportEvents();
        }

        // ---- Transport injection ----
        /// <summary>
        /// Swap transport at runtime (before connect).
        /// Example: SetTransport(new PhotonTransport());
        /// </summary>
        public void SetTransport(INetworkTransport transport)
        {
            DetachTransportEvents();
            _transport = transport;
            AttachTransportEvents();
            Debug.Log($"[NetworkManager] Transport set: {transport?.GetType().Name}");
        }

        // ---- Public methods ----
        /// <summary>Connect using the default room ID.</summary>
        public void Connect(Action<bool> onResult = null)
        {
            Connect(defaultRoomId, onResult);
        }

        /// <summary>Connect to a specific room.</summary>
        public void Connect(string roomId, Action<bool> onResult = null)
        {
            if (_transport == null)
            {
                Debug.LogError("[NetworkManager] No transport set.");
                onResult?.Invoke(false);
                return;
            }

            if (IsConnected)
            {
                Debug.LogWarning("[NetworkManager] Already connected.");
                onResult?.Invoke(true);
                return;
            }

            Debug.Log($"[NetworkManager] Connecting to room '{roomId}'...");
            _transport.Connect(roomId, success =>
            {
                if (success)
                {
                    LocalPlayerId = 0;  // transport should assign real id; loopback = 0
                    OnConnected?.Invoke(LocalPlayerId);
                }
                onResult?.Invoke(success);
            });
        }

        public void Disconnect()
        {
            if (!IsConnected)
            {
                Debug.LogWarning("[NetworkManager] Not connected.");
                return;
            }
            _transport.Disconnect();
        }

        /// <summary>Send reliable (ordered) data to target player. -1 = broadcast.</summary>
        public void SendReliable(byte[] data, int targetPlayerId = -1)
        {
            if (!IsConnected) return;
            _transport.SendReliable(data, targetPlayerId);
        }

        /// <summary>Send unreliable (unordered, low-latency) data. -1 = broadcast.</summary>
        public void SendUnreliable(byte[] data, int targetPlayerId = -1)
        {
            if (!IsConnected) return;
            _transport.SendUnreliable(data, targetPlayerId);
        }

        // ---- Event wiring ----
        private void AttachTransportEvents()
        {
            if (_transport == null) return;
            _transport.OnDataReceived  += HandleDataReceived;
            _transport.OnPlayerJoined  += HandlePlayerJoined;
            _transport.OnPlayerLeft    += HandlePlayerLeft;
        }

        private void DetachTransportEvents()
        {
            if (_transport == null) return;
            _transport.OnDataReceived  -= HandleDataReceived;
            _transport.OnPlayerJoined  -= HandlePlayerJoined;
            _transport.OnPlayerLeft    -= HandlePlayerLeft;
        }

        private void HandleDataReceived(int senderId, byte[] data)
        {
            OnDataReceived?.Invoke(senderId, data);
        }

        private void HandlePlayerJoined(int playerId)
        {
            Debug.Log($"[NetworkManager] Player joined: {playerId}");
            OnPlayerJoined?.Invoke(playerId);
        }

        private void HandlePlayerLeft(int playerId)
        {
            Debug.Log($"[NetworkManager] Player left: {playerId}");
            OnPlayerLeft?.Invoke(playerId);
            if (playerId == LocalPlayerId)
                OnDisconnected?.Invoke();
        }
    }
}
