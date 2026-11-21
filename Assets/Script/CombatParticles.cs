using UnityEngine;
using UnityEngine.Rendering;

namespace Deadline4Sec
{
    // Particle and slash-arc layer on top of CombatVisualFeedback's outline rings:
    // sparks, soul wisps, dust, scythe slashes, a scythe trail and speed lines.
    // Everything is pooled and created once; effects run on unscaled time so they
    // keep moving through hit stop and never touch gameplay state.
    // Weapon effects (2026-11-21): the swing slash, kill slash, homing path
    // and swing trail belong to the equipped skin and are drawn by
    // WeaponVfxController; this used to draw them all in NEON REAPER pink.
    [RequireComponent(typeof(Camera))]
    public sealed class CombatParticles : MonoBehaviour
    {
        private const int SlashPoolSize = 6;

        private static readonly Color Pink = new Color(1f, 0.22f, 0.55f);
        private static readonly Color HotWhite = new Color(1f, 0.9f, 0.97f);
        private static readonly Color Violet = new Color(0.62f, 0.3f, 1f);
        private static readonly Color Cyan = new Color(0.45f, 0.95f, 1f);
        private static readonly Color Amber = new Color(1f, 0.6f, 0.2f);
        private static readonly Color Dust = new Color(0.55f, 0.42f, 0.65f);

        private sealed class Slash
        {
            public Transform Transform;
            public MeshRenderer Renderer;
            public MaterialPropertyBlock Block;
            public float Start, Duration;
            public Color Color;
            public bool Active;
        }

        private static readonly int ProgressId = Shader.PropertyToID("_Progress");
        private static readonly int FadeId = Shader.PropertyToID("_Fade");
        private static readonly int ColorId = Shader.PropertyToID("_Color");

        private Camera view;
        private PlayerController player;
        private GameFlowManager gameFlow;
        private RunManager runManager;
        private GameObject root;
        private Material additive;
        private Material ribbon;
        private Material slashMaterial;
        private ParticleSystem sparks;
        private ParticleSystem wisps;
        private ParticleSystem dust;
        private ParticleSystem speedLines;
        private readonly Slash[] slashes = new Slash[SlashPoolSize];
        private int nextSlash;
        private float trailUntil;

        private static WeaponVfxController Weapon => WeaponVfxController.Current;
        private static Color SkinPrimary => Weapon != null ? Weapon.Primary : Pink;
        private static Color SkinSecondary => Weapon != null ? Weapon.Secondary : Violet;

        private void Awake()
        {
            view = GetComponent<Camera>();
            Shader fx = Resources.Load<Shader>("Feedback/FxAdditive");
            Shader slash = Resources.Load<Shader>("Feedback/FxSlash");
            if (fx == null || slash == null)
            {
                Debug.LogError("Combat particle shaders are missing.");
                enabled = false;
                return;
            }
            additive = new Material(fx) { name = "Fx Additive (Runtime)" };
            ribbon = new Material(fx) { name = "Fx Ribbon (Runtime)" };
            ribbon.SetFloat("_Ribbon", 1f);
            ribbon.SetFloat("_Softness", 1.4f);
            ribbon.SetFloat("_Core", 0.6f);
            slashMaterial = new Material(slash) { name = "Fx Slash (Runtime)" };

            root = new GameObject("Combat Particles");
            sparks = CreateSystem("Sparks", 240, ParticleSystemRenderMode.Stretch, 0.035f);
            wisps = CreateSystem("Soul Wisps", 120, ParticleSystemRenderMode.Billboard, 0f);
            dust = CreateSystem("Dust", 160, ParticleSystemRenderMode.Billboard, 0f);
            speedLines = CreateSystem("Speed Lines", 160,ParticleSystemRenderMode.Stretch, 0.02f);
            ParticleSystem.MainModule speedMain = speedLines.main;
            speedMain.simulationSpace = ParticleSystemSimulationSpace.Local;
            speedLines.transform.SetParent(transform, false);

            Mesh arc = BuildCrescent(24, 0.55f, 1.35f, 170f);
            for (int i = 0; i < SlashPoolSize; i++)
            {
                GameObject go = new GameObject("Slash " + i);
                go.transform.SetParent(root.transform, false);
                go.AddComponent<MeshFilter>().sharedMesh = arc;
                MeshRenderer r = go.AddComponent<MeshRenderer>();
                r.sharedMaterial = slashMaterial;
                r.shadowCastingMode = ShadowCastingMode.Off;
                r.receiveShadows = false;
                r.enabled = false;
                slashes[i] = new Slash { Transform = go.transform, Renderer = r, Block = new MaterialPropertyBlock() };
            }
        }

