using UnityEngine;

namespace Deadline4Sec
{
    // Replaces the obstacle's graybox cube look with a model stretched to the
    // exact hazard box, so what the player sees is what kills them. Without a
    // model, the cube gets the gothic stone material and a glowing danger edge.
    // An area can give the same hazard box another model (2026-11-06).
    public sealed class ObstacleVisual : MonoBehaviour
    {
        [SerializeField] private GameObject modelPrefab;
        [SerializeField] private Vector3 modelEuler;
        [SerializeField] private Material fallbackMaterial;
        [SerializeField, Range(0.5f, 1.2f)] private float fill = 1f;

        // Set by PatternSpawner around Instantiate, for the area being built.
        public static ObstacleSkinSet ActiveSkins;

        private void Awake()
        {
            BoxCollider box = GetComponent<BoxCollider>();
            MeshRenderer graybox = GetComponent<MeshRenderer>();
            if (box == null)
                return;
            ObstacleSkin skin = ActiveSkins != null && TryGetComponent(out Obstacle obstacle) ? ActiveSkins.For(obstacle.Type) : null;
            bool skinned = skin != null && skin.model != null;
            GameObject modelPrefab = skinned ? skin.model : this.modelPrefab;
            Vector3 modelEuler = skinned ? skin.euler : this.modelEuler;
            if (modelPrefab == null)
            {
                if (graybox != null && fallbackMaterial != null)
                    graybox.sharedMaterial = fallbackMaterial;
                return;
            }
            if (graybox != null)
                graybox.enabled = false;

            // Work in world space: the obstacle root carries a non-uniform scale.
            Vector3 size = Vector3.Scale(box.size, transform.lossyScale) * fill;
            Vector3 center = transform.TransformPoint(box.center);
            GameObject holder = new GameObject("Visual");
            holder.transform.SetParent(transform, true);
            holder.transform.SetPositionAndRotation(center, Quaternion.identity);
            holder.transform.localScale = new Vector3(1f / transform.lossyScale.x,
                1f / transform.lossyScale.y, 1f / transform.lossyScale.z);

            GameObject model = Instantiate(modelPrefab, holder.transform);
            model.transform.localRotation = Quaternion.Euler(modelEuler);
            foreach (Collider c in model.GetComponentsInChildren<Collider>(true))
                Destroy(c);
            Renderer[] renderers = model.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0)
                return;
            Bounds b = renderers[0].bounds;
            foreach (Renderer r in renderers)
                b.Encapsulate(r.bounds);
            Vector3 k = new Vector3(size.x / Mathf.Max(0.01f, b.size.x),
                size.y / Mathf.Max(0.01f, b.size.y), size.z / Mathf.Max(0.01f, b.size.z));
            Vector3 offset = model.transform.position - b.center;
            model.transform.localScale = Vector3.Scale(model.transform.localScale, k);
            model.transform.position = center + Vector3.Scale(offset, k);
        }
    }
}
