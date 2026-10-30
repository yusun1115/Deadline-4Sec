using UnityEngine;
using UnityEngine.Rendering;

namespace Deadline4Sec
{
    // Weapon skin aura (2026-10-30): a halo and two procedural particle layers around the
    // blade, built at runtime with the shared additive FX shader, so a skin
    // needs no particle assets. Particles live in the runner's space: they ride
    // along with the run but trail behind the weapon's swings, and never stream
    // back past the camera. Sizes are in world units whatever the hand scale.
    public sealed class WeaponSkinFx : MonoBehaviour
    {
        public enum Effect { None, Starlight, FrostBite, BloodMoon, CyberDeath, Eclipse }

        [SerializeField] private Effect effect;
        [SerializeField] private Color primary = Color.white;
        [SerializeField] private Color secondary = Color.white;
        // The blade's box in this transform's space (emission volume).
        [SerializeField] private Bounds blade = new Bounds(new Vector3(0f, 0.3f, 0f), new Vector3(0.6f, 0.6f, 0.1f));
        [SerializeField, Range(0f, 2f)] private float intensity = 1f;

        private static Material sharedMaterial;
        private Transform space;

        public Effect Kind => effect;

        public void Configure(Effect kind, Color main, Color second, Bounds bladeBox)
        {
            effect = kind;
            primary = main;
            secondary = second;
            blade = bladeBox;
        }

        private void Start()
        {
            if (effect == Effect.None)
                return;
            if (sharedMaterial == null)
            {
                Shader fx = Resources.Load<Shader>("Feedback/FxAdditive");
                if (fx == null)
                    return;
                sharedMaterial = new Material(fx) { name = "Weapon Skin Fx (Runtime)" };
            }
            PlayerController player = GetComponentInParent<PlayerController>();
            space = player != null ? player.transform : null;

            // Every skin: a soft halo of its colour around the blade, readable
            // from the run camera; then its own two layers.
            Layer("Halo", 7f * intensity, new Vector2(0.35f, 0.55f), new Vector2(0.55f, 0.8f), Vector2.zero,
                primary * 0.32f, primary * 0.1f, pop: true);

            switch (effect)
            {
                case Effect.Starlight:
                    // Twinkling gold / white stars, and a few bright star pops.
                    Layer("Twinkles", 34f * intensity, new Vector2(0.5f, 0.9f), new Vector2(0.08f, 0.18f), new Vector2(0.05f, 0.35f),
                        primary, Color.white, gravity: -0.05f, twinkle: true);
                    Layer("Star Pops", 4f * intensity, new Vector2(0.18f, 0.28f), new Vector2(0.4f, 0.6f), Vector2.zero,
                        Color.white, primary, pop: true);
                    break;
                case Effect.FrostBite:
                    // Cold mist sinking off the blade, with sharp ice glints.
                    Layer("Frost Mist", 26f * intensity, new Vector2(0.7f, 1.1f), new Vector2(0.18f, 0.34f), new Vector2(0.05f, 0.2f),
                        primary * 0.8f, secondary * 0.4f, gravity: 0.12f, grow: true);
                    Layer("Ice Glints", 16f * intensity, new Vector2(0.25f, 0.45f), new Vector2(0.05f, 0.09f), new Vector2(0.4f, 1.1f),
                        Color.white, primary, stretch: 0.06f);
                    break;
                case Effect.BloodMoon:
                    // Blood drips falling from the blade, and rising red embers.
                    Layer("Blood Drips", 16f * intensity, new Vector2(0.45f, 0.7f), new Vector2(0.07f, 0.12f), Vector2.zero,
                        primary, secondary, gravity: 1.1f, stretch: 0.08f);
                    Layer("Embers", 22f * intensity, new Vector2(0.5f, 0.85f), new Vector2(0.06f, 0.13f), new Vector2(0.1f, 0.35f),
                        secondary, primary * 0.5f, gravity: -0.25f, twinkle: true);
                    break;
                case Effect.CyberDeath:
                    // Flickering data bits and sideways glitch streaks.
                    Layer("Data Bits", 38f * intensity, new Vector2(0.25f, 0.5f), new Vector2(0.05f, 0.1f), new Vector2(0.05f, 0.3f),
                        primary, Color.white, flicker: true);
                    Layer("Glitch Streaks", 12f * intensity, new Vector2(0.08f, 0.14f), new Vector2(0.045f, 0.07f), new Vector2(2f, 3.5f),
                        secondary, primary, stretch: 0.09f, sideways: true);
                    break;
                case Effect.Eclipse:
                    // A slow violet aura, and gold corona sparks.
                    Layer("Dark Aura", 18f * intensity, new Vector2(0.8f, 1.2f), new Vector2(0.38f, 0.6f), new Vector2(0.02f, 0.12f),
                        primary * 0.6f, primary * 0.15f, grow: true);
                    Layer("Corona Sparks", 14f * intensity, new Vector2(0.35f, 0.55f), new Vector2(0.06f, 0.1f), new Vector2(0.3f, 0.8f),
                        secondary, primary, stretch: 0.04f);
                    break;
            }
        }

