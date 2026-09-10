using UnityEngine;
namespace RogueDrive.UI
{
    [RequireComponent(typeof(RectTransform))]
    public sealed class SceneSafeArea : MonoBehaviour
    {
        private Rect previous;
        void Update()
        {
            Rect safe = Screen.safeArea;
            if (safe == previous || Screen.width == 0 || Screen.height == 0) return;
            previous = safe;
            var rect = (RectTransform)transform;
            rect.anchorMin = new Vector2(safe.xMin / Screen.width, safe.yMin / Screen.height);
            rect.anchorMax = new Vector2(safe.xMax / Screen.width, safe.yMax / Screen.height);
            rect.offsetMin = rect.offsetMax = Vector2.zero;
        }
    }
}
