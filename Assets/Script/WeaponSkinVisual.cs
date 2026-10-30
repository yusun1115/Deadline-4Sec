using System;
using UnityEngine;

namespace Deadline4Sec
{
    // Skins swap the scythe in the Reaper's hand for the skin's own weapon
    // model (2026-10-30): the skin prefab is placed in the built-in weapon's own
    // (animated) node, in that model's space with its origin on the grip, and
    // the built-in renderer is only hidden, so anything attached to it (the
    // swing trail) keeps following the hand. A skin without a prefab shows the
    // built-in scythe, tinted by its material as before.
    public sealed class WeaponSkinVisual : MonoBehaviour
    {
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");
        private static readonly Color DefaultAccent = new Color(1f, 0.22f, 0.55f);

        [SerializeField] private Renderer[] weaponRenderers;
        [SerializeField] private Material untintedSkin;
        [SerializeField, Min(0f)] private float skinGlow = 0.6f;

        private MaterialPropertyBlock block;
        private GameObject skinWeapon;

        // The equipped skin's colour (swing trail, kill sparks) and its changes.
        public static Color CurrentAccent { get; private set; } = DefaultAccent;
        public static event Action<Color> AccentChanged;

        public void Apply(GameExtrasCatalog.Skin skin)
        {
            if (skin == null)
                return;
            Renderer hand = HandWeapon();
            if (skinWeapon != null)
            {
                Destroy(skinWeapon);
                skinWeapon = null;
            }
            if (skin.weaponPrefab != null && hand != null)
            {
                // Under the built-in weapon's own node: the clips animate that
                // node, so the skin follows every pose exactly.
                hand.enabled = false;
                Transform slot = hand.transform;
                skinWeapon = Instantiate(skin.weaponPrefab, slot, false);
                skinWeapon.name = skin.weaponPrefab.name;
                skinWeapon.transform.localPosition = Vector3.zero;
                skinWeapon.transform.localRotation = Quaternion.identity;
                skinWeapon.transform.localScale = Vector3.one;
                skinWeapon.layer = slot.gameObject.layer;
                foreach (Transform t in skinWeapon.GetComponentsInChildren<Transform>(true))
                    t.gameObject.layer = slot.gameObject.layer;
            }
            else
            {
                if (hand != null)
                    hand.enabled = true;
                Apply(skin.weaponMaterial);
            }
            Color accent = skin.accentColor.a > 0f ? skin.accentColor : DefaultAccent;
            if (accent != CurrentAccent)
            {
                CurrentAccent = accent;
                AccentChanged?.Invoke(accent);
            }
        }

        // The built-in weapon in the hand (the first renderer under a socket).
        private Renderer HandWeapon()
        {
            if (weaponRenderers == null)
                return null;
            foreach (Renderer renderer in weaponRenderers)
                if (renderer != null)
                    return renderer;
            return null;
        }

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
