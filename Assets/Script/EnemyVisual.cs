using UnityEngine;

namespace Deadline4Sec
{
    // Character look for enemies, built on top of the gameplay box. Uses an
    // assigned model when one exists; otherwise builds a stylized shadow imp
    // (ground) or winged wraith (air) with a pink halo, matching the key art.
    // Purely visual: no colliders, and the Enemy/attack-zone logic is untouched.
    public sealed class EnemyVisual : MonoBehaviour
    {
        [SerializeField] private GameObject modelPrefab;
        // Converted Tripo models face -X; -90° turns them toward the oncoming player (-Z).
        [SerializeField] private float modelYaw = -90f;
        [SerializeField, Min(0f)] private float threatRange = 7f;
        [Header("Readability")]
        [SerializeField, Min(0.1f)] private float groundVisualHeight = 1.6f;
        [SerializeField, Min(0.1f)] private float airVisualHeight = 1.7f;
        [SerializeField] private Color outlineColor = new Color(1f, 0.38f, 0.72f);
        [SerializeField] private Color markerColor = new Color(1f, 0.15f, 0.5f, 0.9f);

        private static Material outlineMaterial;
        private static Material markerMaterial;
        private static Mesh markerMesh;
        private Transform marker;
        private float markerBaseScale;

        private static Material bodyMaterial;
        private static Material eyeMaterial;
        private static Material glowMaterial;
        private static Mesh sphereMesh;
        private static Mesh haloMesh;
        private static Mesh wingMesh;

        private Enemy enemy;
        private Transform visual;
        private Transform halo;
        private Transform leftWing, rightWing;
        private Renderer[] eyes;
        private Transform player;
        private Vector3 visualBase;
        private float phase;
        private MaterialPropertyBlock block;
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int EmissionId = Shader.PropertyToID("_EmissionColor");

        private void Awake()
        {
            enemy = GetComponent<Enemy>();
            Collider body = GetComponent<Collider>();
            if (body == null)
                return;
            MeshRenderer graybox = GetComponent<MeshRenderer>();
            if (graybox != null)
                graybox.enabled = false;
            phase = Random.value * 6.28f;
            block = new MaterialPropertyBlock();

            // Size from the gameplay box so visuals match what actually hits.
            Vector3 size = body is BoxCollider box ? Vector3.Scale(box.size, transform.lossyScale) : body.bounds.size;
            Vector3 center = body is BoxCollider b2 ? transform.TransformPoint(b2.center) : body.bounds.center;
            visual = new GameObject("Visual").transform;
            visual.SetParent(transform, false);
            visual.position = center;
            visual.localRotation = Quaternion.identity;
            visualBase = visual.localPosition;

            bool air = enemy != null && enemy.Type == Enemy.EnemyType.Air;
            if (modelPrefab != null)
                BuildFromModel(size, center, air);
            else if (air)
                BuildWraith(size);
            else
                BuildImp(size);
            AddOutline();
        }

        // The floor is spawned after the pattern, so find it in Start.
        private void Start()
        {
            bool air = enemy != null && enemy.Type == Enemy.EnemyType.Air;
            Collider body = GetComponent<Collider>();
            float y = 0f;
            Vector3 origin = body != null ? body.bounds.center : transform.position;
            foreach (RaycastHit hit in Physics.RaycastAll(origin, Vector3.down, 30f, ~0, QueryTriggerInteraction.Ignore))
                if (!hit.collider.transform.IsChildOf(transform) && hit.collider.GetComponentInParent<Enemy>() == null &&
                    hit.collider.GetComponentInParent<PlayerController>() == null && hit.point.y < origin.y)
                    y = Mathf.Max(y, hit.point.y);
            AddFloorMarker(new Vector3(origin.x, y + 0.03f, origin.z), air ? 1.1f : 1.5f, air ? 0.55f : 1f);
        }

        // Pink outline as an extra material: the dark enemies read on the dark track.
        private void AddOutline()
        {
            if (outlineMaterial == null)
            {
                Shader shader = Resources.Load<Shader>("Feedback/FxOutline");
                if (shader == null)
                    return;
                outlineMaterial = new Material(shader) { name = "Enemy Outline (Runtime)" };
            }
            outlineMaterial.SetColor("_Color", outlineColor);
            foreach (Renderer r in visual.GetComponentsInChildren<Renderer>())
            {
                if (!(r is MeshRenderer) && !(r is SkinnedMeshRenderer))
                    continue;
                Material[] current = r.sharedMaterials;
                Material[] withOutline = new Material[current.Length + 1];
                current.CopyTo(withOutline, 0);
                withOutline[current.Length] = outlineMaterial;
                r.sharedMaterials = withOutline;
            }
        }

