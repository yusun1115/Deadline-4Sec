using UnityEngine;

namespace Deadline4Sec
{
    // Idle motion for pickups: a gentle bob, plus an optional icon that always
    // faces the camera. Moves only child visuals, never the trigger collider.
    public sealed class PickupFloat : MonoBehaviour
    {
        [SerializeField] private Transform bobTarget;
        [SerializeField] private Transform billboard;
        [SerializeField, Min(0f)] private float bobHeight = 0.12f;
        [SerializeField, Min(0f)] private float bobSpeed = 3.2f;
        [SerializeField, Min(0f)] private float pulse = 0.08f;

        private Vector3 basePosition;
        private Vector3 baseScale;
        private float phase;
        private Transform view;
        private CoinPickup coin;

        private void Awake()
        {
            if (bobTarget == null)
                bobTarget = transform.childCount > 0 ? transform.GetChild(0) : null;
            if (bobTarget != null)
            {
                basePosition = bobTarget.localPosition;
                baseScale = bobTarget.localScale;
            }
            phase = (transform.position.x * 1.7f + transform.position.z * 0.37f) % 6.28f;
            TryGetComponent(out coin);
        }

        private void LateUpdate()
        {
            float t = Time.time * bobSpeed + phase;
            if (bobTarget != null)
            {
                bobTarget.localPosition = basePosition + Vector3.up * (Mathf.Sin(t) * bobHeight) + MagnetArc();
                bobTarget.localScale = baseScale * (1f + Mathf.Sin(t * 2f) * pulse) * MagnetShrink();
            }
            if (billboard != null)
            {
                if (view == null && Camera.main != null)
                    view = Camera.main.transform;
                if (view != null)
                    billboard.rotation = Quaternion.LookRotation(billboard.position - view.position, Vector3.up);
            }
        }

        // Soul Magnet: the collider flies straight (pickup timing unchanged) while
        // the visual swings out sideways and up on an arc that closes at the player.
        private Vector3 MagnetArc()
        {
            if (coin == null || !coin.BeingPulled)
                return Vector3.zero;
            Vector3 to = coin.PullTarget - transform.position;
            to.y = 0f;
            if (to.sqrMagnitude < 0.0001f)
                return Vector3.zero;
            float side = transform.position.x >= coin.PullTarget.x ? 1f : -1f;
            float bend = Mathf.Sin(coin.PullProgress * Mathf.PI);
            Vector3 world = (Vector3.Cross(Vector3.up, to.normalized) * -side * 1.1f + Vector3.up * 0.6f) * bend;
            return transform.InverseTransformVector(world);
        }

        private float MagnetShrink() =>
            coin != null && coin.BeingPulled ? Mathf.Lerp(1f, 0.55f, coin.PullProgress) : 1f;
    }
}
