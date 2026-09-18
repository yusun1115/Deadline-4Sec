using UnityEngine;

namespace Deadline4Sec
{
    [RequireComponent(typeof(BoxCollider))]
    public sealed class Obstacle : MonoBehaviour
    {
        // Labels for arranging test cubes. Avoidance is decided only by collision.
        public enum ObstacleType { LaneBlocker, JumpObstacle, SlideObstacle }

        [SerializeField] private ObstacleType obstacleType;
        [Header("Near miss zone (extra space outside the solid collider)")]
        [SerializeField] private Vector2 nearMissMargin = new Vector2(0.95f, 0.65f);

        private BoxCollider hazard;
        private CharacterController playerCollider;
        private GameTimer gameTimer;
        private RunManager runManager;
        private Bounds previousPlayerBounds;
        private bool hasPreviousBounds;
        private bool wasNearWhilePassing;
        private bool physicallyTouched;
        private bool resolved;

        private static Obstacle debugMessageOwner;
        private static float debugMessageUntil;

        private void Awake()
        {
            hazard = GetComponent<BoxCollider>();
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

            // Forward movement is +Z. The whole player must clear the obstacle.
            if (current.min.z > obstacleBounds.max.z)
            {
                resolved = true;
                if (wasNearWhilePassing && !physicallyTouched)
                {
                    Debug.Log("NEAR MISS! [" + name + "]");
                    gameTimer.ResetTimer();
                    Debug.Log("Timer Reset by Near Miss");
                    if (runManager == null)
                        runManager = FindFirstObjectByType<RunManager>();
                    if (runManager != null)
                        runManager.RecordNearMiss();
                    debugMessageOwner = this;
                    debugMessageUntil = Time.unscaledTime + 0.75f;
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
            if (playerCollider != null)
            {
                previousPlayerBounds = playerCollider.bounds;
                hasPreviousBounds = true;
            }
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
            return ClipAxis(start.z, movement.z, obstacleBounds.min.z, obstacleBounds.max.z,
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

        private void OnGUI()
        {
            if (debugMessageOwner != this || Time.unscaledTime >= debugMessageUntil ||
                (gameTimer != null && gameTimer.IsGameOver))
                return;

            GUIStyle style = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 26,
                fontStyle = FontStyle.Bold
            };
            GUI.Label(new Rect(Screen.width * 0.5f - 140f, Screen.height * 0.2f,
                280f, 50f), "NEAR MISS!", style);
        }

        private void OnValidate()
        {
            nearMissMargin.x = Mathf.Max(0f, nearMissMargin.x);
            nearMissMargin.y = Mathf.Max(0f, nearMissMargin.y);
        }

        private void OnDrawGizmosSelected()
        {
            BoxCollider hazard = GetComponent<BoxCollider>();
            if (hazard == null)
                return;

            Bounds nearZone = hazard.bounds;
            nearZone.Expand(new Vector3(nearMissMargin.x * 2f,
                nearMissMargin.y * 2f, 0f));
            Gizmos.color = new Color(1f, 0f, 1f, 0.8f);
            Gizmos.DrawWireCube(nearZone.center, nearZone.size);

            Gizmos.color = obstacleType == ObstacleType.LaneBlocker ? Color.red :
                obstacleType == ObstacleType.JumpObstacle ? Color.yellow : Color.cyan;
            Gizmos.matrix = hazard.transform.localToWorldMatrix;
            Gizmos.DrawWireCube(hazard.center, hazard.size);
            Gizmos.matrix = Matrix4x4.identity;
        }
    }
}