        // A pulsing danger ring on the floor under every enemy (a shadow marker
        // for air enemies) so threats are visible from far down the track.
        private void AddFloorMarker(Vector3 position, float radius, float strength)
        {
            if (markerMaterial == null)
            {
                Shader shader = Resources.Load<Shader>("Feedback/FxAdditive");
                if (shader == null)
                    return;
                markerMaterial = new Material(shader) { name = "Enemy Marker (Runtime)" };
                markerMaterial.SetFloat("_Softness", 1.1f);
                markerMaterial.SetFloat("_Core", 0f);
                markerMesh = new Mesh { name = "Enemy Marker" };
                markerMesh.vertices = new[] { new Vector3(-1f, 0f, -1f), new Vector3(-1f, 0f, 1f), new Vector3(1f, 0f, 1f), new Vector3(1f, 0f, -1f) };
                markerMesh.uv = new[] { new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(1f, 0f) };
                markerMesh.triangles = new[] { 0, 1, 2, 0, 2, 3 };
                markerMesh.RecalculateBounds();
            }
            GameObject go = new GameObject("FloorMarker");
            go.transform.SetParent(transform, true);
            go.transform.position = position;
            go.transform.rotation = Quaternion.identity;
            Mesh mesh = Instantiate(markerMesh);
            Color c = markerColor;
            c.a *= strength;
            mesh.colors = new[] { c, c, c, c };
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            MeshRenderer r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = markerMaterial;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            markerBaseScale = radius;
            go.transform.localScale = Vector3.one * radius;
            marker = go.transform;
        }

        private void BuildFromModel(Vector3 size, Vector3 center, bool air)
        {
            GameObject model = Instantiate(modelPrefab, visual);
            model.transform.localRotation = Quaternion.Euler(0f, modelYaw, 0f);
            foreach (Collider c in model.GetComponentsInChildren<Collider>(true))
                Destroy(c);
            Bounds bounds = new Bounds(model.transform.position, Vector3.zero);
            bool first = true;
            foreach (Renderer r in model.GetComponentsInChildren<Renderer>())
            {
                if (first) { bounds = r.bounds; first = false; }
                else bounds.Encapsulate(r.bounds);
            }
            // Visuals are drawn larger than the 1m hit box (cartoon scale) so they
            // read at phone size; ground enemies stand on the surface under the box.
            float height = air ? airVisualHeight : groundVisualHeight;
            float scale = first ? 1f : height / Mathf.Max(0.01f, bounds.size.y);
            model.transform.localScale *= scale;
            Vector3 target = visual.position;
            if (!air)
                target.y = center.y - size.y * 0.5f - 0.24f + height * 0.5f;
            Vector3 offset = target - (bounds.center - model.transform.position) * scale - model.transform.position;
            model.transform.position += offset;
        }

        private void BuildImp(Vector3 size)
        {
            EnsureShared();
            float h = size.y, w = Mathf.Min(size.x, size.z);
            Part("Body", sphereMesh, bodyMaterial, new Vector3(0f, -h * 0.08f, 0f), new Vector3(w * 1.05f, h * 0.82f, w * 0.95f));
            Part("Belly", sphereMesh, bodyMaterial, new Vector3(0f, -h * 0.32f, 0f), new Vector3(w * 1.15f, h * 0.45f, w * 1.05f));
            // Eyes face the oncoming player (-Z).
            eyes = new Renderer[2];
            for (int i = 0; i < 2; i++)
            {
                float s = i == 0 ? -1f : 1f;
                Transform eye = Part("Eye", sphereMesh, eyeMaterial,
                    new Vector3(s * w * 0.2f, h * 0.12f, -w * 0.42f), new Vector3(w * 0.26f, h * 0.2f, w * 0.12f));
                eye.localRotation = Quaternion.Euler(0f, 0f, s * -18f);
                eyes[i] = eye.GetComponent<Renderer>();
            }
            Part("Mouth", sphereMesh, glowMaterial, new Vector3(0f, -h * 0.12f, -w * 0.44f), new Vector3(w * 0.5f, h * 0.07f, w * 0.08f));
            for (int i = 0; i < 2; i++)
            {
                float s = i == 0 ? -1f : 1f;
                Transform horn = Part("Ear", sphereMesh, bodyMaterial,
                    new Vector3(s * w * 0.36f, h * 0.36f, 0f), new Vector3(w * 0.18f, h * 0.32f, w * 0.18f));
                horn.localRotation = Quaternion.Euler(0f, 0f, s * -28f);
                Transform claw = Part("Arm", sphereMesh, bodyMaterial,
                    new Vector3(s * w * 0.62f, -h * 0.05f, -w * 0.1f), new Vector3(w * 0.2f, h * 0.38f, w * 0.2f));
                claw.localRotation = Quaternion.Euler(-30f, 0f, s * 35f);
            }
            halo = Part("Halo", haloMesh, glowMaterial, new Vector3(0f, h * 0.62f, 0f), new Vector3(w * 0.75f, w * 0.75f, w * 0.75f));
        }

