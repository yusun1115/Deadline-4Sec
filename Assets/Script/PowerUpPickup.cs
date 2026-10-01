using UnityEngine;

namespace Deadline4Sec
{
    [RequireComponent(typeof(SphereCollider))]
    public sealed class PowerUpPickup : MonoBehaviour
    {
        [SerializeField] private PowerUpType itemType;
        [SerializeField, Min(0f)] private float rotationSpeed = 75f;
        private bool collected;
        public PowerUpType ItemType => itemType;

        private void OnEnable() => collected = false;
        private void Update() => transform.Rotate(Vector3.up, rotationSpeed * Time.deltaTime, Space.World);

        private void OnTriggerEnter(Collider other)
        {
            if (collected || other.GetComponentInParent<PlayerController>() == null)
                return;
            PowerUpManager manager = FindFirstObjectByType<PowerUpManager>();
            if (manager == null || manager.GetComponent<GameTimer>()?.IsRunning != true)
                return;
            collected = true;
            manager.Activate(itemType);
            CameraFeedbackController feedback = FindFirstObjectByType<CameraFeedbackController>();
            if (feedback != null)
                feedback.PlayPickup(true);
            gameObject.SetActive(false);
        }

        private void OnValidate()
        {
            SphereCollider collider = GetComponent<SphereCollider>();
            if (collider != null)
                collider.isTrigger = true;
        }
    }
}
