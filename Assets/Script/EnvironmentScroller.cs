using System.Collections.Generic;
using UnityEngine;

namespace Deadline4Sec
{
    // Builds the gothic afterlife backdrop (spires, track-side pillars with
    // chains and banners, a looming clock tower, a cloud sea) from procedural
    // meshes once, then recycles the pieces ahead of the camera. Visual only:
    // no colliders, so gameplay and pattern fairness are untouched.
    // Each area has its own backdrop (2026-11-06, themes in
    // EnvironmentScroller.Themes.cs): at most the current one and the next are
    // alive; the old one and its meshes are removed at the swap.
    // Visual upgrade (2026-11-15): each backdrop also has a mid layer (recycled
    // like the spires, between the pillars and the far towers, dimmer) and a
    // far layer (one skyline riding with the camera, mostly sunk in the fog),
    // with their own material copies, as does the landmark (less fog, more
    // glow). The low fog, particles and background light are AreaAtmosphere.
    public sealed partial class EnvironmentScroller : MonoBehaviour
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

        [Header("Mid / far layers (visual upgrade)")]
        [SerializeField] private int midCount = 10;
        [SerializeField] private Vector2 midDistanceX = new Vector2(14f, 24f);
        [SerializeField] private float midSpan = 260f;
        [Tooltip("Distance of the far skyline ahead of the camera.")]
        [SerializeField] private float farDistance = 285f;

        [Header("Atmosphere (visual upgrade)")]
        [Tooltip("Stacked low fog sheets: more is softer, each one is screen overdraw.")]
        [SerializeField, Range(1, 8)] private int lowFogSheets = 4;
        [Tooltip("Half width of the strip over the track kept clear of low fog.")]
        [SerializeField] private float lowFogClearHalfWidth = 4.6f;

        private static readonly Color Stone = new Color(0.075f, 0.055f, 0.1f, 0f);
        private static readonly Color StoneLight = new Color(0.13f, 0.1f, 0.17f, 0f);
        private static readonly Color Iron = new Color(0.05f, 0.04f, 0.06f, 0f);
        private static readonly Color Glow = new Color(1f, 0.2f, 0.52f, 1f);
        private static readonly Color Banner = new Color(0.42f, 0.04f, 0.15f, 0f);
        private static readonly Color BannerMark = new Color(0.95f, 0.15f, 0.42f, 1f);
        private static readonly Color ClockFace = new Color(0.9f, 0.82f, 0.86f, 1f);

        // A turning part (clock hand, gear): around its local Z, degrees per second.
        private struct Spinner
        {
            public Transform Part;
            public float Speed;
            public float Phase;
        }

        private sealed class Backdrop
        {
            public Transform Root;
            public Material Material;
            public bool OwnsMaterial;
            public float BuiltAtZ;
            public Transform Landmark;
            public Vector3 LandmarkOffset;
            public Transform CloudSea;
            public Transform Far;
            public Material MidMaterial, FarMaterial, LandmarkMaterial;
            public readonly List<Transform> Mids = new List<Transform>();
            public readonly List<Transform> Pillars = new List<Transform>();
            public readonly List<Transform> Spires = new List<Transform>();
            public readonly List<Spinner> Spinners = new List<Spinner>();
            public readonly List<Mesh> Meshes = new List<Mesh>();
        }

        private Backdrop current;
        private Backdrop prepared;
        private Backdrop building;
        private float trackX;

        // For tests: pieces alive under this object.
        public int BackdropCount => transform.childCount;
        public AreaType ShownArea { get; private set; }
        public AreaAtmosphere Atmosphere { get; private set; }
        // For tests: the shown backdrop's layers.
        public int MidLayerCount => current != null ? current.Mids.Count : 0;
        public bool HasFarLayer => current != null && current.Far != null;

        private void Awake()
        {
            if (followTarget == null && Camera.main != null)
                followTarget = Camera.main.transform;
            trackX = transform.position.x;
            Material material = new Material(silhouetteMaterial) { name = "Silhouette " + AreaType.SoulRailway };
            current = CreateBackdrop(AreaType.SoulRailway, material, true);
            ApplyVisuals(current, silhouetteMaterial.GetColor("_HazeColor"), new AreaVisualProfile());
            Atmosphere = AreaAtmosphere.Create(followTarget, trackX, lowFogClearHalfWidth, lowFogSheets, gameObject.scene);
        }

        // Builds the next area's backdrop out of sight, ready for the swap.
        public void PrepareArea(AreaType area, Color haze, AreaVisualProfile visuals)
        {
            if (prepared != null)
                DestroyBackdrop(prepared);
            Material material = new Material(silhouetteMaterial) { name = "Silhouette " + area };
            prepared = CreateBackdrop(area, material, true);
            ApplyVisuals(prepared, haze, visuals ?? new AreaVisualProfile());
            prepared.Root.gameObject.SetActive(false);
            preparedArea = area;
        }

