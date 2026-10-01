using System.Collections.Generic;
using UnityEngine;

namespace Deadline4Sec
{
    // Builds the gothic afterlife backdrop (spires, track-side pillars with
    // chains and banners, a looming clock tower, a cloud sea) from procedural
    // meshes once, then recycles the pieces ahead of the camera. Visual only:
    // no colliders, so gameplay and pattern fairness are untouched.
    public sealed class EnvironmentScroller : MonoBehaviour
    {
        [SerializeField] private Material silhouetteMaterial;
        [SerializeField] private Transform followTarget;
        [SerializeField] private int seed = 4;

        [Header("Track-side pillars")]
        [SerializeField] private float pillarOffsetX = 7.2f;
        [SerializeField] private float pillarSpacing = 16f;
        [SerializeField] private int pillarCount = 14;

        [Header("Spires")]
        [SerializeField] private int spireCount = 18;
        [SerializeField] private Vector2 spireDistanceX = new Vector2(24f, 80f);
        [SerializeField] private float spireSpan = 320f;

        [Header("Landmarks")]
        [SerializeField] private Vector3 clockTowerOffset = new Vector3(34f, -8f, 230f);
        [SerializeField] private float cloudSeaY = -9f;

        private static readonly Color Stone = new Color(0.075f, 0.055f, 0.1f, 0f);
        private static readonly Color StoneLight = new Color(0.13f, 0.1f, 0.17f, 0f);
        private static readonly Color Iron = new Color(0.05f, 0.04f, 0.06f, 0f);
        private static readonly Color Glow = new Color(1f, 0.2f, 0.52f, 1f);
        private static readonly Color Banner = new Color(0.42f, 0.04f, 0.15f, 0f);
        private static readonly Color BannerMark = new Color(0.95f, 0.15f, 0.42f, 1f);
        private static readonly Color ClockFace = new Color(0.9f, 0.82f, 0.86f, 1f);

        private readonly List<Transform> pillars = new List<Transform>();
        private readonly List<Transform> spires = new List<Transform>();
        private Transform clockTower;
        private Transform cloudSea;
        private Transform clockHandHour;
        private Transform clockHandMinute;
        private float trackX;

        private void Awake()
        {
            if (followTarget == null && Camera.main != null)
                followTarget = Camera.main.transform;
            trackX = transform.position.x;
            Random.State saved = Random.state;
            Random.InitState(seed);
            Build();
            Random.state = saved;
        }

        private void Build()
        {
            float startZ = followTarget != null ? followTarget.position.z : 0f;

            Mesh pillarMesh = BuildPillarMesh();
            for (int i = 0; i < pillarCount; i++)
            {
                float side = i % 2 == 0 ? -1f : 1f;
                Transform p = Spawn("Pillar", pillarMesh);
                p.position = new Vector3(trackX + side * pillarOffsetX, 0f,
                    startZ - 10f + (i / 2) * pillarSpacing + (side > 0 ? pillarSpacing * 0.5f : 0f));
                p.localScale = new Vector3(side, 1f, 1f);
                pillars.Add(p);
            }

            Mesh[] spireMeshes = new Mesh[6];
            for (int i = 0; i < spireMeshes.Length; i++)
                spireMeshes[i] = BuildSpireMesh(Random.Range(3f, 7f), Random.Range(14f, 42f), Random.Range(0, 3));
            for (int i = 0; i < spireCount; i++)
            {
                float side = Random.value < 0.5f ? -1f : 1f;
                Transform s = Spawn("Spire", spireMeshes[i % spireMeshes.Length]);
                s.position = new Vector3(trackX + side * Random.Range(spireDistanceX.x, spireDistanceX.y),
                    Random.Range(-10f, -4f), startZ + Random.Range(-20f, spireSpan));
                s.rotation = Quaternion.Euler(0f, Random.Range(-20f, 20f), 0f);
                float k = Random.Range(0.8f, 1.3f);
                s.localScale = new Vector3(k, k, k);
                spires.Add(s);
            }

            clockTower = Spawn("ClockTower", BuildClockTowerMesh());
            clockHandHour = Spawn("ClockHourHand", BuildBox(new Vector3(-0.6f, 0f, -0.4f), new Vector3(0.6f, 7f, 0f), ClockFace));
            clockHandMinute = Spawn("ClockMinuteHand", BuildBox(new Vector3(-0.4f, 0f, -0.5f), new Vector3(0.4f, 10f, 0f), ClockFace));
            clockHandHour.SetParent(clockTower, false);
            clockHandMinute.SetParent(clockTower, false);
            clockHandHour.localPosition = clockHandMinute.localPosition = new Vector3(0f, 66f, -9.2f);

            cloudSea = Spawn("CloudSea", BuildCloudSeaMesh());
        }

