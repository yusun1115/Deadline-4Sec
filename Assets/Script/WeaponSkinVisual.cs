using UnityEngine;

namespace Deadline4Sec
{
    // Skins tint the textured scythe instead of replacing its material, so the
    // model's painted detail survives. The default skin shows the texture as-is.
    // Renderers without a texture (graybox primitives) still get the skin material.
    public sealed class WeaponSkinVisual : MonoBehaviour
    {
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");

        [SerializeField] private Renderer[] weaponRenderers;
        [SerializeField] private Material untintedSkin;
        [SerializeField, Min(0f)] private float skinGlow = 0.6f;

        private MaterialPropertyBlock block;

        public void Apply(Material material)
        {
            if (material == null || weaponRenderers == null)
                return;
            block ??= new MaterialPropertyBlock();
            foreach (Renderer renderer in weaponRenderers)
            {
                if (renderer == null)
                    continue;
                Material current = renderer.sharedMaterial;
                // Any painted blade (URP _BaseMap or legacy _MainTex) keeps its texture.
                bool textured = current != null &&
                    ((current.HasProperty("_BaseMap") && current.GetTexture("_BaseMap") != null) ||
                     (current.HasProperty("_MainTex") && current.GetTexture("_MainTex") != null));
                if (!textured)
                {
                    renderer.sharedMaterial = material;
                    continue;
                }
                if (material == untintedSkin || !material.HasProperty(BaseColorId))
                {
                    renderer.SetPropertyBlock(null);
                    continue;
                }
                Color tint = material.GetColor(BaseColorId);
                renderer.GetPropertyBlock(block);
                block.SetColor(BaseColorId, Color.Lerp(Color.white, tint, 0.85f));
                block.SetColor(EmissionColorId, tint * skinGlow);
                renderer.SetPropertyBlock(block);
            }
        }
    }
}
