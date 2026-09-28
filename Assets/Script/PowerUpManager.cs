using System;
using System.Collections.Generic;
using UnityEngine;

namespace Deadline4Sec
{
    [DefaultExecutionOrder(-100)]
    public sealed class PowerUpManager : MonoBehaviour
    {
        private const string LevelKeyPrefix = "ItemLevel_";
        [SerializeField] private PowerUpBalance balance;
        [SerializeField] private GameTimer gameTimer;
        [SerializeField] private CoinWallet wallet;
        [SerializeField] private PlayerController player;

        private readonly float[] remaining = new float[6];
        private readonly HashSet<Collider> ignoredObstacles = new HashSet<Collider>();
        private readonly List<CoinPickup> nearbyCoins = new List<CoinPickup>();
        private CharacterController playerCollider;
        public event Action EffectsChanged;
        public PowerUpBalance Balance => balance;
        public bool TimerFrozen => IsActive(PowerUpType.FreezeClock);
        public bool ComboSealed => IsActive(PowerUpType.ComboSeal);
        public bool ReaperRushActive => IsActive(PowerUpType.ReaperRush);
        public int ScoreMultiplier => IsActive(PowerUpType.SoulAmplifier) ? 2 : 1;
        public float SpeedMultiplier => ReaperRushActive && balance != null
            ? balance.ReaperRushSpeedMultiplier : 1f;

        private void Awake()
        {
            if (balance == null)
                balance = Resources.Load<PowerUpBalance>("PowerUps/PowerUpBalance");
            if (gameTimer == null)
                gameTimer = GetComponent<GameTimer>();
            if (wallet == null)
                wallet = GetComponent<CoinWallet>();
            if (player == null)
                player = FindFirstObjectByType<PlayerController>();
            if (player != null)
                playerCollider = player.GetComponent<CharacterController>();
            if (balance == null)
                Debug.LogError("Power-Up Balance asset is missing.", this);
        }

        private void Update()
        {
            if (gameTimer == null || !gameTimer.IsRunning)
                return;
            bool changed = false;
            for (int i = 0; i < remaining.Length; i++)
            {
                if (remaining[i] <= 0f)
                    continue;
                remaining[i] = Mathf.Max(0f, remaining[i] - Time.deltaTime);
                if (remaining[i] <= 0f)
                {
                    EndEffect((PowerUpType)i);
                    changed = true;
                }
            }
            if (ReaperRushActive)
                IgnoreCurrentObstacles();
            if (IsActive(PowerUpType.SoulMagnet))
                PullNearbyCoins();
            if (changed)
                EffectsChanged?.Invoke();
        }

        public bool IsActive(PowerUpType type) => remaining[(int)type] > 0f;
        public float Remaining(PowerUpType type) => remaining[(int)type];
        public int GetLevel(PowerUpType type) =>
            Mathf.Clamp(PlayerPrefs.GetInt(LevelKeyPrefix + type, 1), 1, 7);

        public bool TryUpgrade(PowerUpType type)
        {
            if (balance == null || wallet == null)
                return false;
            int level = GetLevel(type);
            if (level >= 7)
                return false;
            int cost = balance.UpgradeCost(level);
            if (!wallet.TrySpend(cost))
            {
                Debug.Log("Not Enough Coins");
                return false;
            }
            PlayerPrefs.SetInt(LevelKeyPrefix + type, level + 1);
            PlayerPrefs.Save();
            return true;
        }

        public void Activate(PowerUpType type)
        {
            if (balance == null || gameTimer == null || !gameTimer.IsRunning)
                return;
            remaining[(int)type] = balance.Duration(type, GetLevel(type));
            if (type == PowerUpType.TimeHeart)
                gameTimer.SetTemporaryMaximum(balance.TimeHeartMaximum(GetLevel(type)), true);
            if (type == PowerUpType.ReaperRush)
                IgnoreCurrentObstacles();
            Debug.Log("Power-Up: " + type + " (" + remaining[(int)type].ToString("0.0") + "s)");
            EffectsChanged?.Invoke();
        }

        public void ResetRunEffects()
        {
            for (int i = 0; i < remaining.Length; i++)
            {
                if (remaining[i] > 0f)
                    EndEffect((PowerUpType)i);
                remaining[i] = 0f;
            }
            RestoreObstacleCollisions();
            EffectsChanged?.Invoke();
        }

        private void EndEffect(PowerUpType type)
        {
            if (type == PowerUpType.TimeHeart && gameTimer != null)
                gameTimer.SetTemporaryMaximum(GameTimer.BaseDuration, false);
            if (type == PowerUpType.ReaperRush)
                RestoreObstacleCollisions();
        }

        private void IgnoreCurrentObstacles()
        {
            if (playerCollider == null)
                return;
            foreach (Obstacle obstacle in Obstacle.Active)
            {
                if (obstacle == null)
                    continue;
                Collider body = obstacle.GetComponent<Collider>();
                if (body != null && body.enabled && ignoredObstacles.Add(body))
                    Physics.IgnoreCollision(playerCollider, body, true);
            }
        }

        private void RestoreObstacleCollisions()
        {
            if (playerCollider != null)
                foreach (Collider body in ignoredObstacles)
                    if (body != null)
                        Physics.IgnoreCollision(playerCollider, body, false);
            ignoredObstacles.Clear();
        }

        private void PullNearbyCoins()
        {
            if (player == null || balance == null)
                return;
            Vector3 target = player.GetComponent<CharacterController>().bounds.center;
            float radiusSquared = balance.MagnetRadius * balance.MagnetRadius;
            nearbyCoins.Clear();
            nearbyCoins.AddRange(CoinPickup.Active);
            foreach (CoinPickup coin in nearbyCoins)
            {
                if (coin == null || (coin.transform.position - target).sqrMagnitude > radiusSquared)
                    continue;
                coin.PullTowards(target, balance.MagnetPullSpeed * Time.deltaTime, wallet);
            }
        }

        private void OnDisable() => ResetRunEffects();
    }
}
