// =============================================================================
// SettingsLogger.cs
// CYBERNOMAD -- centralne logowanie zmian parametrow (skybox, fog, itd).
// Uzywa CallerInfo zeby automatycznie wstawic kto wywolal metode.
// =============================================================================
using System.IO;
using System.Runtime.CompilerServices;
using UnityEngine;

namespace Plaga44.UI
{
    public static class SettingsLogger
    {
        /// <summary>
        /// Loguje zmiane parametru. Context optional (np. section='SKYBOX' albo reason='PlayerHeight').
        /// Kompilator automatycznie wypelnia caller/file/line.
        /// </summary>
        public static void Log(string settingName, float oldVal, float newVal,
            string context = null,
            [CallerMemberName] string caller = "",
            [CallerFilePath]   string file   = "",
            [CallerLineNumber] int    line   = 0)
        {
            string scriptName = Path.GetFileNameWithoutExtension(file);
            float delta = newVal - oldVal;
            string ctx = string.IsNullOrEmpty(context) ? "" : $"[{context}] ";
            Debug.Log(
                $"[PLAGA44][Settings] {ctx}{settingName}: {oldVal:F3} -> {newVal:F3} " +
                $"(delta={delta:+0.000;-0.000;0.000}) | caller={scriptName}.{caller}:{line}"
            );
        }
    }
}
