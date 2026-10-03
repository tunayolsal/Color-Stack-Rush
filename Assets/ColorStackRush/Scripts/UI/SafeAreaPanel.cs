using UnityEngine;
namespace ColorStackRush
{
    public class SafeAreaPanel : MonoBehaviour
    {
        Rect last;
        Vector2 resolution;
        void OnEnable() => Apply();
        void Update() { if (last != Screen.safeArea || resolution.x != Screen.width || resolution.y != Screen.height) Apply(); }
        void Apply()
        {
            last = Screen.safeArea;
            resolution = new Vector2(Mathf.Max(1, Screen.width), Mathf.Max(1, Screen.height));
            var rt = (RectTransform)transform;
            rt.anchorMin = last.position / resolution;
            rt.anchorMax = (last.position + last.size) / resolution;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
        }
    }
}
