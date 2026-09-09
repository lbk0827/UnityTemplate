using System;
using R3;
using UnityEngine;
using UnityEngine.UI;

namespace Project
{
    /// <summary>
    /// 재화 하나의 HUD 표시. 하트처럼 충전형이면 남은 시간과 FULL 표기를 함께 다룹니다.
    /// 클릭 동작은 부모 HUD 가 <see cref="Clicked"/> 로 결정합니다.
    /// </summary>
    public sealed class CurrencyHudView : ViewComponent
    {
        [SerializeField] private string _currencyId = CurrencyId.Gold;
        [SerializeField] private Image _icon;
        [SerializeField] private Text _iconLabel;
        [SerializeField] private Text _countText;
        [SerializeField, Tooltip("충전형일 때만 배선")] private Text _timerText;
        [SerializeField] private Button _button;
        [SerializeField] private Sprite _goldSprite;
        [SerializeField] private Sprite _heartSprite;

        public string Id => _currencyId;
        public event Action Clicked;

        private void Awake()
        {
            var sprite = SpriteOf(_currencyId);
            _icon.sprite = sprite;
            _icon.color = sprite != null ? Color.white : ItemVisuals.ColorOf(_currencyId);
            _icon.preserveAspect = sprite != null;
            if (_iconLabel != null)
                _iconLabel.text = sprite != null ? string.Empty : ItemVisuals.ShortLabel(_currencyId);
            if (_button != null)
                _button.onClick.AddListener(() => Clicked?.Invoke());
        }

        public void Bind(PlayerWallet wallet, Func<string> fullLabel)
        {
            ClearSubscriptions();

            switch (_currencyId)
            {
                case CurrencyId.Gold:
                    wallet.Gold.Subscribe(v => _countText.text = ItemVisuals.Grouped(v)).AddTo(Disposables);
                    break;

                case CurrencyId.Heart:
                    wallet.Heart.Subscribe(v => _countText.text = $"{v}/{PlayerWallet.HeartMax}").AddTo(Disposables);
                    if (_timerText != null)
                        wallet.HeartRechargeRemainingSeconds
                            .Subscribe(s => _timerText.text = s <= 0 ? fullLabel() : ItemVisuals.Countdown(s))
                            .AddTo(Disposables);
                    break;
            }
        }

        private Sprite SpriteOf(string itemId) => itemId switch
        {
            CurrencyId.Gold => _goldSprite,
            CurrencyId.Heart => _heartSprite,
            _ => null,
        };
    }
}
