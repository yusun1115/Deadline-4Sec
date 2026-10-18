using UnityEngine;

namespace Deadline4Sec
{
    // The air route's upper deck is solid only from above: while the runner's
    // feet are below its top it ignores the runner, so the lower route has no
    // ceiling or front edge to snag on (stomp / Ground Slam bounces, a missed
    // chain), and the deck catches the runner as soon as it comes down on top.
    // Runs before PlayerController moves (execution order), so each frame's
    // move already uses the right state. Raycasts (Ground Slam height, floor
    // checks) still see the deck from above.
    [DefaultExecutionOrder(-50)]
    [RequireComponent(typeof(Collider))]
    public sealed class OneWayPlatform : MonoBehaviour
    {
        [SerializeField, Min(0f)] private float landingTolerance = 0.15f;

        private static CharacterController runner;
        private Collider platform;
        private bool ignoring;

        private void Awake() => platform = GetComponent<Collider>();

        private void Update()
        {
            if (runner == null)
            {
                PlayerController player = FindFirstObjectByType<PlayerController>();
                if (player == null)
                    return;
                runner = player.GetComponent<CharacterController>();
            }
            if (runner == null || !runner.enabled)
                return;
            bool below = runner.bounds.min.y < platform.bounds.max.y - landingTolerance;
            if (below == ignoring)
                return;
            ignoring = below;
            Physics.IgnoreCollision(runner, platform, below);
        }
    }
}
