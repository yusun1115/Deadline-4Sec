using UnityEngine;

namespace Deadline4Sec
{
    [RequireComponent(typeof(BoxCollider))]
    public sealed class EnemyAttackZone : MonoBehaviour
    {
        public enum AttackZoneType
        {
            GroundFront,
            GroundSide,
            AirDown
        }

        [SerializeField] private Enemy owner;
        [SerializeField] private AttackZoneType zoneType;
        [SerializeField, Min(0f)] private float highJumpClearance = 0.05f;
        // The front zone stands far above the enemy's head so a plain jump can't
        // clear it (spec 11.1: a jump peaks with the feet ~1.68 m up). Well above
        // that the runner is flying over it, out of an air kill or a bounce, and
        // nothing on screen touches it (2026-11-07): feet this far over the
        // zone's base are safe, ~2 m off the floor.
        [SerializeField, Min(0f)] private float frontClearAbove = 1.75f;
        // An air kill's arc is already falling as it passes a ground enemy placed
        // right behind the air enemy, with no time to answer. Only an air kill
        // gets here, so a plain jump still can't use this lower line.
        [SerializeField, Min(0f)] private float afterAirKillClearAbove = 0.5f;

        private BoxCollider zoneCollider;
        private PlayerController trackedPlayer;
        private CharacterController trackedCollider;
        private GameTimer gameTimer;
        private bool playerInside;
        private bool exitedWhilePending;
        private bool disabled;

        private void Awake()
        {
            zoneCollider = GetComponent<BoxCollider>();
            zoneCollider.isTrigger = true;
            if (owner == null)
                owner = GetComponentInParent<Enemy>();
        }

        private void OnTriggerEnter(Collider other)
        {
            TrackPlayer(other);
        }

        private void OnTriggerStay(Collider other)
        {
            TrackPlayer(other);
        }

        private void OnTriggerExit(Collider other)
        {
            PlayerController player = other.GetComponentInParent<PlayerController>();
            if (player == null || player != trackedPlayer)
                return;

            playerInside = false;
            exitedWhilePending = true;
        }

        private void TrackPlayer(Collider other)
        {
            if (disabled || owner == null || owner.IsKilled)
                return;

            PlayerController player = other.GetComponentInParent<PlayerController>();
            if (player == null)
                return;

            trackedPlayer = player;
            trackedCollider = player.GetComponent<CharacterController>();
            playerInside = true;
            exitedWhilePending = false;
            if (gameTimer == null)
                gameTimer = FindFirstObjectByType<GameTimer>();
        }

        private void LateUpdate()
        {
            if (disabled || owner == null || owner.IsKilled || trackedPlayer == null)
                return;
            if (gameTimer == null)
                gameTimer = FindFirstObjectByType<GameTimer>();
            if (gameTimer == null || !gameTimer.IsRunning)
                return;

            PowerUpManager powerUps = gameTimer.GetComponent<PowerUpManager>();
            if (powerUps != null && powerUps.ReaperRushActive)
            {
                trackedPlayer.TryReaperRushKill(owner);
                ClearTrackedPlayer();
                return;
            }

            if (IsSafelyAboveAttack())
            {
                ClearTrackedPlayer();
                return;
            }

            // Player attack checks run during PlayerController.Update. Resolving the
            // enemy attack in LateUpdate gives a valid kill first priority.
            if (trackedPlayer.IsAttackingEnemy(owner))
                return;

            if (playerInside || exitedWhilePending)
                TriggerEnemyAttack();
        }

        public void DisableImmediately()
        {
            disabled = true;
            playerInside = false;
            exitedWhilePending = false;
            trackedPlayer = null;
            trackedCollider = null;
            if (zoneCollider == null)
                zoneCollider = GetComponent<BoxCollider>();
            if (zoneCollider != null)
                zoneCollider.enabled = false;
        }

        private bool IsSafelyAboveAttack()
        {
            if (trackedCollider == null || zoneCollider == null)
                return false;

            float feet = trackedCollider.bounds.min.y;
            if (zoneType == AttackZoneType.GroundSide)
                return feet >= zoneCollider.bounds.max.y + highJumpClearance;
            if (zoneType == AttackZoneType.GroundFront)
                return feet >= zoneCollider.bounds.min.y +
                    (trackedPlayer != null && trackedPlayer.IsAirComboReady
                        ? afterAirKillClearAbove
                        : frontClearAbove);
            return false;
        }

        private void TriggerEnemyAttack()
        {
            string reason = zoneType == AttackZoneType.GroundFront
                ? "Game Over: Ground Enemy Front Attack"
                : zoneType == AttackZoneType.GroundSide
                    ? "Game Over: Ground Enemy Side Attack"
                    : "Game Over: Air Enemy Down Attack";
                        Debug.Log(reason);
            gameTimer.TriggerFatalContactFrom(zoneCollider);
            ClearTrackedPlayer();
        }

        private void ClearTrackedPlayer()
        {
            playerInside = false;
            exitedWhilePending = false;
            trackedPlayer = null;
            trackedCollider = null;
        }

        private void OnValidate()
        {
            BoxCollider box = GetComponent<BoxCollider>();
            if (box != null)
                box.isTrigger = true;
            if (owner == null)
                owner = GetComponentInParent<Enemy>();
            highJumpClearance = Mathf.Max(0f, highJumpClearance);
            frontClearAbove = Mathf.Max(0f, frontClearAbove);
            afterAirKillClearAbove = Mathf.Max(0f, afterAirKillClearAbove);
        }

        private void OnDrawGizmosSelected()
        {
            BoxCollider box = GetComponent<BoxCollider>();
            if (box == null)
                return;

            Gizmos.color = zoneType == AttackZoneType.GroundFront
                ? new Color(1f, 0.15f, 0.1f, 0.9f)
                : zoneType == AttackZoneType.GroundSide
                    ? new Color(1f, 0.55f, 0.05f, 0.9f)
                    : new Color(0.15f, 0.75f, 1f, 0.9f);
            Gizmos.matrix = box.transform.localToWorldMatrix;
            Gizmos.DrawWireCube(box.center, box.size);
            Gizmos.matrix = Matrix4x4.identity;
        }
    }
}
