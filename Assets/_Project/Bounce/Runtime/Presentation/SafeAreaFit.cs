using UnityEngine;

namespace BK.Kit
{
    /// <summary>
    /// Insets this rect to the device safe area. By default no inset is applied at all, so the
    /// lobby reaches every screen edge (requested: no top/bottom padding). Turn on conformY to
    /// keep content below a notch; keepBottom then still keeps the bottom flush like the sf HUD.
    /// </summary>
    public sealed class SafeAreaFit : MonoBehaviour
    {
        [SerializeField] private bool conformX = false;
        [SerializeField] private bool conformY = false;
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