        private void BuildWraith(Vector3 size)
        {
            EnsureShared();
            float h = size.y, w = Mathf.Min(size.x, size.z);
            Part("Hood", sphereMesh, bodyMaterial, new Vector3(0f, h * 0.18f, 0f), new Vector3(w * 0.7f, h * 0.65f, w * 0.7f));
            Transform robe = Part("Robe", sphereMesh, bodyMaterial, new Vector3(0f, -h * 0.25f, 0f), new Vector3(w * 0.55f, h * 0.9f, w * 0.5f));
            robe.localRotation = Quaternion.Euler(12f, 0f, 0f);
            eyes = new Renderer[1];
            eyes[0] = Part("Eye", sphereMesh, eyeMaterial, new Vector3(0f, h * 0.2f, -w * 0.33f),
                new Vector3(w * 0.38f, h * 0.07f, w * 0.08f)).GetComponent<Renderer>();
            leftWing = Part("WingL", wingMesh, bodyMaterial, new Vector3(-w * 0.25f, h * 0.15f, w * 0.1f), new Vector3(-w * 1.6f, h * 0.9f, 1f));
            rightWing = Part("WingR", wingMesh, bodyMaterial, new Vector3(w * 0.25f, h * 0.15f, w * 0.1f), new Vector3(w * 1.6f, h * 0.9f, 1f));
            Part("WingGlowL", wingMesh, glowMaterial, new Vector3(-w * 0.27f, h * 0.15f, w * 0.12f), new Vector3(-w * 1.2f, h * 0.55f, 1f))
                .SetParent(leftWing, true);
            Part("WingGlowR", wingMesh, glowMaterial, new Vector3(w * 0.27f, h * 0.15f, w * 0.12f), new Vector3(w * 1.2f, h * 0.55f, 1f))
                .SetParent(rightWing, true);
            halo = Part("Halo", haloMesh, glowMaterial, new Vector3(0f, h * 0.68f, 0f), new Vector3(w * 0.65f, w * 0.65f, w * 0.65f));
        }

        private Transform Part(string name, Mesh mesh, Material material, Vector3 position, Vector3 scale)
        {
            GameObject go = new GameObject(name);
            go.transform.SetParent(visual, false);
            go.transform.localPosition = position;
            go.transform.localScale = scale;
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            MeshRenderer r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = material;
            r.shadowCastingMode = material == bodyMaterial
                ? UnityEngine.Rendering.ShadowCastingMode.On : UnityEngine.Rendering.ShadowCastingMode.Off;
            return go.transform;
        }

        private void LateUpdate()
        {
            if (marker != null)
                marker.localScale = Vector3.one * markerBaseScale * (1f + Mathf.Sin(Time.time * 6f + phase) * 0.08f);
            if (visual == null)
                return;
            if (player == null)
            {
                PlayerController pc = FindFirstObjectByType<PlayerController>();
                if (pc == null)
                    return;
                player = pc.transform;
            }
            float t = Time.time * 4f + phase;
            bool air = enemy != null && enemy.Type == Enemy.EnemyType.Air;
            Vector3 toPlayer = player.position - transform.position;
            float dz = -toPlayer.z;
            float threat = dz > 0f && dz < threatRange ? 1f - dz / threatRange : 0f;

            // Idle bounce; tense up and lean toward the player as it closes in.
            float bob = air ? Mathf.Sin(t * 0.8f) * 0.18f : Mathf.Abs(Mathf.Sin(t)) * 0.08f;
            visual.localPosition = visualBase + Vector3.up * bob;
            float squash = air ? 1f : 1f + Mathf.Sin(t * 2f) * 0.04f;
            visual.localScale = new Vector3(1f / squash, squash, 1f / squash) * (1f + threat * 0.12f);
            float lean = Mathf.Clamp(toPlayer.x, -1f, 1f) * 18f * threat;
            visual.localRotation = Quaternion.Euler(-threat * 12f, 0f, -lean);

            if (halo != null)
                halo.localRotation = Quaternion.Euler(0f, Time.time * 90f, 0f);
            if (leftWing != null)
            {
                float flap = Mathf.Sin(t * 2.4f) * 35f;
                leftWing.localRotation = Quaternion.Euler(0f, flap, 0f);
                rightWing.localRotation = Quaternion.Euler(0f, -flap, 0f);
            }
            if (eyes != null && threat > 0f)
            {
                Color warn = Color.Lerp(Color.white, new Color(1f, 0.15f, 0.45f), threat);
                foreach (Renderer eye in eyes)
                {
                    eye.GetPropertyBlock(block);
                    block.SetColor(BaseColorId, warn);
                    block.SetColor(EmissionId, warn * (1.5f + threat * 2.5f));
                    eye.SetPropertyBlock(block);
                }
            }
        }

