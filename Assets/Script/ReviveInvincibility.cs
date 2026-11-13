using UnityEngine;

namespace Deadline4Sec
{
    // Revive invincibility presentation (2026-11-14 request). While
    // RunInventory.CollisionProtected runs (the window Revive() opens — the
    // same one that already forgives a fatal contact), this:
    //  - flickers the runner's visuals (renderers blink on/off) so standing
    //    there obviously invincible reads as such, not as a glitch,
    //  - lets obstacles pass through (Obstacle.Phasing) instead of physically
    //    stopping the CharacterController, which used to leave the runner
    //    stuck standing against the first obstacle ahead.
    // The timer hold and its "PAUSE" tag are GameTimer.IsHeldByRevive /
    // MomentFx, already wired the same way as Wall Run. The light burst and
    // the speed ease-in are one-shot calls from GameFlowManager.ResumeAfterRevive.
    // Added to the GameFlowManager object at runtime, like MusicDirector.
    public sealed class ReviveInvincibility : MonoBehaviour
    {
        private const float FlickerInterval = 0.08f;

        private RunInventory inventory;
        private PlayerController player;
        private GameTimer timer;
        private Renderer[] visualRenderers;
        private bool wasProtected;
        private bool flickerOn = true;
        private float nextFlickerAt;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Attach()
        {
            UnityEngine.SceneManagement.SceneManager.sceneLoaded -= OnSceneLoaded;
            UnityEngine.SceneManagement.SceneManager.sceneLoaded += OnSceneLoaded;
            OnSceneLoaded(default, default);
        }

        private static void OnSceneLoaded(UnityEngine.SceneManagement.Scene s, UnityEngine.SceneManagement.LoadSceneMode m)
        {
            GameFlowManager found = FindFirstObjectByType<GameFlowManager>();
            if (found != null && !found.TryGetComponent(out ReviveInvincibility _))
                found.gameObject.AddComponent<ReviveInvincibility>();
        }

        private void Start()
        {
            inventory = GetComponent<RunInventory>();
            timer = GetComponent<GameTimer>();
            player = FindFirstObjectByType<PlayerController>();
        }

        private void OnDestroy()
        {
            // Never leave a run mid-reload with obstacles disabled.
            if (wasProtected)
                Obstacle.Phasing = false;
        }

        private void Update()
        {
            if (inventory == null)
                return;
            // Gated on the timer actually running: a death that isn't forgiven
            // (falling, the countdown reaching 0) still ends the window's
            // presentation at once, mid fall or Game Over.
            bool protectedNow = inventory.CollisionProtected && timer != null && timer.IsRunning;
            if (protectedNow && !wasProtected)
                BeginInvincibility();
            else if (!protectedNow && wasProtected)
                EndInvincibility();
            wasProtected = protectedNow;
            if (protectedNow)
                UpdateFlicker();
        }

        private void BeginInvincibility()
        {
            Obstacle.Phasing = true;
            if (player == null)
                player = FindFirstObjectByType<PlayerController>();
            // Only what is actually shown right now: the model keeps a disabled
            // graybox-fallback renderer (SetupVisual) that must stay off.
            visualRenderers = null;
            if (player != null)
            {
                Renderer[] all = player.GetComponentsInChildren<Renderer>(true);
                var shown = new System.Collections.Generic.List<Renderer>(all.Length);
                foreach (Renderer part in all)
                    if (part != null && part.enabled)
                        shown.Add(part);
                visualRenderers = shown.ToArray();
            }
            flickerOn = true;
            nextFlickerAt = Time.time + FlickerInterval;
        }

        private void EndInvincibility()
        {
            Obstacle.Phasing = false;
            SetRenderersEnabled(true);
        }

        private void UpdateFlicker()
        {
            if (Time.time < nextFlickerAt)
                return;
            nextFlickerAt = Time.time + FlickerInterval;
            flickerOn = !flickerOn;
            SetRenderersEnabled(flickerOn);
        }

        private void SetRenderersEnabled(bool visible)
        {
            if (visualRenderers == null)
                return;
            foreach (Renderer part in visualRenderers)
                if (part != null)
                    part.enabled = visible;
        }
    }
}
