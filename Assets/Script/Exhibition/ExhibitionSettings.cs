using UnityEngine;

namespace Deadline4Sec
{
    // Exhibition build (ExhibitionRanking.md, 2026-11-13): an environment mode,
    // separate from GameMode (Deadline / Classic). On: every visitor is forced
    // through the tutorial, plays Deadline with one free revive per run, enters
    // a nickname and lands on the local ranking; FINISH hands the device to the
    // next visitor. The title, upgrades, items, souls and skins are the normal
    // game's (2026-11-13). Off (the default): the game is exactly as before.
    // Switched with Deadline 4 Sec/Exhibition/... (this asset, in Resources).
    [CreateAssetMenu(menuName = "Deadline 4 Sec/Exhibition Settings", fileName = "ExhibitionSettings")]
    public sealed class ExhibitionSettings : ScriptableObject
    {
        public const string ResourcePath = "Exhibition/ExhibitionSettings";

        [Tooltip("The exhibition flow. Off for store builds.")]
        public bool exhibitionMode;

        [Header("Ranking")]
        [Min(10)] public int keepEntries = 100;
        [Min(1)] public int shownEntries = 10;

        [Header("Nickname")]
        [Min(1)] public int nicknameMinLength = 2;
        [Min(1)] public int nicknameMaxLength = 8;
        [Tooltip("Rejected when the nickname contains one of these (case-insensitive).")]
        public string[] bannedWords = new string[0];

        [Header("Session")]
        [Tooltip("The ranking screen finishes the session by itself after this long.")]
        [Min(5f)] public float autoFinishIdleSeconds = 45f;
        [Tooltip("ONE MORE CHANCE waits this long, then the run ends.")]
        [Min(3f)] public float revivePromptSeconds = 10f;

        // Admin (Windows exhibition): Ctrl + Shift + Delete opens the reset panel.
    }
}