        private ParticleSystem Layer(string name, float rate, Vector2 life, Vector2 size, Vector2 speed, Color start, Color end,
            float gravity = 0f, float stretch = 0f, bool twinkle = false, bool pop = false, bool grow = false,
            bool flicker = false, bool sideways = false)
        {
            GameObject go = new GameObject(name);
            go.transform.SetParent(transform, false);
            go.layer = gameObject.layer;
            ParticleSystem ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            ParticleSystem.MainModule main = ps.main;
            main.loop = true;
            main.playOnAwake = true;
            main.duration = 1f;
            main.maxParticles = 80;
            main.startLifetime = new ParticleSystem.MinMaxCurve(life.x, life.y);
            main.startSize = new ParticleSystem.MinMaxCurve(size.x, size.y);
            main.startSpeed = new ParticleSystem.MinMaxCurve(speed.x, speed.y);
            main.startColor = start;
            main.gravityModifier = gravity;
            main.scalingMode = ParticleSystemScalingMode.Shape;
            if (space != null)
            {
                main.simulationSpace = ParticleSystemSimulationSpace.Custom;
                main.customSimulationSpace = space;
            }
            else
                main.simulationSpace = ParticleSystemSimulationSpace.Local;

            ParticleSystem.EmissionModule emission = ps.emission;
            emission.rateOverTime = rate;

            ParticleSystem.ShapeModule shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.position = blade.center;
            shape.scale = blade.size;
            shape.randomDirectionAmount = 1f;

            if (sideways)
            {
                ParticleSystem.VelocityOverLifetimeModule velocity = ps.velocityOverLifetime;
                velocity.enabled = true;
                velocity.space = ParticleSystemSimulationSpace.World;
                velocity.x = new ParticleSystem.MinMaxCurve(-3f, 3f);
                velocity.y = new ParticleSystem.MinMaxCurve(0f, 0f);
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
                ? new AnimationCurve(new Keyframe(0f, 0.2f), new Keyframe(0.25f, 1f), new Keyframe(0.5f, 0.45f), new Keyframe(0.75f, 1f), new Keyframe(1f, 0f))
                : pop ? new AnimationCurve(new Keyframe(0f, 0.3f), new Keyframe(0.2f, 1f), new Keyframe(1f, 0f))
                : grow ? AnimationCurve.EaseInOut(0f, 0.5f, 1f, 1.4f)
                : AnimationCurve.Linear(0f, 1f, 1f, 0.3f);
            sizeOverLife.size = new ParticleSystem.MinMaxCurve(1f, curve);

            ParticleSystemRenderer render = go.GetComponent<ParticleSystemRenderer>();
            render.sharedMaterial = sharedMaterial;
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
