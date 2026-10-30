using UnityEngine;

namespace Deadline4Sec
{
    // Skin shop stage (2026-10-30): while the skin screen is open, the run
    // scenery behind the Reaper gives way to a shop space — a dark violet
    // gradient wall, glowing rings of light behind the character and a round
    // platform under its feet on a dark floor. Built at runtime (sprites and
    // a primitive), placed around the Reaper each time the screen opens.
    public sealed class SkinShopStage : MonoBehaviour
    {
        private static readonly Color Pink = new Color(1f, 0.3f, 0.65f);
        private static readonly Color Violet = new Color(0.62f, 0.3f, 1f);

        private Transform ringA;
        private Transform ringB;
        private SpriteRenderer ringGlow;
        private SpriteRenderer rim;
        private Material platformMaterial;
        private Texture2D wallTexture;
        private Texture2D floorTexture;

        public static SkinShopStage Create()
        {
            GameObject go = new GameObject("Skin Shop Stage");
            SkinShopStage stage = go.AddComponent<SkinShopStage>();
            stage.Build();
            go.SetActive(false);
            return stage;
        }

        private void Build()
        {
            // Wall: vertical violet gradient with a soft bright centre.
            wallTexture = Gradient(64, 128, (u, v) =>
            {
                Color bottom = new Color(0.03f, 0.01f, 0.06f), top = new Color(0.2f, 0.07f, 0.3f);
                Color c = Color.Lerp(bottom, top, Mathf.SmoothStep(0f, 1f, v));
                float d = Vector2.Distance(new Vector2(u, v), new Vector2(0.5f, 0.42f));
                return c + new Color(0.16f, 0.04f, 0.2f) * Mathf.Clamp01(1f - d * 2.2f);
            });
            Sprite(transform, "Wall", wallTexture, new Vector3(0f, 2.5f, -3.4f), Quaternion.identity, new Vector2(24f, 14f), Color.white, -10);

            // Floor: dark disc of light fading out over the run track.
            floorTexture = Gradient(64, 64, (u, v) =>
            {
                float d = Vector2.Distance(new Vector2(u, v), new Vector2(0.5f, 0.5f)) * 2f;
                return Color.Lerp(new Color(0.12f, 0.04f, 0.17f), new Color(0.02f, 0.01f, 0.04f), Mathf.SmoothStep(0f, 1f, d));
            });
            Sprite(transform, "Floor", floorTexture, new Vector3(0f, 0.004f, 0f), Quaternion.Euler(90f, 0f, 0f), new Vector2(26f, 26f), Color.white, -9);

            // Rings of light behind the Reaper.
            ringGlow = Sprite(transform, "Ring Glow", FxSprites.Glow.texture, new Vector3(-0.3f, 1.55f, -2.2f), Quaternion.identity,
                new Vector2(3.4f, 3.4f), new Color(Violet.r, Violet.g, Violet.b, 0.35f), -8);
            ringA = Sprite(transform, "Ring", FxSprites.Ring.texture, new Vector3(-0.3f, 1.55f, -2.1f), Quaternion.identity,
                new Vector2(2.1f, 2.1f), new Color(Pink.r, Pink.g, Pink.b, 0.7f), -7).transform;
            ringB = Sprite(transform, "Outer Ring", FxSprites.Ring.texture, new Vector3(-0.3f, 1.55f, -2.15f), Quaternion.identity,
                new Vector2(2.75f, 2.75f), new Color(Violet.r, Violet.g, Violet.b, 0.35f), -7).transform;

            // Round platform under the feet with a glowing rim.
            GameObject platform = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            platform.name = "Platform";
            Destroy(platform.GetComponent<Collider>());
            platform.transform.SetParent(transform, false);
            platform.transform.localPosition = new Vector3(0f, 0.035f, 0f);
            platform.transform.localScale = new Vector3(1.7f, 0.03f, 1.7f);
            platformMaterial = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
            platformMaterial.SetColor("_BaseColor", new Color(0.13f, 0.05f, 0.19f));
            MeshRenderer renderer = platform.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = platformMaterial;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            rim = Sprite(transform, "Platform Rim", FxSprites.Ring.texture, new Vector3(0f, 0.072f, 0f), Quaternion.Euler(90f, 0f, 0f),
                new Vector2(1.9f, 1.9f), new Color(Pink.r, Pink.g, Pink.b, 0.8f), -6);
            Sprite(transform, "Platform Glow", FxSprites.Glow.texture, new Vector3(0f, 0.071f, 0f), Quaternion.Euler(90f, 0f, 0f),
                new Vector2(1.8f, 1.8f), new Color(Pink.r, Pink.g, Pink.b, 0.2f), -6);
        }

        // Around the Reaper (its feet on the platform), facing the shop camera.
        public void Show(bool shown)
        {
            if (shown)
            {
                PlayerController player = FindFirstObjectByType<PlayerController>();
                if (player != null)
                {
                    CharacterController body = player.GetComponent<CharacterController>();
                    float feet = body != null ? body.bounds.min.y : player.transform.position.y - 1f;
                    transform.position = new Vector3(player.transform.position.x, feet, player.transform.position.z);
                    transform.rotation = Quaternion.identity;
                }
            }
            gameObject.SetActive(shown);
        }

        private void Update()
        {
            float t = Time.unscaledTime;
            if (ringA != null)
                ringA.localRotation = Quaternion.Euler(0f, 0f, t * 12f);
            if (ringB != null)
            {
                ringB.localRotation = Quaternion.Euler(0f, 0f, -t * 7f);
                ringB.localScale = Vector3.one * (1f + 0.03f * Mathf.Sin(t * 1.4f));
            }
            if (ringGlow != null)
                ringGlow.color = new Color(Violet.r, Violet.g, Violet.b, 0.28f + 0.1f * Mathf.Sin(t * 1.8f));
            if (rim != null)
                rim.color = new Color(Pink.r, Pink.g, Pink.b, 0.65f + 0.15f * Mathf.Sin(t * 2.4f));
        }

        private void OnDestroy()
        {
            Destroy(platformMaterial);
            Destroy(wallTexture);
            Destroy(floorTexture);
        }

        private static SpriteRenderer Sprite(Transform parent, string name, Texture2D texture, Vector3 position, Quaternion rotation,
            Vector2 size, Color color, int order)
        {
            GameObject go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = position;
            go.transform.localRotation = rotation;
            SpriteRenderer sprite = go.AddComponent<SpriteRenderer>();
            sprite.sprite = UnityEngine.Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), new Vector2(0.5f, 0.5f),
                texture.width);
            sprite.color = color;
            sprite.sortingOrder = order;
            go.transform.localScale = new Vector3(size.x, size.y * texture.width / texture.height, 1f);
            return sprite;
        }

        private static Texture2D Gradient(int w, int h, System.Func<float, float, Color> color)
        {
            Texture2D texture = new Texture2D(w, h, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                Color c = color((x + 0.5f) / w, (y + 0.5f) / h);
                c.a = 1f;
                texture.SetPixel(x, y, c);
            }
            texture.Apply(false);
            return texture;
        }
    }
}
