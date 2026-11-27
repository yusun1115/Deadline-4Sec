using UnityEngine;
using UnityEngine.Rendering;

namespace Deadline4Sec
{
    // Visual upgrade (2026-11-15, VisualUpgrade.md): the air of an area, around
    // the camera. Owned by EnvironmentScroller (a separate root object, so the
    // backdrop count under the scroller stays what it was), driven by
    // AreaDirector with the blended AreaVisualProfile:
    //  - low fog: stacked translucent sheets under and beside the track that
    //    swallow the bottoms of the side structures; the track strip is kept clear,
    //  - distance fog start/end (RenderSettings, linear fog),
    //  - the light on the background (Silhouette globals: side light, back light),
    //  - light particles: neon dust, ash and embers, or steam and sparks, emitted
    //    by script around the camera (pooled systems, no colliders, no gameplay).
    public sealed class AreaAtmosphere : MonoBehaviour
    {
        private static readonly int SideLightId = Shader.PropertyToID("_AreaSideLight");
        private static readonly int SideDirId = Shader.PropertyToID("_AreaSideDir");
        private static readonly int BackLightId = Shader.PropertyToID("_AreaBackLight");
        private static readonly int ColorId = Shader.PropertyToID("_Color");
        private static readonly int GlowColorId = Shader.PropertyToID("_GlowColor");
        private static readonly int AlphaId = Shader.PropertyToID("_Alpha");
        private static readonly int ScaleId = Shader.PropertyToID("_Scale");
        private static readonly int DriftId = Shader.PropertyToID("_Drift");
        private static readonly int TrackXId = Shader.PropertyToID("_TrackX");
        private static readonly int ClearHalfWidthId = Shader.PropertyToID("_ClearHalfWidth");

        private Transform viewer;
        private float trackX;
        private float clearHalfWidth;
        private Transform fog;
        private Material fogMaterial;
        private Mesh fogMesh;
        private Texture2D noise;
        private Material moteMaterial;
        private ParticleSystem motes;
        private ParticleSystem sparks;
        private float moteDebt, sparkDebt;
        private readonly AreaVisualProfile shown = new AreaVisualProfile();

        // Main menu (2026-11-27): the title camera looks back down the line (-Z),
        // where the fog and the motes, laid out ahead of the camera, are not. While
        // on, a second copy of the fog sheets (same mesh and material) lies behind,
        // and the motes rise there, fewer.
        private Transform backFog;
        private bool backView;
        public bool BackView => backView;
        public bool BackFogVisible => backFog != null && backFog.gameObject.activeInHierarchy;

        public AreaVisualProfile Shown => shown;
        public bool LowFogVisible => fog != null && fog.gameObject.activeInHierarchy && shown.lowFogAlpha > 0.001f;
        public int ParticleCount => (motes != null ? motes.particleCount : 0) + (sparks != null ? sparks.particleCount : 0);

        public static AreaAtmosphere Create(Transform viewer, float trackX, float clearHalfWidth, int fogSheets,
            UnityEngine.SceneManagement.Scene scene)
        {
            GameObject go = new GameObject("Area Atmosphere");
            if (scene.IsValid() && scene.isLoaded)
                UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(go, scene);
            AreaAtmosphere atmosphere = go.AddComponent<AreaAtmosphere>();
            atmosphere.viewer = viewer;
            atmosphere.trackX = trackX;
            atmosphere.clearHalfWidth = clearHalfWidth;
            atmosphere.Build(Mathf.Clamp(fogSheets, 1, 8));
            atmosphere.Apply(new AreaVisualProfile());
            return atmosphere;
        }

