using System.Collections.Generic;
using UnityEngine;

namespace Deadline4Sec
{
    // Kill window prediction (2026-11-11, first-run target markers): would the
    // swipe that counters this enemy kill it if made right now? Uses the same
    // boxes, speeds and timings as the attacks themselves. secondsUntilOpen is 0
    // while a swipe kills; ahead of the window it says how soon it opens.
    public sealed partial class PlayerController
    {
        private static readonly List<EnemyAttackZone> zoneBuffer = new List<EnemyAttackZone>();

        private float ForwardSpeedNow => runForwardSpeed >= 0f ? runForwardSpeed : forwardSpeed;

        public bool TryGetKillWindow(Enemy enemy, out float secondsUntilOpen)
        {
            secondsUntilOpen = 0f;
            if (enemy == null || enemy.IsKilled || !CanReceiveInput || characterController == null ||
                movementState == MovementState.WallRun)
                return false;
            Collider body = enemy.GetComponent<Collider>();
            if (body == null || !body.enabled)
                return false;
            Bounds target = body.bounds;
            return enemy.Type == Enemy.EnemyType.Air
                ? AirKillWindow(enemy, target, out secondsUntilOpen)
                : GroundKillWindow(enemy, target, out secondsUntilOpen);
        }

        private bool AirKillWindow(Enemy enemy, Bounds target, out float secondsUntilOpen)
        {
            secondsUntilOpen = 0f;
            if (isHomingDash)
            {
                return airDashTarget == enemy;
            }
            Bounds self = characterController.bounds;
            if (!isGrounded)
            {
                // Up in the air: an up swipe homes onto the best air target.
                if (Time.time - lastAirJumpTime < airJumpMinInterval || FindAirTarget(0, true) != enemy)
                    return false;
                return true;
            }

            // On the ground: the jump's attack box rises with the runner.
            if (Mathf.Abs(target.center.x - transform.position.x) > jumpAttackBoxSize.x * 0.5f + target.extents.x)
                return false;
            float rise = Mathf.Sqrt(jumpHeight * -2f * gravity);
            float firstTime = -1f, lastTime = -1f;
            for (float t = 0f; t <= jumpAttackDuration; t += 0.01f)
            {
                float boxY = self.center.y + rise * t + 0.5f * gravity * t * t + jumpAttackHeightOffset;
                bool overlaps = boxY + jumpAttackBoxSize.y * 0.5f >= target.min.y &&
                    boxY - jumpAttackBoxSize.y * 0.5f <= target.max.y;
                if (!overlaps)
                    continue;
                if (firstTime < 0f)
                    firstTime = t;
                lastTime = t;
            }
            if (firstTime < 0f)
                return false;
            float speed = ForwardSpeedNow;
            float near = jumpAttackForwardOffset - jumpAttackBoxSize.z * 0.5f - target.extents.z + speed * firstTime;
            // Running in under it, its down attack reaches the runner first.
            near = Mathf.Max(near, ZoneLimit(enemy, EnemyAttackZone.AttackZoneType.AirDown, target, self));
            float far = jumpAttackForwardOffset + jumpAttackBoxSize.z * 0.5f + target.extents.z + speed * lastTime;
            return InWindow(target.center.z - self.center.z, near, far, speed, out secondsUntilOpen);
        }

        private bool GroundKillWindow(Enemy enemy, Bounds target, out float secondsUntilOpen)
        {
            secondsUntilOpen = 0f;
            Bounds self = characterController.bounds;
            float speed = ForwardSpeedNow;
            float dz = target.center.z - transform.position.z;

            if (!isGrounded && self.min.y > target.max.y)
            {
                // Above it: a down swipe (fast fall or, from high up, a slam).
                bool slam = TryGetGroundDistance(out float height, out _) && height >= groundSlamMinHeight;
                float fall = slam ? groundSlamFallSpeed : fastFallSpeed;
                float drop = slam ? height : self.min.y - target.max.y;
                float landing = speed * Mathf.Max(0f, drop) / fall;
                if (slam)
                {
                    Vector2 impact = new Vector2(transform.position.x, transform.position.z + landing);
                    float reach = Vector2.Distance(impact, new Vector2(target.center.x, target.center.z));
                    return reach <= groundSlamRadius;
                }
                if (Mathf.Abs(target.center.x - transform.position.x) > target.extents.x + characterController.radius * 0.6f)
                    return false;
                float footprint = (target.extents.z + characterController.radius) * 0.8f;
                return InWindow(dz, landing - footprint, landing + footprint, speed, out secondsUntilOpen);
            }

            // Beside it: a swipe toward it attacks the next lane while moving there.
            int side = target.center.x > transform.position.x ? 1 : -1;
            if (Mathf.Abs(target.center.x - (centerX + (laneIndex + side) * laneWidth)) > laneWidth * 0.3f ||
                Mathf.Abs(laneIndex + side) > 1)
                return false;
            float attackTime = Mathf.Min(attackDuration, laneWidth * 0.9f / laneChangeSpeed);
            float nearEdge = attackForwardOffset - attackBoxSize.z * 0.5f - target.extents.z;
            float farEdge = attackForwardOffset + attackBoxSize.z * 0.5f + target.extents.z + speed * attackTime;
            // Its side attack reaches the runner first when left too late.
            nearEdge = Mathf.Max(nearEdge, ZoneLimit(enemy, EnemyAttackZone.AttackZoneType.GroundSide, target, self));
            return InWindow(dz, nearEdge, farEdge, speed, out secondsUntilOpen);
        }

        // How close (enemy centre ahead of the runner) before that attack zone,
        // where the runner is, touches the runner.
        private float ZoneLimit(Enemy enemy, EnemyAttackZone.AttackZoneType type, Bounds target, Bounds self)
        {
            float limit = float.NegativeInfinity;
            enemy.GetComponentsInChildren(zoneBuffer);
            foreach (EnemyAttackZone zone in zoneBuffer)
            {
                Collider area = zone.GetComponent<Collider>();
                if (zone.Type != type || area == null || self.min.y > area.bounds.max.y ||
                    transform.position.x < area.bounds.min.x || transform.position.x > area.bounds.max.x)
                    continue;
                limit = Mathf.Max(limit, target.center.z - area.bounds.min.z + characterController.radius + 0.15f);
            }
            return limit;
        }

        // Open, or still ahead (then: how soon). Past it: closed for good.
        private static bool InWindow(float distance, float near, float far, float speed, out float secondsUntilOpen)
        {
            secondsUntilOpen = 0f;
            // A frame or so inside each end: "now" must really be now.
            near += speed * 0.02f;
            far -= speed * 0.01f;
            if (far <= near || distance < near)
                return false;
            if (distance > far)
                secondsUntilOpen = (distance - far) / Mathf.Max(0.1f, speed);
            return true;
        }
    }
}
