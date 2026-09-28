using UnityEngine;

namespace Deadline4Sec
{
    public sealed class Enemy : MonoBehaviour
    {
        public enum EnemyType { Ground, Air }

        [SerializeField] private EnemyType enemyType = EnemyType.Ground;
        [SerializeField, Min(1f)] private float stompDetectionWidthMultiplier = 1.8f;
        [SerializeField, Min(0.1f)] private float stompDetectionHeight = 0.8f;

        private bool isKilled;

        public bool IsKilled => isKilled;
        public EnemyType Type => enemyType;

        public bool IsInStompPath(Bounds playerBounds, float projectedX, float projectedZ)
        {
            Collider body = GetComponent<Collider>();
            if (isKilled || body == null || !body.enabled)
                return false;

            Bounds enemyBounds = body.bounds;
            if (playerBounds.min.y < enemyBounds.max.y - 0.1f ||
                playerBounds.center.y <= enemyBounds.max.y)
                return false;

            float halfWidth = enemyBounds.extents.x * stompDetectionWidthMultiplier +
                playerBounds.extents.x;
            float halfDepth = enemyBounds.extents.z * stompDetectionWidthMultiplier +
                playerBounds.extents.z;
            return Mathf.Abs(projectedX - enemyBounds.center.x) <= halfWidth &&
                Mathf.Abs(projectedZ - enemyBounds.center.z) <= halfDepth;
        }

        public bool IsValidStomp(Bounds beforePlayer, Bounds afterPlayer)
        {
            return TryGetStompContact(beforePlayer, afterPlayer, out _, out _);
        }

        public bool TryGetStompContact(Bounds beforePlayer, Bounds afterPlayer,
            out float crossingTime, out float top)
        {
            crossingTime = 0f;
            top = 0f;
            if (isKilled)
                return false;

            Collider body = GetComponent<Collider>();
            if (body == null || !body.enabled)
                return false;

            Bounds enemyBounds = body.bounds;
            top = enemyBounds.max.y;
            float previousFeet = beforePlayer.min.y;
            float currentFeet = afterPlayer.min.y;
            if (currentFeet >= previousFeet || beforePlayer.center.y <= top ||
                previousFeet < top - 0.1f || currentFeet > top + 0.02f)
                return false;

            // Both Fast Fall and Slam use the same swept head plane and footprint.
            float denominator = previousFeet - currentFeet;
            crossingTime = denominator > Mathf.Epsilon
                ? Mathf.Clamp01((previousFeet - top) / denominator)
                : 0f;
            Vector3 centerAtTop = Vector3.Lerp(beforePlayer.center,
                afterPlayer.center, crossingTime);
            float halfWidth = enemyBounds.extents.x * stompDetectionWidthMultiplier +
                Mathf.Min(beforePlayer.extents.x, afterPlayer.extents.x);
            float halfDepth = enemyBounds.extents.z * stompDetectionWidthMultiplier +
                Mathf.Min(beforePlayer.extents.z, afterPlayer.extents.z);
            return Mathf.Abs(centerAtTop.x - enemyBounds.center.x) <= halfWidth &&
                Mathf.Abs(centerAtTop.z - enemyBounds.center.z) <= halfDepth;
        }

        public bool TryKill(string attackName = "Lane Attack")
        {
            if (isKilled)
                return false;

            isKilled = true;
            EnemyAttackZone[] attackZones = GetComponentsInChildren<EnemyAttackZone>(true);
            foreach (EnemyAttackZone attackZone in attackZones)
                attackZone.DisableImmediately();

            Collider[] colliders = GetComponentsInChildren<Collider>(true);
            foreach (Collider enemyCollider in colliders)
                enemyCollider.enabled = false;

            Debug.Log("Enemy Killed by " + attackName);
            Destroy(gameObject);
            return true;
        }

        private void OnDrawGizmosSelected()
        {
            Collider body = GetComponent<Collider>();
            if (body == null)
                return;

            Bounds bounds = body.bounds;
            float lowerTolerance = Mathf.Min(0.15f, stompDetectionHeight * 0.25f);
            Gizmos.color = Color.magenta;
            Gizmos.DrawWireCube(
                new Vector3(bounds.center.x,
                    bounds.max.y + (stompDetectionHeight - lowerTolerance) * 0.5f,
                    bounds.center.z),
                new Vector3(
                    bounds.size.x * stompDetectionWidthMultiplier,
                    stompDetectionHeight + lowerTolerance,
                    bounds.size.z * stompDetectionWidthMultiplier));
        }
    }
}
