using System.Threading;
using Cysharp.Threading.Tasks;
using R3;
using UnityEngine;
using UnityEngine.UI;
using VContainer;
using BK.Data;

namespace Project
{
    /// <summary>
    /// 로비 HUD. 상단에 프로필/재화/설정, 하단에 탭. 로비 패널과는 LobbyState 로만 대화합니다.
    /// 상점 탭에서는 프로필·하트·설정을 숨겨 상점 화면을 넓게 씁니다.
    /// </summary>
    public sealed class HudView : ProjectViewBase
    {
        public const string Address = "UI/Lobby/Hud";

        [SerializeField] private Button _optionButton;
        [SerializeField] private Button _profileButton;
        [SerializeField] private ProfileBadgeView _profileBadge;
        [SerializeField] private Text _nicknameText;
        [SerializeField] private CurrencyHudView _gold;
        [SerializeField] private CurrencyHudView _heart;
        [SerializeField] private LobbyTabButton[] _tabs;
        [SerializeField] private bool _hideTopButtonsOnStoreTab = true;

        private LobbyState _lobby;
        private PlayerWallet _wallet;
        private PlayerProfile _profile;
        private ITableService _tables;

        [Inject]
        public void Construct(LobbyState lobby, PlayerWallet wallet, PlayerProfile profile, ITableService tables)
        {
            _lobby = lobby;
            _wallet = wallet;
            _profile = profile;
            _tables = tables;
        }

        public override UniTask OnInitializeAsync(CancellationToken cancellationToken)
        {
            _optionButton.onClick.AddListener(() => UI.OpenAsync<OptionPopup>(OptionPopup.Address).Forget());
            _profileButton.onClick.AddListener(() => UI.OpenAsync<ProfilePopup>(ProfilePopup.Address).Forget());

            _profileBadge.Bind(_profile, _tables);
            _profile.Nickname.Subscribe(n => _nicknameText.text = n).AddTo(Disposables);

            _gold.Bind(_wallet, () => Loc.Get("hud.full"));
            _heart.Bind(_wallet, () => Loc.Get("hud.full"));
            _gold.Clicked += () => _lobby.CurrentTab.Value = LobbyTab.Store;
            _heart.Clicked += OnHeartClicked;

            foreach (var tab in _tabs)
            {
                var captured = tab;
                captured.Toggle.onValueChanged.AddListener(isOn =>
                {
                    if (isOn) _lobby.CurrentTab.Value = captured.Tab;
                });
            }

            _lobby.CurrentTab.Subscribe(OnTabChanged).AddTo(Disposables);
            return base.OnInitializeAsync(cancellationToken);
        }

        protected override void ApplyTexts()
        {
            foreach (var tab in _tabs)
            {
                var key = tab.Tab switch
                {
                    LobbyTab.Home => "hud.tab.home",
                    LobbyTab.Lock => "hud.tab.lock",
                    _ => "hud.tab.store",
                };
                tab.SetLabel(Loc.Get(key));
            }
        }

        private void OnTabChanged(LobbyTab tab)
        {
            foreach (var button in _tabs)
                if (button.Tab == tab && !button.Toggle.isOn)
                    button.Toggle.isOn = true;

            if (!_hideTopButtonsOnStoreTab)
                return;

            var visible = tab != LobbyTab.Store;
            _profileButton.gameObject.SetActive(visible);
            _optionButton.gameObject.SetActive(visible);
            _heart.gameObject.SetActive(visible);
        }

        private void OnHeartClicked()
        {
            if (_wallet.IsHeartFull)
                return;
            UI.OpenAsync<RefillPopup>(RefillPopup.Address).Forget();
        }
    }
}
