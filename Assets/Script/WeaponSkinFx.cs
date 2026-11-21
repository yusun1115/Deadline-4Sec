using UnityEngine;
using UnityEngine.Rendering;

namespace Deadline4Sec
{
    // A weapon skin's idle effect (2026-10-30, unique per skin since
    // 2026-11-21): procedural particle layers around the blade, made of the
    // shared WeaponVfxSprites shapes, so a skin needs no particle assets.
    // Particles live in the runner's space: they ride along with the run but
    // trail behind the weapon's swings. Sizes are in world units whatever the
    // hand scale. Idle stays subtle: just enough to tell the skins apart.
    // Configured by WeaponVfxController from the skin's WeaponVfxProfile (the
    // values saved on the skin prefabs are only a fallback).
    public sealed class WeaponSkinFx : MonoBehaviour
    {
        // Saved as numbers on the prefabs: append only.
        public enum Effect { None, Starlight, FrostBite, BloodMoon, CyberDeath, Eclipse, NeonReaper }

        [SerializeField] private Effect effect;
        [SerializeField] private Color primary = Color.white;
        [SerializeField] private Color secondary = Color.white;
        // The blade's box in this transform's space (emission volume).
        [SerializeField] private Bounds blade = new Bounds(new Vector3(0f, 0.3f, 0f), new Vector3(0.6f, 0.6f, 0.1f));
        [SerializeField, Range(0f, 2f)] private float intensity = 1f;
        [SerializeField, Range(0.25f, 3f)] private float scale = 1f;

        private Transform space;
        private bool built;

        public Effect Kind => effect;
        public Bounds Blade => blade;
        public int LayerCount { get; private set; }

        public void Configure(Effect kind, Color main, Color second, Bounds bladeBox)
        {
            effect = kind;
            primary = main;
            secondary = second;
            blade = bladeBox;
        }

        public void Configure(Effect kind, Color main, Color second, float strength, float size)
        {
            effect = kind;
            primary = main;
            secondary = second;
            intensity = strength;
            scale = size;
        }

        private void Start() => Build();

