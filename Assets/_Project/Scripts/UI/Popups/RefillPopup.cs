using System.Threading;
using Cysharp.Threading.Tasks;
using R3;
using UnityEngine;
using UnityEngine.UI;
using VContainer;

namespace Project
{
    /// <summary>
    /// 하트 리필 팝업. 부족한 만큼을 골드로 채웁니다. 골드가 모자라면 팝업을 닫고 상점 탭으로 보냅니다.
    /// 하트가 가득 차면 스스로 닫힙니다.
    /// </summary>
    public sealed class RefillPopup : PopupViewBase
    {
        public const string Address = "UI/Popups/Refill";
        private const long GoldPerHeart = 100;

        [SerializeField] private Text _title;
        [SerializeField] private Text _desc;
        [SerializeField] private Image[] _heartIcons;
        [SerializeField] private Text _refillCountText;
        [SerializeField] private Text _priceText;
        [SerializeField] private Button _buyButton;
        [SerializeField] private Text _buyLabel;

        private PlayerWallet _wallet;
        private LobbyState _lobby;

        [Inject]
        public void Construct(PlayerWallet wallet, LobbyState lobby)
        {
            _wallet = wallet;
            _lobby = lobby;
        }

        public override UniTask OnInitializeAsync(CancellationToken cancellationToken)
        {
            _wallet.Heart.Subscribe(Refresh).AddTo(Disposables);
            _buyButton.onClick.AddListener(OnBuy);
            return base.OnInitializeAsync(cancellationToken);
        }

        private void Refresh(long heart)
        {
            if (heart >= PlayerWallet.HeartMax)
            {
                if (IsOpen) RequestClose();
                return;
            }

            for (var i = 0; i < _heartIcons.Length; i++)
                _heartIcons[i].color = i < heart ? ItemVisuals.ColorOf(CurrencyId.Heart) : new Color(0.3f, 0.3f, 0.35f);

            var missing = PlayerWallet.HeartMax - heart;
            _refillCountText.text = $"+{missing}";
            _priceText.text = ItemVisuals.Grouped(missing * GoldPerHeart);
        }

        private void OnBuy()
        {
            var missing = PlayerWallet.HeartMax - _wallet.Heart.CurrentValue;
            var price = missing * GoldPerHeart;

            if (_wallet.CanAfford(CurrencyId.Gold, price))
            {
                _wallet.Add(CurrencyId.Gold, -price);
                _wallet.Add(CurrencyId.Heart, missing);
                return;
            }

            // 원본 로비 리필과 같은 흐름: 열린 팝업을 정리하고 상점 탭으로.
            UI.CloseAllAsync(BK.UI.UILayer.Popup).Forget();
            _lobby.CurrentTab.Value = LobbyTab.Store;
        }

        protected override void ApplyTexts()
        {
            _title.text = Loc.Get("popup.refill.title");
            _desc.text = Loc.Get("popup.refill.desc");
            _buyLabel.text = Loc.Get("popup.refill.buy");
        }
    }
}
