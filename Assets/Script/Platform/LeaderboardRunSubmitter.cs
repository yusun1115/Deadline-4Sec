using UnityEngine;
using UnityEngine.SceneManagement;

namespace Deadline4Sec
{
    // Sends each finished run's final score to its mode's leaderboard.
    // Listens to GameFlowManager.RunFinished, which fires once after the revive
    // offer is declined (or used up) and the local best is saved — so a revived
    // run sends only its final score and the first death sends nothing. The
    // run's mode travels in the RunRecord (GameModeState.CurrentRunMode), not
    // the title selection. hasSubmittedLeaderboardScore allows one submit per
    // run: it is re-armed only while a run is in progress.
    // Added to the GameFlowManager object at runtime, like MusicDirector.
    public sealed class LeaderboardRunSubmitter : MonoBehaviour
    {
        private GameFlowManager flow;
        private bool hasSubmittedLeaderboardScore = true;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AttachToGameFlow()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
            OnSceneLoaded(default, default);
        }

        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            GameFlowManager flow = FindFirstObjectByType<GameFlowManager>();
            if (flow != null && flow.GetComponent<LeaderboardRunSubmitter>() == null)
                flow.gameObject.AddComponent<LeaderboardRunSubmitter>();
        }

        private void Awake() => flow = GetComponent<GameFlowManager>();
        private void OnEnable() => GameFlowManager.RunFinished += OnRunFinished;
        private void OnDisable() => GameFlowManager.RunFinished -= OnRunFinished;

        private void Update()
        {
            // A run in progress (re-)arms the submit; the result screens do not.
            // Dying always lasts some frames before the result, so a run is
            // armed by the time RunFinished fires.
            if (!hasSubmittedLeaderboardScore || flow == null)
                return;
            GameFlowManager.GameState state = flow.State;
            if (state == GameFlowManager.GameState.Ready || state == GameFlowManager.GameState.Playing ||
                state == GameFlowManager.GameState.Dying || state == GameFlowManager.GameState.RevivePrompt)
                hasSubmittedLeaderboardScore = false;
        }

        private void OnRunFinished(RunRecord record)
        {
            if (hasSubmittedLeaderboardScore)
                return;
            hasSubmittedLeaderboardScore = true;
            GooglePlayService.SubmitScore(record.Mode, record.Score);
        }
    }
}
