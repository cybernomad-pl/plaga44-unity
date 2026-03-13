// AchievementManager.cs
// CYBERNOMAD -- Oculus Achievements wrappers: UnlockAchievement, GetProgress.
// Predefiniowane osiagniecia PLAGA '44:
//   FIRST_HEADSHOT    -- pierwszy headshot w grze
//   STONE_MASTER_10   -- 10 kills kamieniem
//   LONG_RANGE_30M    -- kill z >= 30m odleglosci
//   PACIFIST          -- ukonczenie etapu bez kill
// Rejestracja nazw w Meta Developer Dashboard obowiazkowa przed release.

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
    /// Achievement API names used in PLAGA '44.
    /// Must match exactly the names registered in Meta Developer Dashboard.
    /// </summary>
    public static class AchievementNames
    {
        /// <summary>Awarded on the first headshot kill.</summary>
        public const string FirstHeadshot = "FIRST_HEADSHOT";

        /// <summary>Awarded after 10 kills with thrown stones.</summary>
        public const string StoneMaster10 = "STONE_MASTER_10";

        /// <summary>Awarded for a kill from >= 30 metres range.</summary>
        public const string LongRange30M = "LONG_RANGE_30M";

        /// <summary>Awarded for completing a stage without any kills.</summary>
        public const string Pacifist = "PACIFIST";
    }

    /// <summary>
    /// Progress data for a single achievement.
    /// </summary>
    public class AchievementProgress
    {
        public string Name;
        public bool IsUnlocked;
        /// <summary>Current count for COUNT-type achievements (e.g. STONE_MASTER_10).</summary>
        public long Count;
        /// <summary>Bitfield string for BITFIELD-type achievements.</summary>
        public string Bitfield;
    }

    /// <summary>
    /// Wraps Oculus Platform achievements API.
    /// Singleton accessed via AchievementManager.Instance.
    /// </summary>
    public class AchievementManager : MonoBehaviour
    {
        // ------------------------------------------------------------------ //
        // Singleton
        // ------------------------------------------------------------------ //

        private static AchievementManager _instance;

        public static AchievementManager Instance
        {
            get
            {
                if (_instance == null)
                {
                    var go = new GameObject("[AchievementManager]");
                    _instance = go.AddComponent<AchievementManager>();
                    DontDestroyOnLoad(go);
                }
                return _instance;
            }
        }

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }
            _instance = this;
            DontDestroyOnLoad(gameObject);
        }

        // ------------------------------------------------------------------ //
        // Public API
        // ------------------------------------------------------------------ //

        /// <summary>
        /// Unlock a SIMPLE achievement by name. Safe to call multiple times --
        /// Oculus SDK ignores duplicate unlocks.
        /// </summary>
        /// <param name="achievementName">Use AchievementNames constants.</param>
        /// <returns>True if the request succeeded (not necessarily newly unlocked).</returns>
        public async Task<bool> UnlockAchievement(string achievementName)
        {
            if (!PlatformManager.Instance.IsInitialized)
            {
                Debug.LogWarning("[AchievementManager] PlatformManager not initialized. Skipping UnlockAchievement.");
                return false;
            }

#if HAS_META_XR
            try
            {
                var tcs = new TaskCompletionSource<bool>();

                Achievements.Unlock(achievementName).OnComplete(msg =>
                {
                    if (msg.IsError)
                    {
                        Debug.LogError($"[AchievementManager] UnlockAchievement error '{achievementName}': {msg.GetError().Message}");
                        tcs.SetResult(false);
                    }
                    else
                    {
                        Debug.Log($"[AchievementManager] UnlockAchievement '{achievementName}' -- success.");
                        tcs.SetResult(true);
                    }
                });

                return await tcs.Task;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[AchievementManager] UnlockAchievement exception: {ex}");
                return false;
            }
#else
            Debug.LogWarning($"[AchievementManager] Mock UnlockAchievement: '{achievementName}'");
            await Task.CompletedTask;
            return true;
#endif
        }

        /// <summary>
        /// Add to the count of a COUNT-type achievement.
        /// </summary>
        /// <param name="achievementName">Use AchievementNames constants.</param>
        /// <param name="count">Amount to add (must be > 0).</param>
        /// <returns>True if request succeeded.</returns>
        public async Task<bool> AddCount(string achievementName, ulong count = 1)
        {
            if (!PlatformManager.Instance.IsInitialized)
            {
                Debug.LogWarning("[AchievementManager] PlatformManager not initialized. Skipping AddCount.");
                return false;
            }

#if HAS_META_XR
            try
            {
                var tcs = new TaskCompletionSource<bool>();

                Achievements.AddCount(achievementName, count).OnComplete(msg =>
                {
                    if (msg.IsError)
                    {
                        Debug.LogError($"[AchievementManager] AddCount error '{achievementName}': {msg.GetError().Message}");
                        tcs.SetResult(false);
                    }
                    else
                    {
                        Debug.Log($"[AchievementManager] AddCount '{achievementName}' +{count} -- success.");
                        tcs.SetResult(true);
                    }
                });

                return await tcs.Task;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[AchievementManager] AddCount exception: {ex}");
                return false;
            }
#else
            Debug.LogWarning($"[AchievementManager] Mock AddCount: '{achievementName}' +{count}");
            await Task.CompletedTask;
            return true;
#endif
        }

        /// <summary>
        /// Get progress for a named achievement.
        /// </summary>
        /// <param name="achievementName">Use AchievementNames constants.</param>
        /// <returns>AchievementProgress or null on error.</returns>
        public async Task<AchievementProgress> GetProgress(string achievementName)
        {
            if (!PlatformManager.Instance.IsInitialized)
            {
                Debug.LogWarning("[AchievementManager] PlatformManager not initialized. Returning null progress.");
                return null;
            }

#if HAS_META_XR
            try
            {
                var tcs = new TaskCompletionSource<AchievementProgress>();

                Achievements.GetProgressByName(new string[] { achievementName }).OnComplete(msg =>
                {
                    if (msg.IsError)
                    {
                        Debug.LogError($"[AchievementManager] GetProgress error '{achievementName}': {msg.GetError().Message}");
                        tcs.SetResult(null);
                        return;
                    }

                    var list = msg.GetAchievementProgressList();
                    AchievementProgress result = null;

                    foreach (var item in list)
                    {
                        if (item.Name == achievementName)
                        {
                            result = new AchievementProgress
                            {
                                Name = item.Name,
                                IsUnlocked = item.IsUnlocked,
                                Count = item.Count,
                                Bitfield = item.Bitfield
                            };
                            break;
                        }
                    }

                    tcs.SetResult(result);
                });

                return await tcs.Task;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[AchievementManager] GetProgress exception: {ex}");
                return null;
            }
#else
            await Task.CompletedTask;
            return new AchievementProgress
            {
                Name = achievementName,
                IsUnlocked = false,
                Count = 0,
                Bitfield = string.Empty
            };
#endif
        }

        // ------------------------------------------------------------------ //
        // Convenience wrappers for PLAGA '44 specific achievements
        // ------------------------------------------------------------------ //

        /// <summary>Unlock FIRST_HEADSHOT achievement.</summary>
        public Task<bool> UnlockFirstHeadshot() => UnlockAchievement(AchievementNames.FirstHeadshot);

        /// <summary>Increment STONE_MASTER_10 count by 1 kill.</summary>
        public Task<bool> AddStoneKill() => AddCount(AchievementNames.StoneMaster10, 1);

        /// <summary>Unlock LONG_RANGE_30M achievement (check distance >= 30m before calling).</summary>
        public Task<bool> UnlockLongRange() => UnlockAchievement(AchievementNames.LongRange30M);

        /// <summary>Unlock PACIFIST achievement at stage completion.</summary>
        public Task<bool> UnlockPacifist() => UnlockAchievement(AchievementNames.Pacifist);
    }
}
