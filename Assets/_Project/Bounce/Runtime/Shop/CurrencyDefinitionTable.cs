using System;
using BK.Data;
using UnityEngine;
namespace BK.Kit
{
    [Serializable] public sealed class CurrencyDefinitionRow : ITableRow<string>
    {
        public string id, name, icon, boosterKind, purchaseCurrency;
        public int rewardPriority, purchaseAmount;
        public bool duration;
        public string Id => id;
    }
    [CreateAssetMenu(menuName="BK/Bounce/Currency Definition Table")]
    public sealed class CurrencyDefinitionTable : TableAsset<string,CurrencyDefinitionRow> { }
}
