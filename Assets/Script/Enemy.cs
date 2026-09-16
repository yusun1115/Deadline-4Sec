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

        public bool IsValidStomp(Bounds beforePlayer, Bounds afterPlayer)
        {
            if (isKilled)
                return false;

            Collider body = GetComponent<Collider>();
            if (body == null)
                return false;

            Bounds enemyBounds = body.bounds;
            float top = enemyBounds.max.y;
            float previousFeet = beforePlayer.min.y;
            float currentFeet = afterPlayer.min.y;
            float lowerTolerance = Mathf.Min(0.15f, stompDetectionHeight * 0.25f);
            if (beforePlayer.center.y <= top || previousFeet < top - lowerTolerance ||
                currentFeet > top + stompDetectionHeight || currentFeet >= previousFeet)
                return false;

            float crossingTime = Mathf.Clamp01(
                (previousFeet - (top + stompDetectionHeight)) / (previousFeet - currentFeet));
            Vector3 centerAtZone = Vector3.Lerp(beforePlayer.center, afterPlayer.center, crossingTime);
            float halfWidth = enemyBounds.extents.x * stompDetectionWidthMultiplier;
            float halfDepth = enemyBounds.extents.z * stompDetectionWidthMultiplier;
            return Mathf.Abs(centerAtZone.x - enemyBounds.center.x) <= halfWidth &&
                Mathf.Abs(centerAtZone.z - enemyBounds.center.z) <= halfDepth;
        }

        public bool TryKill(string attackName = "Lane Attack")
        {
            if (isKilled)
                return false;

            isKilled = true;
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
