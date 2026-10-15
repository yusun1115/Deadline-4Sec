using System;
using UnityEngine;
#if DEADLINE_GPGS && UNITY_ANDROID && !UNITY_EDITOR
using GooglePlayGames;
using GooglePlayGames.BasicApi;
#endif

namespace Deadline4Sec
{
    // The one place that talks to Google Play Games: sign-in, achievement
    // reports and the two mode leaderboards. Gameplay and UI only call this.
    //
    // The real API is compiled only with DEADLINE_GPGS (added automatically
    // while the Google Play Games plugin is imported) on an Android player.
    // Everywhere else it is a mock that logs (Editor) or a no-op. A failure
    // or a missing sign-in never stops the game: calls are skipped and logged.
    public static class GooglePlayService
    {
        private static GooglePlaySettings settings;
#if DEADLINE_GPGS && UNITY_ANDROID && !UNITY_EDITOR
        private static bool authenticating;
#endif

        // Every submit request that passed the checks (mode, score, ID) — the
        // hook tests and debug tools listen to; raised before the API call.
        public static event Action<GameMode, long, string> ScoreSubmitted;

        public static GooglePlaySettings Settings
        {
            get
            {
                if (settings == null)
                    settings = Resources.Load<GooglePlaySettings>(GooglePlaySettings.ResourcePath);
                return settings;
            }
        }

        private static bool Logs => Settings == null || Settings.showLeaderboardDebugLogs;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            settings = null;
#if DEADLINE_GPGS && UNITY_ANDROID && !UNITY_EDITOR
            authenticating = false;
#endif
        }

        // Silent sign-in at launch; the game never waits for it.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void SignInAtLaunch() => Authenticate(null, false);

        public static bool IsMock =>
#if DEADLINE_GPGS && UNITY_ANDROID && !UNITY_EDITOR
            false;
#else
            Application.isEditor && (Settings == null || Settings.useMockLeaderboardInEditor);
#endif

        public static bool IsAuthenticated
        {
            get
            {
#if DEADLINE_GPGS && UNITY_ANDROID && !UNITY_EDITOR
                return PlayGamesPlatform.Instance.IsAuthenticated();
#else
                return IsMock && (Settings == null || !Settings.simulateSignedOutInEditor);
#endif
            }
        }

        // interactive = the player pressed something (shows the sign-in UI);
        // otherwise a silent attempt. done(true) when signed in.
        public static void Authenticate(Action<bool> done, bool interactive = true)
        {
            if (IsAuthenticated)
            {
                done?.Invoke(true);
                return;
            }
#if DEADLINE_GPGS && UNITY_ANDROID && !UNITY_EDITOR
            if (authenticating)
            {
                done?.Invoke(false);
                return;
            }
            authenticating = true;
            PlayGamesPlatform.Activate();
            Action<SignInStatus> result = status =>
            {
                authenticating = false;
                bool ok = status == SignInStatus.Success;
                if (!ok)
                    Debug.Log("Google Play Games sign-in: " + status);
                done?.Invoke(ok);
            };
            if (interactive)
                PlayGamesPlatform.Instance.ManuallyAuthenticate(result);
            else
                PlayGamesPlatform.Instance.Authenticate(result);
#else
            if (Logs && interactive)
                Debug.Log("Google Play Games sign-in " + (IsMock ? "(mock) failed: simulateSignedOutInEditor" : "unavailable on this platform"));
            done?.Invoke(false);
#endif
        }

        // ---- Leaderboards -----------------------------------------------------

        // The only Mode → leaderboard mapping. Empty when not configured.
        public static string GetLeaderboardId(GameMode mode)
        {
            if (Settings == null)
                return "";
            switch (mode)
            {
                case GameMode.Deadline: return Settings.deadlineLeaderboardId ?? "";
                case GameMode.Classic: return Settings.classicLeaderboardId ?? "";
                default: return "";
            }
        }

        // Final score of one finished run. Google Play keeps each player's best
        // per leaderboard, so every final score is sent; lower ones are ignored
        // there. Skipped (logged, never thrown) for 0 points, no sign-in or a
        // missing ID.
        public static void SubmitScore(GameMode mode, long score)
        {
            if (score <= 0)
            {
                if (Logs)
                    Debug.Log("Leaderboard submit skipped: score " + score + " (" + mode + ")");
                return;
            }
            string id = GetLeaderboardId(mode);
            if (Logs)
                Debug.Log("Leaderboard Submit\nMode: " + mode + "\nScore: " + score + "\nID: " + (id.Length > 0 ? id : "(not set)"));
            if (!IsAuthenticated)
            {
                if (Logs)
                    Debug.Log("Leaderboard submit skipped: not signed in to Google Play Games");
                return;
            }
            if (id.Length == 0 && !IsMock)
            {
                Debug.LogWarning("Leaderboard submit skipped: no " + mode + " leaderboard ID in GooglePlaySettings");
                return;
            }
            ScoreSubmitted?.Invoke(mode, score, id);
#if DEADLINE_GPGS && UNITY_ANDROID && !UNITY_EDITOR
            PlayGamesPlatform.Instance.ReportScore(score, id, ok =>
            {
                if (!ok)
                    Debug.Log("Leaderboard submit failed (" + mode + ")");
            });
#endif
        }

        // Opens Google Play's own leaderboard UI for one mode. Signs in first
        // when needed; done(false) when that fails (the panel says so).
        public static void ShowLeaderboard(GameMode mode, Action<bool> done)
        {
            string id = GetLeaderboardId(mode);
            Authenticate(signedIn =>
            {
                if (!signedIn)
                {
                    done?.Invoke(false);
                    return;
                }
                if (Logs)
                    Debug.Log("Leaderboard Open\nMode: " + mode + "\nID: " + (id.Length > 0 ? id : "(not set)") +
                              (IsMock ? "\n(mock: Google Play UI opens on an Android build)" : ""));
#if DEADLINE_GPGS && UNITY_ANDROID && !UNITY_EDITOR
                if (id.Length == 0)
                {
                    Debug.LogWarning("No " + mode + " leaderboard ID in GooglePlaySettings");
                    done?.Invoke(false);
                    return;
                }
                PlayGamesPlatform.Instance.ShowLeaderboardUI(id);
#endif
                done?.Invoke(true);
            });
        }

        // ---- Achievements -------------------------------------------------------

        // Called by AchievementManager after a local unlock (already saved).
        public static void ReportAchievement(string googlePlayId)
        {
            if (string.IsNullOrEmpty(googlePlayId) || !IsAuthenticated)
                return;
            if (Logs && IsMock)
                Debug.Log("Achievement Report (mock): " + googlePlayId);
#if DEADLINE_GPGS && UNITY_ANDROID && !UNITY_EDITOR
            PlayGamesPlatform.Instance.ReportProgress(googlePlayId, 100.0, ok =>
            {
                if (!ok)
                    Debug.Log("Achievement report failed: " + googlePlayId);
            });
#endif
        }
    }
}
