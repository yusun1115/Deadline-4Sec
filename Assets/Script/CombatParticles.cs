using UnityEngine;
using UnityEngine.Rendering;

namespace Deadline4Sec
{
    // Particle and slash-arc layer on top of CombatVisualFeedback's outline rings:
    // sparks, soul wisps, dust, scythe slashes, a scythe trail and speed lines.
    // Everything is pooled and created once; effects run on unscaled time so they
    // keep moving through hit stop and never touch gameplay state.
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
        private TrailRenderer scytheTrail;
        private float trailUntil;

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
            speedLines = CreateSystem("Speed Lines", 80, ParticleSystemRenderMode.Stretch, 0.02f);
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
            {
                player.ActionPerformed += OnPlayerAction;
                AttachScytheTrail();
            }
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
            Color main = stomp ? Amber : homing ? Cyan : Pink;
            Burst(sparks, point, stomp ? 26 : 20, stomp ? 11f : 9f, new Vector2(0.06f, 0.16f), new Vector2(0.18f, 0.34f), main, HotWhite, 0.4f);
            Burst(wisps, point, 7, 1.6f, new Vector2(0.25f, 0.5f), new Vector2(0.5f, 0.85f), Pink, Violet, -0.6f);
            Burst(wisps, point, 1, 0f, new Vector2(1.6f, 1.6f), new Vector2(0.12f, 0.12f), HotWhite, HotWhite, 0f);
            float roll = Random.Range(-35f, 35f);
            Quaternion facing = Quaternion.LookRotation(view.transform.forward, Vector3.up);
            PlaySlash(point, facing * Quaternion.Euler(0f, 0f, roll + 30f), 1.1f, main, 0.16f);
            if (homing)
                PlaySlash(point, facing * Quaternion.Euler(0f, 0f, roll - 70f), 1.1f, Cyan, 0.16f);
            if (stomp)
                Ring(dust, point + Vector3.down * 0.4f, 18, 6f, Dust, 0.45f);
        }

        public void PlaySlam(Vector3 point, float radius)
        {
            Ring(dust, point + Vector3.up * 0.1f, 40, radius * 3.2f, Dust, 0.6f);
            Ring(sparks, point + Vector3.up * 0.15f, 32, radius * 4.5f, Violet, 0.35f);
            Burst(sparks, point, 24, 10f, new Vector2(0.08f, 0.2f), new Vector2(0.25f, 0.45f), Pink, HotWhite, 1.2f, true);
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
            Vector3 dir = end - start;
            for (int i = 0; i < 12; i++)
            {
                var p = new ParticleSystem.EmitParams
                {
                    position = Vector3.Lerp(start, end, i / 12f) + Random.insideUnitSphere * 0.25f,
                    velocity = -dir.normalized * Random.Range(2f, 6f),
                    startSize = Random.Range(0.06f, 0.12f),
                    startLifetime = Random.Range(0.15f, 0.3f),
                    startColor = Color.Lerp(Cyan, HotWhite, Random.value),
                };
                sparks.Emit(p, 1);
            }
            trailUntil = Time.unscaledTime + 0.25f;
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
                    PlaySlash(chest, rot, 1.15f, Pink, 0.14f, side < 0);
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

            if (scytheTrail != null)
                scytheTrail.emitting = Playing && (now < trailUntil || (player != null && player.IsHoming));

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
            int count = Mathf.FloorToInt(rate * Time.unscaledDeltaTime + Random.value);
            for (int i = 0; i < count; i++)
            {
                Vector2 ring = Random.insideUnitCircle.normalized * Random.Range(2.6f, 5f);
                var p = new ParticleSystem.EmitParams
                {
                    position = new Vector3(ring.x, ring.y * 0.75f + 0.5f, Random.Range(18f, 26f)),
                    velocity = new Vector3(0f, 0f, -Random.Range(55f, 75f)),
                    startSize = Random.Range(0.02f, 0.04f),
                    startLifetime = 0.45f,
                    startColor = new Color(0.85f, 0.75f, 1f, 0.35f),
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

        private void AttachScytheTrail()
        {
            Transform socket = FindDeep(player.transform, "WeaponSocket");
            Renderer blade = null;
            if (socket != null)
                blade = socket.GetComponentInChildren<MeshRenderer>();
            if (blade == null)
                return;
            // The tip sits at the far end of the blade mesh from the socket.
            MeshFilter filter = blade.GetComponent<MeshFilter>();
            Vector3 tipLocal = Vector3.zero;
            if (filter != null && filter.sharedMesh != null)
            {
                Bounds b = filter.sharedMesh.bounds;
                Vector3 socketLocal = blade.transform.InverseTransformPoint(socket.position);
                float best = -1f;
                for (int i = 0; i < 8; i++)
                {
                    Vector3 corner = b.center + Vector3.Scale(b.extents,
                        new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1)) * 0.8f;
                    float d = (corner - socketLocal).sqrMagnitude;
                    if (d > best)
                    {
                        best = d;
                        tipLocal = corner;
                    }
                }
            }
            GameObject tip = new GameObject("ScytheTrail");
            tip.transform.SetParent(blade.transform, false);
            tip.transform.localPosition = tipLocal;
            scytheTrail = tip.AddComponent<TrailRenderer>();
            scytheTrail.sharedMaterial = ribbon;
            scytheTrail.time = 0.14f;
            scytheTrail.minVertexDistance = 0.05f;
            scytheTrail.widthCurve = new AnimationCurve(new Keyframe(0f, 0.32f), new Keyframe(1f, 0f));
            Gradient g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(HotWhite, 0f), new GradientColorKey(Pink, 0.35f), new GradientColorKey(Violet, 1f) },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0f, 1f) });
            scytheTrail.colorGradient = g;
            scytheTrail.shadowCastingMode = ShadowCastingMode.Off;
            scytheTrail.receiveShadows = false;
            scytheTrail.emitting = false;
        }

        private static Transform FindDeep(Transform t, string name)
        {
            if (t.name == name)
                return t;
            foreach (Transform c in t)
            {
                Transform f = FindDeep(c, name);
                if (f != null)
                    return f;
            }
            return null;
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
            if (scytheTrail != null)
                scytheTrail.Clear();
        }
    }
}
