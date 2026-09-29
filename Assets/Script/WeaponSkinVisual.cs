using UnityEngine;

namespace Deadline4Sec
{
    public sealed class WeaponSkinVisual : MonoBehaviour
    {
        [SerializeField] private Renderer[] weaponRenderers;

        public void Apply(Material material)
        {
            if (material == null || weaponRenderers == null)
                return;
            foreach (Renderer renderer in weaponRenderers)
                if (renderer != null)
                    renderer.sharedMaterial = material;
        }
    }
}