        private void LateUpdate()
        {
            if (followTarget == null)
                return;
            float z = followTarget.position.z;

            float pillarLoop = (pillarCount / 2) * pillarSpacing;
            foreach (Transform p in pillars)
                if (p.position.z < z - 14f)
                    p.position += Vector3.forward * pillarLoop;

            foreach (Transform s in spires)
                if (s.position.z < z - 30f)
                {
                    s.position += Vector3.forward * (spireSpan + 30f);
                    float side = s.position.x < trackX ? -1f : 1f;
                    s.position = new Vector3(trackX + side * Random.Range(spireDistanceX.x, spireDistanceX.y),
                        s.position.y, s.position.z);
                }

            // Landmarks ride with the camera so they always loom on the horizon.
            clockTower.position = new Vector3(trackX + clockTowerOffset.x, clockTowerOffset.y, z + clockTowerOffset.z);
            cloudSea.position = new Vector3(trackX, cloudSeaY, z + 150f);
            float t = Time.time;
            clockHandMinute.localRotation = Quaternion.Euler(0f, 0f, -t * 24f);
            clockHandHour.localRotation = Quaternion.Euler(0f, 0f, -t * 2f - 40f);
        }

        private Transform Spawn(string name, Mesh mesh)
        {
            GameObject go = new GameObject(name);
            go.transform.SetParent(transform, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            MeshRenderer r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = silhouetteMaterial;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            return go.transform;
        }

        // ---------- mesh builders ----------

        private sealed class MeshBuilder
        {
            public readonly List<Vector3> V = new List<Vector3>();
            public readonly List<Color> C = new List<Color>();
            public readonly List<int> T = new List<int>();

            public void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Color col)
            {
                int i = V.Count;
                V.Add(a); V.Add(b); V.Add(c); V.Add(d);
                C.Add(col); C.Add(col); C.Add(col); C.Add(col);
                T.Add(i); T.Add(i + 1); T.Add(i + 2); T.Add(i); T.Add(i + 2); T.Add(i + 3);
            }

            public void Tri(Vector3 a, Vector3 b, Vector3 c, Color col)
            {
                int i = V.Count;
                V.Add(a); V.Add(b); V.Add(c);
                C.Add(col); C.Add(col); C.Add(col);
                T.Add(i); T.Add(i + 1); T.Add(i + 2);
            }

            public void Box(Vector3 min, Vector3 max, Color col)
            {
                Vector3 a = new Vector3(min.x, min.y, min.z), b = new Vector3(max.x, min.y, min.z);
                Vector3 c = new Vector3(max.x, min.y, max.z), d = new Vector3(min.x, min.y, max.z);
                Vector3 e = new Vector3(min.x, max.y, min.z), f = new Vector3(max.x, max.y, min.z);
                Vector3 g = new Vector3(max.x, max.y, max.z), h = new Vector3(min.x, max.y, max.z);
                Quad(a, e, f, b, col); Quad(b, f, g, c, col); Quad(c, g, h, d, col);
                Quad(d, h, e, a, col); Quad(e, h, g, f, col); Quad(a, b, c, d, col);
            }

            public void Pyramid(Vector3 baseCenter, float half, float height, Color col)
            {
                Vector3 top = baseCenter + Vector3.up * height;
                Vector3 a = baseCenter + new Vector3(-half, 0, -half), b = baseCenter + new Vector3(half, 0, -half);
                Vector3 c = baseCenter + new Vector3(half, 0, half), d = baseCenter + new Vector3(-half, 0, half);
                Tri(a, top, b, col); Tri(b, top, c, col); Tri(c, top, d, col); Tri(d, top, a, col);
            }

            // Flat ring facing -Z.
            public void Ring(Vector3 center, float inner, float outer, int segments, Color col)
            {
                for (int s = 0; s < segments; s++)
                {
                    float a0 = s * Mathf.PI * 2f / segments, a1 = (s + 1) * Mathf.PI * 2f / segments;
                    Vector3 d0 = new Vector3(Mathf.Cos(a0), Mathf.Sin(a0), 0), d1 = new Vector3(Mathf.Cos(a1), Mathf.Sin(a1), 0);
                    Quad(center + d0 * inner, center + d0 * outer, center + d1 * outer, center + d1 * inner, col);
                }
            }

            public Mesh Build(string name)
            {
                Mesh m = new Mesh { name = name };
                if (V.Count > 65000)
                    m.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
                m.SetVertices(V);
                m.SetColors(C);
                m.SetTriangles(T, 0);
                m.RecalculateBounds();
                return m;
            }
        }

