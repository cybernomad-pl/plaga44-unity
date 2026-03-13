// LeaderboardManager.cs
// CYBERNOMAD -- Oculus Leaderboard wrappers: SubmitScore, GetScores.
// Predefiniowane tablice dla PLAGA '44:
//   mors_cerebri_distance -- najdalszy kill (metry * 100, int)
//   mors_cerebri_streak   -- najdluzsza seria combo
//   mors_cerebri_speed    -- najszybszy kill (ms od startu rundy)
// Wymagana kolejnosc inicjalizacji: PlatformManager.IsInitialized == true.

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

#if HAS_META_XR
using Oculus.Platform;
using Oculus.Platform.Models;
#endif

namespace Plaga44.Platform
{
    /// <summary>
    /// Leaderboard names used in PLAGA '44.
    /// Register these exact names in the Meta Developer dashboard.
    /// </summary>
    public static class LeaderboardNames
    {
        /// <summary>Furthest kill distance. Score = metres * 100 (int).</summary>
        public const string MorsCerebriDistance = "mors_cerebri_distance";

        /// <summary>Longest kill combo streak.</summary>
        public const string MorsCerebriStreak = "mors_cerebri_streak";

        /// <summary>Fastest kill time. Score = milliseconds since round start.</summary>
        public const string MorsCerebriSpeed = "mors_cerebri_speed";
    }

    /// <summary>
    /// Single leaderboard entry returned by GetScores.
    /// </summary>
    public class LeaderboardEntry
    {
        public string DisplayName;
        public long Score;
        public int Rank;
    }

    /// <summary>
    /// Wraps Oculus Platform leaderboard API.
    /// Singleton accessed via LeaderboardManager.Instance.
    /// </summary>
    public class LeaderboardManager : MonoBehaviour
    {
        // ------------------------------------------------------------------ //
        // Singleton
        // ------------------------------------------------------------------ //

        private static LeaderboardManager _instance;

        public static LeaderboardManager Instance
        {
            get
            {
                if (_instance == null)
                {
                    var go = new GameObject("[LeaderboardManager]");
                    _instance = go.AddComponent<LeaderboardManager>();
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
        /// Submit a score to the named leaderboard.
        /// </summary>
        /// <param name="leaderboardName">Use LeaderboardNames constants.</param>
        /// <param name="score">Integer score value.</param>
        /// <returns>True if submitted successfully.</returns>
        public async Task<bool> SubmitScore(string leaderboardName, long score)
        {
            if (!PlatformManager.Instance.IsInitialized)
            {
                Debug.LogWarning("[LeaderboardManager] PlatformManager not initialized. Skipping SubmitScore.");
                return false;
            }

#if HAS_META_XR
            try
            {
                var tcs = new TaskCompletionSource<bool>();

                Leaderboards.WriteEntry(leaderboardName, score).OnComplete(msg =>
                {
                    if (msg.IsError)
                    {
                        Debug.LogError($"[LeaderboardManager] SubmitScore error on '{leaderboardName}': {msg.GetError().Message}");
                        tcs.SetResult(false);
                    }
                    else
                    {
                        bool updated = msg.GetLeaderboardUpdateStatus().DidUpdate;
                        Debug.Log($"[LeaderboardManager] SubmitScore '{leaderboardName}' score={score} didUpdate={updated}");
                        tcs.SetResult(true);
                    }
                });

                return await tcs.Task;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[LeaderboardManager] SubmitScore exception: {ex}");
                return false;
            }
#else
            Debug.LogWarning($"[LeaderboardManager] Mock SubmitScore: '{leaderboardName}' score={score}");
            await Task.CompletedTask;
            return true;
#endif
        }

        /// <summary>
        /// Fetch top scores from the named leaderboard.
        /// </summary>
        /// <param name="leaderboardName">Use LeaderboardNames constants.</param>
        /// <param name="count">Number of entries to fetch (max 100).</param>
        /// <returns>List of LeaderboardEntry, ordered by rank.</returns>
        public async Task<List<LeaderboardEntry>> GetScores(string leaderboardName, int count = 10)
        {
            if (!PlatformManager.Instance.IsInitialized)
            {
                Debug.LogWarning("[LeaderboardManager] PlatformManager not initialized. Returning empty list.");
                return new List<LeaderboardEntry>();
            }

#if HAS_META_XR
            try
            {
                var tcs = new TaskCompletionSource<List<LeaderboardEntry>>();

                Leaderboards.GetEntries(leaderboardName, count, LeaderboardFilterType.None, LeaderboardStartAt.Top)
                    .OnComplete(msg =>
                    {
                        var result = new List<LeaderboardEntry>();

                        if (msg.IsError)
                        {
                            Debug.LogError($"[LeaderboardManager] GetScores error on '{leaderboardName}': {msg.GetError().Message}");
                            tcs.SetResult(result);
                            return;
                        }

                        var entries = msg.GetLeaderboardEntryList();
                        foreach (var entry in entries)
                        {
                            result.Add(new LeaderboardEntry
                            {
                                DisplayName = entry.User.DisplayName,
                                Score = entry.Score,
                                Rank = entry.Rank
                            });
                        }

                        Debug.Log($"[LeaderboardManager] GetScores '{leaderboardName}' returned {result.Count} entries.");
                        tcs.SetResult(result);
                    });

                return await tcs.Task;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[LeaderboardManager] GetScores exception: {ex}");
                return new List<LeaderboardEntry>();
            }
#else
            // Mock data for editor development.
            await Task.CompletedTask;
            return new List<LeaderboardEntry>
            {
                new LeaderboardEntry { DisplayName = "DevUser",   Score = 9999, Rank = 1 },
                new LeaderboardEntry { DisplayName = "Borys",     Score = 8500, Rank = 2 },
                new LeaderboardEntry { DisplayName = "TestPilot", Score = 7200, Rank = 3 },
            };
#endif
        }

        /// <summary>
        /// Helper: submit distance kill score (metres converted to int precision * 100).
        /// </summary>
        public Task<bool> SubmitDistanceKill(float distanceMetres)
        {
            long score = (long)(distanceMetres * 100f);
            return SubmitScore(LeaderboardNames.MorsCerebriDistance, score);
        }

        /// <summary>
        /// Helper: submit combo streak score.
        /// </summary>
        public Task<bool> SubmitStreakScore(int streak)
        {
            return SubmitScore(LeaderboardNames.MorsCerebriStreak, streak);
        }

        /// <summary>
        /// Helper: submit speed kill time in milliseconds.
        /// </summary>
        public Task<bool> SubmitSpeedKill(int milliseconds)
        {
            return SubmitScore(LeaderboardNames.MorsCerebriSpeed, milliseconds);
        }
    }
}
