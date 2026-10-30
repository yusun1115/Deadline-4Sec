using TMPro;
using UnityEngine;

namespace Deadline4Sec
{
    // Keeps an icon just left of a (right-aligned) number, however many digits
    // it has — the skin shop's Soul balance (2026-10-30). The icon's pivot
    // should be on its right edge.
    [RequireComponent(typeof(RectTransform))]
    public sealed class TextLeadingIcon : MonoBehaviour
    {
        [SerializeField] private TMP_Text text;
        [SerializeField, Min(0f)] private float gap = 8f;

        private string shown;

        private void LateUpdate()
        {
            if (text == null || text.text == shown)
                return;
            shown = text.text;
            text.ForceMeshUpdate();
            Bounds bounds = text.textBounds;
            if (bounds.size.x <= 0f)
                return;
            RectTransform rect = (RectTransform)transform;
            rect.position = text.rectTransform.TransformPoint(new Vector3(bounds.min.x, bounds.center.y, 0f));
            rect.anchoredPosition += Vector2.left * gap;
        }
    }
}
