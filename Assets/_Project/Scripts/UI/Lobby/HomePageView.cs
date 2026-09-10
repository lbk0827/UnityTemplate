using System.Threading;
using Cysharp.Threading.Tasks;
using R3;
using UnityEngine;
using UnityEngine.UI;
using VContainer;
using BK.Localization;
using BK.UI;

namespace Project
{
    /// <summary>
    /// 홈 페이지. 현재 스테이지 버튼과 출석 보상 버튼.
    /// 인게임 씬이 아직 없으므로 스테이지 버튼은 하트를 쓰고 클리어를 시뮬레이션합니다.
    /// </summary>
    public sealed class HomePageView : ViewComponent
    {
        private const int StageEntryHeartCost = 1;
        private const long StageClearGold = 100;

        [SerializeField] private Button _stageButton;
        [SerializeField] private Text _stageButtonText;
        [SerializeField] private Text[] _nextStageTexts;
        [SerializeField] private Button _dailyRewardButton;
        [SerializeField] private Text _dailyRewardLabel;
        [SerializeField] private GameObject _dailyRedDot;
        [SerializeField] private Text _dailyRedDotCount;

        private IUIService _ui;
        private ILocalizationService _loc;
        private PlayerWallet _wallet;
        private PlayerProfile _profile;
        private DailyRewardState _daily;
        private MessageService _messages;

        [Inject]
        public void Construct(IUIService ui, ILocalizationService loc, PlayerWallet wallet,
            PlayerProfile profile, DailyRewardState daily, MessageService messages)
        {
            _ui = ui;
            _loc = loc;
            _wallet = wallet;
            _profile = profile;
            _daily = daily;
            _messages = messages;
        }

        public void Bind(ProjectViewBase owner)
        {
            ClearSubscriptions();

            _stageButton.onClick.AddListener(() => OnStageClicked(owner.destroyCancellationToken).Forget());
            _dailyRewardButton.onClick.AddListener(
                () => _ui.OpenAsync<DailyRewardsPopup>(DailyRewardsPopup.Address).Forget());

            _profile.CurrentStage
                .CombineLatest(_loc.CurrentLanguage, (stage, _) => stage)
                .Subscribe(ApplyStageNumbers)
                .AddTo(Disposables);

            _loc.CurrentLanguage
                .Subscribe(_ => _dailyRewardLabel.text = _loc.Get("lobby.daily"))
                .AddTo(Disposables);

            _daily.ClaimableCount.Subscribe(count =>
            {
                _dailyRedDot.SetActive(count > 0);
                _dailyRedDotCount.text = count.ToString();
            }).AddTo(Disposables);
        }

        private void ApplyStageNumbers(int stage)
        {
            _stageButtonText.text = stage.ToString();
            for (var i = 0; i < _nextStageTexts.Length; i++)
                _nextStageTexts[i].text = (stage + i + 1).ToString();
        }

        private async UniTask OnStageClicked(CancellationToken cancellationToken)
        {
            if (!_wallet.CanAfford(CurrencyId.Heart, StageEntryHeartCost))
            {
                await _ui.OpenAsync<RefillPopup>(RefillPopup.Address, cancellationToken);
                return;
            }

            _wallet.Add(CurrencyId.Heart, -StageEntryHeartCost);

            // 인게임 대체: 즉시 클리어 처리 후 보상 지급.
            var stage = _profile.CurrentStage.CurrentValue;
            _profile.AdvanceStage();
            _wallet.Add(CurrencyId.Gold, StageClearGold);

            await _messages.ShowAsync(
                _loc.Get("msg.stageclear.title"),
                _loc.Format("msg.stageclear.body", stage),
                cancellationToken);

            await RewardPopup.ShowAsync(_ui, new[] { new RewardItem(CurrencyId.Gold, StageClearGold) }, cancellationToken);
        }
    }
}
