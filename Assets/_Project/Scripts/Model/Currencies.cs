using System;
using UnityEngine;

namespace Project
{
    /// <summary>재화 ID. 테이블/세이브/UI 전부 이 문자열로 재화를 가리킵니다.</summary>
    public static class CurrencyId
    {
        public const string Gold = "Gold";
        public const string Heart = "Heart";
    }

    /// <summary>보상 1건. 상점 패키지, 일일 보상, 보상 팝업이 공유합니다.</summary>
    [Serializable]
    public struct RewardItem
    {
        [SerializeField] private string _itemId;
        [SerializeField] private long _count;

        public string ItemId => _itemId;
        public long Count => _count;

        public RewardItem(string itemId, long count)
        {
            _itemId = itemId;
            _count = count;
        }

        public bool IsValid => !string.IsNullOrEmpty(_itemId) && _count > 0;
    }
}
