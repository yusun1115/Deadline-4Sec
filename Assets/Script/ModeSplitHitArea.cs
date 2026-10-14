using UnityEngine;
using UnityEngine.UI;

namespace Deadline4Sec
{
    // Limits taps on a full-screen art to its side of the diagonal split, so the
    // mode-select popup's Deadline art (top-right) lets taps on the left fall
    // through to the Classic art below it. The split line runs from topX at the
    // top edge to bottomX at the bottom edge, both normalized across the rect.
    [RequireComponent(typeof(RectTransform))]
    public sealed class ModeSplitHitArea : MonoBehaviour, ICanvasRaycastFilter
    {
        [SerializeField, Range(0f, 1f)] private float topX = 0.02f;
        [SerializeField, Range(0f, 1f)] private float bottomX = 0.96f;
        [SerializeField] private bool rightSide = true;

        public bool IsRaycastLocationValid(Vector2 screenPoint, Camera eventCamera)
        {
            RectTransform rect = (RectTransform)transform;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(rect, screenPoint, eventCamera, out Vector2 local))
                return false;
            Rect r = rect.rect;
            float u = Mathf.InverseLerp(r.xMin, r.xMax, local.x);
            float v = Mathf.InverseLerp(r.yMin, r.yMax, local.y);
            bool right = u > Mathf.Lerp(bottomX, topX, v);
            return right == rightSide;
        }
    }
}