        private void Start()
        {
            player = FindFirstObjectByType<PlayerController>();
            gameFlow = FindFirstObjectByType<GameFlowManager>();
            runManager = FindFirstObjectByType<RunManager>();
            if (player != null)
                player.ActionPerformed += OnPlayerAction;
        }

        private void OnDestroy()
        {
            if (player != null)
                player.ActionPerformed -= OnPlayerAction;
            if (root != null)
                Destroy(root);
            Destroy(additive);
            Destroy(ribbon);
            Destroy(slashMaterial);
        }

        private bool Playing => gameFlow != null && gameFlow.IsPlaying;

        // ---------------- public effects ----------------

        public void PlayKill(Vector3 point, string attackName)
        {
            bool stomp = attackName == "Stomp Attack";
            bool homing = attackName == "Homing Dash Attack";
            // The neutral part of a kill: a white flash and the soul leaving, in the skin's colours.
            Burst(sparks, point, stomp ? 26 : 10, stomp ? 11f : 8f, new Vector2(0.06f, 0.16f), new Vector2(0.18f, 0.34f),
                stomp ? Amber : HotWhite, HotWhite, 0.4f);
            Burst(wisps, point, 7, 1.6f, new Vector2(0.25f, 0.5f), new Vector2(0.5f, 0.85f), SkinPrimary, SkinSecondary, -0.6f);
            Burst(wisps, point, 1, 0f, new Vector2(1.6f, 1.6f), new Vector2(0.12f, 0.12f), HotWhite, HotWhite, 0f);
            Quaternion facing = Quaternion.LookRotation(view.transform.forward, Vector3.up);
            if (stomp)
            {
                // The stomp keeps its own amber impact (not a weapon swing).
                float roll = Random.Range(-35f, 35f);
                PlaySlash(point, facing * Quaternion.Euler(0f, 0f, roll + 30f), 1.1f, Amber, 0.16f);
                Ring(dust, point + Vector3.down * 0.4f, 18, 6f, Dust, 0.45f);
            }
            else if (Weapon != null)
                Weapon.PlayHit(point, facing, homing);
        }

        public void PlaySlam(Vector3 point, float radius)
        {
            Ring(dust, point + Vector3.up * 0.1f, 40, radius * 3.2f, Dust, 0.6f);
            Ring(sparks, point + Vector3.up * 0.15f, 32, radius * 4.5f, Violet, 0.35f);
            Burst(sparks, point, 24, 10f, new Vector2(0.08f, 0.2f), new Vector2(0.25f, 0.45f), SkinPrimary, HotWhite, 1.2f, true);
            Burst(wisps, point + Vector3.up * 0.3f, 1, 0f, new Vector2(radius * 1.6f, radius * 1.6f), new Vector2(0.14f, 0.14f), Violet, Violet, 0f);
        }

        public void PlayNearMiss(Vector3 point)
        {
            for (int i = 0; i < 10; i++)
            {
                var p = new ParticleSystem.EmitParams
                {
                    position = point + new Vector3(Random.Range(-0.6f, 0.6f), Random.Range(-0.4f, 1.2f), Random.Range(-0.5f, 0.5f)),
                    velocity = new Vector3(0f, 0f, -Random.Range(18f, 28f)),
                    startSize = Random.Range(0.05f, 0.09f),
                    startLifetime = Random.Range(0.18f, 0.28f),
                    startColor = Color.Lerp(Cyan, HotWhite, Random.value),
                };
                sparks.Emit(p, 1);
            }
        }

        public void PlayHoming(Vector3 start, Vector3 end)
        {
            if (Weapon != null)
                Weapon.PlayHomingPath(start, end);
            trailUntil = Time.unscaledTime + 0.25f;
        }

