using System.Collections.Generic;
using UnityEngine;

namespace Deadline4Sec
{
    // Kill payoff: the defeated enemy's visual is detached from the gameplay
    // object (which Enemy.TryKill destroys as before), flashes hot white-pink and
    // is flung away spinning while it shrinks. Direction follows the attack:
    // lane slashes knock it sideways, homing knocks it forward, stomps squash it.
    // Purely cosmetic: no colliders, unscaled time so hit stop freezes nothing.
    public sealed class EnemyDeathFx : MonoBehaviour
    {
        private const float Duration = 0.42f;
        private static readonly int EmissionId = Shader.PropertyToID("_EmissionColor");

        private sealed class Flying
        {
            public Transform Visual;
            public Renderer[] Renderers;
            public Vector3 Velocity;
            public Vector3 Spin;
            public Vector3 StartScale;
            public float Start;
            public bool Squash;
        }

        private readonly List<Flying> active = new List<Flying>();
        private MaterialPropertyBlock block;
        private PlayerController player;
        private GameObject root;

        private void Start()
        {
            block = new MaterialPropertyBlock();
            root = new GameObject("Enemy Death Fx");
            player = FindFirstObjectByType<PlayerController>();
            if (player != null)
            {
                player.EnemyKilled += OnEnemyKilled;
                player.ActionPerformed += OnAction;
            }
        }

        // Ground Slam kills resolve without EnemyKilled; at the impact event the
        // victims are already marked killed (destroyed at frame end), so fling
        // every killed enemy near the player outward from the shockwave.
        private void OnAction(PlayerController.PlayerAction action)
        {
            if (action != PlayerController.PlayerAction.SlamImpact)
                return;
            foreach (EnemyVisual visual in FindObjectsByType<EnemyVisual>(FindObjectsSortMode.None))
            {
                Enemy enemy = visual.GetComponent<Enemy>();
                if (enemy != null && enemy.IsKilled &&
                    (enemy.transform.position - player.transform.position).sqrMagnitude < 100f)
                    Fling(enemy, "Ground Slam Attack");
            }
        }

        private void OnDestroy()
        {
            if (player != null)
            {
                player.EnemyKilled -= OnEnemyKilled;
                player.ActionPerformed -= OnAction;
            }
            if (root != null)
                Destroy(root);
        }

        private void OnEnemyKilled(Enemy enemy, string attackName) => Fling(enemy, attackName);

        private void Fling(Enemy enemy, string attackName)
        {
            if (enemy == null || player == null)
                return;
            Transform visual = enemy.transform.Find("Visual");
            if (visual == null)
                return;
            EnemyVisual idle = enemy.GetComponent<EnemyVisual>();
            if (idle != null)
                idle.enabled = false; // stop idle bobbing writing to the detached visual
            visual.SetParent(root.transform, true);

            bool stomp = attackName == "Stomp Attack";
            bool homing = attackName == "Homing Dash Attack";
            float side = Mathf.Sign(enemy.transform.position.x - player.transform.position.x);
            if (Mathf.Abs(enemy.transform.position.x - player.transform.position.x) < 0.3f)
                side = Random.value < 0.5f ? -1f : 1f;
            Vector3 away = enemy.transform.position - player.transform.position;
            away.y = 0f;
            Vector3 velocity = stomp ? Vector3.zero
                : attackName == "Ground Slam Attack" ? away.normalized * 13f + Vector3.up * 11f
                : homing ? new Vector3(side * 2f, 7f, 16f)
                : new Vector3(side * 11f, 6.5f, 9f);
            active.Add(new Flying
            {
                Visual = visual,
                Renderers = visual.GetComponentsInChildren<Renderer>(),
                Velocity = velocity,
                Spin = stomp ? Vector3.zero : new Vector3(Random.Range(-600f, 600f), Random.Range(-900f, 900f), side * -720f),
                StartScale = visual.localScale,
                Start = Time.unscaledTime,
                Squash = stomp,
            });
        }

        private void LateUpdate()
        {
            float now = Time.unscaledTime;
            float dt = Time.unscaledDeltaTime;
            for (int i = active.Count - 1; i >= 0; i--)
            {
                Flying f = active[i];
                float t = (now - f.Start) / Duration;
                if (f.Visual == null || t >= 1f)
                {
                    if (f.Visual != null)
                        Destroy(f.Visual.gameObject);
                    active.RemoveAt(i);
                    continue;
                }
                if (f.Squash)
                {
                    // Pancake under the boot, then pop.
                    float squash = Mathf.Lerp(1f, 0.15f, Mathf.Clamp01(t * 3f));
                    float spread = Mathf.Lerp(1f, 1.6f, Mathf.Clamp01(t * 3f));
                    float vanish = 1f - Mathf.Clamp01((t - 0.6f) / 0.4f);
                    f.Visual.localScale = Vector3.Scale(f.StartScale, new Vector3(spread, squash, spread)) * vanish;
                }
                else
                {
                    f.Velocity += Vector3.down * 28f * dt;
                    f.Visual.position += f.Velocity * dt;
                    f.Visual.Rotate(f.Spin * dt, Space.World);
                    f.Visual.localScale = f.StartScale * Mathf.Lerp(1f, 0.25f, t * t);
                }
                Color flash = Color.Lerp(new Color(1f, 0.9f, 1f) * 3f, new Color(1f, 0.15f, 0.55f) * 1.2f, Mathf.Clamp01(t * 2.5f));
                foreach (Renderer r in f.Renderers)
                {
                    if (r == null)
                        continue;
                    r.GetPropertyBlock(block);
                    block.SetColor(EmissionId, flash);
                    r.SetPropertyBlock(block);
                }
            }
        }

        public void Clear()
        {
            foreach (Flying f in active)
                if (f.Visual != null)
                    Destroy(f.Visual.gameObject);
            active.Clear();
        }
    }
}