        public void Build()
        {
            if (built || effect == Effect.None)
                return;
            built = true;
            PlayerController player = GetComponentInParent<PlayerController>();
            space = player != null ? player.transform : null;
            float k = intensity;
            float s = scale;

            switch (effect)
            {
                case Effect.NeonReaper:
                    // Pink energy sparks running up the blade, a faint glow.
                    Halo(primary, 0.22f);
                    Layer("Energy Sparks", WeaponVfxSprites.Shape.Dot, false, 16f * k, new Vector2(0.35f, 0.6f), new Vector2(0.04f, 0.08f) * s,
                        new Vector2(0.05f, 0.2f), primary, secondary, flowUp: 0.9f, stretch: 0.05f);
                    break;
                case Effect.Starlight:
                    // Little four-point stars twinkling and rising, gold and pale.
                    Halo(primary, 0.18f);
                    Layer("Star Twinkles", WeaponVfxSprites.Shape.Star, false, 11f * k, new Vector2(0.6f, 1f), new Vector2(0.08f, 0.15f) * s,
                        new Vector2(0.02f, 0.12f), primary, secondary, gravity: -0.06f, twinkle: true, spin: true);
                    Layer("Star Pops", WeaponVfxSprites.Shape.Star, false, 1.8f * k, new Vector2(0.35f, 0.5f), new Vector2(0.2f, 0.28f) * s,
                        Vector2.zero, Color.white, primary, pop: true);
                    break;
                case Effect.FrostBite:
                    // Ice crystals falling off the blade, a thin cold mist.
                    Layer("Ice Crystals", WeaponVfxSprites.Shape.Crystal, false, 9f * k, new Vector2(0.7f, 1.1f), new Vector2(0.07f, 0.12f) * s,
                        new Vector2(0f, 0.08f), Color.white, primary, gravity: 0.22f, spin: true);
                    Layer("Frost Mist", WeaponVfxSprites.Shape.Smoke, false, 5f * k, new Vector2(0.8f, 1.2f), new Vector2(0.2f, 0.34f) * s,
                        new Vector2(0.02f, 0.1f), primary * 0.35f, secondary * 0.15f, gravity: 0.06f, grow: true);
                    Layer("Ice Glints", WeaponVfxSprites.Shape.Shard, false, 4f * k, new Vector2(0.25f, 0.4f), new Vector2(0.05f, 0.09f) * s,
                        Vector2.zero, Color.white, secondary, pop: true, spin: true);
                    break;
                case Effect.BloodMoon:
                    // Red drops falling, a slow red pulse and a dark red aura.
                    Layer("Dark Aura", WeaponVfxSprites.Shape.Smoke, true, 4f * k, new Vector2(0.9f, 1.3f), new Vector2(0.28f, 0.42f) * s,
                        new Vector2(0f, 0.05f), new Color(0.25f, 0f, 0.03f, 0.35f), new Color(0.1f, 0f, 0.02f, 0f), grow: true);
                    Layer("Blood Drops", WeaponVfxSprites.Shape.Drop, false, 7f * k, new Vector2(0.5f, 0.75f), new Vector2(0.05f, 0.08f) * s,
                        Vector2.zero, primary, secondary, gravity: 1.1f);
                    Layer("Red Pulse", WeaponVfxSprites.Shape.Ring, false, 0.9f * k, new Vector2(0.8f, 0.8f), new Vector2(0.45f, 0.45f) * s,
                        Vector2.zero, primary * 0.6f, primary * 0.2f, grow: true, centre: true);
                    break;
                case Effect.CyberDeath:
                    // 0 / 1 code falling past the blade, flickering pixels and scan lines.
                    Layer("Code 0", WeaponVfxSprites.Shape.Zero, false, 5f * k, new Vector2(0.45f, 0.7f), new Vector2(0.07f, 0.1f) * s,
                        Vector2.zero, primary, secondary, fall: 0.6f, flicker: true);
                    Layer("Code 1", WeaponVfxSprites.Shape.One, false, 5f * k, new Vector2(0.45f, 0.7f), new Vector2(0.07f, 0.1f) * s,
                        Vector2.zero, primary, secondary, fall: 0.6f, flicker: true);
                    Layer("Pixels", WeaponVfxSprites.Shape.Pixel, false, 7f * k, new Vector2(0.15f, 0.35f), new Vector2(0.03f, 0.05f) * s,
                        Vector2.zero, secondary, primary, flicker: true);
                    Layer("Scan Lines", WeaponVfxSprites.Shape.Pixel, false, 2.5f * k, new Vector2(0.08f, 0.14f), new Vector2(0.035f, 0.05f) * s,
                        new Vector2(2f, 3.5f), primary, Color.white, stretch: 0.09f, sideways: true);
                    break;
                case Effect.Eclipse:
                    // Inward: purple motes and dark smoke drawn into a dark orb.
                    Layer("Dark Orb", WeaponVfxSprites.Shape.Smoke, true, 2f * k, new Vector2(1f, 1.3f), new Vector2(0.22f, 0.3f) * s,
                        Vector2.zero, new Color(0.04f, 0f, 0.08f, 0.55f), new Color(0.02f, 0f, 0.04f, 0f), centre: true, shrink: true);
                    Layer("Void Smoke", WeaponVfxSprites.Shape.Smoke, true, 5f * k, new Vector2(0.7f, 0.9f), new Vector2(0.16f, 0.28f) * s,
                        new Vector2(0.35f, 0.5f), new Color(0.08f, 0.02f, 0.14f, 0.4f), new Color(0.03f, 0f, 0.06f, 0f), inward: true, shrink: true);
                    Layer("Void Motes", WeaponVfxSprites.Shape.Dot, false, 12f * k, new Vector2(0.45f, 0.6f), new Vector2(0.04f, 0.08f) * s,
                        new Vector2(0.6f, 0.9f), primary, secondary, inward: true, shrink: true);
                    break;
            }
        }

        private void Halo(Color color, float strength) =>
            Layer("Halo", WeaponVfxSprites.Shape.Dot, false, 5f * intensity, new Vector2(0.4f, 0.6f), new Vector2(0.5f, 0.75f) * scale,
                Vector2.zero, color * strength, color * (strength * 0.3f), pop: true, centre: true);