        private static Mesh BuildBox(Vector3 min, Vector3 max, Color col)
        {
            MeshBuilder b = new MeshBuilder();
            b.Box(min, max, col);
            return b.Build("Box");
        }

        // Pillar on the left side of the track (mirrored for the right): base,
        // shaft, cap spire, lantern, a hanging banner and a sagging chain run.
        private Mesh BuildPillarMesh()
        {
            MeshBuilder b = new MeshBuilder();
            b.Box(new Vector3(-0.75f, -6f, -0.75f), new Vector3(0.75f, 0.6f, 0.75f), StoneLight);
            b.Box(new Vector3(-0.45f, 0.6f, -0.45f), new Vector3(0.45f, 7.5f, 0.45f), Stone);
            b.Box(new Vector3(-0.65f, 7.5f, -0.65f), new Vector3(0.65f, 8.0f, 0.65f), StoneLight);
            b.Pyramid(new Vector3(0f, 8f, 0f), 0.55f, 3.2f, Stone);
            // Lantern facing the track.
            b.Box(new Vector3(0.45f, 5.6f, -0.18f), new Vector3(0.62f, 6.2f, 0.18f), Glow);
            // Banner hanging on the track-facing side.
            b.Quad(new Vector3(0.47f, 3.2f, -0.35f), new Vector3(0.47f, 6.8f, -0.35f),
                new Vector3(0.47f, 6.8f, 0.35f), new Vector3(0.47f, 3.2f, 0.35f), Banner);
            b.Tri(new Vector3(0.47f, 3.2f, -0.35f), new Vector3(0.47f, 3.2f, 0.35f), new Vector3(0.47f, 2.6f, 0f), Banner);
            b.Quad(new Vector3(0.48f, 4.4f, -0.06f), new Vector3(0.48f, 6.0f, -0.06f),
                new Vector3(0.48f, 6.0f, 0.06f), new Vector3(0.48f, 4.4f, 0.06f), BannerMark);
            b.Quad(new Vector3(0.48f, 5.1f, -0.22f), new Vector3(0.48f, 5.25f, -0.22f),
                new Vector3(0.48f, 5.25f, 0.22f), new Vector3(0.48f, 5.1f, 0.22f), BannerMark);
            // Chain links sagging to the next pillar on this side.
            float span = pillarSpacing;
            int links = 22;
            for (int i = 0; i < links; i++)
            {
                float t = (i + 0.5f) / links;
                float z = t * span;
                float y = 7f - Mathf.Sin(t * Mathf.PI) * 2.2f;
                float s = i % 2 == 0 ? 0.16f : 0.06f;
                b.Box(new Vector3(-s, y - 0.06f, z - 0.32f), new Vector3(s, y + 0.06f, z + 0.32f), Iron);
            }
            return b.Build("Pillar");
        }

