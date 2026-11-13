using System.Collections.Generic;
using UnityEngine;

namespace Deadline4Sec
{
    [RequireComponent(typeof(BoxCollider))]
    public sealed class Obstacle : MonoBehaviour
    {
        public static readonly HashSet<Obstacle> Active = new HashSet<Obstacle>();

        // Revive invincibility (2026-11-14): obstacles pass through instead of
        // physically stopping the runner (RunInventory.CollisionProtected already
        // forgives the death; without this the CharacterController still
        // collided and the runner stood pressed against the obstacle). Toggled
        // by ReviveInvincibility; a new obstacle spawned mid-window starts
        // passable too.
        private static bool phasing;

        public static bool Phasing
        {
            get => phasing;
            set
            {
                if (phasing == value)
                    return;
                phasing = value;
                foreach (Obstacle obstacle in Active)
                    if (obstacle != null && obstacle.hazard != null)
                        obstacle.hazard.enabled = !value;
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetPhasing() => phasing = false;
        // Labels for arranging test cubes. Avoidance is decided only by collision.
        // RampTrain: a tall train with a ramp in front; run up the ramp onto the roof.
        public enum ObstacleType { LaneBlocker, JumpObstacle, SlideObstacle, RampTrain }

        [SerializeField] private ObstacleType obstacleType;
        public ObstacleType Type => obstacleType;
        [Header("Near miss zone (extra space outside the solid collider)")]
        [SerializeField] private Vector3 nearMissMargin = new Vector3(0.95f, 0.65f, 1.25f);
        // A Near Miss is a last-moment escape (2026-11-01): the runner must have
        // been heading straight into the solid box (overlapping it sideways and
        // in height) within this distance of its front, then got clear of it.
        // Just running past in another lane, or dodging early, never counts.
        [Tooltip("How close (m, runner front to obstacle front) the runner must still be on a collision course.")]
        [SerializeField, Min(0f)] private float lastMomentDistance = 2.6f;

        private BoxCollider hazard;
        private CharacterController playerCollider;
        private GameTimer gameTimer;
        private RunManager runManager;
        private CameraFeedbackController cameraFeedback;
        private Bounds previousPlayerBounds;
        private bool hasPreviousBounds;
        private bool wasNearWhilePassing;
        private bool wasHeadingInto;
        private bool physicallyTouched;
        private bool resolved;

        private void OnEnable() => Active.Add(this);
        private void OnDisable() => Active.Remove(this);

        private void Awake()
        {
            hazard = GetComponent<BoxCollider>();
            hazard.enabled = !phasing;
            FindSceneReferences();
        }

        private void LateUpdate()
        {
            if (resolved)
                return;
            if (playerCollider == null || gameTimer == null)
                FindSceneReferences();
            if (playerCollider == null || gameTimer == null)
                return;

            Bounds current = playerCollider.bounds;
            if (!hasPreviousBounds)
            {
                previousPlayerBounds = current;
                hasPreviousBounds = true;
            }

            if (gameTimer.IsGameOver)
            {
                // A candidate that ended in a collision or timeout cannot later earn a reset.
                if (wasNearWhilePassing || physicallyTouched)
                    resolved = true;
                previousPlayerBounds = current;
                return;
            }

            Bounds obstacleBounds = hazard.bounds;
            if (!physicallyTouched && CrossedNearZone(previousPlayerBounds, current, obstacleBounds))
                wasNearWhilePassing = true;
            if (!wasHeadingInto && HeadingInto(current, obstacleBounds))
                wasHeadingInto = true;

            // Forward movement is +Z. The whole player must clear the obstacle.
            if (current.min.z > obstacleBounds.max.z)
            {
                resolved = true;
                if (wasNearWhilePassing && wasHeadingInto && !physicallyTouched)
                {
                    Debug.Log("NEAR MISS! [" + name + "]");
                    gameTimer.ResetTimer();
                    Debug.Log("Timer Reset by Near Miss");
                    if (runManager == null)
                        runManager = FindFirstObjectByType<RunManager>();
                    if (runManager != null)
                        runManager.RecordNearMiss();
                    if (cameraFeedback != null)
                        cameraFeedback.PlayNearMiss(current.center, obstacleBounds.center);
                }
            }

            previousPlayerBounds = current;
        }

        public void RegisterPhysicalContact()
        {
            physicallyTouched = true;
        }

        private void FindSceneReferences()
        {
            PlayerController player = FindFirstObjectByType<PlayerController>();
            if (player != null)
                playerCollider = player.GetComponent<CharacterController>();
            if (gameTimer == null)
                gameTimer = FindFirstObjectByType<GameTimer>();
            if (runManager == null)
                runManager = FindFirstObjectByType<RunManager>();
            if (cameraFeedback == null)
                cameraFeedback = FindFirstObjectByType<CameraFeedbackController>();
            if (playerCollider != null)
            {
                previousPlayerBounds = playerCollider.bounds;
                hasPreviousBounds = true;
            }
        }

        // Still in front of the box, close to it, and lined up to hit it.
        private bool HeadingInto(Bounds player, Bounds obstacleBounds)
        {
            float gap = obstacleBounds.min.z - player.max.z;
            return gap <= lastMomentDistance && player.min.z <= obstacleBounds.min.z &&
                player.min.x < obstacleBounds.max.x && player.max.x > obstacleBounds.min.x &&
                player.min.y < obstacleBounds.max.y && player.max.y > obstacleBounds.min.y;
        }

        private bool CrossedNearZone(Bounds before, Bounds after, Bounds obstacleBounds)
        {
            Vector3 start = before.center;
            Vector3 movement = after.center - start;
            if (movement.z < 0f)
                return false;

            Vector3 playerExtents = Vector3.Max(before.extents, after.extents);
            float enter = 0f;
            float exit = 1f;
            return ClipAxis(start.z, movement.z,
                       obstacleBounds.min.z - nearMissMargin.z - playerExtents.z,
                       obstacleBounds.max.z + nearMissMargin.z + playerExtents.z,
                       ref enter, ref exit) &&
                   ClipAxis(start.x, movement.x,
                       obstacleBounds.min.x - nearMissMargin.x - playerExtents.x,
                       obstacleBounds.max.x + nearMissMargin.x + playerExtents.x,
                       ref enter, ref exit) &&
                   ClipAxis(start.y, movement.y,
                       obstacleBounds.min.y - nearMissMargin.y - playerExtents.y,
                       obstacleBounds.max.y + nearMissMargin.y + playerExtents.y,
                       ref enter, ref exit);
        }

        private static bool ClipAxis(float start, float movement, float min, float max,
            ref float enter, ref float exit)
        {
            if (Mathf.Abs(movement) < 0.0001f)
                return start >= min && start <= max;

            float first = (min - start) / movement;
            float last = (max - start) / movement;
            if (first > last)
            {
                float swap = first;
                first = last;
                last = swap;
            }
            enter = Mathf.Max(enter, first);
            exit = Mathf.Min(exit, last);
            return enter <= exit;
        }

        private void OnValidate()
        {
            nearMissMargin.x = Mathf.Max(0f, nearMissMargin.x);
            nearMissMargin.y = Mathf.Max(0f, nearMissMargin.y);
            nearMissMargin.z = Mathf.Max(0f, nearMissMargin.z);
        }

        private void OnDrawGizmosSelected()
        {
            BoxCollider hazard = GetComponent<BoxCollider>();
            if (hazard == null)
                return;

            Bounds nearZone = hazard.bounds;
            nearZone.Expand(new Vector3(nearMissMargin.x * 2f,
                nearMissMargin.y * 2f, nearMissMargin.z * 2f));
            Gizmos.color = new Color(1f, 0f, 1f, 0.8f);
            Gizmos.DrawWireCube(nearZone.center, nearZone.size);

            Gizmos.color = obstacleType == ObstacleType.LaneBlocker ? Color.red :
                obstacleType == ObstacleType.JumpObstacle ? Color.yellow :
                obstacleType == ObstacleType.RampTrain ? new Color(1f, 0.5f, 0f) : Color.cyan;
            Gizmos.matrix = hazard.transform.localToWorldMatrix;
            Gizmos.DrawWireCube(hazard.center, hazard.size);
            Gizmos.matrix = Matrix4x4.identity;
        }
    }
}
