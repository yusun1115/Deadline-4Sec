using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Deadline4Sec
{
    // Weapon Skin Unique VFX (2026-11-21, Weapon Skin Unique VFX.md): every
    // weapon effect comes from the equipped skin's WeaponVfxProfile, in one
    // place:
    //  - idle: the skin's WeaponSkinFx layers (or its idle prefab) on the weapon,
    //    removed with the previous skin before the new one starts,
    //  - swing: a slash of the skin's own shape (crescent, star arc, ice arc,
    //    heavy arc, segmented digital arc, void arc) with its particles,
    //  - trail: the skin's trail texture and colours, on its blade tip,
    //  - hit: the skin's kill burst (and homing path).
    // Before this, CombatParticles drew every swing and kill slash in the
    // NEON REAPER pink whatever the skin (the colour was hard-coded there), so
    // the default effect showed with every skin. Visual only: no gameplay
    // state, unscaled time (keeps moving through hit stop), pooled.
    // Added by WeaponSkinVisual (the object holding the weapon).
    public sealed class WeaponVfxController : MonoBehaviour
    {
        private const int SlashPoolSize = 8;
        private const string IdleObjectName = "Skin Idle VFX";
        public const string IdleAnchorName = "VFX_IdleAnchor";
        public const string BladeEndName = "VFX_BladeEnd";

        private static readonly Color HotWhite = new Color(1f, 0.92f, 0.97f);
        private static readonly int ProgressId = Shader.PropertyToID("_Progress");
        private static readonly int FadeId = Shader.PropertyToID("_Fade");
        private static readonly int ColorId = Shader.PropertyToID("_Color");
        private static readonly int DarkColorId = Shader.PropertyToID("_DarkColor");
        private static readonly int TailId = Shader.PropertyToID("_Tail");
        // The built-in scythe's blade, in its own node's space (= a skin prefab's space).
        private static readonly Bounds BuiltInBlade = new Bounds(new Vector3(-0.15f, 0.39f, 0f), new Vector3(0.7f, 0.54f, 0.24f));

        public static WeaponVfxController Current { get; private set; }

        private sealed class Slash
        {
            public Transform Transform;
            public MeshFilter Filter;
            public MeshRenderer Renderer;
            public MaterialPropertyBlock Block;
            public float Start, Duration, Tail;
            public Color Color, Dark;
            public bool Active;
        }

        private sealed class Pooled
        {
            public GameObject Instance;
            public float Until;
        }

        private WeaponVfxProfile profile = new WeaponVfxProfile();
        private Transform hand;
        private GameObject idleObject;
        private GameObject idlePrefabInstance;
        private TrailRenderer trail;
        private GameObject root;
        private Material slashAdd, slashDark;
        private readonly Slash[] slashes = new Slash[SlashPoolSize];
        private int nextSlash;
        private readonly Dictionary<int, ParticleSystem> systems = new Dictionary<int, ParticleSystem>();
        private readonly Dictionary<GameObject, List<Pooled>> prefabPool = new Dictionary<GameObject, List<Pooled>>();
        private readonly List<Pooled> livePooled = new List<Pooled>();
        private float tipEmitDebt;

        public WeaponVfxProfile Profile => profile;
        public WeaponSkinFx.Effect Style => profile.style;
        public Color Primary => profile.primaryColor;
        public Color Secondary => profile.secondaryColor;
        // For tests / debugging.
        public GameObject IdleEffect => idlePrefabInstance != null ? idlePrefabInstance : idleObject;
        public WeaponSkinFx.Effect IdleStyle { get; private set; } = WeaponSkinFx.Effect.None;
        public WeaponSkinFx.Effect LastSwingStyle { get; private set; } = WeaponSkinFx.Effect.None;
        public WeaponSkinFx.Effect LastHitStyle { get; private set; } = WeaponSkinFx.Effect.None;
        public Color LastSwingColor { get; private set; }
        public int SwingCount { get; private set; }
        public TrailRenderer Trail => trail;
        public bool TrailEmitting
        {
            get => trail != null && trail.emitting;
            set { if (trail != null) trail.emitting = value; }
        }

        private void Awake() => Current = this;

        private void OnEnable() => Current = this;

        private void OnDestroy()
        {
            if (Current == this)
                Current = null;
            if (root != null)
                Destroy(root);
            if (slashAdd != null)
                Destroy(slashAdd);
            if (slashDark != null)
                Destroy(slashDark);
        }

        // ---------------- skin ----------------

        // handNode: the built-in weapon's (animated) node; skinWeapon: the skin
        // model placed in it (null: the built-in scythe shows).
        public void Apply(GameExtrasCatalog.Skin skin, Transform handNode, GameObject skinWeapon)
        {
            hand = handNode;
            profile = skin != null && skin.vfx != null ? skin.vfx : new WeaponVfxProfile();
            ClearIdle();
            StartIdle(skinWeapon);
            AttachTrail(skinWeapon);
        }

        // Old idle effects go first: stopped, cleared and removed.
        public void ClearIdle()
        {
            if (idleObject != null)
            {
                foreach (ParticleSystem ps in idleObject.GetComponentsInChildren<ParticleSystem>(true))
                    ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                idleObject.SetActive(false);
                Destroy(idleObject);
                idleObject = null;
            }
            if (idlePrefabInstance != null)
            {
                idlePrefabInstance.SetActive(false);
                Destroy(idlePrefabInstance);
                idlePrefabInstance = null;
            }
            IdleStyle = WeaponSkinFx.Effect.None;
        }

        private void StartIdle(GameObject skinWeapon)
        {
            WeaponSkinFx onSkin = skinWeapon != null ? skinWeapon.GetComponent<WeaponSkinFx>() : null;
            Transform weapon = skinWeapon != null ? skinWeapon.transform : hand;
            if (weapon == null)
                return;
            if (profile.idleVfxPrefab != null)
            {
                if (onSkin != null)
                    Destroy(onSkin); // not started yet: its layers never get built
                Transform anchor = FindDeep(weapon, IdleAnchorName);
                idlePrefabInstance = Instantiate(profile.idleVfxPrefab, anchor != null ? anchor : weapon, false);
                if (anchor == null)
                    idlePrefabInstance.transform.localPosition = BladeOf(skinWeapon).center;
                SetLayer(idlePrefabInstance, weapon.gameObject.layer);
                IdleStyle = profile.style;
                return;
            }
            WeaponSkinFx fx = onSkin;
            if (fx == null)
            {
                // The built-in scythe (or a skin prefab without its own): an
                // idle object in the hand node, removed at the next skin change.
                idleObject = new GameObject(IdleObjectName);
                idleObject.transform.SetParent(weapon, false);
                idleObject.layer = weapon.gameObject.layer;
                fx = idleObject.AddComponent<WeaponSkinFx>();
                fx.Configure(profile.style, profile.primaryColor, profile.secondaryColor, BladeOf(skinWeapon));
            }
            // The catalog decides, whatever the prefab carries.
            fx.Configure(profile.style, profile.primaryColor, profile.secondaryColor, profile.idleIntensity, profile.idleVfxScale);
            IdleStyle = profile.style;
        }

        private static Bounds BladeOf(GameObject skinWeapon)
        {
            if (skinWeapon != null && skinWeapon.TryGetComponent(out WeaponSkinFx fx))
                return fx.Blade;
            return BuiltInBlade;
        }

        // ---------------- trail ----------------

        private void AttachTrail(GameObject skinWeapon)
        {
            if (hand == null)
                return;
            if (trail == null)
            {
                GameObject tip = new GameObject("Weapon Trail");
                trail = tip.AddComponent<TrailRenderer>();
                trail.minVertexDistance = 0.05f;
                trail.shadowCastingMode = ShadowCastingMode.Off;
                trail.receiveShadows = false;
                trail.emitting = false;
            }
            // In the hand node (it outlives the skin models), at the blade's tip.
            trail.transform.SetParent(hand, false);
            trail.transform.localPosition = TipOf(skinWeapon);
            trail.gameObject.layer = hand.gameObject.layer;
            trail.Clear();
            StyleTrail();
        }

        private Vector3 TipOf(GameObject skinWeapon)
        {
            Transform weapon = skinWeapon != null ? skinWeapon.transform : hand;
            Transform anchor = FindDeep(weapon, BladeEndName);
            if (anchor != null)
                return hand.InverseTransformPoint(anchor.position);
            if (skinWeapon == null && hand.TryGetComponent(out MeshFilter filter) && filter.sharedMesh != null)
                return FarCorner(filter.sharedMesh.bounds, hand.InverseTransformPoint(hand.parent != null ? hand.parent.position : hand.position), 0.8f);
            return FarCorner(BladeOf(skinWeapon), Vector3.zero, 0.9f);
        }

        // The corner of a box farthest from a point (the grip): the blade's tip.
        private static Vector3 FarCorner(Bounds b, Vector3 from, float inset)
        {
            Vector3 best = b.center;
            float far = -1f;
            for (int i = 0; i < 8; i++)
            {
                Vector3 corner = b.center + Vector3.Scale(b.extents,
                    new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1)) * inset;
                float d = (corner - from).sqrMagnitude;
                if (d > far)
                {
                    far = d;
                    best = corner;
                }
            }
            return best;
        }

        private void StyleTrail()
        {
            Color a = profile.primaryColor, b = profile.secondaryColor;
            WeaponVfxSprites.Shape shape = WeaponVfxSprites.Shape.TrailSmooth;
            float width = 0.32f, time = 0.14f;
            bool tile = false;
            Color head = HotWhite;
            switch (profile.style)
            {
                case WeaponSkinFx.Effect.Starlight: shape = WeaponVfxSprites.Shape.TrailStars; width = 0.34f; time = 0.2f; tile = true; break;
                case WeaponSkinFx.Effect.FrostBite: shape = WeaponVfxSprites.Shape.TrailIce; width = 0.3f; time = 0.12f; break;
                case WeaponSkinFx.Effect.BloodMoon: shape = WeaponVfxSprites.Shape.TrailHeavy; width = 0.5f; time = 0.2f; head = Color.Lerp(a, HotWhite, 0.3f); break;
                case WeaponSkinFx.Effect.CyberDeath: shape = WeaponVfxSprites.Shape.TrailDigital; width = 0.3f; time = 0.16f; tile = true; head = Color.Lerp(a, HotWhite, 0.5f); break;
                case WeaponSkinFx.Effect.Eclipse: shape = WeaponVfxSprites.Shape.TrailVoid; width = 0.42f; time = 0.18f; head = Color.Lerp(a, HotWhite, 0.2f); break;
            }
            trail.sharedMaterial = profile.trailMaterial != null ? profile.trailMaterial : WeaponVfxSprites.Get(shape);
            trail.time = time;
            trail.widthCurve = new AnimationCurve(new Keyframe(0f, width), new Keyframe(1f, profile.style == WeaponSkinFx.Effect.BloodMoon ? width * 0.4f : 0f));
            trail.textureMode = tile ? LineTextureMode.Tile : LineTextureMode.Stretch;
            trail.textureScale = new Vector2(tile ? 1.8f : 1f, 1f);
            Gradient g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(head, 0f), new GradientColorKey(a, 0.3f), new GradientColorKey(b, 1f) },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0f, 1f) });
            trail.colorGradient = g;
        }

        // ---------------- swing / hit ----------------

        // A lane attack's swing at `position` (rotation: the arc's plane).
        public void PlaySwing(Vector3 position, Quaternion rotation, bool mirror)
        {
            Ensure();
            SwingCount++;
            LastSwingStyle = profile.style;
            LastSwingColor = profile.primaryColor;
            float scale = 1.15f * profile.attackVfxScale;
            float duration = profile.attackDuration;
            Color a = profile.primaryColor, b = profile.secondaryColor;
            switch (profile.style)
            {
                case WeaponSkinFx.Effect.Starlight:
                    PlaySlash(StarArc, false, position, rotation, scale, a, duration * 1.1f, mirror, tail: 0.45f);
                    StarsAlongArc(position, rotation, scale, mirror, 9, 150f, 1.05f);
                    break;
                case WeaponSkinFx.Effect.FrostBite:
                    PlaySlash(IceArc, false, position, rotation, scale, Color.Lerp(a, Color.white, 0.25f), duration * 0.9f, mirror, tail: 0.4f);
                    Along(position, rotation, scale, mirror, 160f, 1.3f, 10, WeaponVfxSprites.Shape.Shard, false, 4.5f, new Vector2(0.06f, 0.12f), new Vector2(0.2f, 0.32f), Color.white, a);
                    Along(position, rotation, scale, mirror, 160f, 1.0f, 3, WeaponVfxSprites.Shape.Smoke, false, 0.6f, new Vector2(0.35f, 0.5f), new Vector2(0.3f, 0.4f), a * 0.35f, b * 0.25f);
                    break;
                case WeaponSkinFx.Effect.BloodMoon:
                    // Heavy: thick dark arc, a red afterimage just behind it, fragments falling.
                    PlaySlash(HeavyArc, true, position, rotation * Quaternion.Euler(0f, 0f, mirror ? 18f : -18f), scale * 1.2f, a,
                        duration * 1.9f, mirror, tail: 1f, dark: new Color(0.16f, 0f, 0.02f));
                    PlaySlash(HeavyArc, false, position, rotation * Quaternion.Euler(0f, 0f, mirror ? 26f : -26f), scale * 1.28f, a * 0.55f,
                        duration * 1.6f, mirror, tail: 0.8f, delay: 0.05f);
                    Along(position, rotation, scale * 1.2f, mirror, 120f, 1.4f, 9, WeaponVfxSprites.Shape.Shard, false, 3f, new Vector2(0.07f, 0.13f), new Vector2(0.3f, 0.45f), a, b, gravity: true);
                    break;
                case WeaponSkinFx.Effect.CyberDeath:
                    // Digital: a segmented arc, a glitch copy a beat later, pixels and 0/1 left behind.
                    PlaySlash(DigitalArc, false, position, rotation, scale, a, duration, mirror, tail: 0.5f);
                    PlaySlash(DigitalArc, false, position + rotation * new Vector3(0.08f, -0.05f, 0f), rotation * Quaternion.Euler(0f, 0f, mirror ? -7f : 7f),
                        scale * 0.93f, b, duration * 0.9f, mirror, tail: 0.35f, delay: 0.04f);
                    Along(position, rotation, scale, mirror, 165f, 1.25f, 10, WeaponVfxSprites.Shape.Pixel, false, 2.5f, new Vector2(0.05f, 0.09f), new Vector2(0.18f, 0.3f), a, Color.white);
                    Along(position, rotation, scale, mirror, 165f, 1.0f, 3, WeaponVfxSprites.Shape.Zero, false, 0.4f, new Vector2(0.1f, 0.14f), new Vector2(0.28f, 0.4f), a, b, drift: Vector3.down * 0.8f);
                    Along(position, rotation, scale, mirror, 165f, 1.0f, 3, WeaponVfxSprites.Shape.One, false, 0.4f, new Vector2(0.1f, 0.14f), new Vector2(0.28f, 0.4f), a, b, drift: Vector3.down * 0.8f);
                    break;
                case WeaponSkinFx.Effect.Eclipse:
                    // Void: a wide dark arc with a violet rim, a crack left in the air, motes pulled in.
                    PlaySlash(VoidArc, true, position, rotation, scale * 1.15f, a, duration * 1.5f, mirror, tail: 0.9f, dark: new Color(0.02f, 0f, 0.05f));
                    PlaySlash(VoidArc, false, position, rotation, scale * 1.2f, a * 0.7f, duration * 1.2f, mirror, tail: 0.5f);
                    PlaySlash(Crack, true, position + rotation * new Vector3(0f, 0f, 0.05f), rotation * Quaternion.Euler(0f, 0f, mirror ? -20f : 20f),
                        scale, b, 0.4f, mirror, tail: 1.5f, dark: new Color(0.01f, 0f, 0.03f), delay: 0.06f);
                    Inward(position + rotation * Vector3.zero, scale * 1.3f, 10, a, b);
                    break;
                default:
                    // NEON REAPER: its wide crescent and a few pink sparks.
                    PlaySlash(Crescent, false, position, rotation, scale, a, duration * 0.9f, mirror);
                    Along(position, rotation, scale, mirror, 170f, 1.3f, 6, WeaponVfxSprites.Shape.Dot, false, 3f, new Vector2(0.05f, 0.09f), new Vector2(0.15f, 0.25f), a, HotWhite);
                    break;
            }
            SpawnPrefab(profile.attackVfxPrefab, position, rotation);
        }

        // A kill (lane / air / homing): the skin's slash across the target and its burst.
        public void PlayHit(Vector3 point, Quaternion facing, bool homing)
        {
            Ensure();
            LastHitStyle = profile.style;
            Color a = profile.primaryColor, b = profile.secondaryColor;
            float roll = Random.Range(-35f, 35f);
            Quaternion rot = facing * Quaternion.Euler(0f, 0f, roll + 30f);
            float scale = 1.1f * profile.attackVfxScale;
            switch (profile.style)
            {
                case WeaponSkinFx.Effect.Starlight:
                    PlaySlash(StarArc, false, point, rot, scale, a, 0.18f, false, tail: 0.45f);
                    Burst(WeaponVfxSprites.Shape.Star, false, point, 10, 4f, new Vector2(0.1f, 0.2f), new Vector2(0.3f, 0.5f), a, Color.white);
                    Burst(WeaponVfxSprites.Shape.Star, false, point, 1, 0f, new Vector2(0.7f, 0.7f), new Vector2(0.22f, 0.22f), Color.white, a);
                    break;
                case WeaponSkinFx.Effect.FrostBite:
                    PlaySlash(IceArc, false, point, rot, scale, Color.Lerp(a, Color.white, 0.25f), 0.15f, false, tail: 0.4f);
                    Burst(WeaponVfxSprites.Shape.Shard, false, point, 12, 7f, new Vector2(0.08f, 0.16f), new Vector2(0.22f, 0.35f), Color.white, a);
                    Burst(WeaponVfxSprites.Shape.Crystal, false, point, 4, 2f, new Vector2(0.14f, 0.22f), new Vector2(0.3f, 0.45f), Color.white, b);
                    break;
                case WeaponSkinFx.Effect.BloodMoon:
                    PlaySlash(HeavyArc, true, point, rot, scale * 1.2f, a, 0.28f, false, tail: 1f, dark: new Color(0.16f, 0f, 0.02f));
                    Burst(WeaponVfxSprites.Shape.Shard, false, point, 12, 6f, new Vector2(0.08f, 0.15f), new Vector2(0.35f, 0.5f), a, b, gravity: true);
                    Burst(WeaponVfxSprites.Shape.Ring, false, point, 1, 0f, new Vector2(1.4f, 1.4f), new Vector2(0.2f, 0.2f), a, a);
                    break;
                case WeaponSkinFx.Effect.CyberDeath:
                    PlaySlash(DigitalArc, false, point, rot, scale, a, 0.16f, false, tail: 0.5f);
                    Burst(WeaponVfxSprites.Shape.Pixel, false, point, 14, 5f, new Vector2(0.06f, 0.11f), new Vector2(0.2f, 0.35f), a, Color.white);
                    Burst(WeaponVfxSprites.Shape.Zero, false, point, 3, 1.2f, new Vector2(0.12f, 0.16f), new Vector2(0.3f, 0.4f), a, b);
                    Burst(WeaponVfxSprites.Shape.One, false, point, 3, 1.2f, new Vector2(0.12f, 0.16f), new Vector2(0.3f, 0.4f), a, b);
                    break;
                case WeaponSkinFx.Effect.Eclipse:
                    PlaySlash(VoidArc, true, point, rot, scale * 1.15f, a, 0.26f, false, tail: 0.9f, dark: new Color(0.02f, 0f, 0.05f));
                    Inward(point, 1.4f, 14, a, b);
                    Burst(WeaponVfxSprites.Shape.Smoke, true, point, 3, 0.3f, new Vector2(0.4f, 0.6f), new Vector2(0.3f, 0.4f),
                        new Color(0.05f, 0f, 0.1f, 0.6f), new Color(0.1f, 0.02f, 0.18f, 0.5f));
                    break;
                default:
                    PlaySlash(Crescent, false, point, rot, scale, a, 0.16f, false);
                    Burst(WeaponVfxSprites.Shape.Dot, false, point, 12, 7f, new Vector2(0.06f, 0.12f), new Vector2(0.18f, 0.3f), a, HotWhite, stretch: true);
                    break;
            }
            if (homing)
                PlaySlash(SlashFor(profile.style), false, point, facing * Quaternion.Euler(0f, 0f, roll - 70f), scale, b, 0.16f, false);
            SpawnPrefab(profile.hitVfxPrefab, point, facing);
        }

        // The homing dash's path: the skin's own bits along it.
        public void PlayHomingPath(Vector3 start, Vector3 end)
        {
            Ensure();
            Vector3 dir = (end - start).normalized;
            WeaponVfxSprites.Shape shape = TipShape(profile.style, out bool dark);
            for (int i = 0; i < 12; i++)
            {
                Emit(shape, dark, false, Vector3.Lerp(start, end, i / 12f) + Random.insideUnitSphere * 0.25f, -dir * Random.Range(2f, 6f),
                    Random.Range(0.08f, 0.14f), Random.Range(0.15f, 0.3f), Color.Lerp(profile.primaryColor, profile.secondaryColor, Random.value));
            }
        }

        public void ClearEffects()
        {
            foreach (Slash s in slashes)
                if (s != null)
                {
                    s.Active = false;
                    if (s.Renderer != null) // gone already while the scene unloads
                        s.Renderer.enabled = false;
                }
            foreach (ParticleSystem ps in systems.Values)
                if (ps != null)
                    ps.Clear();
            if (trail != null)
                trail.Clear();
        }

        // ---------------- internals ----------------

        private void LateUpdate()
        {
            float now = Time.unscaledTime;
            foreach (Slash s in slashes)
            {
                if (s == null || !s.Active || s.Renderer == null)
                    continue;
                float t = (now - s.Start) / s.Duration;
                if (t < 0f)
                    continue; // delayed (afterimage)
                if (t >= 1f)
                {
                    s.Active = false;
                    s.Renderer.enabled = false;
                    continue;
                }
                s.Renderer.enabled = true;
                s.Block.SetFloat(ProgressId, Mathf.Lerp(0f, 1.5f, t));
                s.Block.SetFloat(FadeId, 1f - t * t);
                s.Block.SetColor(ColorId, s.Color);
                s.Block.SetColor(DarkColorId, s.Dark);
                s.Block.SetFloat(TailId, s.Tail);
                s.Renderer.SetPropertyBlock(s.Block);
            }
            for (int i = livePooled.Count - 1; i >= 0; i--)
                if (now >= livePooled[i].Until)
                {
                    if (livePooled[i].Instance != null)
                        livePooled[i].Instance.SetActive(false);
                    livePooled.RemoveAt(i);
                }
            // Bits shed from the blade tip while the trail runs (stars, code, shards, smoke).
            if (trail != null && trail.emitting && profile.style != WeaponSkinFx.Effect.NeonReaper)
            {
                tipEmitDebt += Time.unscaledDeltaTime * 28f;
                WeaponVfxSprites.Shape shape = TipShape(profile.style, out bool dark);
                while (tipEmitDebt >= 1f)
                {
                    tipEmitDebt -= 1f;
                    bool falls = profile.style == WeaponSkinFx.Effect.BloodMoon || profile.style == WeaponSkinFx.Effect.CyberDeath;
                    Emit(shape, dark, profile.style == WeaponSkinFx.Effect.BloodMoon, trail.transform.position + Random.insideUnitSphere * 0.08f,
                        (falls ? Vector3.down : Random.onUnitSphere) * Random.Range(0.3f, 0.9f), Random.Range(0.06f, 0.12f) * (dark ? 2.5f : 1f),
                        Random.Range(0.2f, 0.4f), dark ? new Color(0.06f, 0f, 0.12f, 0.5f) : Color.Lerp(profile.primaryColor, profile.secondaryColor, Random.value));
                }
            }
            else
                tipEmitDebt = 0f;
        }

        private static WeaponVfxSprites.Shape TipShape(WeaponSkinFx.Effect style, out bool dark)
        {
            dark = false;
            switch (style)
            {
                case WeaponSkinFx.Effect.Starlight: return WeaponVfxSprites.Shape.Star;
                case WeaponSkinFx.Effect.FrostBite: return WeaponVfxSprites.Shape.Shard;
                case WeaponSkinFx.Effect.BloodMoon: return WeaponVfxSprites.Shape.Drop;
                case WeaponSkinFx.Effect.CyberDeath: return Random.value < 0.5f ? WeaponVfxSprites.Shape.Zero : WeaponVfxSprites.Shape.One;
                case WeaponSkinFx.Effect.Eclipse: dark = true; return WeaponVfxSprites.Shape.Smoke;
                default: return WeaponVfxSprites.Shape.Dot;
            }
        }

        private void Ensure()
        {
            if (root != null)
                return;
            root = new GameObject("Weapon Vfx");
            if (gameObject.scene.IsValid())
                UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(root, gameObject.scene);
            Shader slash = Resources.Load<Shader>("Feedback/FxSlash");
            if (slash != null)
            {
                slashAdd = new Material(slash) { name = "Weapon Slash Add (Runtime)" };
                slashDark = new Material(slash) { name = "Weapon Slash Dark (Runtime)" };
                slashDark.SetFloat("_Dark", 1f);
                slashDark.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
                slashDark.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
            }
            for (int i = 0; i < SlashPoolSize; i++)
            {
                GameObject go = new GameObject("Weapon Slash " + i);
                go.transform.SetParent(root.transform, false);
                MeshFilter filter = go.AddComponent<MeshFilter>();
                MeshRenderer r = go.AddComponent<MeshRenderer>();
                r.shadowCastingMode = ShadowCastingMode.Off;
                r.receiveShadows = false;
                r.enabled = false;
                slashes[i] = new Slash { Transform = go.transform, Filter = filter, Renderer = r, Block = new MaterialPropertyBlock() };
            }
        }

        private void PlaySlash(Mesh mesh, bool darkArc, Vector3 position, Quaternion rotation, float scale, Color color, float duration,
            bool mirror, float tail = 0.6f, Color? dark = null, float delay = 0f)
        {
            Material material = darkArc ? slashDark : slashAdd;
            if (material == null || mesh == null)
                return;
            Slash s = slashes[nextSlash];
            nextSlash = (nextSlash + 1) % SlashPoolSize;
            s.Filter.sharedMesh = mesh;
            s.Renderer.sharedMaterial = material;
            s.Transform.SetPositionAndRotation(position, rotation);
            s.Transform.localScale = new Vector3(mirror ? -scale : scale, scale, scale);
            s.Start = Time.unscaledTime + delay;
            s.Duration = Mathf.Max(0.05f, duration);
            s.Color = color;
            s.Dark = dark ?? Color.black;
            s.Tail = tail;
            s.Active = true;
            s.Renderer.enabled = delay <= 0f;
        }

        private static Mesh SlashFor(WeaponSkinFx.Effect style)
        {
            switch (style)
            {
                case WeaponSkinFx.Effect.Starlight: return StarArc;
                case WeaponSkinFx.Effect.FrostBite: return IceArc;
                case WeaponSkinFx.Effect.BloodMoon: return HeavyArc;
                case WeaponSkinFx.Effect.CyberDeath: return DigitalArc;
                case WeaponSkinFx.Effect.Eclipse: return VoidArc;
                default: return Crescent;
            }
        }

        // A point on a slash arc (crescent space: XY plane, angle 0 = +X).
        private static Vector3 ArcPoint(Vector3 position, Quaternion rotation, float scale, bool mirror, float degrees, float radius, float t)
        {
            float a = (-degrees * 0.5f + degrees * t) * Mathf.Deg2Rad;
            Vector3 local = new Vector3(Mathf.Cos(a) * (mirror ? -1f : 1f), Mathf.Sin(a), 0f) * radius * scale;
            return position + rotation * local;
        }

        private void Along(Vector3 position, Quaternion rotation, float scale, bool mirror, float degrees, float radius, int count,
            WeaponVfxSprites.Shape shape, bool dark, float speed, Vector2 size, Vector2 life, Color a, Color b,
            bool gravity = false, Vector3 drift = default)
        {
            for (int i = 0; i < count; i++)
            {
                float t = (i + Random.value) / count;
                Vector3 p = ArcPoint(position, rotation, scale, mirror, degrees, radius, t);
                Vector3 outward = (p - position).normalized;
                Emit(shape, dark, gravity, p, outward * speed * Random.Range(0.5f, 1f) + drift, Random.Range(size.x, size.y),
                    Random.Range(life.x, life.y), Color.Lerp(a, b, Random.value));
            }
        }

        // Stars left along the arc, lingering a moment, and a star burst at its end.
        private void StarsAlongArc(Vector3 position, Quaternion rotation, float scale, bool mirror, int count, float degrees, float radius)
        {
            Color a = profile.primaryColor, b = profile.secondaryColor;
            Along(position, rotation, scale, mirror, degrees, radius, count, WeaponVfxSprites.Shape.Star, false, 0.5f,
                new Vector2(0.1f, 0.2f), new Vector2(0.35f, 0.5f), a, b);
            Vector3 end = ArcPoint(position, rotation, scale, mirror, degrees, radius, 1f);
            Burst(WeaponVfxSprites.Shape.Star, false, end, 1, 0f, new Vector2(0.45f, 0.45f), new Vector2(0.25f, 0.25f), Color.white, a);
            Burst(WeaponVfxSprites.Shape.Star, false, end, 4, 2.2f, new Vector2(0.08f, 0.13f), new Vector2(0.25f, 0.35f), a, b);
        }

        // Motes on a ring drawn into the centre (ECLIPSE).
        private void Inward(Vector3 centre, float radius, int count, Color a, Color b)
        {
            for (int i = 0; i < count; i++)
            {
                float angle = (i + Random.value * 0.5f) * Mathf.PI * 2f / count;
                Vector3 offset = new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), Random.Range(-0.3f, 0.3f)) * radius;
                float life = Random.Range(0.25f, 0.35f);
                Emit(WeaponVfxSprites.Shape.Shard, false, false, centre + offset, -offset / life, Random.Range(0.08f, 0.14f), life,
                    Color.Lerp(a, b, Random.value * 0.4f));
            }
        }

        private void Burst(WeaponVfxSprites.Shape shape, bool dark, Vector3 point, int count, float speed, Vector2 size, Vector2 life,
            Color a, Color b, bool gravity = false, bool stretch = false)
        {
            for (int i = 0; i < count; i++)
            {
                Vector3 dir = Random.onUnitSphere;
                Emit(shape, dark, gravity, point, (dir + Vector3.up * (gravity ? 0.6f : 0.2f)) * speed * Random.Range(0.45f, 1f),
                    Random.Range(size.x, size.y), Random.Range(life.x, life.y), Color.Lerp(a, b, Random.value), stretch);
            }
        }

        private void Emit(WeaponVfxSprites.Shape shape, bool dark, bool gravity, Vector3 position, Vector3 velocity, float size, float life,
            Color color, bool stretch = false)
        {
            ParticleSystem ps = System(shape, dark, gravity, stretch);
            if (ps == null)
                return;
            var p = new ParticleSystem.EmitParams
            {
                position = position,
                velocity = velocity,
                startSize = size,
                startLifetime = life,
                startColor = color,
                rotation = shape == WeaponVfxSprites.Shape.Star || shape == WeaponVfxSprites.Shape.Zero || shape == WeaponVfxSprites.Shape.One
                    ? 0f : Random.Range(0f, 360f),
            };
            ps.Emit(p, 1);
        }

        // One pooled world-space system per shape (and blend / gravity / stretch), made on first use.
        private ParticleSystem System(WeaponVfxSprites.Shape shape, bool dark, bool gravity, bool stretch)
        {
            Ensure();
            int key = ((int)shape << 3) | (dark ? 1 : 0) | (gravity ? 2 : 0) | (stretch ? 4 : 0);
            if (systems.TryGetValue(key, out ParticleSystem found) && found != null)
                return found;
            Material material = WeaponVfxSprites.Get(shape, dark);
            if (material == null)
                return null;
            GameObject go = new GameObject("Weapon Vfx " + shape + (dark ? " Dark" : "") + (gravity ? " Falling" : ""));
            go.transform.SetParent(root.transform, false);
            ParticleSystem ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            ParticleSystem.MainModule main = ps.main;
            main.playOnAwake = false;
            main.loop = false;
            main.duration = 1f;
            main.maxParticles = 80;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.useUnscaledTime = true;
            main.gravityModifier = gravity ? 1.6f : 0f;
            ParticleSystem.EmissionModule emission = ps.emission;
            emission.enabled = false;
            ParticleSystem.ShapeModule shapeModule = ps.shape;
            shapeModule.enabled = false;
            ParticleSystem.ColorOverLifetimeModule col = ps.colorOverLifetime;
            col.enabled = true;
            Gradient g = new Gradient();
            bool flicker = shape == WeaponVfxSprites.Shape.Pixel || shape == WeaponVfxSprites.Shape.Zero || shape == WeaponVfxSprites.Shape.One;
            g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                flicker
                    ? new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0.2f, 0.3f), new GradientAlphaKey(1f, 0.5f), new GradientAlphaKey(0.8f, 0.75f), new GradientAlphaKey(0f, 1f) }
                    : new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0.8f, 0.5f), new GradientAlphaKey(0f, 1f) });
            col.color = g;
            ParticleSystem.SizeOverLifetimeModule size = ps.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, shape == WeaponVfxSprites.Shape.Ring || shape == WeaponVfxSprites.Shape.Smoke
                ? AnimationCurve.EaseInOut(0f, 0.4f, 1f, 1.3f)
                : AnimationCurve.Linear(0f, 1f, 1f, 0.25f));
            ParticleSystemRenderer r = go.GetComponent<ParticleSystemRenderer>();
            r.sharedMaterial = material;
            r.renderMode = stretch ? ParticleSystemRenderMode.Stretch : ParticleSystemRenderMode.Billboard;
            r.velocityScale = stretch ? 0.035f : 0f;
            r.lengthScale = 1f;
            r.shadowCastingMode = ShadowCastingMode.Off;
            r.receiveShadows = false;
            ps.Play();
            systems[key] = ps;
            return ps;
        }

        // Optional prefab effects from the profile: pooled, switched off after 1.5 s.
        private void SpawnPrefab(GameObject prefab, Vector3 position, Quaternion rotation)
        {
            if (prefab == null)
                return;
            Ensure();
            if (!prefabPool.TryGetValue(prefab, out List<Pooled> pool))
                prefabPool[prefab] = pool = new List<Pooled>();
            Pooled item = null;
            foreach (Pooled candidate in pool)
                if (candidate.Instance != null && !candidate.Instance.activeSelf)
                {
                    item = candidate;
                    break;
                }
            if (item == null)
            {
                if (pool.Count >= 4)
                    return;
                item = new Pooled { Instance = Instantiate(prefab, root.transform) };
                pool.Add(item);
            }
            item.Instance.transform.SetPositionAndRotation(position, rotation);
            item.Instance.SetActive(true);
            foreach (ParticleSystem ps in item.Instance.GetComponentsInChildren<ParticleSystem>(true))
            {
                ps.Clear(true);
                ps.Play(true);
            }
            item.Until = Time.unscaledTime + 1.5f;
            livePooled.Add(item);
        }

        private static Transform FindDeep(Transform t, string name)
        {
            if (t == null)
                return null;
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

        private static void SetLayer(GameObject go, int layer)
        {
            foreach (Transform t in go.GetComponentsInChildren<Transform>(true))
                t.gameObject.layer = layer;
        }

        // ---------------- slash shapes (built once) ----------------

        private static Mesh crescent, starArc, iceArc, heavyArc, digitalArc, voidArc, crack;
        // NEON REAPER: the original wide crescent.
        private static Mesh Crescent => crescent != null ? crescent : crescent = Arc("Neon Crescent", 24, 0.55f, 1.35f, 170f);
        // STARLIGHT: a thin, long golden line.
        private static Mesh StarArc => starArc != null ? starArc : starArc = Arc("Star Arc", 24, 0.92f, 1.3f, 150f);
        // FROST BITE: sharp, toothed outer edge.
        private static Mesh IceArc => iceArc != null ? iceArc : iceArc = Arc("Ice Arc", 18, 0.7f, 1.4f, 160f, jag: 0.28f);
        // BLOOD MOON: short, very thick and heavy.
        private static Mesh HeavyArc => heavyArc != null ? heavyArc : heavyArc = Arc("Heavy Arc", 22, 0.62f, 1.62f, 125f, blunt: true);
        // CYBER DEATH: blocks with gaps, stepped radius.
        private static Mesh DigitalArc => digitalArc != null ? digitalArc : digitalArc = Segmented("Digital Arc", 9, 0.8f, 1.3f, 165f);
        // ECLIPSE: wide and broad.
        private static Mesh VoidArc => voidArc != null ? voidArc : voidArc = Arc("Void Arc", 28, 0.35f, 1.6f, 205f);
        private static Mesh Crack => crack != null ? crack : crack = BuildCrack();

        // Crescent in the local XY plane; UV.x along the arc, UV.y inner(0)->outer(1).
        private static Mesh Arc(string name, int segments, float inner, float outer, float degrees, float jag = 0f, bool blunt = false)
        {
            var v = new Vector3[(segments + 1) * 2];
            var uv = new Vector2[v.Length];
            var tris = new int[segments * 6];
            float start = -degrees * 0.5f * Mathf.Deg2Rad;
            float step = degrees * Mathf.Deg2Rad / segments;
            for (int i = 0; i <= segments; i++)
            {
                float t = i / (float)segments;
                float a = start + step * i;
                // Tapered at both ends into a blade (a heavy arc stays thick longer).
                // (sin(pi) is a hair below zero: clamp before the power, or it is NaN.)
                float sine = Mathf.Max(0f, Mathf.Sin(t * Mathf.PI));
                float thickness = blunt ? Mathf.Pow(sine, 0.45f) : sine;
                float rOut = outer + (jag > 0f ? (i % 2 == 0 ? jag : -jag * 0.3f) * thickness : 0f);
                float rIn = Mathf.Lerp(rOut, inner, thickness);
                Vector3 dir = new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0f);
                v[i * 2] = dir * rIn;
                v[i * 2 + 1] = dir * rOut;
                uv[i * 2] = new Vector2(t, 0f);
                uv[i * 2 + 1] = new Vector2(t, 1f);
            }
            for (int i = 0; i < segments; i++)
            {
                int k = i * 6, b = i * 2;
                tris[k] = b; tris[k + 1] = b + 1; tris[k + 2] = b + 3;
                tris[k + 3] = b; tris[k + 4] = b + 3; tris[k + 5] = b + 2;
            }
            Mesh m = new Mesh { name = name, vertices = v, uv = uv, triangles = tris };
            m.RecalculateBounds();
            return m;
        }

        private static Mesh Segmented(string name, int blocks, float inner, float outer, float degrees)
        {
            var v = new List<Vector3>();
            var uv = new List<Vector2>();
            var tris = new List<int>();
            float span = degrees / blocks;
            for (int i = 0; i < blocks; i++)
            {
                // 70 % block, 30 % gap; each block a step in or out.
                float a0 = -degrees * 0.5f + i * span, a1 = a0 + span * 0.7f;
                float shift = ((i * 5) % 3 - 1) * 0.08f;
                float rIn = inner + shift, rOut = outer + shift - (i == 0 || i == blocks - 1 ? 0.2f : 0f);
                int baseIndex = v.Count;
                foreach (float a in new[] { a0, a1 })
                {
                    Vector3 dir = new Vector3(Mathf.Cos(a * Mathf.Deg2Rad), Mathf.Sin(a * Mathf.Deg2Rad), 0f);
                    float t = (a + degrees * 0.5f) / degrees;
                    v.Add(dir * rIn); uv.Add(new Vector2(t, 0.3f));
                    v.Add(dir * rOut); uv.Add(new Vector2(t, 1f));
                }
                tris.AddRange(new[] { baseIndex, baseIndex + 1, baseIndex + 3, baseIndex, baseIndex + 3, baseIndex + 2 });
            }
            Mesh m = new Mesh { name = name };
            m.SetVertices(v);
            m.SetUVs(0, uv);
            m.SetTriangles(tris, 0);
            m.RecalculateBounds();
            return m;
        }

        // A jagged tear in the air (ECLIPSE), drawn dark with a violet rim.
        private static Mesh BuildCrack()
        {
            var v = new List<Vector3>();
            var uv = new List<Vector2>();
            var tris = new List<int>();
            int points = 9;
            System.Random rng = new System.Random(7);
            for (int i = 0; i <= points; i++)
            {
                float t = i / (float)points;
                float x = Mathf.Lerp(-1.1f, 1.1f, t);
                float y = (float)(rng.NextDouble() - 0.5) * 0.35f;
                float half = Mathf.Sin(t * Mathf.PI) * 0.12f + 0.01f;
                v.Add(new Vector3(x, y - half, 0f)); uv.Add(new Vector2(t, 0f));
                v.Add(new Vector3(x, y + half, 0f)); uv.Add(new Vector2(t, 1f));
            }
            for (int i = 0; i < points; i++)
            {
                int b = i * 2;
                tris.AddRange(new[] { b, b + 1, b + 3, b, b + 3, b + 2 });
            }
            Mesh m = new Mesh { name = "Void Crack" };
            m.SetVertices(v);
            m.SetUVs(0, uv);
            m.SetTriangles(tris, 0);
            m.RecalculateBounds();
            return m;
        }
    }
}
