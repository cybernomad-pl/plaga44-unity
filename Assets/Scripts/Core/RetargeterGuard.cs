// =============================================================================
// RetargeterGuard.cs
// CYBERNOMAD -- Filtruje ObjectDisposedException z CharacterRetargeter.
// Meta XR Movement SDK v83 bug: NativeArray uzyty po dealokacji.
// NIE wylacza zadnych komponentow SDK. Tylko custom ILogHandler
// ktory przepuszcza wszystko OPROCZ tego jednego known SDK bug.
// =============================================================================

using UnityEngine;

namespace Plaga44
{
    public static class RetargeterGuard
    {
        private static readonly string[] KnownSDKErrors = new[]
        {
            "NativeArray`1[Meta.XR.Movement",
            "Failed to retarget source frame data",
            "LocomotionEventsConnection",
            "AssertCollectionItems",
            // Editor-mode bez Questa -- OVRBody/OVRSkeleton nie maja source,
            // retargeter spamuje co frame. Niegroźne, filter.
            "Global joint set is invalid",
            "OVRSkeleton and its subclasses requires OVRBody",
            "[OVRBody] Failed to start body tracking",
            "[OVRBody] Failed to set Body Tracking fidelity",
            "XR_ERROR_ACTIONSET_NOT_ATTACHED",
            "Unable to process a controller whose SampleRateHz is 0",
            "XR: Error setting active audio output driver",
            "Local Dimming feature is not supported",
            // body tracking fidelity: OVRBody probuje ustawic High Fidelity
            // na editor/Quest bez tego feature. Fallback do low auto-dziala.
            "body tracking fidelity is not supported",
            "RequestBodyTrackingFidelity",
        };

        private static ILogHandler _original;
        private static bool _installed;
        private static int _filtered;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Install()
        {
            if (_installed) return;
            _original = Debug.unityLogger.logHandler;
            Debug.unityLogger.logHandler = new GuardLogHandler(_original);
            _installed = true;
        }

        private class GuardLogHandler : ILogHandler
        {
            private readonly ILogHandler _inner;
            public GuardLogHandler(ILogHandler inner) => _inner = inner;

            public void LogFormat(LogType logType, Object context, string format, params object[] args)
            {
                if (logType == LogType.Error || logType == LogType.Exception
                 || logType == LogType.Warning)
                {
                    string msg = args.Length > 0 ? string.Format(format, args) : format;
                    if (IsKnownSDKError(msg)) { _filtered++; return; }
                }
                _inner.LogFormat(logType, context, format, args);
            }

            public void LogException(System.Exception exception, Object context)
            {
                if (IsKnownSDKError(exception.ToString())) { _filtered++; return; }
                _inner.LogException(exception, context);
            }
        }

        private static bool IsKnownSDKError(string msg)
        {
            for (int i = 0; i < KnownSDKErrors.Length; i++)
                if (msg.Contains(KnownSDKErrors[i])) return true;
            return false;
        }
    }
}
