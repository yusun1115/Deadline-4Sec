using UnityEngine;

namespace Deadline4Sec
{
    // Kill volume below a floor gap (Wall Run): a runner who missed the wall
    // falls into it and the run ends with DeathReason.Fall (not a contact, so a
    // Shield does not absorb it).
    [RequireComponent(typeof(BoxCollider))]
    public sealed class FallDeathTrigger : MonoBehaviour
    {
        private GameTimer gameTimer;

        private void Awake() => GetComponent<BoxCollider>().isTrigger = true;

        private void OnTriggerEnter(Collider other) => Check(other);
        private void OnTriggerStay(Collider other) => Check(other);

        private void Check(Collider other)
        {
            if (other.GetComponentInParent<PlayerController>() == null)
                return;
            if (gameTimer == null)
                gameTimer = FindFirstObjectByType<GameTimer>();
            if (gameTimer != null)
                gameTimer.TriggerFallDeath();
        }
    }
}
