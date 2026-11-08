using UnityEngine;

namespace Deadline4Sec
{
    // The tunnel between two areas. Its walls and ceiling hide everything around
    // the track and its portal hides the course ahead, so the world can change
    // unseen once the camera is inside (AreaDirector). After that the portal has
    // nothing left to hide: it opens well before the runner gets there, so the
    // next area and its first hazards can be read in time. Visual only: the
    // portal and walls have no colliders.
    public sealed class AreaTransitionSection : MonoBehaviour
    {
        private static readonly int TintId = Shader.PropertyToID("_Tint");
        private static readonly int OpenId = Shader.PropertyToID("_Open");

        [SerializeField] private Renderer[] portalRenderers;
        [Tooltip("The world changes when the camera is this far inside (m from the pattern start).")]
        [SerializeField, Min(0f)] private float switchAfter = 6f;
        [Tooltip("The portal starts opening with the camera here (m from the pattern start, after the switch).")]
        [SerializeField, Min(0f)] private float revealFrom = 8f;
        [Tooltip("The portal is fully open with the camera here (m from the pattern start).")]
        [SerializeField, Min(0f)] private float revealTo = 18f;
        [Tooltip("The area name shows when the camera has passed this point (m from the pattern start).")]
        [SerializeField, Min(0f)] private float exitAt = 40f;

        private MaterialPropertyBlock block;
        private Color tint = Color.white;

        public float SwitchZ => transform.position.z + switchAfter;
        public float RevealEndZ => transform.position.z + revealTo;
        public float ExitZ => transform.position.z + exitAt;
        public float OpenAmount { get; private set; }

        public void SetPortalColor(Color color)
        {
            tint = color;
            Apply();
        }

        // Follows the camera through the tunnel (only after the world has changed).
        public void RevealFor(float viewerZ)
        {
            float open = Mathf.InverseLerp(transform.position.z + revealFrom, RevealEndZ, viewerZ);
            if (open <= OpenAmount)
                return;
            OpenAmount = open;
            Apply();
        }

        private void Apply()
        {
            if (portalRenderers == null)
                return;
            block ??= new MaterialPropertyBlock();
            block.SetColor(TintId, tint);
            block.SetFloat(OpenId, OpenAmount);
            foreach (Renderer portal in portalRenderers)
                if (portal != null)
                {
                    portal.SetPropertyBlock(block);
                    portal.enabled = OpenAmount < 1f;
                }
        }
    }
}
