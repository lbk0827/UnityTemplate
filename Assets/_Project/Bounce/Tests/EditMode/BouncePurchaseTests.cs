using System;
using System.Collections.Generic;
using System.IO;
using BK.Core.Time;
using BK.Kit;
using BK.Meta;
using BK.Purchasing;
using BK.Save;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using UnityEditor;

public sealed class BouncePurchaseTests
{
    private sealed class Clock : IClock { public DateTime Now = new(2026,1,5,12,0,0,DateTimeKind.Utc); public DateTime UtcNow => Now; }
    private sealed class Store : IStore
    {
        public bool IsReady => true;
        public bool IsBusy { get; private set; }
        public event Action<string> Completed;
        public Func<StorePurchase,bool> Fulfill;
        public string Product;
        public UniTask InitializeAsync(IEnumerable<StoreProduct> products, Func<StorePurchase,bool> fulfill)
        { Fulfill = fulfill; return UniTask.CompletedTask; }
        public bool TryPurchase(string id,out string message) { Product=id; IsBusy=true; message=""; return true; }
        public bool Deliver(string tx) { bool ok=Fulfill(new StorePurchase(Product,tx)); if(ok) {IsBusy=false;Completed?.Invoke("done");} return ok; }
        public void RetryPending() { }
        public void Dispose() { }
    }
    private sealed class Saves : ISaveService
    {
        public SaveService Inner;
        public bool Fail;
        public T Get<T>() where T:SaveData,new() => Inner.Get<T>();
        public bool Flush() => !Fail && Inner.Flush();
        public bool HasUnsavedChanges => Inner.HasUnsavedChanges;
    }
    private string directory;
    private Saves saves;
    private Clock clock;
    private Wallet wallet;
    private StageProgress progress;
    private StepOffers offers;
    private Store store;
    private BouncePurchases purchases;
    private ShopCatalog shop;
    [SetUp] public void SetUp()
    {
        directory=Path.Combine(Path.GetTempPath(),"BKPurchaseTests",Guid.NewGuid().ToString("N"));
        saves=new Saves {Inner=new SaveService(directory)}; clock=new Clock();
        wallet=new Wallet(new BounceCurrencies(),saves,clock);
        progress=new StageProgress(saves,wallet,BounceEntry.Policy); progress.SetCurrentStage(2);
        offers=new StepOffers(new BounceStepOffers(),saves,wallet,clock,progress);
        shop=new ShopCatalog(AssetDatabase.LoadAssetAtPath<ShopProductTable>("Assets/_Project/Bounce/Content/Tables/ShopProductTable.asset"),
            AssetDatabase.LoadAssetAtPath<CurrencyDefinitionTable>("Assets/_Project/Bounce/Content/Tables/CurrencyDefinitionTable.asset"));
        store=new Store(); purchases=new BouncePurchases(shop,saves,wallet,offers,store);
        purchases.InitializeAsync().GetAwaiter().GetResult();
    }
    [TearDown] public void TearDown()
    { purchases.Dispose(); offers.Dispose(); progress.Dispose(); wallet.Dispose(); saves.Inner.Dispose(); if(Directory.Exists(directory))Directory.Delete(directory,true); }

    [Test] public void BundleMapsImportedBoosterIdsAndPersistsOnce()
    {
        Assert.That(purchases.TryBuy(9,out _),Is.True);
        Assert.That(store.Deliver("bundle"),Is.True);
        foreach(var grant in purchases.RewardsFor(shop.Products.Get(9))) Assert.That(wallet.ValueOf(grant.itemId),Is.EqualTo(grant.amount));
        long gold=wallet.ValueOf("Gold");
        Assert.That(store.Deliver("bundle"),Is.True);
        Assert.That(wallet.ValueOf("Gold"),Is.EqualTo(gold));
        var reloaded=new SaveService(directory);
        Assert.That(reloaded.Get<WalletData>().purchases.Count,Is.EqualTo(1));
        Assert.That(reloaded.Get<WalletData>().Find("Gold").value,Is.EqualTo(gold));
    }
    [Test] public void NoAdsAndWelcomeCanOnlyBeBoughtOnce()
    {
        Assert.That(purchases.TryBuy(7,out _),Is.True);Assert.That(store.Deliver("noads"),Is.True);
        Assert.That(wallet.ValueOf(BounceCurrencies.NoAds),Is.EqualTo(1));
        Assert.That(purchases.TryBuy(7,out _),Is.False);
        Assert.That(purchases.TryBuyWelcome(out _),Is.True);Assert.That(store.Deliver("welcome"),Is.True);
        Assert.That(purchases.WelcomeOwned,Is.True);Assert.That(purchases.TryBuyWelcome(out _),Is.False);
    }
    [Test] public void SaveFailureBlocksConfirmationAndRetryDoesNotDoubleGrant()
    {
        Assert.That(purchases.TryBuy(2,out _),Is.True);saves.Fail=true;
        Assert.That(store.Deliver("retry"),Is.False);
        Assert.That(wallet.ValueOf("Gold"),Is.EqualTo(1000));
        saves.Fail=false;Assert.That(store.Deliver("retry"),Is.True);
        Assert.That(wallet.ValueOf("Gold"),Is.EqualTo(1000));
        Assert.That(new SaveService(directory).Get<WalletData>().Find("Gold").value,Is.EqualTo(1000));
    }
    [Test] public void PaidStepAdvancesOnlyAfterPurchaseAndRejectsStaleButton()
    {
        var free=offers.GetActive().Value;Assert.That(offers.TryClaimFree(free),Is.True);
        var paid=offers.GetActive().Value;Assert.That(purchases.TryBuyStep(paid,out _),Is.True);
        Assert.That(offers.GetActive().Value.NextStep,Is.EqualTo(2));
        Assert.That(store.Deliver("step"),Is.True);
        Assert.That(offers.GetActive().Value.NextStep,Is.EqualTo(3));
        Assert.That(purchases.TryBuyStep(paid,out _),Is.False);
        Assert.That(purchases.TryBuy(1011,out _),Is.False,"A hidden step must not bypass the campaign gate.");
        Assert.That(wallet.ValueOf("Bomb"),Is.EqualTo(1));
    }
    [Test] public void WeekRolloverStillGrantsPaidGoodsWithoutAdvancingNewCampaign()
    {
        offers.TryClaimFree(offers.GetActive().Value);
        Assert.That(purchases.TryBuyStep(offers.GetActive().Value,out _),Is.True);
        clock.Now=clock.Now.AddDays(7);
        Assert.That(store.Deliver("late"),Is.True);
        Assert.That(offers.GetActive().Value.NextStep,Is.EqualTo(1));
        Assert.That(wallet.ValueOf("Gold"),Is.GreaterThan(3000));
    }
    [Test] public void RestartReconcilesStepFromDurableWalletReceipt()
    {
        offers.TryClaimFree(offers.GetActive().Value);
        var paid=offers.GetActive().Value;
        var ledger=new PurchaseRewards(wallet,saves);
        ledger.Grant("crash",paid.Current.ProductId,$"step:{paid.WeekIndex}:{paid.Definition.OfferId}:{paid.NextStep}",new[]{BounceItems.Gold(3000)});
        saves.Flush();
        purchases.Dispose();
        purchases=new BouncePurchases(shop,saves,wallet,offers,new Store());
        purchases.InitializeAsync().GetAwaiter().GetResult();
        Assert.That(offers.GetActive().Value.NextStep,Is.EqualTo(3));
        Assert.That(wallet.ValueOf("Gold"),Is.EqualTo(3050));
    }
}
