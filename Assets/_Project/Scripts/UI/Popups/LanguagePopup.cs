using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using R3;
using UnityEngine;
using UnityEngine.UI;
using VContainer;

namespace Project
{
    /// <summary>언어 선택 팝업. 토글 하나가 언어 코드 하나입니다.</summary>
    public sealed class LanguagePopup : PopupViewBase
    {
        public const string Address = "UI/Popups/Language";

        [Serializable]
        private struct LanguageToggle
        {
            public string Code;
            public Toggle Toggle;
            public Text Label;
        }

        [SerializeField] private Text _title;
        [SerializeField] private LanguageToggle[] _toggles;

        private GameOptions _options;
        private bool _switching;

        [Inject]
        public void Construct(GameOptions options) => _options = options;

        public override UniTask OnInitializeAsync(CancellationToken cancellationToken)
        {
            foreach (var entry in _toggles)
            {
                var captured = entry;
                captured.Toggle.onValueChanged.AddListener(isOn =>
                {
                    if (isOn) SwitchAsync(captured.Code, destroyCancellationToken).Forget();
                });
            }

            Loc.CurrentLanguage.Subscribe(code =>
            {
                foreach (var entry in _toggles)
                    entry.Toggle.SetIsOnWithoutNotify(entry.Code == code);
            }).AddTo(Disposables);

            return base.OnInitializeAsync(cancellationToken);
        }

        private async UniTask SwitchAsync(string code, CancellationToken cancellationToken)
        {
            if (_switching || code == Loc.CurrentLanguage.CurrentValue)
                return;
            _switching = true;
            try
            {
                await Loc.SetLanguageAsync(code, cancellationToken);
                _options.Language.Value = code;
            }
            finally
            {
                _switching = false;
            }
        }

        protected override void ApplyTexts()
        {
            _title.text = Loc.Get("popup.language.title");
            foreach (var entry in _toggles)
                entry.Label.text = Loc.Get($"lang.{entry.Code}");
        }
    }
}
