using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using BK.Meta;
using Cysharp.Threading.Tasks;
using R3;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using VContainer;

namespace BK.Kit
{
    /// <summary>
    /// sf template daily rewards: a seven-day cycle, three daily bonus slots and an hourly gift.
    /// State and granting live in BK.Meta.DailyRewards; this view only renders and routes claims through KitApp.
    /// </summary>
    public sealed class DailyRewardPopup : BouncePopupView
    {
        public const string Address = "Bounce/UI/DailyRewardPopup";

        private sealed class DayCell { public Image background; public Button claim; public TMP_Text state; }
        private readonly List<DayCell> days = new();
        private readonly List<Button> bonus = new();
        private Button hourly;
        private IDailyRewardCatalog catalog;
        private IDisposable subscription;

        protected override Vector2 PanelSize => new(840, 1340);
        public Button DayButton(int index) => days[index].claim;
        public Button BonusButton(int index) => bonus[index];
        public Button HourlyButton => hourly;

        [Inject]
        public void Construct(IDailyRewardCatalog catalog) => this.catalog = catalog;

        protected override void Build()
        {
            float top = PanelSize.y / 2;
            Label(Panel, "Title", "Daily Rewards", new Vector2(700, 90), new Vector2(0, top - 80), 46);
            for (int i = 0; i < catalog.Days.Count; i++)
            {
                int row = i / 4, column = i % 4;
                var cell = new DayCell();
                cell.background = MakeImage(Panel, "Day " + (i + 1), new Vector2(190, 230), new Vector2((column - 1.5f) * 200, top - 290 - row * 250), null, Color.white);
                Label(cell.background.transform, "Day", "Day " + (i + 1), new Vector2(170, 44), new Vector2(0, 85), 28, new Color(.1f, .1f, .25f));
                Label(cell.background.transform, "Reward", BounceItems.Describe(catalog.Days[i]), new Vector2(170, 90), new Vector2(0, 15), 24, new Color(.1f, .1f, .25f));
                int day = i;
                cell.claim = MakeButton(cell.background.transform, "Claim", "Claim", new Vector2(0, -75), new Vector2(160, 64), () => Claim(App.TryClaimDaily));
                cell.state = Label(cell.background.transform, "State", "", new Vector2(170, 44), new Vector2(0, -75), 26, new Color(.3f, .3f, .4f));
                days.Add(cell);
            }
            float bonusTop = top - 290 - 2 * 250 + 60;
            Label(Panel, "Bonus title", "Daily bonus", new Vector2(700, 60), new Vector2(0, bonusTop), 34);
            for (int i = 0; i < catalog.BonusSlots.Count; i++)
            {
                int slot = i;
                bonus.Add(MakeButton(Panel, "Bonus " + (i + 1), BounceItems.Describe(catalog.BonusSlots[i]), new Vector2((i - (catalog.BonusSlots.Count - 1) * .5f) * 250, bonusTop - 80),
                    new Vector2(230, 80), () => Claim((out string message) => App.TryClaimDailyBonus(slot, out message))));
            }
            Label(Panel, "Hourly title", "Hourly gift", new Vector2(700, 60), new Vector2(0, bonusTop - 190), 34);
            hourly = MakeButton(Panel, "Hourly", "", new Vector2(0, bonusTop - 270), new Vector2(420, 80), () => Claim(App.TryClaimHourly));
        }

        public override UniTask OnInitializeAsync(CancellationToken cancellationToken)
        {
            var task = base.OnInitializeAsync(cancellationToken);
            subscription = App.Daily.Changed.Subscribe(_ => Refresh());
            StartCoroutine(Tick());
            Refresh();
            return task;
        }

        private delegate bool ClaimFunc(out string message);
        private void Claim(ClaimFunc claim)
        {
            claim(out var message);
            Feedback.text = message;
            Refresh();
        }

        private void Refresh()
        {
            if (App == null) return;
            var daily = App.Daily;
            for (int i = 0; i < days.Count; i++)
            {
                bool claimable = daily.NextDay == i && daily.CanClaimDaily;
                bool received = i < daily.NextDay;
                days[i].claim.gameObject.SetActive(claimable);
                days[i].state.gameObject.SetActive(!claimable);
                days[i].state.text = received ? "Done" : "Locked";
                days[i].background.color = claimable ? Color.white : received ? new Color(.75f, .95f, .8f) : new Color(.8f, .8f, .9f);
            }
            for (int i = 0; i < bonus.Count; i++)
            {
                bonus[i].interactable = daily.CanClaimBonus(i);
                LabelOf(bonus[i]).text = i < daily.BonusClaimed ? "Done" : BounceItems.Describe(catalog.BonusSlots[i]);
            }
            RefreshHourly();
        }

        private void RefreshHourly()
        {
            if (App == null || hourly == null) return;
            bool ready = App.Daily.CanClaimHourly;
            hourly.interactable = ready;
            LabelOf(hourly).text = ready ? "Collect " + BounceItems.Describe(catalog.Hourly) : "Next in " + KitApp.FormatTimer(App.Daily.TimeToHourly);
        }

        private IEnumerator Tick()
        {
            var wait = new WaitForSecondsRealtime(.5f);
            while (true) { yield return wait; RefreshHourly(); }
        }

        private void OnDestroy() => subscription?.Dispose();
    }
}
