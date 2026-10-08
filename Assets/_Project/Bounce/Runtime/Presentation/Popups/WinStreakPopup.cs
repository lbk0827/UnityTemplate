using System.Linq;
using BK.Meta;
using BK.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using VContainer;

namespace BK.Kit
{
    /// <summary>
    /// sf pre-play popup: opened by the lobby Play button. Shows the level, the win-streak gauge with its
    /// milestones and the boosters the streak hands to the next round, then starts the stage through KitApp.Play.
    /// </summary>
    public sealed class WinStreakPopup : BouncePopupView, IUIView<int>
    {
        public const string Address = "Bounce/UI/WinStreakPopup";

        private int level;
        private IWinStreakCatalog catalog;
        private Image fill;
        private float gauge;

        public int Level => level;
        public Button PlayButton { get; private set; }
        public float GaugeFill => gauge;

        protected override Vector2 PanelSize => new(820, 1120);

        public void SetArgs(int args) => level = args;

        [Inject]
        public void Construct(IWinStreakCatalog catalog) => this.catalog = catalog;

        protected override void Build()
        {
            float top = PanelSize.y / 2;
            var streak = App.Streak;
            int cap = catalog.Cap <= 0 ? WinStreakLogic.DefaultCap : catalog.Cap;
            Label(Panel, "Title", "Level " + level, new Vector2(700, 90), new Vector2(0, top - 80), 50);
            Label(Panel, "Streak", "Win streak " + streak.Current.CurrentValue + " / " + cap, new Vector2(700, 60), new Vector2(0, top - 180), 34);
            var track = MakeImage(Panel, "Gauge", new Vector2(640, 44), new Vector2(0, top - 250), null, new Color(.12f, .1f, .35f));
            // A plain (sprite-less) Image ignores fillAmount, so the bar is sized instead of filled.
            gauge = streak.GaugeFill;
            fill = MakeImage(track.transform, "Fill", new Vector2(640 * gauge, 36), new Vector2(4, 0), null, new Color(1f, .8f, .2f));
            fill.rectTransform.anchorMin = new Vector2(0, .5f); fill.rectTransform.anchorMax = new Vector2(0, .5f); fill.rectTransform.pivot = new Vector2(0, .5f);
            var tiers = catalog.Tiers.OrderBy(t => t.Threshold).ToList();
            for (int i = 0; i < tiers.Count; i++)
            {
                bool reached = streak.Current.CurrentValue >= tiers[i].Threshold;
                Label(Panel, "Milestone " + tiers[i].Threshold,
                    tiers[i].Threshold + " wins: " + BounceItems.Describe(tiers[i].Rewards) + (reached ? "  (reached)" : ""),
                    new Vector2(700, 50), new Vector2(0, top - 330 - i * 56), 28, reached ? new Color(1f, .9f, .4f) : Color.white);
            }
            string pending = streak.Pending.Count > 0 ? "Next round bonus: " + BounceItems.Describe(streak.Pending) : "Win " + tiers[0].Threshold + " in a row for a bonus booster";
            Label(Panel, "Pending", pending, new Vector2(700, 70), new Vector2(0, top - 560), 30);
            PlayButton = MakeButton(Panel, "Play", "Play", new Vector2(0, top - 720), new Vector2(420, 120), () =>
            {
                // Without a heart Play only shows the "No hearts" message; keep the popup up behind it.
                if (App.CanEnter(level)) RequestClose();
                App.Play(level);
            });
        }
    }
}