        // Wall Run attach / switch: a short streak off the feet toward the wall
        // and a brief scythe trail.
        public void PlayWallRun(int direction)
        {
            if (player == null)
                return;
            Vector3 feet = player.transform.position + new Vector3(Mathf.Sign(direction) * 0.4f, -0.7f, 0f);
            for (int i = 0; i < 12; i++)
            {
                var p = new ParticleSystem.EmitParams
                {
                    position = feet + new Vector3(Random.Range(-0.2f, 0.2f), Random.Range(0f, 1.4f), Random.Range(-0.4f, 0.4f)),
                    velocity = new Vector3(Mathf.Sign(direction) * Random.Range(1f, 3f), 0f, -Random.Range(14f, 22f)),
                    startSize = Random.Range(0.05f, 0.1f),
                    startLifetime = Random.Range(0.16f, 0.26f),
                    startColor = Color.Lerp(Violet, HotWhite, Random.value * 0.6f),
                };
                sparks.Emit(p, 1);
            }
            trailUntil = Time.unscaledTime + 0.3f;
        }

        // ---------------- power-up hooks (PowerUpFx) ----------------

        // 0..1 while Reaper Rush runs: denser, brighter speed lines and a
        // permanently lit scythe trail.
        public float RushBoost { get; set; }

        // Reaper Rush contact kill: a big crossed double slash and a spark spray.
        public void PlayRushSlash(Vector3 point)
        {
            Quaternion facing = Quaternion.LookRotation(view.transform.forward, Vector3.up);
            float roll = Random.Range(-20f, 20f);
            PlaySlash(point, facing * Quaternion.Euler(0f, 0f, roll + 35f), 1.9f, HotWhite, 0.2f);
            PlaySlash(point, facing * Quaternion.Euler(0f, 0f, roll - 55f), 1.7f, SkinPrimary, 0.22f, true);
            Burst(sparks, point, 34, 14f, new Vector2(0.08f, 0.2f), new Vector2(0.2f, 0.4f), SkinPrimary, HotWhite, 0.3f);
        }

        // Revive (2026-11-14): a light burst spreading from the runner.
        public void PlayRevive(Vector3 point)
        {
            Ring(dust, point + Vector3.down * 0.1f, 36, 5f, HotWhite, 0.55f);
            Ring(sparks, point + Vector3.up * 0.15f, 28, 6.5f, Pink, 0.5f);
            Burst(sparks, point, 30, 9f, new Vector2(0.07f, 0.17f), new Vector2(0.3f, 0.55f), Pink, HotWhite, 0.9f, true);
            Burst(wisps, point + Vector3.up * 0.4f, 10, 2f, new Vector2(0.3f, 0.6f), new Vector2(0.5f, 0.9f), HotWhite, Pink, -0.2f);
        }

        // Generic soul burst for power-up pickups.
        public void PlayBurst(Vector3 point, Color a, Color b, int count, float speed, float size)
        {
            Burst(sparks, point, count, speed, new Vector2(size * 0.5f, size), new Vector2(0.25f, 0.5f), a, b, 0.3f);
            Burst(wisps, point, count / 3, speed * 0.25f, new Vector2(size * 2f, size * 4f), new Vector2(0.4f, 0.8f), a, b, 0.6f);
        }

        // Soul Magnet: a few wisps on a circle drifting into the centre. `carry`
        // is the run velocity so they keep pace with the player.
        public void EmitInward(Vector3 centre, float radius, Color color, Vector3 carry)
        {
            for (int i = 0; i < 2; i++)
            {
                float angle = Random.value * Mathf.PI * 2f;
                Vector3 offset = new Vector3(Mathf.Cos(angle), Random.Range(-0.5f, 0.6f), Mathf.Sin(angle)) * radius;
                float life = Random.Range(0.35f, 0.5f);
                var p = new ParticleSystem.EmitParams
                {
                    position = centre + offset,
                    velocity = carry - offset / life,
                    startSize = Random.Range(0.12f, 0.22f),
                    startLifetime = life,
                    startColor = Color.Lerp(color, HotWhite, Random.value * 0.4f),
                };
                wisps.Emit(p, 1);
            }
        }

        // ---------------- internals ----------------

