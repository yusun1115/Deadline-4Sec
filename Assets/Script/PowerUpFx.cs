using UnityEngine;
using UnityEngine.Rendering;

namespace Deadline4Sec
{
    // Power-up presentation (2026-10-05 request): pickup burst, held effect and
    // end effect for each of the six items, with their own sounds.
    //   Freeze Clock   — crystal "쩡" + blue flash; iced timer, crystals, edge frost; cracks and shatters
    //   Soul Amplifier — rising energy rings + "x2 SCORE!"; aura on score/combo, big popups, higher pitch
    //   Reaper Rush    — FOV punch + energy burst; speed lines, afterimages, trails, heavy slashes, drone
    //   Soul Magnet    — field pulse; orbit ring, inward wisps, coins curve in with a zip
    //   Combo Seal     — chains wrap the combo badge + "COMBO LOCK"; gauge frozen; chains snap
    //   Time Heart     — heartbeat timer pulse; glowing frame, edge pulse, >4s colour; last big beat
    // Presentation only: reads PowerUpManager each frame and never changes rules.
    public sealed partial class PowerUpFx : MonoBehaviour
    {
        private const int TypeCount = 6;

        private static readonly Color Ice = new Color(0.6f, 0.88f, 1f);
        private static readonly Color Gold = new Color(1f, 0.82f, 0.3f);
        private static readonly Color RushPink = new Color(1f, 0.25f, 0.6f);
        private static readonly Color RushViolet = new Color(0.6f, 0.3f, 1f);
        private static readonly Color MagnetCyan = new Color(0.4f, 0.95f, 1f);
        private static readonly Color SealViolet = new Color(0.72f, 0.48f, 1f);
        private static readonly Color HeartRed = new Color(1f, 0.22f, 0.38f);
        private static readonly Color HotWhite = new Color(1f, 0.95f, 1f);

        private PowerUpManager manager;
        private GameFlowManager flow;
        private GameTimer timer;
        private RunManager run;
        private PlayerController player;
        private CharacterController body;
        private CameraFeedbackController feedback;
        private CombatParticles particles;
        private HudJuice juice;
        private SoundLibrary library;
        private AudioSource oneShots;
        private AudioSource drone;

        private readonly bool[] wasActive = new bool[TypeCount];
        private readonly float[] lastRemaining = new float[TypeCount];
        private float rushBlend;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Attach()
        {
            UnityEngine.SceneManagement.SceneManager.sceneLoaded -= OnSceneLoaded;
            UnityEngine.SceneManagement.SceneManager.sceneLoaded += OnSceneLoaded;
            OnSceneLoaded(default, default);
        }

        private static void OnSceneLoaded(UnityEngine.SceneManagement.Scene s, UnityEngine.SceneManagement.LoadSceneMode m)
        {
            PowerUpManager found = FindFirstObjectByType<PowerUpManager>();
            if (found != null && !found.TryGetComponent(out PowerUpFx _) && FindFirstObjectByType<GameFlowManager>() != null)
                found.gameObject.AddComponent<PowerUpFx>();
        }

        private void Start()
        {
            manager = GetComponent<PowerUpManager>();
            flow = FindFirstObjectByType<GameFlowManager>();
            timer = FindFirstObjectByType<GameTimer>();
            run = FindFirstObjectByType<RunManager>();
            player = FindFirstObjectByType<PlayerController>();
            feedback = FindFirstObjectByType<CameraFeedbackController>();
            if (feedback != null)
                feedback.TryGetComponent(out particles);
            if (run != null)
                run.TryGetComponent(out juice);
            if (player != null)
            {
                player.TryGetComponent(out body);
                player.EnemyKilled += OnEnemyKilled;
            }
            library = SoundLibrary.Instance;
            oneShots = gameObject.AddComponent<AudioSource>();
            oneShots.playOnAwake = false;
            oneShots.spatialBlend = 0f;
            drone = gameObject.AddComponent<AudioSource>();
            drone.playOnAwake = false;
            drone.loop = true;
            drone.spatialBlend = 0f;
            drone.volume = 0f;
            drone.clip = Clip(library != null ? library.rushDrone : null, "RushDrone");
            foreach (string name in new[] { "FreezeStart", "FreezeShatter", "AmplifierStart", "RushStart", "RushSlash",
                         "MagnetHum", "MagnetZip", "ChainLock", "ChainBreak", "Heartbeat", "HeartSurge" })
                SfxSynth.Get(name); // build now, not on the pickup frame
            BuildWorld();
            BuildHud();
        }

