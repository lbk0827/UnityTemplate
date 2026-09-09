using UnityEngine;
using UnityEngine.UI;

namespace Project
{
    /// <summary>아이콘 + 수량. 상점 카드, 보상 팝업, 출석 보상이 공유하는 최소 단위.</summary>
    public sealed class GoodsItemView : MonoBehaviour
    {
        [SerializeField] private Image _icon;
        [SerializeField] private Text _iconLabel;
        [SerializeField] private Text _countText;
        [SerializeField] private Sprite _goldSprite;
        [SerializeField] private Sprite _heartSprite;

        public void Bind(RewardItem item, string countFormat = "{0}")
        {
            var sprite = SpriteOf(item.ItemId);
            _icon.sprite = sprite;
            _icon.color = sprite != null ? Color.white : ItemVisuals.ColorOf(item.ItemId);
            _icon.preserveAspect = sprite != null;
            if (_iconLabel != null)
                _iconLabel.text = sprite != null ? string.Empty : ItemVisuals.ShortLabel(item.ItemId);
            _countText.text = string.Format(countFormat, ItemVisuals.Grouped(item.Count));
        }

        private Sprite SpriteOf(string itemId) => itemId switch
        {
            CurrencyId.Gold => _goldSprite,
            CurrencyId.Heart => _heartSprite,
            _ => null,
        };
    }
}