        // The area the run starts in (AreaDirector.Begin): its profile on the shown backdrop.
        public void ApplyVisuals(Color haze, AreaVisualProfile visuals)
        {
            if (current != null && visuals != null)
                ApplyVisuals(current, haze, visuals);
        }

        private static void ApplyVisuals(Backdrop backdrop, Color haze, AreaVisualProfile v)
        {
            foreach (Material m in new[] { backdrop.Material, backdrop.MidMaterial, backdrop.FarMaterial, backdrop.LandmarkMaterial })
            {
                if (m == null)
                    continue;
                m.SetColor("_HazeColor", haze);
                m.SetFloat("_HazeHeight", v.hazeHeight);
            }
            if (backdrop.MidMaterial != null)
                backdrop.MidMaterial.SetColor("_Tint", v.midLayerTint);
            if (backdrop.FarMaterial != null)
            {
                backdrop.FarMaterial.SetColor("_Tint", v.farLayerTint);
                backdrop.FarMaterial.SetFloat("_FogAmount", v.farFogAmount);
                backdrop.FarMaterial.SetFloat("_Glow", 1.4f);
                backdrop.FarMaterial.SetFloat("_LightAmount", 0.5f);
            }
            if (backdrop.LandmarkMaterial != null)
            {
                backdrop.LandmarkMaterial.SetFloat("_FogAmount", v.landmarkFogAmount);
                backdrop.LandmarkMaterial.SetFloat("_Glow", v.landmarkGlow);
            }
        }

        // Called with the camera inside a transition tunnel: nothing to see the change.
        public void ShowPreparedArea()
        {
            if (prepared == null)
                return;
            float z = followTarget != null ? followTarget.position.z : 0f;
            float shift = z - prepared.BuiltAtZ;
            foreach (Transform p in prepared.Pillars)
                p.position += Vector3.forward * shift;
            foreach (Transform s in prepared.Spires)
                s.position += Vector3.forward * shift;
            foreach (Transform m in prepared.Mids)
                m.position += Vector3.forward * shift;
            prepared.Root.gameObject.SetActive(true);
            DestroyBackdrop(current);
            current = prepared;
            prepared = null;
            ShownArea = preparedArea;
        }

        private AreaType preparedArea;

        private Backdrop CreateBackdrop(AreaType area, Material material, bool ownsMaterial)
        {
            GameObject root = new GameObject("Backdrop " + area);
            root.transform.SetParent(transform, false);
            building = new Backdrop { Root = root.transform, Material = material, OwnsMaterial = ownsMaterial };
            building.MidMaterial = new Material(material) { name = material.name + " Mid" };
            building.FarMaterial = new Material(material) { name = material.name + " Far" };
            building.LandmarkMaterial = new Material(material) { name = material.name + " Landmark" };
            building.BuiltAtZ = followTarget != null ? followTarget.position.z : 0f;
            Random.State saved = Random.state;
            Random.InitState(seed + (int)area);
            switch (area)
            {
                case AreaType.BrokenCathedral: BuildCathedral(building.BuiltAtZ); break;
                case AreaType.ClockworkAbyss: BuildClockwork(building.BuiltAtZ); break;
                default: Build(building.BuiltAtZ); break;
            }
            BuildLayers(area, building.BuiltAtZ);
            Random.state = saved;
            Backdrop built = building;
            building = null;
            return built;
        }

        private static void DestroyBackdrop(Backdrop backdrop)
        {
            if (backdrop.Root != null)
                Destroy(backdrop.Root.gameObject);
            foreach (Mesh mesh in backdrop.Meshes)
                Destroy(mesh);
            if (backdrop.OwnsMaterial)
                Destroy(backdrop.Material);
            foreach (Material m in new[] { backdrop.MidMaterial, backdrop.FarMaterial, backdrop.LandmarkMaterial })
                if (m != null)
                    Destroy(m);
        }

        private void OnDisable()
        {
            if (Atmosphere != null)
                Atmosphere.gameObject.SetActive(false);
        }

        private void OnEnable()
        {
            if (Atmosphere != null)
                Atmosphere.gameObject.SetActive(true);
        }

        private void OnDestroy()
        {
            if (current != null)
                DestroyBackdrop(current);
            if (prepared != null)
                DestroyBackdrop(prepared);
            if (Atmosphere != null)
                Destroy(Atmosphere.gameObject);
            DestroyMainMenuBackdrop();
        }

