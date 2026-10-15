using UnityEngine;
using UnityEngine.SceneManagement;

namespace Deadline4Sec
{
    // Forwards the gameplay events that already exist to PlayerStats, so no
    // gameplay script needs to know about stats for these: kills (PlayerController
    // .EnemyKilled, after Enemy.TryKill — one kill counts once), Ground Slam
    // landings (SlamResolved), Near Miss (RunManager.NearMissRecorded, once per
    // obstacle), timer resets (GameTimer.TimerReset, with the value before the
    // reset), Soul pickups (CoinWallet run total), gift box pickups and the end
    // of the run (GameFlowManager.RunFinished). It also samples the live run
    // values (time, combo, distance, score) and ends the air chain on landing.
    // Added to the GameFlowManager object at runtime, like MusicDirector.
    public sealed class PlayerStatsRecorder : MonoBehaviour
    {
        private PlayerController player;
        private RunManager run;
        private GameTimer timer;
        private CoinWallet wallet;
        private int lastRunCoins;

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
            if (flow != null && flow.GetComponent<PlayerStatsRecorder>() == null)
                flow.gameObject.AddComponent<PlayerStatsRecorder>();
        }

        private void Awake()
        {
            AchievementManager.EnsureInitialized();
            player = FindFirstObjectByType<PlayerController>();
            run = FindFirstObjectByType<RunManager>();
            timer = TryGetComponent(out GameTimer own) ? own : FindFirstObjectByType<GameTimer>();
            wallet = TryGetComponent(out CoinWallet ownWallet) ? ownWallet : FindFirstObjectByType<CoinWallet>();
        }

        private void OnEnable()
        {
            if (player != null)
            {
                player.EnemyKilled += OnEnemyKilled;
                player.SlamResolved += OnSlamResolved;
            }
            if (run != null)
                run.NearMissRecorded += OnNearMiss;
            if (timer != null)
                timer.TimerReset += OnTimerReset;
            if (wallet != null)
                wallet.Changed += OnWalletChanged;
            GiftBoxPickup.Collected += OnGiftBoxCollected;
            GameFlowManager.RunFinished += OnRunFinished;
        }

        private void OnDisable()
        {
            if (player != null)
            {
                player.EnemyKilled -= OnEnemyKilled;
                player.SlamResolved -= OnSlamResolved;
            }
            if (run != null)
                run.NearMissRecorded -= OnNearMiss;
            if (timer != null)
                timer.TimerReset -= OnTimerReset;
            if (wallet != null)
                wallet.Changed -= OnWalletChanged;
            GiftBoxPickup.Collected -= OnGiftBoxCollected;
            GameFlowManager.RunFinished -= OnRunFinished;
            PlayerStats.Save();
        }

        private void Update()
        {
            if (!PlayerStats.RunActive)
                return;
            if (player != null && player.IsGrounded)
                PlayerStats.RecordLanded();
            if (run != null)
                PlayerStats.UpdateRun(run.CurrentRunTime, run.Distance, run.CurrentScore, run.BestCombo);
        }

        private void OnApplicationPause(bool paused)
        {
            if (paused)
                PlayerStats.Save();
        }

        private void OnApplicationQuit() => PlayerStats.Save();

        private static void OnEnemyKilled(Enemy enemy, string attackName) =>
            PlayerStats.RecordEnemyKill(enemy != null && enemy.Type == Enemy.EnemyType.Air,
                attackName == "Homing Dash Attack", attackName == "Stomp Attack");

        private static void OnSlamResolved(int killedCount) => PlayerStats.RecordGroundSlam(killedCount);
        private static void OnNearMiss() => PlayerStats.RecordNearMiss();
        private static void OnTimerReset(float remainingBefore) => PlayerStats.RecordTimerReset(remainingBefore);
        private static void OnGiftBoxCollected(Vector3 position) => PlayerStats.RecordGiftBoxCollected();

        // The wallet's run total only grows from pickups and returns to 0 at
        // the start of a run; spending touches TotalCoins, not this.
        private void OnWalletChanged()
        {
            int now = wallet.CurrentRunCoins;
            if (now > lastRunCoins)
                PlayerStats.RecordCoins(now - lastRunCoins);
            lastRunCoins = now;
        }

        private static void OnRunFinished(RunRecord record) =>
            PlayerStats.EndRun(record.Mode, record.Score, record.Distance, record.BestCombo, record.RunTime);
    }
}