        private void OnPlayerAction(PlayerController.PlayerAction action)
        {
            if (!Playing || player == null)
                return;
            switch (action)
            {
                case PlayerController.PlayerAction.Left:
                case PlayerController.PlayerAction.Right:
                {
                    float side = action == PlayerController.PlayerAction.Right ? 1f : -1f;
                    Vector3 chest = player.transform.position + new Vector3(side * 1.1f, 0.25f, 0.9f);
                    Quaternion rot = Quaternion.LookRotation(Vector3.forward, Vector3.up) *
                        Quaternion.Euler(0f, 0f, side > 0 ? -15f : 195f);
                    // The equipped skin's swing (never the default pink one by itself).
                    if (Weapon != null)
                        Weapon.PlaySwing(chest, rot, side < 0);
                    trailUntil = Time.unscaledTime + 0.22f;
                    break;
                }
                case PlayerController.PlayerAction.FastFall:
                case PlayerController.PlayerAction.SlamStart:
                    trailUntil = Time.unscaledTime + 0.4f;
                    break;
            }
        }

        private void LateUpdate()
        {
            float now = Time.unscaledTime;
            foreach (Slash s in slashes)
            {
                if (!s.Active)
                    continue;
                float t = (now - s.Start) / s.Duration;
                if (t >= 1f)
                {
                    s.Active = false;
                    s.Renderer.enabled = false;
                    continue;
                }
                s.Block.SetFloat(ProgressId, Mathf.Lerp(0f, 1.5f, t));
                s.Block.SetFloat(FadeId, 1f - t * t);
                s.Block.SetColor(ColorId, s.Color);
                s.Renderer.SetPropertyBlock(s.Block);
            }

            if (Weapon != null)
                Weapon.TrailEmitting = Playing && (now < trailUntil || RushBoost > 0.05f ||
                                                   (player != null && player.IsHoming));

            UpdateSpeedLines();
        }

        private void UpdateSpeedLines()
        {
            if (!Playing || runManager == null)
                return;
            float range = Mathf.Max(0.01f, runManager.MaxForwardSpeed - runManager.BaseForwardSpeed);
            float speedT = Mathf.Clamp01((runManager.CurrentForwardSpeed - runManager.BaseForwardSpeed) / range);
            float rate = Mathf.Lerp(4f, 26f, speedT);
            if (player != null && (player.IsHoming || player.IsGroundSlamming))
                rate += 40f;
            rate += 75f * RushBoost;
            int count = Mathf.FloorToInt(rate * Time.unscaledDeltaTime + Random.value);
            for (int i = 0; i < count; i++)
            {
                Vector2 ring = Random.insideUnitCircle.normalized * Random.Range(2.6f, 5f);
                var p = new ParticleSystem.EmitParams
                {
                    position = new Vector3(ring.x, ring.y * 0.75f + 0.5f, Random.Range(18f, 26f)),
                    velocity = new Vector3(0f, 0f, -Random.Range(55f, 75f) * (1f + RushBoost * 0.5f)),
                    startSize = Random.Range(0.02f, 0.04f) * (1f + RushBoost),
                    startLifetime = 0.45f,
                    startColor = Color.Lerp(new Color(0.85f, 0.75f, 1f, 0.35f), new Color(1f, 0.45f, 0.8f, 0.8f), RushBoost),
                };
                speedLines.Emit(p, 1);
            }
        }

        private void PlaySlash(Vector3 position, Quaternion rotation, float scale, Color color, float duration, bool mirror = false)
        {
            Slash s = slashes[nextSlash];
            nextSlash = (nextSlash + 1) % SlashPoolSize;
            s.Transform.SetPositionAndRotation(position, rotation);
            s.Transform.localScale = new Vector3(mirror ? -scale : scale, scale, scale);
            s.Start = Time.unscaledTime;
            s.Duration = duration;
            s.Color = color;
            s.Active = true;
            s.Renderer.enabled = true;
        }

        private void Burst(ParticleSystem system, Vector3 point, int count, float speed, Vector2 size, Vector2 life,
            Color a, Color b, float upBias, bool upwardOnly = false)
        {
            for (int i = 0; i < count; i++)
            {
                Vector3 dir = Random.onUnitSphere;
                if (upwardOnly)
                    dir.y = Mathf.Abs(dir.y);
                var p = new ParticleSystem.EmitParams
                {
                    position = point,
                    velocity = (dir + Vector3.up * upBias) * speed * Random.Range(0.45f, 1f),
                    startSize = Random.Range(size.x, size.y),
                    startLifetime = Random.Range(life.x, life.y),
                    startColor = Color.Lerp(a, b, Random.value),
                };
                system.Emit(p, 1);
            }
        }

