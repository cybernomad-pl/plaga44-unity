// PlatformManager.cs
// CYBERNOMAD -- Oculus Platform SDK initialization, entitlement check, logged-in user.
// Singleton. Call PlatformManager.Instance from any script after scene load.
// Wraps Oculus.Platform.Core and Oculus.Platform.Users.
// Guarded by HAS_META_XR -- safe to compile without Meta XR package.

using System;
using System.Threading.Tasks;
using UnityEngine;

#if HAS_META_XR
using Oculus.Platform;
using Oculus.Platform.Models;
#endif

namespace Plaga44.Platform
{
    /// <summary>
    /// Initializes Oculus Platform SDK, performs entitlement check, and exposes
    /// the logged-in user. Must be placed in the first scene. All other Platform
    /// managers depend on this being initialized first.
    /// </summary>
    public class PlatformManager : MonoBehaviour
    {
        // ------------------------------------------------------------------ //
        // Singleton
        // ------------------------------------------------------------------ //

        private static PlatformManager _instance;

        public static PlatformManager Instance
        {
            get
            {
                if (_instance == null)
                {
                    var go = new GameObject("[PlatformManager]");
                    _instance = go.AddComponent<PlatformManager>();
                    DontDestroyOnLoad(go);
                }
                return _instance;
            }
        }

        // ------------------------------------------------------------------ //
        // Public state
        // ------------------------------------------------------------------ //

        /// <summary>True after Core.Initialize and entitlement check succeeded.</summary>
        public bool IsInitialized { get; private set; }

        /// <summary>True if the entitlement check passed (user owns the app).</summary>
        public bool IsEntitled { get; private set; }

        /// <summary>Display name of the logged-in Oculus user. Empty until initialized.</summary>
        public string LoggedInUserDisplayName { get; private set; } = string.Empty;

        /// <summary>Oculus user ID (ulong) of the logged-in user. 0 until initialized.</summary>
        public ulong LoggedInUserId { get; private set; }

        /// <summary>Fired after initialization + entitlement check completes (success or failure).</summary>
        public event Action<bool> OnInitialized;

        // ------------------------------------------------------------------ //
        // Unity lifecycle
        // ------------------------------------------------------------------ //

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }

            _instance = this;
            DontDestroyOnLoad(gameObject);

            _ = InitializeAsync();
        }

        // ------------------------------------------------------------------ //
        // Initialization
        // ------------------------------------------------------------------ //

        private async Task InitializeAsync()
        {
#if HAS_META_XR
            try
            {
                // Initialize Oculus Platform SDK with the App ID from OVR settings.
                Core.Initialize();
                Debug.Log("[PlatformManager] Oculus.Platform.Core.Initialize() called.");

                // Entitlement check -- required by Meta policy.
                bool entitled = await CheckEntitlementAsync();
                IsEntitled = entitled;

                if (!entitled)
                {
                    Debug.LogError("[PlatformManager] Entitlement check FAILED. User does not own the app.");
                    IsInitialized = false;
                    OnInitialized?.Invoke(false);
                    return;
                }

                // Fetch logged-in user info.
                await FetchLoggedInUserAsync();

                IsInitialized = true;
                Debug.Log($"[PlatformManager] Ready. User: {LoggedInUserDisplayName} (id={LoggedInUserId})");
                OnInitialized?.Invoke(true);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[PlatformManager] Initialization exception: {ex}");
                IsInitialized = false;
                OnInitialized?.Invoke(false);
            }
#else
            // Editor / non-Meta build -- simulate success for development.
            IsInitialized = true;
            IsEntitled = true;
            LoggedInUserDisplayName = "DevUser";
            LoggedInUserId = 1234567890UL;
            Debug.LogWarning("[PlatformManager] HAS_META_XR not defined -- running in mock mode.");
            OnInitialized?.Invoke(true);
            await Task.CompletedTask;
#endif
        }

#if HAS_META_XR
        private Task<bool> CheckEntitlementAsync()
        {
            var tcs = new TaskCompletionSource<bool>();

            Entitlements.IsUserEntitledToApplication().OnComplete(msg =>
            {
                if (msg.IsError)
                {
                    Debug.LogError($"[PlatformManager] Entitlement error: {msg.GetError().Message}");
                    tcs.SetResult(false);
                }
                else
                {
                    tcs.SetResult(true);
                }
            });

            return tcs.Task;
        }

        private Task FetchLoggedInUserAsync()
        {
            var tcs = new TaskCompletionSource<bool>();

            Users.GetLoggedInUser().OnComplete(msg =>
            {
                if (msg.IsError)
                {
                    Debug.LogError($"[PlatformManager] GetLoggedInUser error: {msg.GetError().Message}");
                }
                else
                {
                    var user = msg.GetUser();
                    LoggedInUserDisplayName = user.DisplayName;
                    LoggedInUserId = user.ID;
                }
                tcs.SetResult(true);
            });

            return tcs.Task;
        }
#endif
    }
}