        private void OnDestroy()
        {
            if (player != null)
                player.EnemyKilled -= OnEnemyKilled;
            DestroyWorld();
        }

        private bool Playing => flow != null && flow.IsPlaying;

        private void LateUpdate()
        {
            if (manager == null)
                return;
            bool playing = Playing;
            for (int i = 0; i < TypeCount; i++)
            {
                PowerUpType type = (PowerUpType)i;
                bool active = manager.IsActive(type);
                float remaining = manager.Remaining(type);
                if (active && (!wasActive[i] || remaining > lastRemaining[i] + 0.05f))
                {
                    if (playing)
                        OnAcquire(type, wasActive[i]);
                }
                else if (!active && wasActive[i])
                {
                    OnEnd(type, playing);
                }
                wasActive[i] = active;
                lastRemaining[i] = remaining;
            }
            float dt = Time.unscaledDeltaTime;
            UpdateWorld(dt, playing);
            UpdateHud(dt, playing);
        }

        private bool Held(PowerUpType type) => manager != null && manager.IsActive(type);

        // ---------------- routing ----------------

        private void OnAcquire(PowerUpType type, bool refresh)
        {
            Vector3 chest = Chest();
            switch (type)
            {
                case PowerUpType.FreezeClock:
                    Play(library != null ? library.freezeStart : null, "FreezeStart");
                    FreezeAcquired(refresh);
                    if (particles != null)
                        particles.PlayBurst(chest, Ice, HotWhite, 24, 7f, 0.12f);
                    break;
                case PowerUpType.SoulAmplifier:
                    Play(library != null ? library.amplifierStart : null, "AmplifierStart");
                    for (int k = 0; k < 3; k++)
                        SpawnRing(Gold, 0.4f, 2.4f, -0.9f, 1.6f + k * 0.35f, 0.75f, k * 0.12f, 0.9f);
                    if (particles != null)
                        particles.PlayBurst(chest, Gold, HotWhite, 30, 8f, 0.14f);
                    AmplifierAcquired();
                    break;
                case PowerUpType.ReaperRush:
                    Play(library != null ? library.rushStart : null, "RushStart");
                    if (feedback != null)
                        feedback.PunchFov(16f, 0.5f);
                    SpawnRing(RushPink, 0.3f, 4.5f, -0.6f, -0.6f, 0.4f, 0f, 1.2f);
                    SpawnRing(HotWhite, 0.2f, 3f, 0f, 0.2f, 0.3f, 0.05f, 1f);
                    if (particles != null)
                    {
                        particles.PlayBurst(chest, RushPink, HotWhite, 60, 14f, 0.2f);
                        particles.PlayBurst(chest, RushViolet, RushPink, 30, 9f, 0.16f);
                    }
                    break;
                case PowerUpType.SoulMagnet:
                    Play(library != null ? library.magnetHum : null, "MagnetHum");
                    float reach = manager.Balance != null ? manager.Balance.MagnetRadius : 6f;
                    for (int k = 0; k < 3; k++)
                        SpawnRing(MagnetCyan, 0.5f, reach, -0.4f, -0.4f, 0.55f, k * 0.14f, 0.85f);
                    break;
                case PowerUpType.ComboSeal:
                    Play(library != null ? library.chainLock : null, "ChainLock");
                    SealAcquired(refresh);
                    break;
                case PowerUpType.TimeHeart:
                    Play(library != null ? library.heartbeat : null, "Heartbeat");
                    Play(library != null ? library.heartSurge : null, "HeartSurge", 0.7f);
                    SpawnRing(HeartRed, 0.4f, 3f, -0.2f, 0.4f, 0.6f, 0f, 0.9f);
                    HeartAcquired();
                    break;
            }
        }