        private void Ring(ParticleSystem system, Vector3 point, int count, float speed, Color color, float life)
        {
            for (int i = 0; i < count; i++)
            {
                float angle = i * Mathf.PI * 2f / count;
                Vector3 dir = new Vector3(Mathf.Cos(angle), 0.08f, Mathf.Sin(angle));
                var p = new ParticleSystem.EmitParams
                {
                    position = point + dir * 0.3f,
                    velocity = dir * speed * Random.Range(0.85f, 1.1f),
                    startSize = Random.Range(0.35f, 0.6f),
                    startLifetime = life * Random.Range(0.8f, 1.15f),
                    startColor = color,
                };
                system.Emit(p, 1);
            }
        }

        private ParticleSystem CreateSystem(string name, int max, ParticleSystemRenderMode mode, float velocityScale)
        {
            GameObject go = new GameObject(name);
            go.transform.SetParent(root.transform, false);
            ParticleSystem ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            ParticleSystem.MainModule main = ps.main;
            main.playOnAwake = false;
            main.loop = false;
            main.duration = 1f;
            main.maxParticles = max;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.useUnscaledTime = true;
            main.gravityModifier = 0f;
            ParticleSystem.EmissionModule emission = ps.emission;
            emission.enabled = false;
            ParticleSystem.ShapeModule shape = ps.shape;
            shape.enabled = false;
            ParticleSystem.ColorOverLifetimeModule col = ps.colorOverLifetime;
            col.enabled = true;
            Gradient g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0.8f, 0.4f), new GradientAlphaKey(0f, 1f) });
            col.color = g;
            ParticleSystem.SizeOverLifetimeModule size = ps.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 0.2f));
            ParticleSystem.LimitVelocityOverLifetimeModule drag = ps.limitVelocityOverLifetime;
            drag.enabled = mode == ParticleSystemRenderMode.Stretch && name == "Sparks";
            drag.drag = 3.5f;
            ParticleSystemRenderer r = go.GetComponent<ParticleSystemRenderer>();
            r.sharedMaterial = additive;
            r.renderMode = mode;
            r.velocityScale = velocityScale;
            r.lengthScale = 1f;
            r.shadowCastingMode = ShadowCastingMode.Off;
            r.receiveShadows = false;
            ps.Play();
            return ps;
        }

        // Crescent in the local XY plane; UV.x along the arc, UV.y inner(0)->outer(1).
        private static Mesh BuildCrescent(int segments, float inner, float outer, float degrees)
        {
            Vector3[] v = new Vector3[(segments + 1) * 2];
            Vector2[] uv = new Vector2[v.Length];
            int[] tris = new int[segments * 6];
            float start = -degrees * 0.5f * Mathf.Deg2Rad;
            float step = degrees * Mathf.Deg2Rad / segments;
            for (int i = 0; i <= segments; i++)
            {
                float t = i / (float)segments;
                float a = start + step * i;
                // Taper both ends into a blade shape.
                float thickness = Mathf.Sin(t * Mathf.PI);
                float rIn = Mathf.Lerp(outer, inner, thickness);
                Vector3 dir = new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0f);
                v[i * 2] = dir * rIn;
                v[i * 2 + 1] = dir * outer;
                uv[i * 2] = new Vector2(t, 0f);
                uv[i * 2 + 1] = new Vector2(t, 1f);
            }
            for (int i = 0; i < segments; i++)
            {
                int k = i * 6, b = i * 2;
                tris[k] = b; tris[k + 1] = b + 1; tris[k + 2] = b + 3;
                tris[k + 3] = b; tris[k + 4] = b + 3; tris[k + 5] = b + 2;
            }
            Mesh m = new Mesh { name = "Slash Crescent", vertices = v, uv = uv, triangles = tris };
            m.RecalculateBounds();
            return m;
        }

        public void Clear()
        {
            foreach (Slash s in slashes)
                if (s != null)
                {
                    s.Active = false;
                    if (s.Renderer != null)
                        s.Renderer.enabled = false;
                }
            if (sparks != null)
            {
                sparks.Clear();
                wisps.Clear();
                dust.Clear();
                speedLines.Clear();
            }
            if (Weapon != null)
                Weapon.ClearEffects();
        }
    }
}
