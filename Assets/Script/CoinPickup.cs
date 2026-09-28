using System.Collections.Generic;
using UnityEngine;

namespace Deadline4Sec
{
    [RequireComponent(typeof(SphereCollider))]
    public sealed class CoinPickup : MonoBehaviour
    {
        public static readonly HashSet<CoinPickup> Active = new HashSet<CoinPickup>();
        [SerializeField, Min(1)] private int amount = 1;
        [SerializeField, Min(0f)] private float rotationSpeed = 100f;
        [SerializeField, Min(0.1f)] private float magnetCollectDistance = 0.9f;
        private bool collected;

        private void OnEnable() { collected = false; Active.Add(this); }
        private void OnDisable() => Active.Remove(this);
        private void Update() => transform.Rotate(Vector3.up, rotationSpeed * Time.deltaTime, Space.World);

        private void OnTriggerEnter(Collider other)
        {
            if (other.GetComponentInParent<PlayerController>() != null)
                Collect(FindFirstObjectByType<CoinWallet>());
        }

        public void PullTowards(Vector3 target, float step, CoinWallet wallet)
        {
            if (collected)
                return;
            transform.position = Vector3.MoveTowards(transform.position, target, step);
            if ((transform.position - target).sqrMagnitude <= magnetCollectDistance * magnetCollectDistance)
                Collect(wallet);
        }

        private void Collect(CoinWallet wallet)
        {
            GameTimer timer = FindFirstObjectByType<GameTimer>();
            if (collected || wallet == null || timer == null || !timer.IsRunning)
                return;
            collected = true;
            wallet.Collect(amount);
            Debug.Log("Coin +" + amount);
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
