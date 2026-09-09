using System;
using UnityEngine;
using UnityEngine.UI;

namespace Project
{
    /// <summary>번들/스페셜 오퍼 카드. 이름, 설명, 메인 보상, 추가 보상 슬롯, 라벨 배지.</summary>
    public sealed class BundleProductCell : ProductCellBase
    {
        [SerializeField] private Text _nameText;
        [SerializeField] private Text _descText;
        [SerializeField] private GoodsItemView _mainItem;
        [SerializeField] private GoodsItemView[] _extraSlots;
        [SerializeField] private GameObject _labelRoot;
        [SerializeField] private Text _labelText;

        protected override void OnBind(ShopProductRow product, Func<string, string> localize)
        {
            _nameText.text = localize(product.NameKey);
            _descText.text = localize(product.DescKey);
            _mainItem.Bind(product.MainReward);

            var extras = product.Extras;
            for (var i = 0; i < _extraSlots.Length; i++)
            {
                var has = i < extras.Length && extras[i].IsValid;
                _extraSlots[i].gameObject.SetActive(has);
                if (has)
                    _extraSlots[i].Bind(extras[i], "x{0}");
            }

            _labelRoot.SetActive(product.Label != ProductLabel.None);
            _labelText.text = product.Label switch
            {
                ProductLabel.Discount => $"{product.DiscountPercent}% OFF",
                ProductLabel.Popular => localize("store.label.popular"),
                ProductLabel.Best => localize("store.label.best"),
                _ => string.Empty,
            };
        }
    }
}
