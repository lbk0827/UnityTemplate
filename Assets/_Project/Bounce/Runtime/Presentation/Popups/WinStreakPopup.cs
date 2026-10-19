using System.Linq;
using BK.Meta;
using Cysharp.Threading.Tasks;
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
        private float gauge;

        public int Level => level;
        public Button PlayButton { get; private set; }
        public float GaugeFill => gauge;

        protected override Vector2 PanelSize => new(820, 1100);

        public void SetArgs(int args) => level = args;

        [Inject]
        public void Construct(IWinStreakCatalog catalog) => this.catalog = catalog;

        protected override void Build()
        {
            var streak = App.Streak;
            int cap = catalog.Cap <= 0 ? WinStreakLogic.DefaultCap : catalog.Cap;
            var ribbon = MakeImage(Panel, "Level ribbon", new Vector2(620, 168), new Vector2(0, 505), Skin("Header"), Color.white);
            Label(ribbon.transform, "Title", "Level " + level, new Vector2(470, 100), new Vector2(0, 10), 66);
            Label(Panel, "Streak", "Win streak", new Vector2(700, 80), new Vector2(0, 352), 58);
            Label(Panel, "Progress", streak.Current.CurrentValue + " / " + cap + " wins", new Vector2(640, 60), new Vector2(0, 279), 36, new Color(1, .88f, .47f));
            var track = MakeImage(Panel, "Gauge", new Vector2(650, 42), new Vector2(0, 215), Skin("Well"), new Color(.46f,.35f,.69f));
            track.pixelsPerUnitMultiplier = 4;
            gauge = streak.GaugeFill;
            var fill = MakeImage(track.transform, "Fill", new Vector2(638 * gauge, 30), new Vector2(6, 0), Skin("Primary"), Color.white);
            fill.pixelsPerUnitMultiplier = 176f / 30f;
            fill.rectTransform.anchorMin = fill.rectTransform.anchorMax = fill.rectTransform.pivot = new Vector2(0, .5f);
            fill.gameObject.SetActive(gauge > 0);
            var tiers = catalog.Tiers.OrderBy(t => t.Threshold).ToList();
            for (int i = 0; i < tiers.Count; i++)
            {
                bool reached = streak.Current.CurrentValue >= tiers[i].Threshold;
                float width = 660f / Mathf.Max(1, tiers.Count);
                var card = MakeImage(Panel, "Milestone " + tiers[i].Threshold, new Vector2(width - 14, 270), new Vector2((i - (tiers.Count - 1) * .5f) * width, 32), Skin("Well"), Color.white);
                card.pixelsPerUnitMultiplier = 1.5f;
                Label(card.transform, "Threshold", tiers[i].Threshold + " wins", new Vector2(width - 20, 50), new Vector2(0, 97), 32, new Color(.24f,.13f,.38f));
                var rewards = tiers[i].Rewards;
                for (int j = 0; j < rewards.Length; j++)
                {
                    var reward = rewards[j];
                    var sprite = BounceItems.TryGetBooster(reward, out var kind) ? CommonPopupView.BoosterIcon(presentation, kind) : presentation.shopSprites.FirstOrDefault(s => s.name == "IMG_Coin");
                    float iconSize = Mathf.Min(130, (width - 24) / Mathf.Max(1, rewards.Length));
                    var icon = MakeImage(card.transform, "Reward " + reward.itemId, Vector2.one * iconSize, new Vector2((j - (rewards.Length - 1) * .5f) * iconSize, 10), sprite, Color.white);
                    icon.preserveAspect = true;
                    Label(icon.transform, "Quantity", "x" + reward.amount, new Vector2(iconSize, 44), new Vector2(0, -iconSize / 2 - 18), 32, new Color(.24f,.13f,.38f));
                }
                Label(card.transform, "Status", reached ? "REACHED" : "BONUS", new Vector2(width - 24, 35), new Vector2(0, -108), 22, reached ? new Color(.14f,.47f,.17f) : new Color(.43f,.30f,.57f));
            }
            string pending = streak.Pending.Count > 0 ? "Next round bonus\n" + BounceItems.Describe(streak.Pending) : tiers.Count > 0 ? "Win " + tiers[0].Threshold + " levels in a row\nto start earning bonus boosters!" : "Keep winning to build your streak!";
            Label(Panel, "Pending", pending, new Vector2(670, 115), new Vector2(0, -205), 32);
            PlayButton = MakeButton(Panel, "Play", "Play", new Vector2(0, -389), new Vector2(470, 164), () => PlayAsync().Forget());
        }

        private async UniTask PlayAsync()
        {
            // Release the pre-play modal before showing the dedicated heart popup.
            await CloseAsync(); // fully released before the scene transition starts
            if (App != null) App.Play(level);
        }
    }
}