        private void OnEnd(PowerUpType type, bool playing)
        {
            switch (type)
            {
                case PowerUpType.FreezeClock:
                    if (playing)
                        Play(library != null ? library.freezeShatter : null, "FreezeShatter");
                    FreezeEnded(playing);
                    break;
                case PowerUpType.SoulAmplifier:
                    AmplifierEnded(playing);
                    break;
                case PowerUpType.ReaperRush:
                    break; // rushBlend eases the speed lines, trails and FOV back out
                case PowerUpType.SoulMagnet:
                    if (playing)
                    {
                        SpawnRing(MagnetCyan, MagnetFieldRadius, 0.05f, -0.75f, -0.75f, 0.4f, 0f, 0.8f);
                        Play(library != null ? library.magnetHum : null, "MagnetHum", 0.45f, 0.7f);
                    }
                    break;
                case PowerUpType.ComboSeal:
                    if (playing)
                        Play(library != null ? library.chainBreak : null, "ChainBreak");
                    SealEnded(playing);
                    break;
                case PowerUpType.TimeHeart:
                    if (playing)
                        Play(library != null ? library.heartbeat : null, "Heartbeat", 1f, 0.9f);
                    HeartEnded(playing);
                    break;
            }
        }

        private void OnEnemyKilled(Enemy enemy, string attack)
        {
            if (enemy == null || !Playing || !Held(PowerUpType.ReaperRush))
                return;
            if (particles != null)
                particles.PlayRushSlash(enemy.transform.position + Vector3.up * 0.9f);
            Play(library != null ? library.rushSlash : null, "RushSlash", 0.9f);
        }

        // ---------------- audio ----------------

        private static AudioClip Clip(AudioClip authored, string synth) => authored != null ? authored : SfxSynth.Get(synth);

        private void Play(AudioClip authored, string synth, float volume = 1f, float pitch = 1f)
        {
            if (!GamePreferences.SoundEnabled || oneShots == null)
                return;
            oneShots.pitch = pitch;
            oneShots.PlayOneShot(Clip(authored, synth), volume * (library != null ? library.sfxVolume : 0.8f));
        }

        // ---------------- world effects ----------------

        private struct RingFx
        {
            public bool Active;
            public float Start, Duration, Delay;
            public float From, To, Low, High, Strength;
            public Color Color;
            public MaterialPropertyBlock Block;
        }

        // One renderer per body part: baked skinned meshes first, then the scythe meshes.
        private struct Ghost
        {
            public bool Active;
            public float Start;
            public Mesh[] Baked;
            public MeshFilter[] Filters;
            public MeshRenderer[] Renderers;
        }

        private const int RingCount = 16;
        private const int GhostCount = 6;
        private const float GhostLife = 0.3f;
        private const float MagnetFieldRadius = 1.4f;
        private static readonly int TintId = Shader.PropertyToID("_Tint");
        private static readonly int ColorId = Shader.PropertyToID("_Color");

        private readonly RingFx[] rings = new RingFx[RingCount];
        private readonly Ghost[] ghosts = new Ghost[GhostCount];
        private Mesh ringMesh;
        private Material ringMaterial;
        private Material ghostMaterial;
        private MaterialPropertyBlock fieldBlock;
        private MaterialPropertyBlock[] ghostBlocks;
        private GameObject ghostRoot;
        private SkinnedMeshRenderer[] skinned;
        private MeshRenderer[] weapon;
        private TrailRenderer bodyTrail;
        private float nextGhost;
        private float nextWisp;
        private float magnetBlend;
        private float zipCooldown;
        private readonly System.Collections.Generic.HashSet<CoinPickup> zipped =
            new System.Collections.Generic.HashSet<CoinPickup>();

