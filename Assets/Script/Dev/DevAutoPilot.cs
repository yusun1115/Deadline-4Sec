#if UNITY_EDITOR
using UnityEngine;

namespace Deadline4Sec.Dev
{
    // Editor-only heuristic player used for visual QA screenshots. It feeds the
    // same Request* entry points as touch input. Not part of builds or tests.
    public sealed class DevAutoPilot : MonoBehaviour
    {
        public bool keepTimerAlive = true;
        public float laneWidth = 2.5f;

        private PlayerController player;
        private GameTimer timer;
        private GameFlowManager flow;
        private float centerX;
        private float nextActionTime;

        private void Start()
        {
            player = FindFirstObjectByType<PlayerController>();
            timer = FindFirstObjectByType<GameTimer>();
            flow = FindFirstObjectByType<GameFlowManager>();
            centerX = player.transform.position.x - player.LaneIndex * laneWidth;
        }

        private void Update()
        {
            if (player == null || flow == null || !flow.CanReceiveInput)
                return;
            if (keepTimerAlive && timer != null && timer.IsRunning && timer.RemainingTime < 1.2f)
                timer.ResetTimer();
            if (Time.time < nextActionTime)
                return;

            Vector3 p = player.transform.position;
            int lane = player.LaneIndex;

            // Air enemies: jump then home in.
            foreach (Enemy e in FindObjectsByType<Enemy>(FindObjectsSortMode.None))
            {
                if (e.IsKilled || e.Type != Enemy.EnemyType.Air)
                    continue;
                float dz = e.transform.position.z - p.z;
                if (dz < 0f || dz > 9f)
                    continue;
                if (player.IsGrounded && dz < 6.5f)
                {
                    Act(player.RequestJump, 0.15f);
                    return;
                }
                if (!player.IsGrounded && !player.IsHoming && dz < 8f)
                {
                    Act(player.RequestJump, 0.2f);
                    return;
                }
            }

            // Ground enemies: lane attack from the side; sidestep if dead ahead.
            foreach (Enemy e in FindObjectsByType<Enemy>(FindObjectsSortMode.None))
            {
                if (e.IsKilled || e.Type != Enemy.EnemyType.Ground)
                    continue;
                float dz = e.transform.position.z - p.z;
                if (dz < -0.5f || dz > 9f)
                    continue;
                int enemyLane = Mathf.RoundToInt((e.transform.position.x - centerX) / laneWidth);
                if (enemyLane == lane && dz > 2.5f && player.IsGrounded)
                {
                    int dodge = lane <= 0 ? lane + 1 : lane - 1;
                    Move(dodge - lane);
                    return;
                }
                if (Mathf.Abs(enemyLane - lane) == 1 && dz < 2.8f)
                {
                    Move(enemyLane - lane);
                    return;
                }
            }

            // Obstacles in the current lane.
            foreach (Obstacle o in Obstacle.Active)
            {
                Bounds b = o.GetComponent<Collider>().bounds;
                float dz = b.min.z - p.z;
                if (dz < 0f || dz > 4.5f || Mathf.Abs(b.center.x - p.x) > b.extents.x + 0.5f)
                    continue;
                if (b.min.y > 0.6f)
                {
                    // Airborne Down is a fast fall; repeat quickly so the slide follows the landing.
                    if (player.IsSliding)
                        return;
                    Act(player.RequestSlide, player.IsGrounded ? 0.3f : 0.03f);
                }
                else if (b.max.y < 1.6f)
                {
                    if (dz < 3.2f && player.IsGrounded)
                        Act(player.RequestJump, 0.3f);
                }
                else
                    Move(lane <= 0 ? 1 : -1);
                return;
            }
        }

        private void Move(int direction)
        {
            if (direction > 0)
                Act(player.RequestMoveRight, 0.12f);
            else if (direction < 0)
                Act(player.RequestMoveLeft, 0.12f);
        }

        private void Act(System.Action action, float cooldown)
        {
            action();
            nextActionTime = Time.time + cooldown;
        }
    }
}
#endif
