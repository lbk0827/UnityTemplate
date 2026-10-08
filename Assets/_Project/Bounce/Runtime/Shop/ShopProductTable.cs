using System;
using BK.Data;
using UnityEngine;
namespace BK.Kit
{
    [Serializable] public sealed class ShopReward { public string currencyId; public int amount; public int group; }
    [Serializable] public sealed class ShopProductRow : ITableRow<int>
    {
        public int id, categoryPriority, priority, purchaseLimit;
        public string category, layout, name, description, icon, badge, conditionType;
        public bool hidden;
        public float price;
        public ShopReward[] rewards;
        public int Id => id;
    }
    [CreateAssetMenu(menuName="BK/Bounce/Shop Product Table")]
    public sealed class ShopProductTable : TableAsset<int,ShopProductRow> { }
}