        private ParticleSystem Layer(string name, WeaponVfxSprites.Shape shape, bool dark, float rate, Vector2 life, Vector2 size,
            Vector2 speed, Color start, Color end, float gravity = 0f, float stretch = 0f, bool twinkle = false, bool pop = false,
            bool grow = false, bool shrink = false, bool flicker = false, bool sideways = false, bool spin = false,
            bool inward = false, bool centre = false, float fall = 0f, float flowUp = 0f)
        {
            Material material = WeaponVfxSprites.Get(shape, dark);
            if (material == null)
                return null;
            GameObject go = new GameObject(name);
            go.transform.SetParent(transform, false);
            go.layer = gameObject.layer;
            ParticleSystem ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            LayerCount++;

            ParticleSystem.MainModule main = ps.main;
            main.loop = true;
            main.playOnAwake = true;
            main.duration = 1f;
            main.maxParticles = 30;
            main.startLifetime = new ParticleSystem.MinMaxCurve(life.x, life.y);
            main.startSize = new ParticleSystem.MinMaxCurve(size.x, size.y);
            main.startSpeed = inward ? new ParticleSystem.MinMaxCurve(-speed.y, -speed.x) : new ParticleSystem.MinMaxCurve(speed.x, speed.y);
            main.startColor = start;
            main.gravityModifier = gravity;
            main.scalingMode = ParticleSystemScalingMode.Shape;
            if (spin)
                main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            if (space != null)
            {
                main.simulationSpace = ParticleSystemSimulationSpace.Custom;
                main.customSimulationSpace = space;
            }
            else
                main.simulationSpace = ParticleSystemSimulationSpace.Local;

            ParticleSystem.EmissionModule emission = ps.emission;
            emission.rateOverTime = rate;

            ParticleSystem.ShapeModule shapeModule = ps.shape;
            if (inward)
            {
                // From a shell around the blade, straight at its centre.
                shapeModule.shapeType = ParticleSystemShapeType.Sphere;
                shapeModule.position = blade.center;
                shapeModule.radius = Mathf.Max(blade.extents.x, blade.extents.y) * 1.25f;
                shapeModule.radiusThickness = 0f;
                shapeModule.scale = Vector3.one;
            }
            else
            {
                shapeModule.shapeType = ParticleSystemShapeType.Box;
                shapeModule.position = blade.center;
                shapeModule.scale = centre ? blade.size * 0.15f : blade.size;
                shapeModule.randomDirectionAmount = 1f;
            }

            if (sideways || fall > 0f || flowUp > 0f)
            {
                ParticleSystem.VelocityOverLifetimeModule velocity = ps.velocityOverLifetime;
                velocity.enabled = true;
                if (sideways)
                {
                    velocity.space = ParticleSystemSimulationSpace.World;
                    velocity.x = new ParticleSystem.MinMaxCurve(-3f, 3f);
                    velocity.y = new ParticleSystem.MinMaxCurve(0f, 0f);
                }
                else if (fall > 0f)
                {
                    velocity.space = ParticleSystemSimulationSpace.World;
                    velocity.x = new ParticleSystem.MinMaxCurve(0f, 0f);
                    velocity.y = new ParticleSystem.MinMaxCurve(-fall * 1.3f, -fall * 0.7f);
                }
                else
                {
                    // Along the blade (its local up), towards the tip.
                    velocity.space = ParticleSystemSimulationSpace.Local;
                    velocity.x = new ParticleSystem.MinMaxCurve(0f, 0f);
                    velocity.y = new ParticleSystem.MinMaxCurve(flowUp * 0.6f, flowUp);
                }
                velocity.z = new ParticleSystem.MinMaxCurve(0f, 0f);
            }

            ParticleSystem.ColorOverLifetimeModule colorOverLife = ps.colorOverLifetime;
            colorOverLife.enabled = true;
            Gradient gradient = new Gradient();
            GradientAlphaKey[] alpha = flicker
                ? new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0.1f, 0.2f), new GradientAlphaKey(1f, 0.4f),
                    new GradientAlphaKey(0.15f, 0.6f), new GradientAlphaKey(0.9f, 0.8f), new GradientAlphaKey(0f, 1f) }
                : new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, pop ? 0.15f : 0.2f), new GradientAlphaKey(0f, 1f) };
            gradient.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Ratio(start, end), 1f) }, alpha);
            colorOverLife.color = gradient;

            ParticleSystem.SizeOverLifetimeModule sizeOverLife = ps.sizeOverLifetime;
            sizeOverLife.enabled = true;
            AnimationCurve curve = twinkle
                ? new AnimationCurve(new Keyframe(0f, 0.2f), new Keyframe(0.25f, 1f), new Keyframe(0.5f, 0.4f), new Keyframe(0.75f, 1f), new Keyframe(1f, 0f))
                : pop ? new AnimationCurve(new Keyframe(0f, 0.3f), new Keyframe(0.2f, 1f), new Keyframe(1f, 0f))
                : grow ? AnimationCurve.EaseInOut(0f, 0.5f, 1f, 1.4f)
                : shrink ? AnimationCurve.EaseInOut(0f, 1f, 1f, 0.15f)
                : AnimationCurve.Linear(0f, 1f, 1f, 0.3f);
            sizeOverLife.size = new ParticleSystem.MinMaxCurve(1f, curve);

            ParticleSystemRenderer render = go.GetComponent<ParticleSystemRenderer>();
            render.sharedMaterial = material;
            render.renderMode = stretch > 0f ? ParticleSystemRenderMode.Stretch : ParticleSystemRenderMode.Billboard;
            render.velocityScale = stretch;
            render.lengthScale = stretch > 0f ? 1.5f : 1f;
            render.shadowCastingMode = ShadowCastingMode.Off;
            render.receiveShadows = false;
            ps.Play();
            return ps;
        }

        // Colour over lifetime multiplies the start colour: express the end
        // colour relative to it (per channel, clamped), so a layer drifts start → end.
        private static Color Ratio(Color start, Color end) => new Color(
            Mathf.Clamp01(end.r / Mathf.Max(0.05f, start.r)),
            Mathf.Clamp01(end.g / Mathf.Max(0.05f, start.g)),
            Mathf.Clamp01(end.b / Mathf.Max(0.05f, start.b)), 1f);
    }
}