        private static void EnsureShared()
        {
            if (bodyMaterial != null)
                return;
            Shader lit = Shader.Find("Universal Render Pipeline/Lit");
            bodyMaterial = new Material(lit) { name = "Enemy Body (Runtime)" };
            bodyMaterial.SetColor(BaseColorId, new Color(0.05f, 0.035f, 0.07f));
            bodyMaterial.SetFloat("_Smoothness", 0.55f);
            bodyMaterial.EnableKeyword("_EMISSION");
            bodyMaterial.SetColor(EmissionId, new Color(0.06f, 0.0f, 0.05f));
            eyeMaterial = new Material(lit) { name = "Enemy Eye (Runtime)" };
            eyeMaterial.SetColor(BaseColorId, Color.white);
            eyeMaterial.EnableKeyword("_EMISSION");
            eyeMaterial.SetColor(EmissionId, Color.white * 1.6f);
            glowMaterial = new Material(lit) { name = "Enemy Glow (Runtime)" };
            glowMaterial.SetColor(BaseColorId, new Color(1f, 0.25f, 0.55f));
            glowMaterial.EnableKeyword("_EMISSION");
            glowMaterial.SetColor(EmissionId, new Color(1f, 0.15f, 0.5f) * 2.4f);
            foreach (Material m in new[] { bodyMaterial, eyeMaterial, glowMaterial })
                m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;

            GameObject sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            sphereMesh = sphere.GetComponent<MeshFilter>().sharedMesh;
            DestroyImmediate(sphere);
            haloMesh = BuildTorus(1f, 0.07f, 28, 6);
            wingMesh = BuildWing();
        }

        private static Mesh BuildTorus(float radius, float tube, int segments, int sides)
        {
            Vector3[] v = new Vector3[segments * sides];
            Vector3[] n = new Vector3[v.Length];
            int[] tris = new int[segments * sides * 6];
            for (int s = 0; s < segments; s++)
            {
                float a = s * Mathf.PI * 2f / segments;
                Vector3 ring = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                for (int k = 0; k < sides; k++)
                {
                    float b = k * Mathf.PI * 2f / sides;
                    Vector3 dir = ring * Mathf.Cos(b) + Vector3.up * Mathf.Sin(b);
                    v[s * sides + k] = ring * radius + dir * tube;
                    n[s * sides + k] = dir;
                }
            }
            int t = 0;
            for (int s = 0; s < segments; s++)
                for (int k = 0; k < sides; k++)
                {
                    int a0 = s * sides + k, a1 = s * sides + (k + 1) % sides;
                    int b0 = ((s + 1) % segments) * sides + k, b1 = ((s + 1) % segments) * sides + (k + 1) % sides;
                    tris[t++] = a0; tris[t++] = b0; tris[t++] = a1;
                    tris[t++] = a1; tris[t++] = b0; tris[t++] = b1;
                }
            Mesh m = new Mesh { name = "Halo", vertices = v, normals = n, triangles = tris };
            m.RecalculateBounds();
            return m;
        }

        // Jagged bat wing in the local XY plane extending toward +X, double-sided.
        private static Mesh BuildWing()
        {
            Vector3[] pts =
            {
                new Vector3(0f, 0f, 0f), new Vector3(0.35f, 0.55f, 0f), new Vector3(1f, 0.45f, 0f),
                new Vector3(0.82f, 0.05f, 0f), new Vector3(0.62f, 0.18f, 0f), new Vector3(0.48f, -0.25f, 0f),
                new Vector3(0.3f, -0.05f, 0f), new Vector3(0.12f, -0.35f, 0f),
            };
            int[] fan = { 0, 1, 2, 0, 2, 3, 0, 3, 4, 0, 4, 5, 0, 5, 6, 0, 6, 7 };
            Vector3[] v = new Vector3[pts.Length * 2];
            int[] tris = new int[fan.Length * 2];
            for (int i = 0; i < pts.Length; i++) { v[i] = pts[i]; v[i + pts.Length] = pts[i]; }
            for (int i = 0; i < fan.Length; i += 3)
            {
                tris[i] = fan[i]; tris[i + 1] = fan[i + 1]; tris[i + 2] = fan[i + 2];
                tris[fan.Length + i] = fan[i] + pts.Length;
                tris[fan.Length + i + 1] = fan[i + 2] + pts.Length;
                tris[fan.Length + i + 2] = fan[i + 1] + pts.Length;
            }
            Mesh m = new Mesh { name = "Wing", vertices = v, triangles = tris };
            m.RecalculateNormals();
            m.RecalculateBounds();
            return m;
        }
    }
}
