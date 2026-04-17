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
                if (logType == LogType.Error || logType == LogType.Exception)
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