        private void BuildWorld()
        {
            ringMesh = BuildAnnulus(72, 0.8f);
            Shader additive = Resources.Load<Shader>("Feedback/FxAdditive");
            if (additive != null)
            {
                ringMaterial = new Material(additive) { name = "Power-Up Ring (Runtime)" };
                ringMaterial.SetFloat("_Ribbon", 1f);
                ringMaterial.SetFloat("_Softness", 1.5f);
                ringMaterial.SetFloat("_Core", 0.5f);
            }
            Shader ghost = Resources.Load<Shader>("Feedback/FxGhost");
            if (ghost != null)
                ghostMaterial = new Material(ghost) { name = "Rush Afterimage (Runtime)" };
            for (int i = 0; i < RingCount; i++)
                rings[i].Block = new MaterialPropertyBlock();
            fieldBlock = new MaterialPropertyBlock();
            ghostBlocks = new MaterialPropertyBlock[GhostCount];
            for (int i = 0; i < GhostCount; i++)
                ghostBlocks[i] = new MaterialPropertyBlock();

            if (player == null)
                return;
            skinned = player.GetComponentsInChildren<SkinnedMeshRenderer>(true);
            Transform socket = FindDeep(player.transform, "WeaponSocket");
            weapon = socket != null ? socket.GetComponentsInChildren<MeshRenderer>(true) : new MeshRenderer[0];
            int parts = skinned.Length + weapon.Length;
            ghostRoot = new GameObject("Rush Afterimages");
            for (int i = 0; i < GhostCount; i++)
            {
                ghosts[i].Baked = new Mesh[skinned.Length];
                ghosts[i].Filters = new MeshFilter[parts];
                ghosts[i].Renderers = new MeshRenderer[parts];
                for (int k = 0; k < parts; k++)
                {
                    GameObject part = new GameObject("Ghost " + i + "." + k);
                    part.transform.SetParent(ghostRoot.transform, false);
                    ghosts[i].Filters[k] = part.AddComponent<MeshFilter>();
                    MeshRenderer r = part.AddComponent<MeshRenderer>();
                    r.sharedMaterial = ghostMaterial;
                    r.shadowCastingMode = ShadowCastingMode.Off;
                    r.receiveShadows = false;
                    r.enabled = false;
                    ghosts[i].Renderers[k] = r;
                    if (k < skinned.Length)
                        ghosts[i].Filters[k].sharedMesh = ghosts[i].Baked[k] = new Mesh { name = "Rush Ghost" };
                }
            }

            // Ribbon streaming off the body while rushing.
            GameObject trail = new GameObject("Rush Body Trail");
            trail.transform.SetParent(player.transform, false);
            trail.transform.localPosition = new Vector3(0f, 0.1f, -0.2f);
            bodyTrail = trail.AddComponent<TrailRenderer>();
            if (ringMaterial != null)
            {
                Material ribbon = new Material(ringMaterial) { name = "Rush Body Trail (Runtime)" };
                ribbon.SetFloat("_Softness", 1.2f);
                bodyTrail.sharedMaterial = ribbon;
            }
            bodyTrail.time = 0.22f;
            bodyTrail.minVertexDistance = 0.1f;
            bodyTrail.widthCurve = new AnimationCurve(new Keyframe(0f, 1.1f), new Keyframe(1f, 0f));
            Gradient g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(HotWhite, 0f), new GradientColorKey(RushPink, 0.3f), new GradientColorKey(RushViolet, 1f) },
                new[] { new GradientAlphaKey(0.8f, 0f), new GradientAlphaKey(0f, 1f) });
            bodyTrail.colorGradient = g;
            bodyTrail.shadowCastingMode = ShadowCastingMode.Off;
            bodyTrail.receiveShadows = false;
            bodyTrail.emitting = false;
        }

        private void DestroyWorld()
        {
            if (ringMesh != null)
                Destroy(ringMesh);
            if (ringMaterial != null)
                Destroy(ringMaterial);
            if (ghostMaterial != null)
                Destroy(ghostMaterial);
            foreach (Ghost g in ghosts)
                if (g.Baked != null)
                    foreach (Mesh mesh in g.Baked)
                        if (mesh != null)
                            Destroy(mesh);
            if (ghostRoot != null)
                Destroy(ghostRoot);
        }

        // Horizontal ring around the player: radius From→To, height Low→High
        // (relative to the waist, 1 m above the feet), fading out over Duration after Delay.
        private void SpawnRing(Color color, float from, float to, float low, float high, float duration, float delay, float strength)
        {
            for (int i = 0; i < RingCount; i++)
            {
                if (rings[i].Active)
                    continue;
                rings[i].Active = true;
                rings[i].Start = Time.unscaledTime;
                rings[i].Delay = delay;
                rings[i].Duration = duration;
                rings[i].From = from;
                rings[i].To = to;
                rings[i].Low = low + 1f;
                rings[i].High = high + 1f;
                rings[i].Color = color;
                rings[i].Strength = strength;
                return;
            }
        }

        private void UpdateWorld(float dt, bool playing)
        {
            float now = Time.unscaledTime;
            Vector3 feet = Feet();

            // Rings
            if (ringMaterial != null)
            {
                for (int i = 0; i < RingCount; i++)
                {
                    if (!rings[i].Active)
                        continue;
                    float t = (now - rings[i].Start - rings[i].Delay) / rings[i].Duration;
                    if (t < 0f)
                        continue;
                    if (t >= 1f || !playing)
                    {
                        rings[i].Active = false;
                        continue;
                    }
                    float e = 1f - (1f - t) * (1f - t);
                    float radius = Mathf.Lerp(rings[i].From, rings[i].To, e);
                    float height = Mathf.Lerp(rings[i].Low, rings[i].High, e);
                    Color c = rings[i].Color * (1.8f * rings[i].Strength * (1f - t) * Mathf.Min(1f, t * 8f + 0.3f));
                    rings[i].Block.SetColor(TintId, c);
                    DrawRing(feet + Vector3.up * height, radius, rings[i].Block);
                }
            }

            // Reaper Rush: blend in/out, afterimages, trail, speed lines, FOV
            bool rush = playing && Held(PowerUpType.ReaperRush);
            rushBlend = Mathf.MoveTowards(rushBlend, rush ? 1f : 0f, dt * (rush ? 4f : 3f));
            if (particles != null)
                particles.RushBoost = playing ? rushBlend : 0f;
            if (feedback != null)
                feedback.FovBias = playing ? 3f * rushBlend : 0f; // speed already maxes the speed FOV
            if (bodyTrail != null)
                bodyTrail.emitting = rush;
            if (rush && now >= nextGhost)
            {
                nextGhost = now + 0.07f;
                SpawnGhost(now);
            }
            DrawGhosts(now, playing);
            float droneTarget = rush && GamePreferences.SoundEnabled ? (library != null ? library.sfxVolume : 0.8f) * 0.55f : 0f;
            drone.volume = Mathf.MoveTowards(drone.volume, droneTarget, dt * (rush ? 1.5f : 1.2f));
            if (drone.volume > 0.001f && !drone.isPlaying)
                drone.Play();
            else if (drone.volume <= 0.001f && drone.isPlaying)
                drone.Stop();

            // Soul Magnet: orbiting field ring, inward wisps, zips as coins take off
            bool magnet = playing && Held(PowerUpType.SoulMagnet);
            magnetBlend = Mathf.MoveTowards(magnetBlend, magnet ? 1f : 0f, dt * 5f);
            if (magnetBlend > 0.001f && ringMaterial != null)
            {
                float breathe = 1f + Mathf.Sin(now * 3f) * 0.06f;
                fieldBlock.SetColor(TintId, MagnetCyan * (0.45f * magnetBlend * (0.75f + 0.25f * Mathf.Sin(now * 6f))));
                DrawRing(feet + Vector3.up * (0.25f + Mathf.Sin(now * 2f) * 0.08f), MagnetFieldRadius * breathe, fieldBlock);
            }
            if (magnet && particles != null && now >= nextWisp)
            {
                nextWisp = now + 0.05f;
                particles.EmitInward(feet + Vector3.up * 0.9f, MagnetFieldRadius * 1.3f, MagnetCyan, Carry());
            }
            zipCooldown -= dt;
            if (magnet)
            {
                foreach (CoinPickup coin in CoinPickup.Active)
                {
                    if (coin == null || !coin.BeingPulled || !zipped.Add(coin))
                        continue;
                    if (zipCooldown <= 0f)
                    {
                        zipCooldown = 0.05f;
                        Play(library != null ? library.magnetZip : null, "MagnetZip", 0.5f, Random.Range(0.9f, 1.25f));
                    }
                }
            }
            zipped.RemoveWhere(c => c == null || !c.isActiveAndEnabled);

            // Soul Amplifier: score sounds ring higher.
            if (feedback != null)
                feedback.ScorePitchBias = playing && Held(PowerUpType.SoulAmplifier) ? 0.18f : 0f;
        }

        private void DrawRing(Vector3 centre, float radius, MaterialPropertyBlock block)
        {
            var rp = new RenderParams(ringMaterial)
            {
                matProps = block,
                shadowCastingMode = ShadowCastingMode.Off,
                receiveShadows = false,
            };
            Graphics.RenderMesh(rp, ringMesh, 0, Matrix4x4.TRS(centre, Quaternion.identity, Vector3.one * radius));
        }

        private void SpawnGhost(float now)
        {
            if (ghostMaterial == null || skinned == null)
                return;
            int slot = -1;
            float oldest = float.MaxValue;
            for (int i = 0; i < GhostCount; i++)
            {
                if (!ghosts[i].Active) { slot = i; break; }
                if (ghosts[i].Start < oldest) { oldest = ghosts[i].Start; slot = i; }
            }
            Ghost g = ghosts[slot];
            bool any = false;
            for (int k = 0; k < skinned.Length; k++)
            {
                SkinnedMeshRenderer r = skinned[k];
                bool used = r != null && r.enabled && r.gameObject.activeInHierarchy;
                g.Renderers[k].enabled = used;
                if (!used)
                    continue;
                r.BakeMesh(g.Baked[k], true);
                g.Renderers[k].transform.SetPositionAndRotation(r.transform.position, r.transform.rotation);
                any = true;
            }
            for (int j = 0; j < weapon.Length; j++)
            {
                int k = skinned.Length + j;
                MeshRenderer r = weapon[j];
                MeshFilter filter = null;
                bool used = r != null && r.enabled && r.TryGetComponent(out filter) && filter.sharedMesh != null;
                g.Renderers[k].enabled = used;
                if (!used)
                    continue;
                g.Filters[k].sharedMesh = filter.sharedMesh;
                Transform part = g.Renderers[k].transform;
                part.SetPositionAndRotation(r.transform.position, r.transform.rotation);
                part.localScale = r.transform.lossyScale;
                any = true;
            }
            g.Start = now;
            g.Active = any;
            ghosts[slot] = g;
        }

        private void DrawGhosts(float now, bool playing)
        {
            if (ghostMaterial == null)
                return;
            for (int i = 0; i < GhostCount; i++)
            {
                if (!ghosts[i].Active)
                    continue;
                float t = (now - ghosts[i].Start) / GhostLife;
                if (t >= 1f || !playing)
                {
                    ghosts[i].Active = false;
                    foreach (MeshRenderer r in ghosts[i].Renderers)
                        r.enabled = false;
                    continue;
                }
                ghostBlocks[i].SetColor(ColorId, Color.Lerp(RushPink, RushViolet, t) * new Color(1f, 1f, 1f, 0.6f * (1f - t)));
                foreach (MeshRenderer r in ghosts[i].Renderers)
                    if (r.enabled)
                        r.SetPropertyBlock(ghostBlocks[i]);
            }
        }

        private Vector3 Feet()
        {
            if (body != null)
            {
                Bounds b = body.bounds;
                return new Vector3(b.center.x, b.min.y, b.center.z);
            }
            return player != null ? player.transform.position - Vector3.up : Vector3.zero;
        }

        private Vector3 Chest() => Feet() + Vector3.up * 1.2f;

        // Particles are simulated in world space; give them the run speed so they keep up.
        private Vector3 Carry() => Vector3.forward * (run != null ? run.CurrentForwardSpeed : 0f);

        // Flat annulus in XZ, outer radius 1; UV.y runs inner (0) → outer (1) so
        // the additive ribbon falloff makes a soft band.
        private static Mesh BuildAnnulus(int segments, float inner)
        {
            var v = new Vector3[(segments + 1) * 2];
            var uv = new Vector2[v.Length];
            var colors = new Color[v.Length];
            var tris = new int[segments * 6];
            for (int i = 0; i <= segments; i++)
            {
                float a = i * Mathf.PI * 2f / segments;
                Vector3 dir = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                v[i * 2] = dir * inner;
                v[i * 2 + 1] = dir;
                uv[i * 2] = new Vector2(i / (float)segments, 0f);
                uv[i * 2 + 1] = new Vector2(i / (float)segments, 1f);
                colors[i * 2] = colors[i * 2 + 1] = Color.white;
            }
            for (int i = 0; i < segments; i++)
            {
                int k = i * 6, b = i * 2;
                tris[k] = b; tris[k + 1] = b + 1; tris[k + 2] = b + 3;
                tris[k + 3] = b; tris[k + 4] = b + 3; tris[k + 5] = b + 2;
            }
            var mesh = new Mesh { name = "Power-Up Ring", vertices = v, uv = uv, colors = colors, triangles = tris };
            mesh.RecalculateBounds();
            return mesh;
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
    }
}