        private void Build(int sheets)
        {
            Shader fogShader = Resources.Load<Shader>("Environment/LowFog");
            if (fogShader != null)
            {
                noise = BuildNoise(128);
                fogMaterial = new Material(fogShader) { name = "Low Fog (Runtime)" };
                fogMaterial.SetTexture("_NoiseTex", noise);
                fogMesh = BuildFogMesh(sheets);
                GameObject go = new GameObject("Low Fog");
                go.transform.SetParent(transform, false);
                go.AddComponent<MeshFilter>().sharedMesh = fogMesh;
                MeshRenderer r = go.AddComponent<MeshRenderer>();
                r.sharedMaterial = fogMaterial;
                r.shadowCastingMode = ShadowCastingMode.Off;
                r.receiveShadows = false;
                fog = go.transform;
            }
            else
                Debug.LogWarning("Low fog shader missing (Resources/Environment/LowFog).");

            Shader fx = Resources.Load<Shader>("Feedback/FxAdditive");
            if (fx != null)
            {
                moteMaterial = new Material(fx) { name = "Ambient Motes (Runtime)" };
                moteMaterial.SetFloat("_Core", 0.35f);
                moteMaterial.SetFloat("_Softness", 1.8f);
                motes = CreateSystem("Ambient Motes", 220, ParticleSystemRenderMode.Billboard, 0f);
                sparks = CreateSystem("Ambient Sparks", 60, ParticleSystemRenderMode.Stretch, 0.06f);
            }
        }

