using System;
using UnityEngine;

namespace Project
{
    /// <summary>코인 상품 카드. 아이콘 + 수량 + 가격.</summary>
    public sealed class CoinProductCell : ProductCellBase
    {
        [SerializeField] private GoodsItemView _mainItem;

        protected override void OnBind(ShopProductRow product, Func<string, string> localize)
            => _mainItem.Bind(product.MainReward);
    }
}
