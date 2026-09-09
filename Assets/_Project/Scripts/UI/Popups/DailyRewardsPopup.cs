using System.Threading;
using Cysharp.Threading.Tasks;
using R3;
using UnityEngine;
using UnityEngine.UI;
using VContainer;
using BK.Data;

namespace Project
{
    /// <summary>출석 보상 팝업. 7일 셀과 1시간 주기 무료 코인.</summary>
    public sealed class DailyRewardsPopup : PopupViewBase
    {
        public const string Address = "UI/Popups/DailyRewards";

        [SerializeField] private Text _title;
        [SerializeField] private DailyRewardCell[] _dayCells;
        [SerializeField] private GoodsItemView _freeItem;
        [SerializeField] private Button _freeClaimButton;
        [SerializeField] private Text _freeClaimLabel;
        [SerializeField] private Text _freeTitle;
        [SerializeField] private Text _freeWaitText;

        private DailyRewardState _daily;
        private PlayerWallet _wallet;
        private ITableService _tables;
        private bool _claiming;

        [Inject]
        public void Construct(DailyRewardState daily, PlayerWallet wallet, ITableService tables)
        {
            _daily = daily;
            _wallet = wallet;
            _tables = tables;
        }

        public override UniTask OnInitializeAsync(CancellationToken cancellationToken)
        {
            var table = _tables.Get<int, DailyRewardRow>();

            for (var i = 0; i < _dayCells.Length; i++)
            {
                var row = table.Get(i + 1);
                _dayCells[i].Bind(i, row, Loc.Format("popup.daily.day", i + 1));
                _dayCells[i].ClaimClicked += day => ClaimDailyAsync(day, destroyCancellationToken).Forget();
            }

            _freeItem.Bind(table.Get(_daily.TodayDayId).FreeReward, "x{0}");
            _freeClaimButton.onClick.AddListener(() => ClaimFreeAsync(destroyCancellationToken).Forget());

            _daily.NextRewardDay.AsUnitObservable()
                .Merge(_daily.ClaimedToday.AsUnitObservable())
                .Subscribe(_ => RefreshDayCells())
                .AddTo(Disposables);

            _daily.FreeCoinAvailable.Subscribe(available =>
            {
                _freeClaimButton.gameObject.SetActive(available);
                _freeWaitText.gameObject.SetActive(!available);
            }).AddTo(Disposables);

            _daily.FreeCoinRemainingSeconds
                .Subscribe(s => _freeWaitText.text = Loc.Format("popup.daily.next", ItemVisuals.Countdown(s)))
                .AddTo(Disposables);

            return base.OnInitializeAsync(cancellationToken);
        }

        private void RefreshDayCells()
        {
            for (var i = 0; i < _dayCells.Length; i++)
            {
                var state = _daily.CanClaimDaily(i) ? DailyRewardCellState.Claimable
                    : _daily.IsDailyReceived(i) ? DailyRewardCellState.Received
                    : DailyRewardCellState.Locked;
                _dayCells[i].SetState(state, Loc.Get("popup.daily.claim"));
            }
        }

        private async UniTask ClaimDailyAsync(int dayIndex, CancellationToken cancellationToken)
        {
            if (_claiming || !_daily.TryClaimDaily(dayIndex))
                return;
            _claiming = true;
            try
            {
                var rewards = _tables.Get<int, DailyRewardRow>().Get(dayIndex + 1).Rewards;
                foreach (var reward in rewards)
                    _wallet.Add(reward.ItemId, reward.Count);
                await RewardPopup.ShowAsync(UI, rewards, cancellationToken);
            }
            finally
            {
                _claiming = false;
            }
        }

        private async UniTask ClaimFreeAsync(CancellationToken cancellationToken)
        {
            if (_claiming)
                return;
            var reward = _tables.Get<int, DailyRewardRow>().Get(_daily.TodayDayId).FreeReward;
            if (!_daily.TryClaimFreeCoin())
                return;
            _claiming = true;
            try
            {
                _wallet.Add(reward.ItemId, reward.Count);
                await RewardPopup.ShowAsync(UI, new[] { reward }, cancellationToken);
            }
            finally
            {
                _claiming = false;
            }
        }

        protected override void ApplyTexts()
        {
            _title.text = Loc.Get("popup.daily.title");
            _freeTitle.text = Loc.Get("popup.daily.free");
            _freeClaimLabel.text = Loc.Get("popup.daily.claim");
            for (var i = 0; i < _dayCells.Length; i++)
                _dayCells[i].Bind(i, _tables.Get<int, DailyRewardRow>().Get(i + 1), Loc.Format("popup.daily.day", i + 1));
            RefreshDayCells();
        }
    }
}
