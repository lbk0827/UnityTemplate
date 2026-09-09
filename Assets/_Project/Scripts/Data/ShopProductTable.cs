using System;
using UnityEngine;
using BK.Data;

namespace Project
{
    public enum ShopCategory
    {
        Coin,
        Bundle,
        SpecialOffer,
    }

    public enum ProductLabel
    {
        None,
        Popular,
        Best,
        Discount,
    }

    [Serializable]
    public struct ShopProductRow : ITableRow<int>
    {
        [SerializeField] private int _id;
        [SerializeField] private ShopCategory _category;
        [SerializeField] private int _categoryPriority;
        [SerializeField] private int _priority;
        [SerializeField] private ProductLabel _label;
        [SerializeField] private int _discountPercent;
        [SerializeField] private string _nameKey;
        [SerializeField] private string _descKey;
        [SerializeField] private float _price;
        [SerializeField, Tooltip("0 이면 무제한")] private int _purchaseLimit;
        [SerializeField] private RewardItem _mainReward;
        [SerializeField] private RewardItem[] _extras;

        public int Id => _id;
        public ShopCategory Category => _category;
        public int CategoryPriority => _categoryPriority;
        public int Priority => _priority;
        public ProductLabel Label => _label;
        public int DiscountPercent => _discountPercent;
        public string NameKey => _nameKey;
        public string DescKey => _descKey;
        public float Price => _price;
        public int PurchaseLimit => _purchaseLimit;
        public RewardItem MainReward => _mainReward;
        public RewardItem[] Extras => _extras ?? Array.Empty<RewardItem>();

        public ShopProductRow(int id, ShopCategory category, int categoryPriority, int priority,
            ProductLabel label, int discountPercent, string nameKey, string descKey, float price,
            int purchaseLimit, RewardItem mainReward, RewardItem[] extras)
        {
            _id = id;
            _category = category;
            _categoryPriority = categoryPriority;
            _priority = priority;
            _label = label;
            _discountPercent = discountPercent;
            _nameKey = nameKey;
            _descKey = descKey;
            _price = price;
            _purchaseLimit = purchaseLimit;
            _mainReward = mainReward;
            _extras = extras;
        }
    }

    [CreateAssetMenu(fileName = "ShopProductTable", menuName = "Project/Shop Product Table")]
    public sealed class ShopProductTable : TableAsset<int, ShopProductRow> { }
}
