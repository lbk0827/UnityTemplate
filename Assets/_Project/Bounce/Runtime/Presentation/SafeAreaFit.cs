using UnityEngine;

namespace BK.Kit
{
    /// <summary>
    /// Insets this rect to the device safe area. Defaults mirror the sf/RootBox HUD setup:
    /// only the top (notch) inset is applied; the bottom stays flush with the screen edge so
    /// the tab bar never floats, and the x-axis is ignored.
    /// </summary>
    public sealed class SafeAreaFit : MonoBehaviour
    {
        [SerializeField] private bool conformX = false;
        [SerializeField] private bool conformY = true;
        [SerializeField, Tooltip("Extend down to the screen bottom instead of stopping at the safe area.")]
        private bool keepBottom = true;

        private Rect previous;
        private Vector2Int previousScreen;

        private void OnEnable() => Fit();
        private void Update()
        {
            if (previous != Screen.safeArea || previousScreen.x != Screen.width || previousScreen.y != Screen.height)
                Fit();
        }

        private void Fit()
        {
            if (!(transform is RectTransform rect) || Screen.width == 0 || Screen.height == 0) return;
            previous = Screen.safeArea;
            previousScreen = new Vector2Int(Screen.width, Screen.height);

            var area = previous;
            if (keepBottom) { area.height += area.y; area.y = 0; }
            if (!conformX) { area.x = 0; area.width = Screen.width; }
            if (!conformY) { area.y = 0; area.height = Screen.height; }

            rect.anchorMin = area.min / new Vector2(Screen.width, Screen.height);
            rect.anchorMax = area.max / new Vector2(Screen.width, Screen.height);
        }
    }
}
