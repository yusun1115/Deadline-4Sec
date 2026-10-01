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
        }

        private void LateUpdate()
        {
            float t = Time.time * bobSpeed + phase;
            if (bobTarget != null)
            {
                bobTarget.localPosition = basePosition + Vector3.up * (Mathf.Sin(t) * bobHeight);
                bobTarget.localScale = baseScale * (1f + Mathf.Sin(t * 2f) * pulse);
            }
            if (billboard != null)
            {
                if (view == null && Camera.main != null)
                    view = Camera.main.transform;
                if (view != null)
                    billboard.rotation = Quaternion.LookRotation(billboard.position - view.position, Vector3.up);
            }
        }
    }
}
