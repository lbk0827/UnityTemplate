using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using BK.Data;
namespace BK.Kit
{
    // Presentation/economy data only. Cash products cannot grant inventory or initiate a payment.
    public sealed class ShopCatalog
    {
        public ITable<int,ShopProductRow> Products { get; }
        public ITable<string,CurrencyDefinitionRow> Currencies { get; }
        public ShopCatalog(ITable<int,ShopProductRow> products,ITable<string,CurrencyDefinitionRow> currencies)
        {
            Products=products;Currencies=currencies;
            var ids=new HashSet<int>();
            foreach(var p in products.Rows)
            {
                if(!ids.Add(p.id) || float.IsNaN(p.price) || float.IsInfinity(p.price) || p.price<0)
                    throw new InvalidOperationException("Invalid shop product: "+p.id);
                foreach(var reward in p.rewards)
                    if(reward.amount<=0 || !currencies.Contains(reward.currencyId))
                        throw new InvalidOperationException("Invalid reward on product "+p.id+": "+reward.currencyId);
            }
        }
        public IEnumerable<ShopProductRow> VisibleProducts(Func<int,int> purchaseCount=null)
        {
            return Products.Rows.Where(p=>!p.hidden && string.IsNullOrEmpty(p.conditionType)
                && (p.layout=="NoAds" || p.layout=="Bundle" || p.layout=="Coin")
                && (p.purchaseLimit<=0 || (purchaseCount?.Invoke(p.id)??0)<p.purchaseLimit))
                .OrderBy(p=>p.categoryPriority).ThenBy(p=>p.priority).ThenBy(p=>p.id);
        }
        public IEnumerable<ShopReward> Rewards(ShopProductRow product,int group)
            =>product.rewards.Where(r=>r.group==group).OrderBy(r=>Currencies.Get(r.currencyId).rewardPriority);
        public CurrencyDefinitionRow Booster(BoosterKind kind)
            =>Currencies.Rows.Single(r=>r.boosterKind==kind.ToString());
        public int BoosterPrice(BoosterKind kind)
        {
            var row=Booster(kind);
            if(row.purchaseCurrency!="Gold" || row.purchaseAmount<=0)throw new InvalidOperationException("Invalid booster cost: "+row.id);
            return row.purchaseAmount;
        }
        public string Amount(ShopReward reward)
        {
            if(!Currencies.Get(reward.currencyId).duration)return reward.amount.ToString("N0",CultureInfo.InvariantCulture);
            int seconds=reward.amount;
            if(seconds%3600==0)return (seconds/3600)+"h";
            if(seconds%60==0)return (seconds/60)+"m";
            return seconds+"s";
        }
        public string Describe(ShopProductRow product)=>string.Join("\n",product.rewards
            .OrderBy(r=>r.group).ThenBy(r=>Currencies.Get(r.currencyId).rewardPriority)
            .Select(r=>Currencies.Get(r.currencyId).name+": "+Amount(r)));
        public static string Price(ShopProductRow product)=>"$"+product.price.ToString("0.00",CultureInfo.InvariantCulture);
    }
}
