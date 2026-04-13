// EditorConfig.cs -- CYBERNOMAD Editor Tool
//
// Steruje: ProjectSettings/EditorSettings.asset + inne editor-only settings
//
// Public API:
//   EditorConfig.Apply(EditorConfig.INITIAL);
//   EditorConfig.SetSerializationMode(2); // ForceText
//   EditorConfig.LogCurrent();

using UnityEditor;
using UnityEngine;

namespace Plaga44.Editor
{
    public struct EditorSettings_
    {
        public int serializationMode;       // 0=Mixed, 1=ForceBinary, 2=ForceText
        public int externalVersionControl;  // "Visible Meta Files" etc (string via API)
        public int spritePackerMode;        // 0=Disabled, 1=BuildTimeOnly, 2=AlwaysOn
        public int lineEndingsForNewScripts; // 0=OS, 1=Unix, 2=Windows
        public int assetPipelineMode;       // 1=v2
        public bool enterPlayModeOptionsEnabled;
        public int enterPlayModeOptions;    // 0=None, 1=DisableDomainReload, 2=DisableSceneReload, 3=Both
    }

    public static class EditorConfig
    {
        private const string LOG = "[PLAGA44]";

        // ---------------------------------------------------------------------
        // Presety
        // ---------------------------------------------------------------------

        public static readonly EditorSettings_ INITIAL = new EditorSettings_
        {
            serializationMode        = 2,       // ForceText (git-friendly)
            spritePackerMode         = 0,       // Disabled (VR nie uzywa sprite atlas)
            lineEndingsForNewScripts = 0,       // OS native
            enterPlayModeOptionsEnabled = true,
            enterPlayModeOptions     = 1,       // DisableDomainReload (szybszy play mode)
        };

        // ---------------------------------------------------------------------
        // Apply
        // ---------------------------------------------------------------------

        public static void Apply(EditorSettings_ s)
        {
            EditorSettings.serializationMode = (SerializationMode)s.serializationMode;
            EditorSettings.spritePackerMode = (SpritePackerMode)s.spritePackerMode;
            EditorSettings.lineEndingsForNewScripts = (LineEndingsMode)s.lineEndingsForNewScripts;
            EditorSettings.enterPlayModeOptionsEnabled = s.enterPlayModeOptionsEnabled;
            EditorSettings.enterPlayModeOptions = (EnterPlayModeOptions)s.enterPlayModeOptions;

            Debug.Log($"{LOG} Editor applied: serialization={s.serializationMode} " +
                      $"playMode={s.enterPlayModeOptions} spritePacker={s.spritePackerMode}");
        }

        // ---------------------------------------------------------------------
        // Single value
        // ---------------------------------------------------------------------

        public static void SetSerializationMode(int v)
        {
            EditorSettings.serializationMode = (SerializationMode)v;
            Log("serialization", v.ToString());
        }

        public static void SetEnterPlayModeOptions(bool enabled, int options)
        {
            EditorSettings.enterPlayModeOptionsEnabled = enabled;
            EditorSettings.enterPlayModeOptions = (EnterPlayModeOptions)options;
            Log("playModeOptions", $"enabled={enabled} options={options}");
        }

        public static void SetSpritePackerMode(int v)
        {
            EditorSettings.spritePackerMode = (SpritePackerMode)v;
            Log("spritePacker", v.ToString());
        }

        public static void SetLineEndings(int v)
        {
            EditorSettings.lineEndingsForNewScripts = (LineEndingsMode)v;
            Log("lineEndings", v.ToString());
        }

        // ---------------------------------------------------------------------
        // Log
        // ---------------------------------------------------------------------

        public static void LogCurrent()
        {
            Debug.Log($"{LOG} Editor: serialization={EditorSettings.serializationMode} " +
                      $"playModeEnabled={EditorSettings.enterPlayModeOptionsEnabled} " +
                      $"playModeOptions={EditorSettings.enterPlayModeOptions} " +
                      $"spritePacker={EditorSettings.spritePackerMode} " +
                      $"lineEndings={EditorSettings.lineEndingsForNewScripts}");
        }

        // ---------------------------------------------------------------------
        // Menu
        // ---------------------------------------------------------------------

        [MenuItem("CYBERNOMAD/Editor/Apply INITIAL", false, 1)]
        static void MenuInitial() => Apply(INITIAL);
        [MenuItem("CYBERNOMAD/Editor/Show Current", false, 100)]
        static void MenuShow() => LogCurrent();

        // ---------------------------------------------------------------------
        static void Log(string f, string v) => Debug.Log($"{LOG} Editor tweak: {f}={v}");
    }
}
