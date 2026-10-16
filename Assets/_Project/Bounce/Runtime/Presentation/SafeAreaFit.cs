using UnityEngine;

namespace BK.Kit
{
    /// <summary>
    /// Insets this rect to the device safe area. By default no inset is applied at all, so the
    /// lobby reaches every screen edge (requested: no top/bottom padding). Turn on conformY to
    /// keep content below a notch; keepBottom then still keeps the bottom flush like the sf HUD.
    /// Backgrounds listed in bleedTop/bleedBottom are stretched back out over the inset so they
    /// still reach the screen edge while the content around them stays inside the safe area.
    /// </summary>
    public sealed class SafeAreaFit : MonoBehaviour
    {
        [SerializeField] private bool conformX = false;
        [SerializeField] private bool conformY = false;
        [SerializeField, Tooltip("Extend down to the screen bottom instead of stopping at the safe area.")]
        private bool keepBottom = true;
        [SerializeField, Tooltip("Backgrounds whose top edge keeps reaching the screen top.")]
        private RectTransform[] bleedTop = { };
        [SerializeField, Tooltip("Backgrounds whose bottom edge keeps reaching the screen bottom.")]
        private RectTransform[] bleedBottom = { };

        private Rect previous;
        private Vector2Int previousScreen;
        private float previousParentHeight;
        private Vector2 bled; // (bottom, top) inset already added to the bleed targets

        public void Configure(bool conformY, bool keepBottom, params RectTransform[] bleedTop)
        {
            this.conformY = conformY; this.keepBottom = keepBottom; this.bleedTop = bleedTop;
            Fit();
        }

        private void OnEnable() => Fit();
        private void Update()
        {
            if (previous != Screen.safeArea || previousScreen.x != Screen.width || previousScreen.y != Screen.height
                || (transform.parent is RectTransform parent && parent.rect.height != previousParentHeight))
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

            if (!(rect.parent is RectTransform parent)) return;
            previousParentHeight = parent.rect.height;
            var inset = new Vector2(rect.anchorMin.y, 1 - rect.anchorMax.y) * previousParentHeight;
            foreach (var target in bleedBottom) if (target != null) target.offsetMin -= new Vector2(0, inset.x - bled.x);
            foreach (var target in bleedTop) if (target != null) target.offsetMax += new Vector2(0, inset.y - bled.y);
            bled = inset;
        }
    }
}
