using System;
using System.Collections.Generic;
using UnityEngine;

namespace Deadline4Sec
{
    // Wall Run (2026-10-18, Wall Run.md): a stretch of a pattern where the floor
    // stops and the runner has to swipe toward a wall to run along it. All
    // positions come from child transforms, so the section works wherever the
    // pattern spawns:
    //   WallRunEntry      trigger box = the attach input window (a left / right
    //                     input inside it attaches instead of changing lane)
    //   WallRunExit       the wall run ends when the runner passes its Z
    //   LeftWallAnchor /  the runner's feet line on each wall (X and height)
    //   RightWallAnchor
    //   GroundExitAnchor  landing point after the exit
    //   ReviveCheckpoint  safe ground before the entry (revive after a fall or
    //                     a wall obstacle hit)
    // The spawner leaves the floor out between floorGapStart and floorGapEnd
    // (Z in this transform's space); a FallDeathTrigger sits below the gap.
    // Nothing attaches by itself: PlayerController checks FindEntry only when a
    // left / right input arrives.
    [DisallowMultipleComponent]
    public sealed class WallRunSection : MonoBehaviour
    {
        public enum WallSide { None, Left, Right }
        public enum AvailableWall { Left, Right, Both }

        public static readonly List<WallRunSection> Active = new List<WallRunSection>();

        // Raised once per section when the runner reaches the entry window
        // (hook for a later "← SWIPE" tutorial prompt).
        public static event Action<WallRunSection> EntryReached;

        [SerializeField] private AvailableWall availableWall = AvailableWall.Left;
        [Header("Children")]
        [SerializeField] private BoxCollider entry;
        [SerializeField] private Transform exit;
        [SerializeField] private Transform leftWallAnchor;
        [SerializeField] private Transform rightWallAnchor;
        [SerializeField] private Transform groundExitAnchor;
        [SerializeField] private Transform reviveCheckpoint;
        [Header("Floor gap (local Z)")]
        [SerializeField, Min(0f)] private float floorGapStart = 26f;
        [SerializeField, Min(0f)] private float floorGapEnd = 54f;

        private bool entryReported;

        public AvailableWall Available => availableWall;
        public float ExitZ => exit != null ? exit.position.z : transform.position.z;
        public Transform GroundExitAnchor => groundExitAnchor;
        public Vector3 CheckpointPosition => reviveCheckpoint != null ? reviveCheckpoint.position : transform.position;
        public float GapStartZ => transform.TransformPoint(0f, 0f, floorGapStart).z;
        public float GapEndZ => transform.TransformPoint(0f, 0f, floorGapEnd).z;
        public float FloorGapStart => floorGapStart;
        public float FloorGapEnd => floorGapEnd;

        private void Awake()
        {
            if (entry != null)
                entry.isTrigger = true;
        }

        private void OnEnable() => Active.Add(this);
        private void OnDisable() => Active.Remove(this);

        public bool Allows(WallSide side) =>
            side == WallSide.Left ? availableWall != AvailableWall.Right && leftWallAnchor != null :
            side == WallSide.Right && availableWall != AvailableWall.Left && rightWallAnchor != null;

        // Switching may use either wall once attached (both anchors present).
        public Transform AnchorFor(WallSide side) =>
            side == WallSide.Left ? leftWallAnchor : side == WallSide.Right ? rightWallAnchor : null;

        public bool EntryContains(Vector3 point) =>
            entry != null && entry.enabled && entry.bounds.Contains(point);

        // The section that owns this point's entry window, if any.
        public static WallRunSection FindEntry(Vector3 point)
        {
            foreach (WallRunSection section in Active)
                if (section != null && section.EntryContains(point))
                    return section;
            return null;
        }

        // Revive: a runner who died between this section's checkpoint and its
        // landing point restarts at the checkpoint.
        public static WallRunSection FindForRevive(Vector3 point)
        {
            foreach (WallRunSection section in Active)
            {
                if (section == null || section.reviveCheckpoint == null)
                    continue;
                float end = section.groundExitAnchor != null ? section.groundExitAnchor.position.z : section.GapEndZ;
                if (point.z >= section.reviveCheckpoint.position.z - 0.5f && point.z <= end)
                    return section;
            }
            return null;
        }

        public void ReportEntryReached()
        {
            if (entryReported)
                return;
            entryReported = true;
            EntryReached?.Invoke(this);
        }

        private void OnValidate()
        {
            floorGapEnd = Mathf.Max(floorGapStart, floorGapEnd);
        }

        private void OnDrawGizmos()
        {
            Gizmos.color = new Color(0.4f, 0.8f, 1f, 0.9f);
            if (entry != null)
                Gizmos.DrawWireCube(entry.bounds.center, entry.bounds.size);
            Gizmos.color = new Color(1f, 0.3f, 0.2f, 0.9f);
            Vector3 a = transform.TransformPoint(0f, 0f, floorGapStart);
            Vector3 b = transform.TransformPoint(0f, 0f, floorGapEnd);
            Gizmos.DrawWireCube((a + b) * 0.5f, new Vector3(8.6f, 0.05f, b.z - a.z));
            Gizmos.color = new Color(1f, 0.3f, 0.75f, 0.9f);
            foreach (Transform anchor in new[] { leftWallAnchor, rightWallAnchor })
                if (anchor != null)
                    Gizmos.DrawLine(new Vector3(anchor.position.x, anchor.position.y, a.z),
                        new Vector3(anchor.position.x, anchor.position.y, ExitZ));
            Gizmos.color = Color.green;
            if (reviveCheckpoint != null)
                Gizmos.DrawWireSphere(reviveCheckpoint.position, 0.5f);
            if (groundExitAnchor != null)
                Gizmos.DrawWireSphere(groundExitAnchor.position, 0.5f);
        }
    }
}