        private static Mesh BuildSpireMesh(float width, float height, int subSpires)
        {
            MeshBuilder b = new MeshBuilder();
            float hw = width * 0.5f;
            b.Box(new Vector3(-hw, -20f, -hw), new Vector3(hw, height, hw), Stone);
            b.Box(new Vector3(-hw - 0.4f, height, -hw - 0.4f), new Vector3(hw + 0.4f, height + 0.8f, hw + 0.4f), StoneLight);
            b.Pyramid(new Vector3(0f, height + 0.8f, 0f), hw * 0.85f, width * 2.2f, Stone);
            // Thin needle on top.
            b.Box(new Vector3(-0.08f, height + 0.8f + width * 2.1f, -0.08f),
                new Vector3(0.08f, height + 0.8f + width * 2.1f + 3f, 0.08f), Iron);
            for (int s = 0; s < subSpires; s++)
            {
                float side = s == 0 ? -1f : 1f;
                float sh = height * Random.Range(0.45f, 0.7f);
                float sw = width * 0.35f;
                float x = side * (hw + sw * 0.5f);
                b.Box(new Vector3(x - sw * 0.5f, -20f, -sw * 0.5f), new Vector3(x + sw * 0.5f, sh, sw * 0.5f), Stone);
                b.Pyramid(new Vector3(x, sh, 0f), sw * 0.6f, sw * 2.6f, Stone);
            }
            // Glowing gothic windows on every face, a few per tower.
            int rows = Mathf.Clamp(Mathf.FloorToInt(height / 9f), 1, 4);
            for (int r = 0; r < rows; r++)
            {
                float y0 = height * 0.35f + r * 7f;
                if (y0 + 3f > height)
                    break;
                float wx = Mathf.Min(0.5f, hw * 0.25f);
                Color c = Random.value < 0.75f ? Glow : new Color(0.55f, 0.1f, 0.3f, 1f);
                b.Quad(new Vector3(-wx, y0, -hw - 0.02f), new Vector3(-wx, y0 + 3f, -hw - 0.02f),
                    new Vector3(wx, y0 + 3f, -hw - 0.02f), new Vector3(wx, y0, -hw - 0.02f), c);
                b.Quad(new Vector3(hw + 0.02f, y0, -wx), new Vector3(hw + 0.02f, y0 + 3f, -wx),
                    new Vector3(hw + 0.02f, y0 + 3f, wx), new Vector3(hw + 0.02f, y0, wx), c);
                b.Quad(new Vector3(-hw - 0.02f, y0, wx), new Vector3(-hw - 0.02f, y0 + 3f, wx),
                    new Vector3(-hw - 0.02f, y0 + 3f, -wx), new Vector3(-hw - 0.02f, y0, -wx), c);
            }
            return b.Build("Spire");
        }

        private static Mesh BuildClockTowerMesh()
        {
            MeshBuilder b = new MeshBuilder();
            b.Box(new Vector3(-9f, -30f, -9f), new Vector3(9f, 80f, 9f), Stone);
            b.Box(new Vector3(-10f, 80f, -10f), new Vector3(10f, 82f, 10f), StoneLight);
            b.Pyramid(new Vector3(0f, 82f, 0f), 8.5f, 34f, Stone);
            for (int s = -1; s <= 1; s += 2)
            {
                b.Box(new Vector3(s * 9f - 2.5f, -30f, -2.5f), new Vector3(s * 9f + 2.5f, 92f, 2.5f), Stone);
                b.Pyramid(new Vector3(s * 9f, 92f, 0f), 2.6f, 16f, Stone);
            }
            b.Box(new Vector3(-17f, -30f, -6f), new Vector3(-11f, 55f, 6f), Stone);
            b.Pyramid(new Vector3(-14f, 55f, 0f), 3.4f, 14f, Stone);
            b.Box(new Vector3(11f, -30f, -6f), new Vector3(17f, 48f, 6f), Stone);
            b.Pyramid(new Vector3(14f, 48f, 0f), 3.4f, 12f, Stone);
            // Clock face, frame ring, hour ticks and a pink crack of light.
            Vector3 face = new Vector3(0f, 66f, -9.1f);
            b.Ring(face, 0f, 11f, 40, new Color(0.8f, 0.72f, 0.78f, 1f));
            b.Ring(face + new Vector3(0, 0, -0.05f), 11f, 12.4f, 40, StoneLight);
            for (int i = 0; i < 12; i++)
            {
                float a = i * Mathf.PI / 6f;
                Vector3 dir = new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0f);
                Vector3 perp = new Vector3(-dir.y, dir.x, 0f) * 0.35f;
                Vector3 p0 = face + dir * 8.8f + new Vector3(0, 0, -0.08f);
                Vector3 p1 = face + dir * 10.4f + new Vector3(0, 0, -0.08f);
                b.Quad(p0 - perp, p1 - perp, p1 + perp, p0 + perp, Iron);
            }
            b.Quad(new Vector3(-0.4f, 20f, -9.05f), new Vector3(-0.4f, 54f, -9.05f),
                new Vector3(0.6f, 54f, -9.05f), new Vector3(0.6f, 20f, -9.05f), Glow);
            return b.Build("ClockTower");
        }

        private static Mesh BuildCloudSeaMesh()
        {
            MeshBuilder b = new MeshBuilder();
            Color haze = new Color(0.24f, 0.17f, 0.31f, 0f);
            b.Quad(new Vector3(-400f, 0f, -250f), new Vector3(-400f, 0f, 250f),
                new Vector3(400f, 0f, 250f), new Vector3(400f, 0f, -250f), haze);
            return b.Build("CloudSea");
        }
    }
}
