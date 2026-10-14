using System;
using UnityEngine;

namespace Deadline4Sec
{
    public enum GameMode
    {
        Deadline,
        Classic,
    }

    // The one place the game mode lives (ClassicMode.md, 2026-10-10).
    //  - CurrentGameMode: what the main menu has selected. Deadline by default;
    //    kept across scene reloads (Retry / Main Menu) for the session.
    //  - CurrentRunMode: locked when a run starts, so Game Over, Revive and the
    //    Result never lose which rules the run was played under.
    // Systems read these instead of PlayerPrefs. Only the 4-second Survival
    // Timer, the timer power-ups and best-record keys differ between modes.
    public static class GameModeState
    {
        public static GameMode CurrentGameMode { get; private set; } = GameMode.Deadline;
        public static GameMode CurrentRunMode { get; private set; } = GameMode.Deadline;

        public static bool IsDeadlineMode => CurrentGameMode == GameMode.Deadline;
        public static bool IsClassicMode => CurrentGameMode == GameMode.Classic;
        public static bool IsClassicRun => CurrentRunMode == GameMode.Classic;

        public static event Action<GameMode> Changed;

        // Every launch (and every Play press in the editor) starts on Deadline.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetForSession()
        {
            CurrentGameMode = GameMode.Deadline;
            CurrentRunMode = GameMode.Deadline;
            Changed = null;
        }

        public static void Select(GameMode mode)
        {
            if (CurrentGameMode == mode)
                return;
            CurrentGameMode = mode;
            Changed?.Invoke(mode);
        }

        // Called by GameFlowManager when a run (or the tutorial) begins.
        public static void LockRun(GameMode mode) => CurrentRunMode = mode;

        // Power-ups that only exist to manipulate the Survival Timer.
        public static bool IsTimerPowerUp(PowerUpType type) =>
            type == PowerUpType.FreezeClock || type == PowerUpType.TimeHeart;

        public static string DisplayName(GameMode mode) => mode == GameMode.Classic ? "CLASSIC" : "DEADLINE";
    }

    // Best-record PlayerPrefs keys per mode. Deadline keeps the original keys,
    // so existing saves (and the PC persistence probe) are Deadline records with
    // no migration; Classic has its own keys and never touches Deadline's.
    public static class RecordKeys
    {
        private const string Score = "Deadline4Sec.BestScore";
        private const string Distance = "Deadline4Sec.BestDistance";
        private const string Combo = "Deadline4Sec.BestCombo";

        public static string BestScore(GameMode mode) => mode == GameMode.Classic ? Score + "_Classic" : Score;
        public static string BestDistance(GameMode mode) => mode == GameMode.Classic ? Distance + "_Classic" : Distance;
        public static string BestCombo(GameMode mode) => mode == GameMode.Classic ? Combo + "_Classic" : Combo;
    }

    // One finished run, raised once by GameFlowManager.RunFinished after the
    // records are saved. Future leaderboard / achievement code subscribes here
    // and branches on Mode (e.g. Leaderboard_DeadlineScore vs ClassicScore).
    public readonly struct RunRecord
    {
        public readonly GameMode Mode;
        public readonly int Score;
        public readonly float Distance;
        public readonly int BestCombo;
        public readonly float RunTime;
        public readonly bool NewBestScore;
        public readonly bool NewBestDistance;

        public RunRecord(GameMode mode, int score, float distance, int bestCombo, float runTime,
            bool newBestScore, bool newBestDistance)
        {
            Mode = mode;
            Score = score;
            Distance = distance;
            BestCombo = bestCombo;
            RunTime = runTime;
            NewBestScore = newBestScore;
            NewBestDistance = newBestDistance;
        }
    }
}
