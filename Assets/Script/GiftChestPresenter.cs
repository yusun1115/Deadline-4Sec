using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Deadline4Sec
{
    // Gift box opening show: a 3D chest on a private stage (far below the course)
    // is rendered into a RawImage over a rotating sunburst. Closed, it wobbles
    // and teases; tapped, it shakes, the lid bursts open with stars and the
    // reward icon pops out. Rewards themselves are granted by RunInventory as
    // before; this class only reads the reward text GameExtrasUI shows.
    public sealed class GiftChestPresenter : MonoBehaviour
    {
        private const int StageLayer = 31;
        private static readonly Vector3 StageOrigin = new Vector3(0f, -500f, 0f);

        [SerializeField] private RawImage chestView;
        [SerializeField] private Image rewardIcon;
        [SerializeField] private Image sunburst;
        [SerializeField] private RectTransform rewardLabel;
        [SerializeField] private Sprite coinIcon;
        [SerializeField] private Sprite shieldIcon;
        [SerializeField] private Sprite dashIcon;
        [SerializeField] private Sprite couponIcon;
        [SerializeField] private Sprite skinCurrencyIcon;

        private GameObject stage;
        private Transform chest;
        private Transform lid;
        private Camera stageCamera;
        private RenderTexture target;
        private ParticleSystem stars;
        private Material sunburstMaterial;
        private bool opened;
        private bool burstDone;
        private float stateStart;
        private static readonly int BurstId = Shader.PropertyToID("_Burst");

        public void Show(string reward, bool allOpened)
        {
            if (allOpened)
            {
                SetVisible(false);
                return;
            }
            EnsureStage();
            SetVisible(true);
            bool nowOpen = !string.IsNullOrEmpty(reward);
            if (nowOpen && !opened)
            {
                opened = true;
                burstDone = false;
                stateStart = Time.unscaledTime;
                if (rewardIcon != null)
                {
                    rewardIcon.sprite = IconFor(reward, out bool skinCurrency);
                    // Skin currency borrows the skin icon, tinted to read as a gem.
                    rewardIcon.color = skinCurrency ? new Color(1f, 0.55f, 0.85f) : Color.white;
                    rewardIcon.rectTransform.localScale = Vector3.zero;
                }
            }
            else if (!nowOpen)
            {
                opened = false;
                stateStart = Time.unscaledTime;
                if (rewardLabel != null)
                    rewardLabel.localScale = Vector3.one;
            }
        }

        public void Hide() => SetVisible(false);

        private void OnDisable() => SetVisible(false);

        private void SetVisible(bool visible)
        {
            if (stage != null)
                stage.SetActive(visible);
            if (stageCamera != null)
                stageCamera.enabled = visible;
            if (chestView != null)
                chestView.enabled = visible;
            if (rewardIcon != null)
                rewardIcon.enabled = false;
            if (!visible)
                opened = false;
        }

        private Sprite IconFor(string reward, out bool skinCurrency)
        {
            skinCurrency = false;
            if (reward.StartsWith("COIN")) return coinIcon;
            if (reward.StartsWith("SHIELD")) return shieldIcon;
            if (reward.StartsWith("DASH")) return dashIcon;
            if (reward.StartsWith("REVIVE")) return couponIcon != null ? couponIcon : coinIcon;
            skinCurrency = true;
            return skinCurrencyIcon != null ? skinCurrencyIcon : coinIcon;
        }

        private void LateUpdate()
        {
            if (stage == null || !stage.activeSelf)
                return;
            float t = Time.unscaledTime - stateStart;
            float now = Time.unscaledTime;
            if (!opened)
            {
                // Closed: slow sway and a little hop every so often to invite a tap.
                float hop = Mathf.Max(0f, Mathf.Sin(now * 2.2f)) ;
                chest.localPosition = new Vector3(0f, Mathf.Pow(hop, 6f) * 0.12f, 0f);
                chest.localRotation = Quaternion.Euler(-8f, 25f + Mathf.Sin(now * 0.9f) * 18f,
                    Mathf.Sin(now * 14f) * Mathf.Pow(hop, 8f) * 6f);
                lid.localRotation = Quaternion.identity;
                chest.localScale = Vector3.one;
                if (sunburstMaterial != null)
                    sunburstMaterial.SetFloat(BurstId, 0f);
                return;
            }

            // Opening: 0.4s shake, then the lid flies open and the reward pops.
            const float shakeTime = 0.4f;
            if (t < shakeTime)
            {
                float k = t / shakeTime;
                chest.localPosition = new Vector3(Mathf.Sin(now * 70f) * 0.04f * k, 0f, 0f);
                chest.localRotation = Quaternion.Euler(-8f, 20f, Mathf.Sin(now * 60f) * 10f * k);
                chest.localScale = new Vector3(1f + k * 0.08f, 1f - k * 0.1f, 1f + k * 0.08f);
                if (rewardLabel != null)
                    rewardLabel.localScale = Vector3.zero;
                return;
            }
            float o = t - shakeTime;
            if (!burstDone)
            {
                burstDone = true;
                if (stars != null)
                    stars.Emit(60);
            }
            float lidT = Mathf.Clamp01(o / 0.22f);
            // Positive X swings the front edge up and back around the rear hinge.
            lid.localRotation = Quaternion.Euler(110f * (1f - (1f - lidT) * (1f - lidT)), 0f, 0f);
            float settle = Mathf.Clamp01(o / 0.25f);
            chest.localScale = Vector3.Lerp(new Vector3(0.9f, 1.18f, 0.9f), Vector3.one, settle);
            chest.localPosition = Vector3.zero;
            chest.localRotation = Quaternion.Euler(-8f, 20f, 0f);
            if (sunburstMaterial != null)
                sunburstMaterial.SetFloat(BurstId, Mathf.Clamp01(1f - o * 2.5f));
            if (rewardLabel != null)
            {
                float l = Mathf.Clamp01((o - 0.3f) / 0.2f);
                rewardLabel.localScale = Vector3.one * (l < 1f ? Mathf.Lerp(0f, 1.15f, l) : 1f);
            }

            if (rewardIcon != null)
            {
                rewardIcon.enabled = true;
                float p = Mathf.Clamp01(o / 0.45f);
                float rise = 1f - (1f - p) * (1f - p) * (1f - p);
                float pop = p < 1f ? Mathf.Lerp(0.2f, 1.25f, rise) : 1f + Mathf.Sin(now * 3f) * 0.04f;
                if (p >= 1f && o < 0.6f)
                    pop = Mathf.Lerp(1.25f, 1f, (o - 0.45f) / 0.15f);
                RectTransform rt = rewardIcon.rectTransform;
                // The icon sits inside the chest view; rise is a fraction of its height.
                float h = chestView != null ? chestView.rectTransform.rect.height : 400f;
                rt.anchoredPosition = new Vector2(0f, h * (Mathf.Lerp(-0.05f, 0.33f, rise) + Mathf.Sin(now * 2.5f) * 0.012f * p));
                rt.localScale = Vector3.one * pop;
                rt.localRotation = Quaternion.Euler(0f, 0f, (1f - rise) * 40f);
            }
        }

        private void EnsureStage()
        {
            if (stage != null)
                return;
            stage = new GameObject("Gift Chest Stage");
            stage.transform.position = StageOrigin;

            chest = new GameObject("Chest").transform;
            chest.SetParent(stage.transform, false);
            Material material = new Material(Shader.Find("Deadline4Sec/VertexColorLit")) { name = "Gift Chest (Runtime)" };
            material.SetColor("_RimColor", new Color(1f, 0.6f, 0.25f));
            material.SetFloat("_RimPower", 4f);
            Part(chest, "Base", BuildChest(false), material, Vector3.zero);
            lid = new GameObject("LidHinge").transform;
            lid.SetParent(chest, false);
            lid.localPosition = new Vector3(0f, 0.16f, 0.32f);
            Part(lid, "Lid", BuildChest(true), material, new Vector3(0f, -0.16f, -0.32f));

            Light glow = new GameObject("Glow").AddComponent<Light>();
            glow.transform.SetParent(stage.transform, false);
            glow.transform.localPosition = new Vector3(0f, 0.6f, -1.4f);
            glow.type = LightType.Point;
            glow.color = new Color(1f, 0.7f, 0.9f);
            glow.range = 4f;
            glow.intensity = 1.2f;
            glow.cullingMask = 1 << StageLayer;

            stars = BuildStars(stage.transform);

            target = new RenderTexture(768, 768, 24, RenderTextureFormat.ARGB32) { name = "Gift Chest View" };
            stageCamera = new GameObject("Gift Chest Camera").AddComponent<Camera>();
            stageCamera.transform.SetParent(stage.transform, false);
            stageCamera.transform.localPosition = new Vector3(0f, 0.85f, -3.4f);
            stageCamera.transform.LookAt(StageOrigin + new Vector3(0f, 0.12f, 0f));
            stageCamera.fieldOfView = 30f;
            stageCamera.clearFlags = CameraClearFlags.SolidColor;
            stageCamera.backgroundColor = new Color(0f, 0f, 0f, 0f);
            stageCamera.cullingMask = 1 << StageLayer;
            stageCamera.targetTexture = target;
            stageCamera.nearClipPlane = 0.1f;
            stageCamera.farClipPlane = 20f;
            var data = stageCamera.GetComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>();
            if (data != null)
                data.renderPostProcessing = false;
            foreach (Transform t in stage.GetComponentsInChildren<Transform>(true))
                t.gameObject.layer = StageLayer;
            // The course camera never needs to see the private stage.
            if (Camera.main != null)
                Camera.main.cullingMask &= ~(1 << StageLayer);

            if (chestView != null)
            {
                chestView.texture = target;
                Shader premultiplied = Resources.Load<Shader>("Feedback/UIPremultiplied");
                if (premultiplied != null)
                    chestView.material = new Material(premultiplied) { name = "Gift Chest View (Runtime)" };
            }
            if (sunburst != null)
            {
                Shader shader = Resources.Load<Shader>("Feedback/UISunburst");
                if (shader != null)
                {
                    sunburstMaterial = new Material(shader) { name = "Sunburst (Runtime)" };
                    sunburst.material = sunburstMaterial;
                }
            }
        }

        private static void Part(Transform parent, string name, Mesh mesh, Material material, Vector3 offset)
        {
            GameObject go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = offset;
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            go.AddComponent<MeshRenderer>().sharedMaterial = material;
        }

        private ParticleSystem BuildStars(Transform parent)
        {
            GameObject go = new GameObject("Stars");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = new Vector3(0f, 0.25f, 0f);
            ParticleSystem ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.playOnAwake = false;
            main.loop = false;
            main.useUnscaledTime = true;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.5f, 1f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(2f, 4.5f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.06f, 0.16f);
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, 0.85f, 0.4f), new Color(1f, 0.4f, 0.8f));
            main.gravityModifier = 0.6f;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            var emission = ps.emission;
            emission.enabled = false;
            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 35f;
            shape.radius = 0.2f;
            shape.rotation = new Vector3(-90f, 0f, 0f);
            var col = ps.colorOverLifetime;
            col.enabled = true;
            Gradient g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0f, 1f) });
            col.color = g;
            ParticleSystemRenderer r = go.GetComponent<ParticleSystemRenderer>();
            Shader fx = Resources.Load<Shader>("Feedback/FxAdditive");
            if (fx != null)
            {
                r.sharedMaterial = new Material(fx) { name = "Gift Stars (Runtime)" };
                r.sharedMaterial.SetFloat("_AlphaWrite", 0f);
            }
            ps.Play();
            return ps;
        }

        // Gothic treasure chest from colored boxes (matches the course gift box).
        private static Mesh BuildChest(bool lidPart)
        {
            var v = new List<Vector3>();
            var c = new List<Color>();
            var t = new List<int>();
            void Box(Vector3 min, Vector3 max, Color col)
            {
                // Picked as sRGB; vertex colors are read as linear in this project.
                col = col.linear;
                Vector3[] p =
                {
                    new Vector3(min.x, min.y, min.z), new Vector3(max.x, min.y, min.z), new Vector3(max.x, max.y, min.z), new Vector3(min.x, max.y, min.z),
                    new Vector3(min.x, min.y, max.z), new Vector3(max.x, min.y, max.z), new Vector3(max.x, max.y, max.z), new Vector3(min.x, max.y, max.z),
                };
                int[][] faces = { new[] { 0, 3, 2, 1 }, new[] { 4, 5, 6, 7 }, new[] { 0, 4, 7, 3 }, new[] { 1, 2, 6, 5 }, new[] { 3, 7, 6, 2 }, new[] { 0, 1, 5, 4 } };
                foreach (int[] f in faces)
                {
                    int b = v.Count;
                    foreach (int i in f) { v.Add(p[i]); c.Add(col); }
                    t.AddRange(new[] { b, b + 1, b + 2, b, b + 2, b + 3 });
                }
            }
            Color wood = new Color(0.24f, 0.09f, 0.3f), lidWood = new Color(0.34f, 0.13f, 0.42f);
            Color silver = new Color(0.85f, 0.83f, 0.92f), gold = new Color(1f, 0.74f, 0.26f);
            Color glow = new Color(1f, 0.2f, 0.6f) * 2.2f;
            if (!lidPart)
            {
                Box(new Vector3(-0.45f, -0.3f, -0.3f), new Vector3(0.45f, 0.14f, 0.3f), wood);
                Box(new Vector3(-0.42f, 0.1f, -0.27f), new Vector3(0.42f, 0.16f, 0.27f), glow);
                foreach (float x in new[] { -0.28f, 0.28f })
                    Box(new Vector3(x - 0.045f, -0.31f, -0.31f), new Vector3(x + 0.045f, 0.15f, 0.31f), silver);
                foreach (float x in new[] { -0.46f, 0.46f })
                    foreach (float z in new[] { -0.3f, 0.3f })
                        Box(new Vector3(x - 0.05f, -0.32f, z - 0.05f), new Vector3(x + 0.05f, -0.2f, z + 0.05f), gold);
                Box(new Vector3(-0.09f, -0.04f, -0.36f), new Vector3(0.09f, 0.14f, -0.3f), gold);
                Box(new Vector3(-0.05f, 0f, -0.38f), new Vector3(0.05f, 0.11f, -0.36f), glow);
            }
            else
            {
                Box(new Vector3(-0.47f, 0.16f, -0.32f), new Vector3(0.47f, 0.32f, 0.32f), lidWood);
                Box(new Vector3(-0.38f, 0.32f, -0.22f), new Vector3(0.38f, 0.39f, 0.22f), lidWood);
                foreach (float x in new[] { -0.28f, 0.28f })
                    Box(new Vector3(x - 0.045f, 0.15f, -0.33f), new Vector3(x + 0.045f, 0.4f, 0.33f), silver);
                foreach (float x in new[] { -0.47f, 0.47f })
                    foreach (float z in new[] { -0.31f, 0.31f })
                        Box(new Vector3(x - 0.05f, 0.24f, z - 0.05f), new Vector3(x + 0.05f, 0.34f, z + 0.05f), gold);
                Box(new Vector3(-0.07f, 0.16f, -0.36f), new Vector3(0.07f, 0.26f, -0.32f), gold);
            }
            Mesh m = new Mesh { name = lidPart ? "Chest Lid" : "Chest Base" };
            m.SetVertices(v);
            m.SetColors(c);
            m.SetTriangles(t, 0);
            m.RecalculateNormals();
            m.RecalculateBounds();
            return m;
        }

        private void OnDestroy()
        {
            if (stage != null)
                Destroy(stage);
            if (target != null)
            {
                target.Release();
                Destroy(target);
            }
            if (sunburstMaterial != null)
                Destroy(sunburstMaterial);
        }
    }
}
