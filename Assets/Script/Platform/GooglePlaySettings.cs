using UnityEngine;

namespace Deadline4Sec
{
    // Google Play Games IDs and debug switches, edited in the Inspector at
    // Resources/GooglePlay/GooglePlaySettings (leaderboard.md §2, §22–24).
    // Paste the IDs from the Play Console (or the generated GPGSIds class).
    [CreateAssetMenu(menuName = "Deadline 4 Sec/Google Play Settings", fileName = "GooglePlaySettings")]
    public sealed class GooglePlaySettings : ScriptableObject
    {
        public const string ResourcePath = "GooglePlay/GooglePlaySettings";

        [Header("Leaderboards (one per mode, never shared)")]
        public string deadlineLeaderboardId = "";
        public string classicLeaderboardId = "";

        [Header("Editor / debug")]
        [Tooltip("Logs every submit / open request with mode, score and ID.")]
        public bool showLeaderboardDebugLogs = true;
        [Tooltip("In the Editor (no Google Play), submits and opens only log instead of calling the API.")]
        public bool useMockLeaderboardInEditor = true;
        [Tooltip("Editor mock only: behave as if Google Play sign-in failed (tests the signed-out path).")]
        public bool simulateSignedOutInEditor;
    }
}
