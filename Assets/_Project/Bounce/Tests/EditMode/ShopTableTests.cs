using System.Collections.Generic;
using System.Reflection;
using BK.Data;
using System;
using System.IO;
using System.Linq;
using BK.Kit;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
public sealed class ShopTableTests
{
        [System.Serializable] private sealed class ProductSource { public List<ShopProductRow> _rows; }
        [System.Serializable] private sealed class CurrencySource { public List<CurrencyDefinitionRow> _rows; }
    private ShopProductTable products;
    private CurrencyDefinitionTable currencies;
    private ShopCatalog catalog;
    [SetUp] public void Load()
    {
        products=ScriptableObject.CreateInstance<ShopProductTable>();currencies=ScriptableObject.CreateInstance<CurrencyDefinitionTable>();
        typeof(TableAsset<int,ShopProductRow>).GetField("_rows",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(products,JsonUtility.FromJson<ProductSource>(File.ReadAllText("Assets/_Project/Bounce/Content/Tables/ShopProductTable.json"))._rows);
        typeof(TableAsset<string,CurrencyDefinitionRow>).GetField("_rows",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(currencies,JsonUtility.FromJson<CurrencySource>(File.ReadAllText("Assets/_Project/Bounce/Content/Tables/CurrencyDefinitionTable.json"))._rows);
        catalog=new ShopCatalog(products,currencies);
    }
    [TearDown] public void Dispose(){UnityEngine.Object.DestroyImmediate(products);UnityEngine.Object.DestroyImmediate(currencies);}
    [Test] public void CurrentTableReplacesScreenshotValuesAndOrdersCurrencyRewards()
    {
        Assert.That(catalog.VisibleProducts().Select(p=>p.id),Is.EqualTo(new[]{7,9,10,11,13,14,2,3,4,5,6,12}));
        Assert.That(products.Get(10).rewards.First(r=>r.currencyId=="Gold").amount,Is.EqualTo(4000));
        Assert.That(catalog.Amount(products.Get(9).rewards.First(r=>r.currencyId=="InfiniteHeart")),Is.EqualTo("3h"));
        Array.Reverse(products.Get(9).rewards);
        Assert.That(catalog.Rewards(products.Get(9),2).Select(r=>r.currencyId),Is.EqualTo(new[]{"MissileIngame","ExtraballIngame","BombIngame","LaserIngame"}));
        Assert.That(catalog.BoosterPrice(BoosterKind.ExtraBall),Is.EqualTo(600));
        Assert.That(BoosterCatalog.All.Sum(o=>catalog.BoosterPrice(o.Kind)),Is.EqualTo(3000));
    }
    [Test] public void VisibilityHonorsHiddenLimitsUnsupportedConditionsAndPriority()
    {
        products.Get(9).purchaseLimit=1;
        Assert.That(catalog.VisibleProducts(id=>id==9?1:0).Any(p=>p.id==9),Is.False);
        Assert.That(catalog.VisibleProducts(id=>0).Any(p=>p.id==9),Is.True);
        products.Get(10).hidden=true;products.Get(11).conditionType="UnsupportedEvent";
        products.Get(13).categoryPriority=0;
        var rows=catalog.VisibleProducts().ToArray();
        Assert.That(rows.First().id,Is.EqualTo(13));
        Assert.That(rows.Any(p=>p.id==10 || p.id==11 || p.id==23 || p.id==26),Is.False);
    }
    [Test] public void UnknownRewardIsRejectedBeforeRendering()
    {
        products.Get(9).rewards[0].currencyId="UnknownCurrency";
        Assert.Throws<InvalidOperationException>(()=>new ShopCatalog(products,currencies));
    }
}