        private void Build(float startZ)
        {
            Mesh pillarMesh = BuildPillarMesh();
            for (int i = 0; i < pillarCount; i++)
            {
                float side = i % 2 == 0 ? -1f : 1f;
                Transform p = Spawn("Pillar", pillarMesh);
                p.position = new Vector3(trackX + side * pillarOffsetX, 0f,
                    startZ - 10f + (i / 2) * pillarSpacing + (side > 0 ? pillarSpacing * 0.5f : 0f));
                p.localScale = new Vector3(side, 1f, 1f);
                building.Pillars.Add(p);
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
                building.Spires.Add(s);
            }

            Transform clockTower = Spawn("ClockTower", BuildClockTowerMesh(), building.LandmarkMaterial);
            Transform clockHandHour = Spawn("ClockHourHand", BuildBox(new Vector3(-0.6f, 0f, -0.4f), new Vector3(0.6f, 7f, 0f), Iron), building.LandmarkMaterial);
            Transform clockHandMinute = Spawn("ClockMinuteHand", BuildBox(new Vector3(-0.4f, 0f, -0.5f), new Vector3(0.4f, 10f, 0f), Iron), building.LandmarkMaterial);
            clockHandHour.SetParent(clockTower, false);
            clockHandMinute.SetParent(clockTower, false);
            clockHandHour.localPosition = clockHandMinute.localPosition = new Vector3(0f, 66f, -9.2f);
            building.Landmark = clockTower;
            building.LandmarkOffset = clockTowerOffset;
            building.Spinners.Add(new Spinner { Part = clockHandMinute, Speed = -24f });
            building.Spinners.Add(new Spinner { Part = clockHandHour, Speed = -2f, Phase = -40f });

            building.CloudSea = Spawn("CloudSea", BuildCloudSeaMesh());
        }

        private void LateUpdate()
        {
            UpdateMainMenuBackdrop();
            if (followTarget == null || current == null)
                return;
            float z = followTarget.position.z;

            float pillarLoop = (pillarCount / 2) * pillarSpacing;
            foreach (Transform p in current.Pillars)
                if (p.position.z < z - 14f)
                    p.position += Vector3.forward * pillarLoop;

            foreach (Transform m in current.Mids)
                if (m.position.z < z - 30f)
                    m.position += Vector3.forward * (midSpan + 30f);

            foreach (Transform s in current.Spires)
                if (s.position.z < z - 30f)
                {
                    s.position += Vector3.forward * (spireSpan + 30f);
                    float side = s.position.x < trackX ? -1f : 1f;
                    s.position = new Vector3(trackX + side * Random.Range(spireDistanceX.x, spireDistanceX.y),
                        s.position.y, s.position.z);
                }

            // Landmarks ride with the camera so they always loom on the horizon.
            current.Landmark.position = new Vector3(trackX + current.LandmarkOffset.x, current.LandmarkOffset.y, z + current.LandmarkOffset.z);
            current.CloudSea.position = new Vector3(trackX, cloudSeaY, z + 150f);
            if (current.Far != null)
                current.Far.position = new Vector3(trackX, 0f, z + farDistance);
            float t = Time.time;
            foreach (Spinner spinner in current.Spinners)
                spinner.Part.localRotation = Quaternion.Euler(0f, 0f, t * spinner.Speed + spinner.Phase);
        }

        private Transform Spawn(string name, Mesh mesh, Material material = null)
        {
            GameObject go = new GameObject(name);
            go.transform.SetParent(building.Root, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            if (!building.Meshes.Contains(mesh))
                building.Meshes.Add(mesh);
            MeshRenderer r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = material != null ? material : building.Material;
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
                // Flat faces (four vertices a quad): normals for the area light.
                m.RecalculateNormals();
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
            // The base reaches down into the cloud sea: no floating bottom.
            b.Box(new Vector3(-0.75f, -12f, -0.75f), new Vector3(0.75f, 0.6f, 0.75f), StoneLight);
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
            // Visual upgrade: a neon halo around the dial, lit lancets up the side
            // towers and a beacon on the spire: the railway's one landmark reads
            // from far away.
            b.Ring(face + new Vector3(0, 0, 0.05f), 12.4f, 13.6f, 48, Glow);
            b.Ring(face + new Vector3(0, 0, 0.1f), 13.6f, 15.2f, 48, new Color(0.35f, 0.05f, 0.22f, 1f));
            for (int s = -1; s <= 1; s += 2)
                for (int i = 0; i < 4; i++)
                {
                    float y0 = 20f + i * 17f;
                    b.Quad(new Vector3(s * 9f - 0.7f, y0, -2.55f), new Vector3(s * 9f - 0.7f, y0 + 9f, -2.55f),
                        new Vector3(s * 9f + 0.7f, y0 + 9f, -2.55f), new Vector3(s * 9f + 0.7f, y0, -2.55f),
                        i % 2 == 0 ? Glow : new Color(0.55f, 0.1f, 0.35f, 1f));
                }
            b.Box(new Vector3(-0.7f, 116f, -0.7f), new Vector3(0.7f, 119f, 0.7f), Glow);
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
