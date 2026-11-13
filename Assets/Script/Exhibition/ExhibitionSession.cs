namespace Deadline4Sec
{
    // The visitor at the device right now. Memory only: it lives across the
    // scene reloads of Retry, and an app restart is always a new visitor.
    // Separate from everything saved (the ranking file, PlayerPrefs).
    public static class ExhibitionSession
    {
        public static string CurrentNickname { get; private set; }
        public static bool HasNickname => !string.IsNullOrEmpty(CurrentNickname);
        public static bool TutorialCompleted { get; private set; }
        public static bool ReviveUsed { get; private set; }
        public static int RetryCount { get; private set; }
        public static bool SessionActive { get; private set; }
        // The score last registered (shown highlighted on the ranking).
        public static int LastScore { get; private set; }

        public static void Begin() => SessionActive = true;
        public static void CompleteTutorial() => TutorialCompleted = true;
        public static void UseRevive() => ReviveUsed = true;

        public static void SetNickname(string nickname, int score)
        {
            CurrentNickname = nickname;
            LastScore = score;
        }

        public static void RecordScore(int score) => LastScore = score;

        // RETRY: same visitor, a new run.
        public static void NextRun()
        {
            RetryCount++;
            ReviveUsed = false;
        }

        // A run starting (first one or a retry): one free revive again.
        public static void BeginRun() => ReviveUsed = false;

        // FINISH (manual or automatic): the next visitor starts from nothing.
        // The ranking file is never touched here.
        public static void ResetExhibitionSession()
        {
            CurrentNickname = null;
            TutorialCompleted = false;
            ReviveUsed = false;
            RetryCount = 0;
            LastScore = 0;
            SessionActive = false;
            GameModeState.Select(GameMode.Deadline); // the next visitor starts on Deadline
        }
    }
}