        private ParticleSystem CreateSystem(string name, int max, ParticleSystemRenderMode mode, float velocityScale)
        {
            GameObject go = new GameObject(name);
            go.transform.SetParent(transform, false);
            ParticleSystem ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            ParticleSystem.MainModule main = ps.main;
            main.playOnAwake = false;
            main.loop = true;
            main.maxParticles = max;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.gravityModifier = 0f;
            ParticleSystem.EmissionModule emission = ps.emission;
            emission.enabled = false;
            ParticleSystem.ShapeModule shape = ps.shape;
            shape.enabled = false;
            ParticleSystem.ColorOverLifetimeModule col = ps.colorOverLifetime;
            col.enabled = true;
            Gradient g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.25f), new GradientAlphaKey(0.8f, 0.7f), new GradientAlphaKey(0f, 1f) });
            col.color = g;
            ParticleSystemRenderer r = go.GetComponent<ParticleSystemRenderer>();
            r.sharedMaterial = moteMaterial;
            r.renderMode = mode;
            r.velocityScale = velocityScale;
            r.lengthScale = 1f;
            r.shadowCastingMode = ShadowCastingMode.Off;
            r.receiveShadows = false;
            r.maxParticleSize = 0.3f;
            ps.Play();
            return ps;
        }

        // Everything that follows the blended profile (called by AreaDirector).
        public void Apply(AreaVisualProfile profile)
        {
            if (profile != shown)
                AreaVisualProfile.Lerp(profile, profile, 1f, shown);
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogStartDistance = shown.fogStart;
            RenderSettings.fogEndDistance = Mathf.Max(shown.fogStart + 1f, shown.fogEnd);
            Shader.SetGlobalColor(SideLightId, shown.sideLight);
            Vector3 dir = shown.sideLightFrom.sqrMagnitude > 0.0001f ? shown.sideLightFrom.normalized : Vector3.up;
            Shader.SetGlobalVector(SideDirId, new Vector4(dir.x, dir.y, dir.z, 0f));
            Shader.SetGlobalColor(BackLightId, shown.backLight);
            if (fogMaterial != null)
            {
                fogMaterial.SetColor(ColorId, shown.lowFogColor);
                fogMaterial.SetColor(GlowColorId, shown.lowFogGlowColor);
                fogMaterial.SetFloat(AlphaId, shown.lowFogAlpha);
                fogMaterial.SetFloat(ScaleId, Mathf.Max(1f, shown.lowFogScale));
                fogMaterial.SetVector(DriftId, new Vector4(shown.lowFogDrift.x, shown.lowFogDrift.y, 0f, 0f));
                fogMaterial.SetFloat(TrackXId, trackX);
                fogMaterial.SetFloat(ClearHalfWidthId, clearHalfWidth);
                fog.gameObject.SetActive(shown.lowFogAlpha > 0.001f);
                if (backFog != null)
                    backFog.gameObject.SetActive(backView && fog.gameObject.activeSelf);
            }
        }

        private void LateUpdate()
        {
            if (viewer == null)
            {
                Camera main = Camera.main;
                if (main == null)
                    return;
                viewer = main.transform;
            }
            Vector3 at = viewer.position;
            if (fog != null)
            {
                fog.position = new Vector3(trackX, shown.lowFogBottom, at.z);
                fog.localScale = new Vector3(1f, Mathf.Max(0.01f, shown.lowFogTop - shown.lowFogBottom), 1f);
            }
            if (backFog != null && backFog.gameObject.activeSelf)
            {
                backFog.position = new Vector3(trackX, shown.lowFogBottom, at.z);
                backFog.localScale = fog.localScale;
            }
            EmitAmbient(at, Time.deltaTime);
        }

        // On while the menu shows the line behind the start.
        public void SetBackView(bool on)
        {
            backView = on;
            if (on && backFog == null && fog != null)
            {
                GameObject go = new GameObject("Low Fog (Menu, behind)");
                go.transform.SetParent(transform, false);
                go.transform.rotation = Quaternion.Euler(0f, 180f, 0f);
                go.AddComponent<MeshFilter>().sharedMesh = fogMesh;
                MeshRenderer r = go.AddComponent<MeshRenderer>();
                r.sharedMaterial = fogMaterial;
                r.shadowCastingMode = ShadowCastingMode.Off;
                r.receiveShadows = false;
                backFog = go.transform;
            }
            if (backFog != null)
                backFog.gameObject.SetActive(on && fog != null && fog.gameObject.activeSelf);
        }

        private void EmitAmbient(Vector3 at, float dt)
        {
            if (motes == null || dt <= 0f)
                return;
            float rate = shown.particleRate;
            if (backView)
            {
                // A few souls rising slowly far behind the Reaper, nothing ahead.
                moteDebt += rate * MenuMoteShare * dt;
                while (moteDebt >= 1f) { moteDebt -= 1f; EmitMenuSoul(at); }
                sparkDebt = 0f;
                return;
            }
            switch (shown.particleStyle)
            {
                case AmbientParticleStyle.Ash:
                    moteDebt += rate * dt;
                    while (moteDebt >= 1f) { moteDebt -= 1f; EmitAsh(at); }
                    sparkDebt += rate * 0.18f * dt;
                    while (sparkDebt >= 1f) { sparkDebt -= 1f; EmitEmber(at); }
                    break;
                case AmbientParticleStyle.SteamAndSparks:
                    moteDebt += rate * 0.55f * dt;
                    while (moteDebt >= 1f) { moteDebt -= 1f; EmitSteam(at); }
                    sparkDebt += rate * 0.35f * dt;
                    while (sparkDebt >= 1f) { sparkDebt -= 1f; EmitSpark(at); }
                    break;
                default:
                    moteDebt += rate * dt;
                    while (moteDebt >= 1f) { moteDebt -= 1f; EmitNeonDust(at); }
                    sparkDebt = 0f;
                    break;
            }
        }

        // Own stream: the shared UnityEngine.Random is left to whatever gameplay uses it.
        private readonly System.Random rng = new System.Random(4115);
        private float Value01 => (float)rng.NextDouble();
        private float Range(float min, float max) => min + (max - min) * (float)rng.NextDouble();

        private Color Pick() => Color.Lerp(shown.particleColor, shown.particleColorAlt, Value01);

        // Anywhere ahead, the track included, but small: specks, not clutter.
        private Vector3 Ahead(Vector3 at, float minY, float maxY, float halfWidth, float clear)
        {
            float x = Range(clear, halfWidth) * (Value01 < 0.5f ? -1f : 1f);
            return new Vector3(trackX + x, Range(minY, maxY), at.z + Range(6f, 50f));
        }

        private void EmitNeonDust(Vector3 at)
        {
            var p = new ParticleSystem.EmitParams
            {
                position = Ahead(at, -1f, 9f, 14f, 0f),
                velocity = new Vector3(Range(-0.35f, 0.35f), Range(0.05f, 0.45f), Range(-0.3f, 0.3f)),
                startSize = Range(0.1f, 0.24f),
                startLifetime = Range(3.5f, 6f),
                startColor = Pick(),
            };
            motes.Emit(p, 1);
        }

        // Menu: share of the area's mote rate, and where they rise (behind the
        // start, mostly off the centre, background distance).
        private const float MenuMoteShare = 0.3f;

        private void EmitMenuSoul(Vector3 at)
        {
            float x = Range(1.5f, 16f) * (Value01 < 0.5f ? -1f : 1f);
            var p = new ParticleSystem.EmitParams
            {
                position = new Vector3(trackX + x, Range(-1f, 7f), at.z - Range(14f, 60f)),
                velocity = new Vector3(Range(-0.15f, 0.15f), Range(0.18f, 0.5f), 0f),
                startSize = Range(0.14f, 0.3f),
                startLifetime = Range(4.5f, 7.5f),
                startColor = Pick(),
            };
            motes.Emit(p, 1);
        }

        private void EmitAsh(Vector3 at)
        {
            Color c = Pick();
            var p = new ParticleSystem.EmitParams
            {
                position = Ahead(at, 1f, 12f, 14f, 0f),
                velocity = new Vector3(Range(0.1f, 0.6f), Range(-0.7f, -0.25f), Range(-0.2f, 0.2f)),
                startSize = Range(0.1f, 0.24f),
                startLifetime = Range(4f, 6.5f),
                startColor = c,
                rotation = Range(0f, 360f),
            };
            motes.Emit(p, 1);
        }

        private void EmitEmber(Vector3 at)
        {
            var p = new ParticleSystem.EmitParams
            {
                position = Ahead(at, -2f, 4f, 15f, 5f),
                velocity = new Vector3(Range(-0.3f, 0.3f), Range(0.6f, 1.4f), 0f),
                startSize = Range(0.08f, 0.14f),
                startLifetime = Range(2.5f, 4f),
                startColor = new Color(1f, 0.32f, 0.2f, shown.particleColor.a),
            };
            sparks.Emit(p, 1);
        }

        // Steam stays off the track: soft puffs rising beside it, out of the low fog.
        private void EmitSteam(Vector3 at)
        {
            Color c = shown.particleColorAlt;
            var p = new ParticleSystem.EmitParams
            {
                position = Ahead(at, -3f, 1.5f, 22f, 6.5f),
                velocity = new Vector3(Range(-0.2f, 0.2f), Range(0.6f, 1.3f), 0f),
                startSize = Range(1.4f, 3f),
                startLifetime = Range(2.5f, 4f),
                startColor = c,
            };
            motes.Emit(p, 1);
        }

        private void EmitSpark(Vector3 at)
        {
            float side = Value01 < 0.5f ? -1f : 1f;
            var p = new ParticleSystem.EmitParams
            {
                position = new Vector3(trackX + side * Range(6.5f, 14f), Range(2f, 9f), at.z + Range(10f, 60f)),
                velocity = new Vector3(-side * Range(0.5f, 2f), Range(1f, 3f), Range(-1f, 1f)),
                startSize = Range(0.06f, 0.11f),
                startLifetime = Range(0.8f, 1.4f),
                startColor = shown.particleColor,
            };
            sparks.Emit(p, 1);
        }

        private void Update()
        {
            // Sparks fall, everything else floats.
            if (sparks != null)
            {
                ParticleSystem.MainModule main = sparks.main;
                main.gravityModifier = shown.particleStyle == AmbientParticleStyle.SteamAndSparks ? 0.35f : 0f;
            }
        }

        private void OnDestroy()
        {
            if (fogMaterial != null) Destroy(fogMaterial);
            if (fogMesh != null) Destroy(fogMesh);
            if (noise != null) Destroy(noise);
            if (moteMaterial != null) Destroy(moteMaterial);
        }

        // Sheets of a grid each (per-vertex fog stays right on huge sheets), unit
        // height: the transform puts them between the profile's bottom and top.
        private static Mesh BuildFogMesh(int sheets)
        {
            const int Columns = 14, Rows = 22;
            float[] xs = new float[Columns + 1];
            for (int c = 0; c <= Columns; c++)
            {
                float u = (c / (float)Columns) * 2f - 1f;
                xs[c] = Mathf.Sign(u) * u * u * 240f; // denser near the track
            }
            var v = new System.Collections.Generic.List<Vector3>();
            var col = new System.Collections.Generic.List<Color>();
            var tri = new System.Collections.Generic.List<int>();
            for (int s = 0; s < sheets; s++)
            {
                float h = sheets == 1 ? 0f : s / (float)(sheets - 1);
                float strength = Mathf.Lerp(1f, 0.5f, h);
                int start = v.Count;
                for (int r = 0; r <= Rows; r++)
                {
                    float w = r / (float)Rows;
                    float z = Mathf.Lerp(-30f, 340f, w * w * 0.6f + w * 0.4f);
                    for (int c = 0; c <= Columns; c++)
                    {
                        v.Add(new Vector3(xs[c], h, z));
                        col.Add(new Color(h, 0f, 0f, strength));
                    }
                }
                for (int r = 0; r < Rows; r++)
                    for (int c = 0; c < Columns; c++)
                    {
                        int i = start + r * (Columns + 1) + c;
                        tri.Add(i); tri.Add(i + Columns + 1); tri.Add(i + Columns + 2);
                        tri.Add(i); tri.Add(i + Columns + 2); tri.Add(i + 1);
                    }
            }
            Mesh m = new Mesh { name = "LowFog" };
            m.SetVertices(v);
            m.SetColors(col);
            m.SetTriangles(tri, 0);
            m.bounds = new Bounds(new Vector3(0f, 0.5f, 155f), new Vector3(480f, 2f, 400f));
            return m;
        }

        // Tileable value-noise fbm: R and G from different lattices.
        private static Texture2D BuildNoise(int size)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, true, true)
            {
                name = "LowFog Noise",
                wrapMode = TextureWrapMode.Repeat,
                filterMode = FilterMode.Bilinear,
            };
            var pixels = new Color32[size * size];
            System.Random rng = new System.Random(1115);
            float[][] latticeR = { Lattice(rng, 4), Lattice(rng, 8), Lattice(rng, 16) };
            float[][] latticeG = { Lattice(rng, 3), Lattice(rng, 6), Lattice(rng, 12) };
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float u = x / (float)size, w = y / (float)size;
                    float r = Fbm(latticeR, new[] { 4, 8, 16 }, u, w);
                    float g = Fbm(latticeG, new[] { 3, 6, 12 }, u, w);
                    pixels[y * size + x] = new Color32((byte)(r * 255f), (byte)(g * 255f), 128, 255);
                }
            tex.SetPixels32(pixels);
            tex.Apply(true, true);
            return tex;
        }

        private static float[] Lattice(System.Random rng, int n)
        {
            float[] l = new float[n * n];
            for (int i = 0; i < l.Length; i++)
                l[i] = (float)rng.NextDouble();
            return l;
        }

        private static float Fbm(float[][] lattices, int[] periods, float u, float v)
        {
            float sum = 0f, amp = 0.55f, total = 0f;
            for (int o = 0; o < lattices.Length; o++)
            {
                sum += Value(lattices[o], periods[o], u, v) * amp;
                total += amp;
                amp *= 0.5f;
            }
            return Mathf.Clamp01(sum / total);
        }

        private static float Value(float[] l, int n, float u, float v)
        {
            float x = u * n, y = v * n;
            int x0 = Mathf.FloorToInt(x), y0 = Mathf.FloorToInt(y);
            float fx = x - x0, fy = y - y0;
            fx = fx * fx * (3f - 2f * fx);
            fy = fy * fy * (3f - 2f * fy);
            int x1 = (x0 + 1) % n, y1 = (y0 + 1) % n;
            x0 %= n; y0 %= n;
            float a = Mathf.Lerp(l[y0 * n + x0], l[y0 * n + x1], fx);
            float b = Mathf.Lerp(l[y1 * n + x0], l[y1 * n + x1], fx);
            return Mathf.Lerp(a, b, fy);
        }
    }
}
