using System.Collections.Generic;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Deadline4Sec
{
    // The root is the start marker; +Z at PatternLength is the end marker.
    public sealed class CoursePattern : MonoBehaviour
    {
        public enum DifficultyLevel { Easy, Medium, Hard }
        public enum PatternCategory { GroundCombat, AirCombat, Obstacle, Stomp, Mixed }
        public enum TravelState { Ground, Air, Any }

        [SerializeField, Min(1f)] private float patternLength = 20f;
        [SerializeField, Min(0.1f)] private float referenceForwardSpeed = 10.2f;
        [SerializeField, Min(0.1f)] private float laneWidth = 2.5f;
        [Header("Spawner selection")]
        [SerializeField] private DifficultyLevel difficulty = DifficultyLevel.Easy;
        [SerializeField] private PatternCategory category = PatternCategory.GroundCombat;
        [SerializeField] private TravelState entryState = TravelState.Ground;
        [SerializeField] private TravelState exitState = TravelState.Ground;

        public DifficultyLevel Difficulty => difficulty;
        public PatternCategory Category => category;
        public TravelState EntryState => entryState;
        public TravelState ExitState => exitState;

        public float PatternLength
        {
            get => patternLength;
            set => patternLength = Mathf.Max(1f, value);
        }

        // These are candidate reward positions. A real route is still checked in play.
        // A Near Miss is awarded only after the player's whole body clears an obstacle.
        public void CollectOpportunityPositions(List<float> positions)
        {
            positions.Clear();
            foreach (Enemy enemy in GetComponentsInChildren<Enemy>(true))
            {
                float z = transform.InverseTransformPoint(enemy.transform.position).z;
                positions.Add(z);
            }
            foreach (Obstacle obstacle in GetComponentsInChildren<Obstacle>(true))
            {
                BoxCollider body = obstacle.GetComponent<BoxCollider>();
                if (body == null || !body.enabled)
                    continue;

                // The maximum world-forward edge works for rotated/scaled prefabs.
                float farEdge = float.NegativeInfinity;
                for (int x = -1; x <= 1; x += 2)
                for (int y = -1; y <= 1; y += 2)
                for (int z = -1; z <= 1; z += 2)
                {
                    Vector3 corner = body.center + Vector3.Scale(body.size * 0.5f,
                        new Vector3(x, y, z));
                    float localZ = transform.InverseTransformPoint(
                        body.transform.TransformPoint(corner)).z;
                    farEdge = Mathf.Max(farEdge, localZ);
                }
                positions.Add(farEdge + 0.5f); // Approximate player rear clearance.
            }
            positions.Sort();
        }

        public bool TryGetOpportunityRange(out float firstZ, out float lastZ)
        {
            List<float> positions = new List<float>();
            CollectOpportunityPositions(positions);
            firstZ = positions.Count > 0 ? positions[0] : float.PositiveInfinity;
            lastZ = positions.Count > 0 ? positions[positions.Count - 1] : float.NegativeInfinity;
            return positions.Count > 0;
        }

        private void OnDrawGizmos()
        {
            Vector3 start = transform.position;
            Vector3 end = transform.TransformPoint(0f, 0f, patternLength);
            Vector3 laneSpan = transform.right * (laneWidth * 1.5f);

            Gizmos.color = difficulty == DifficultyLevel.Easy
                ? new Color(0.3f, 1f, 0.4f, 0.8f)
                : difficulty == DifficultyLevel.Medium
                    ? new Color(1f, 0.8f, 0.2f, 0.8f)
                    : new Color(1f, 0.3f, 0.3f, 0.8f);
            Gizmos.DrawLine(start, end);
            Gizmos.DrawLine(start - laneSpan, start + laneSpan);
            Gizmos.DrawLine(end - laneSpan, end + laneSpan);
            Gizmos.DrawWireCube((start + end) * 0.5f,
                new Vector3(laneWidth * 3f, 0.1f, patternLength));
        }

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            Vector3 start = transform.position;
            Vector3 end = transform.TransformPoint(0f, 0f, patternLength);
            Handles.Label(start + Vector3.up * 0.5f,
                name + " START | " + difficulty + " | " + category +
                " | " + entryState + " → " + exitState);
            Handles.Label(end + Vector3.up * 0.5f,
                "END  ~" + (patternLength / referenceForwardSpeed).ToString("0.0") + "s");

            List<Transform> beats = new List<Transform>();
            foreach (Enemy enemy in GetComponentsInChildren<Enemy>(true))
                beats.Add(enemy.transform);
            foreach (Obstacle obstacle in GetComponentsInChildren<Obstacle>(true))
                beats.Add(obstacle.transform);
            beats.Sort((a, b) =>
                transform.InverseTransformPoint(a.position).z.CompareTo(
                    transform.InverseTransformPoint(b.position).z));

            float previousZ = 0f;
            foreach (Transform beat in beats)
            {
                float z = transform.InverseTransformPoint(beat.position).z;
                float seconds = (z - previousZ) / referenceForwardSpeed;
                Handles.Label(beat.position + Vector3.up * 1.3f,
                    beat.name + "  Δ" + seconds.ToString("0.0") + "s");
                previousZ = z;
            }
        }
#endif
    }
}
