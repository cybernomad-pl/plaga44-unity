// =============================================================================
// PlayerPositionPersistence.cs
// CYBERNOMAD -- zapisuje/odtwarza pozycję gracza między sesjami.
// Save: OnApplicationQuit + na każdą zmianę GameState (defensive)
// Restore: Start() jeśli istnieje saved position dla aktualnej sceny
// =============================================================================

using UnityEngine;
using UnityEngine.SceneManagement;

namespace Plaga44
{
    [DisallowMultipleComponent]
    public class PlayerPositionPersistence : MonoBehaviour
    {
        private const string LOG = "[PLAGA44][PlayerPos]";
        private const string KeyPrefix = "Plaga44_PlayerPos_";

        [Tooltip("Disable to start fresh each session (debug).")]
        public bool enableRestore = true;

        [Tooltip("Save Y position too (uncheck for ground-snap respawn each session).")]
        public bool saveY = true;

        private string PrefsKey => KeyPrefix + SceneManager.GetActiveScene().name;

        private void Start()
        {
            if (!enableRestore)
            {
                Debug.Log($"{LOG} Restore DISABLED (fresh start each session)");
                return;
            }

            string key = PrefsKey;
            if (!PlayerPrefs.HasKey(key + "_x"))
            {
                Debug.Log($"{LOG} No saved position for scene '{SceneManager.GetActiveScene().name}' -- using scene default");
                return;
            }

            float x = PlayerPrefs.GetFloat(key + "_x");
            float y = PlayerPrefs.GetFloat(key + "_y");
            float z = PlayerPrefs.GetFloat(key + "_z");
            float yaw = PlayerPrefs.GetFloat(key + "_yaw");

            Vector3 oldPos = transform.position;
            Vector3 newPos = saveY ? new Vector3(x, y, z) : new Vector3(x, transform.position.y, z);
            transform.position = newPos;
            transform.rotation = Quaternion.Euler(0, yaw, 0);

            Debug.Log($"{LOG} RESTORED position from PlayerPrefs: scene='{SceneManager.GetActiveScene().name}' "
                + $"old={oldPos:F2} -> new={newPos:F2} yaw={yaw:F1}");
        }

        private void OnApplicationQuit() => Save("OnApplicationQuit");

        private void OnDisable()
        {
            // Save when scene unloads / play mode stops
            if (Application.isPlaying) Save("OnDisable");
        }

        private void Save(string trigger)
        {
            string key = PrefsKey;
            Vector3 p = transform.position;
            float yaw = transform.eulerAngles.y;
            PlayerPrefs.SetFloat(key + "_x", p.x);
            PlayerPrefs.SetFloat(key + "_y", p.y);
            PlayerPrefs.SetFloat(key + "_z", p.z);
            PlayerPrefs.SetFloat(key + "_yaw", yaw);
            PlayerPrefs.Save();
            Debug.Log($"{LOG} SAVED position via {trigger}: pos={p:F2} yaw={yaw:F1} -> '{key}'");
        }

        // Public method for explicit save (e.g. from menu / quit button)
        public void SaveNow() => Save("explicit");

        // Public method to clear saved position (e.g. for debug menu)
        public void ClearSaved()
        {
            string key = PrefsKey;
            PlayerPrefs.DeleteKey(key + "_x");
            PlayerPrefs.DeleteKey(key + "_y");
            PlayerPrefs.DeleteKey(key + "_z");
            PlayerPrefs.DeleteKey(key + "_yaw");
            PlayerPrefs.Save();
            Debug.Log($"{LOG} CLEARED saved position for '{SceneManager.GetActiveScene().name}'");
        }
    }
}
