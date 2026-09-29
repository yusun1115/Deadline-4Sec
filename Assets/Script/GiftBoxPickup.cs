using UnityEngine;

namespace Deadline4Sec
{
    [RequireComponent(typeof(SphereCollider))]
    public sealed class GiftBoxPickup : MonoBehaviour
    {
        [SerializeField, Min(0f)] private float rotationSpeed = 75f;
        private bool collected;

        private void OnEnable() => collected = false;
        private void Update() => transform.Rotate(Vector3.up, rotationSpeed * Time.deltaTime, Space.World);

        private void OnTriggerEnter(Collider other)
        {
            if (collected || other.GetComponentInParent<PlayerController>() == null)
                return;
            GameTimer timer = FindFirstObjectByType<GameTimer>();
            RunInventory inventory = FindFirstObjectByType<RunInventory>();
            if (timer == null || !timer.IsRunning || inventory == null)
                return;
            collected = true;
            inventory.CollectGiftBox();
            Debug.Log("Gift Box collected (run total " + inventory.CurrentRunGiftBoxes + ")");
            gameObject.SetActive(false);
        }

        private void OnValidate()
        {
            SphereCollider trigger = GetComponent<SphereCollider>();
            if (trigger != null)
                trigger.isTrigger = true;
        }
    }
}
