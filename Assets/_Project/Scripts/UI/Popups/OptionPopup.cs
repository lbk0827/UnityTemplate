using System.Threading;
using Cysharp.Threading.Tasks;
using R3;
using UnityEngine;
using UnityEngine.UI;
using VContainer;

namespace Project
{
    /// <summary>설정 팝업. 음악/효과음/진동 토글, 언어 선택, 개인정보 링크.</summary>
    public sealed class OptionPopup : PopupViewBase
    {
        public const string Address = "UI/Popups/Option";
        private const string PrivacyUrl = "https://example.com/privacy";

        [SerializeField] private Text _title;
        [SerializeField] private Toggle _music;
        [SerializeField] private Text _musicLabel;
        [SerializeField] private Toggle _sfx;
        [SerializeField] private Text _sfxLabel;
        [SerializeField] private Toggle _haptic;
        [SerializeField] private Text _hapticLabel;
        [SerializeField] private Button _languageButton;
        [SerializeField] private Text _languageLabel;
        [SerializeField] private Button _privacyButton;
        [SerializeField] private Text _privacyLabel;
        [SerializeField] private Text _versionText;

        private GameOptions _options;

        [Inject]
        public void Construct(GameOptions options) => _options = options;

        public override UniTask OnInitializeAsync(CancellationToken cancellationToken)
        {
            BindToggle(_music, _options.Music);
            BindToggle(_sfx, _options.Sfx);
            BindToggle(_haptic, _options.Haptic);

            _languageButton.onClick.AddListener(() => UI.OpenAsync<LanguagePopup>(LanguagePopup.Address).Forget());
            _privacyButton.onClick.AddListener(() => Application.OpenURL(PrivacyUrl));
            _versionText.text = $"v{Application.version}";
            return base.OnInitializeAsync(cancellationToken);
        }

        /// <summary>토글과 설정 값의 양방향 동기화. 같은 값 재대입은 R3 가 걸러 줍니다.</summary>
        private void BindToggle(Toggle toggle, ReactiveProperty<bool> property)
        {
            toggle.SetIsOnWithoutNotify(property.Value);
            property.Subscribe(v => toggle.SetIsOnWithoutNotify(v)).AddTo(Disposables);
            toggle.onValueChanged.AddListener(v => property.Value = v);
        }

        protected override void ApplyTexts()
        {
            _title.text = Loc.Get("popup.option.title");
            _musicLabel.text = Loc.Get("option.music");
            _sfxLabel.text = Loc.Get("option.sfx");
            _hapticLabel.text = Loc.Get("option.haptic");
            _languageLabel.text = Loc.Get("option.language");
            _privacyLabel.text = Loc.Get("option.privacy");
        }
    }
}
