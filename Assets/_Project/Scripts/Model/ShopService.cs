using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using R3;
using UnityEngine;
using BK.Data;

namespace Project
{
    /// <summary>
    /// 상점 구매. 결제 SDK 가 없으므로 구매는 항상 성공으로 시뮬레이션하고,
    /// 상품에 담긴 보상을 지갑에 넣은 뒤 구매 횟수를 기록합니다.
    /// </summary>
    public sealed class ShopService
    {
        private readonly PlayerWallet _wallet;
        private readonly ITableService _tables;
        private readonly Subject<ShopProductRow> _purchased = new();

        public ShopService(PlayerWallet wallet, ITableService tables)
        {
            _wallet = wallet;
            _tables = tables;
        }

        public Observable<ShopProductRow> Purchased => _purchased;

        public ITable<int, ShopProductRow> Products => _tables.Get<int, ShopProductRow>();

        public int GetPurchaseCount(int productId) => PlayerPrefs.GetInt($"shop.count.{productId}", 0);

        public bool IsAvailable(ShopProductRow product)
            => product.PurchaseLimit <= 0 || GetPurchaseCount(product.Id) < product.PurchaseLimit;

        /// <summary>상품이 주는 보상 전체(메인 + 추가).</summary>
        public static List<RewardItem> RewardsOf(ShopProductRow product)
        {
            var list = new List<RewardItem> { product.MainReward };
            foreach (var extra in product.Extras)
                if (extra.IsValid)
                    list.Add(extra);
            return list;
        }

        public async UniTask<bool> PurchaseAsync(ShopProductRow product, CancellationToken cancellationToken)
        {
            if (!IsAvailable(product))
                return false;

            // 결제 창이 뜨는 시간을 흉내내는 짧은 지연.
            await UniTask.Delay(TimeSpan.FromSeconds(0.3f), cancellationToken: cancellationToken);

            foreach (var reward in RewardsOf(product))
                _wallet.Add(reward.ItemId, reward.Count);

            PlayerPrefs.SetInt($"shop.count.{product.Id}", GetPurchaseCount(product.Id) + 1);
            _purchased.OnNext(product);
            return true;
        }
    }
}
