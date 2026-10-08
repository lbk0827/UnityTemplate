using UnityEngine;

namespace BK.Kit
{
    public sealed class SafeAreaFit : MonoBehaviour
    {
        private Rect previous;
        private void OnEnable() => Fit();
        private void Update() { if (previous != Screen.safeArea) Fit(); }
        private void Fit()
        {
            if (!(transform is RectTransform rect) || Screen.width == 0 || Screen.height == 0) return;
            previous = Screen.safeArea;
            rect.anchorMin = previous.min / new Vector2(Screen.width, Screen.height);
            rect.anchorMax = previous.max / new Vector2(Screen.width, Screen.height);
        }
    }
}
