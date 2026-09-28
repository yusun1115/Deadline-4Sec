using UnityEngine;
using UnityEngine.Rendering;

namespace Deadline4Sec
{
    // Thin outlines show confirmed impacts without obscuring the next threat.
    // A fixed pool bounds renderers and avoids creating objects during combos.
    public sealed class CombatVisualFeedback : MonoBehaviour
    {
        private const int PoolSize = 20;
        private const int CircleSegments = 32;

        private sealed class Effect
        {
            public LineRenderer Line;
            public readonly Vector3[] Circle = new Vector3[CircleSegments];
            public readonly Vector3[] Trail = new Vector3[2];
            public Vector3 Center, Right, Up;
            public float Start, Duration, Radius, Width;
            public Color Color;
            public bool IsTrail;
        }

        private readonly Effect[] pool = new Effect[PoolSize];
        private readonly Vector2[] unitCircle = new Vector2[CircleSegments];
        private GameObject effectRoot;
        private Material effectMaterial;
        private Camera view;
        private int nextSlot;

        public int Capacity => PoolSize;
        public int ActiveEffectCount
        {
            get
            {
                int count = 0;
                foreach (Effect effect in pool)
                    if (effect != null && effect.Line != null && effect.Line.enabled)
                        count++;
                return count;
            }
        }

        private void Awake()
        {
            view = GetComponent<Camera>();
            Shader shader = Resources.Load<Shader>("Feedback/CombatFeedback");
            if (shader == null)
            {
                Debug.LogError("Combat feedback shader is missing.");
                enabled = false;
                return;
            }
            effectMaterial = new Material(shader) { name = "Combat Feedback (Runtime)" };
            effectRoot = new GameObject("Combat Feedback Pool");
            for (int i = 0; i < CircleSegments; i++)
            {
                float angle = i * Mathf.PI * 2f / CircleSegments;
                unitCircle[i] = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
            }
            for (int i = 0; i < PoolSize; i++)
            {
                GameObject item = new GameObject("Impact " + i);
                item.transform.SetParent(effectRoot.transform, false);
                LineRenderer line = item.AddComponent<LineRenderer>();
                line.sharedMaterial = effectMaterial;
                line.useWorldSpace = true;
                line.shadowCastingMode = ShadowCastingMode.Off;
                line.receiveShadows = false;
                line.numCornerVertices = 0;
                line.numCapVertices = 0;
                line.enabled = false;
                pool[i] = new Effect { Line = line };
            }
        }

        public void PlayKill(Vector3 point, bool stomp, bool homing)
        {
            Color color = stomp ? new Color(1f, 0.65f, 0.12f) :
                homing ? new Color(0.5f, 0.9f, 1f) : new Color(1f, 0.2f, 0.4f);
            Ring(point, stomp ? 1.25f : 0.8f, stomp ? 0.32f : 0.22f,
                color, stomp, 0.08f);
        }

        public void PlayHoming(Vector3 start, Vector3 end)
        {
            Effect effect = Acquire(0.15f, new Color(0.4f, 0.85f, 1f), 0.055f);
            if (effect == null)
                return;
            effect.IsTrail = true;
            effect.Line.loop = false;
            effect.Line.positionCount = 2;
            effect.Trail[0] = start;
            effect.Trail[1] = end;
            effect.Line.SetPositions(effect.Trail);
        }

        public void PlaySlam(Vector3 point, float radius)
        {
            Ring(point + Vector3.up * 0.06f, radius, 0.4f,
                new Color(0.5f, 0.02f, 1f), true, 0.15f);
        }

        public void PlayNearMiss(Vector3 point)
        {
            Ring(point, 0.6f, 0.25f, new Color(0.2f, 1f, 0.8f), false, 0.05f);
        }

        private Effect Acquire(float duration, Color color, float width)
        {
            if (effectMaterial == null)
                return null;
            Effect effect = pool[nextSlot];
            nextSlot = (nextSlot + 1) % PoolSize;
            effect.Start = Time.unscaledTime;
            effect.Duration = duration;
            effect.Color = color;
            effect.Width = width;
            effect.Line.enabled = true;
            effect.Line.widthMultiplier = width;
            effect.Line.startColor = effect.Line.endColor = color;
            return effect;
        }

        private void Ring(Vector3 point, float radius, float duration,
            Color color, bool horizontal, float width)
        {
            Effect effect = Acquire(duration, color, width);
            if (effect == null)
                return;
            effect.IsTrail = false;
            effect.Center = point;
            effect.Radius = Mathf.Max(0.1f, radius);
            effect.Right = horizontal || view == null ? Vector3.right : view.transform.right;
            effect.Up = horizontal ? Vector3.forward : view != null ? view.transform.up : Vector3.up;
            effect.Line.loop = true;
            effect.Line.positionCount = CircleSegments;
            UpdateEffect(effect, 0f);
        }

        private void Update()
        {
            foreach (Effect effect in pool)
            {
                if (effect == null || effect.Line == null || !effect.Line.enabled)
                    continue;
                float progress = (Time.unscaledTime - effect.Start) / effect.Duration;
                if (progress >= 1f)
                    effect.Line.enabled = false;
                else
                    UpdateEffect(effect, progress);
            }
        }

        private void UpdateEffect(Effect effect, float progress)
        {
            Color color = effect.Color;
            color.a = 1f - progress;
            effect.Line.startColor = effect.Line.endColor = color;
            effect.Line.widthMultiplier = effect.Width * (1f - progress * 0.5f);
            if (effect.IsTrail)
                return;
            float radius = effect.Radius * Mathf.Lerp(0.3f, 1f,
                1f - (1f - progress) * (1f - progress));
            for (int i = 0; i < CircleSegments; i++)
                effect.Circle[i] = effect.Center + radius *
                    (effect.Right * unitCircle[i].x + effect.Up * unitCircle[i].y);
            effect.Line.SetPositions(effect.Circle);
        }

        public void Clear()
        {
            foreach (Effect effect in pool)
                if (effect != null && effect.Line != null)
                    effect.Line.enabled = false;
        }

        private void OnDisable() => Clear();

        private void OnDestroy()
        {
            if (effectRoot != null)
                Destroy(effectRoot);
            if (effectMaterial != null)
                Destroy(effectMaterial);
        }
    }
}
