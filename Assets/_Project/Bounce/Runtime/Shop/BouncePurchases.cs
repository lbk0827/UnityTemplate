using System;
using System.Collections.Generic;
using System.Linq;
using BK.Meta;
using BK.Purchasing;
using BK.Save;
using Cysharp.Threading.Tasks;

namespace BK.Kit
{
    [Serializable]
    public sealed class BouncePurchaseIntent : SaveData
    {
        public override int CurrentVersion => 1;
        public string productId, context;
    }

    /// <summary>Bounce product mapping and fulfillment; no game types leak into the store adapter.</summary>
    public sealed class BouncePurchases : IDisposable
    {
        public const string WelcomeId = "bounce.welcome";
        public static readonly ItemGrant[] WelcomeRewards =
        {
            BounceItems.Gold(3000), BounceItems.Booster(BoosterKind.Missile,3), BounceItems.Booster(BoosterKind.ExtraBall,3),
            BounceItems.Booster(BoosterKind.Bomb,2), BounceItems.Booster(BoosterKind.Laser,2)
        };
        private readonly ShopCatalog shop;
        private readonly ISaveService saves;
        private readonly StepOffers offers;
        private readonly IStore store;
        private readonly PurchaseRewards ledger;
        private readonly BouncePurchaseIntent intent;
        private readonly HashSet<int> regularIds;
        private readonly HashSet<int> stepIds;
        public bool IsReady => store.IsReady;
        public bool IsBusy => store.IsBusy;
        public bool WelcomeOwned => ledger.Count(WelcomeId) > 0;
        public event Action Changed;
        public event Action<string> Completed;

        public BouncePurchases(ShopCatalog shop, ISaveService saves, IWallet wallet, StepOffers offers, IStore store)
        {
            this.shop = shop; this.saves = saves; this.offers = offers; this.store = store;
            ledger = new PurchaseRewards(wallet, saves);
            intent = saves.Get<BouncePurchaseIntent>();
            regularIds = shop.VisibleProducts().Select(p => p.id).ToHashSet();
            stepIds = new BounceStepOffers().Offers.SelectMany(o => o.Steps).Where(s => s.IsPaid)
                .Select(s => int.Parse(s.ProductId)).ToHashSet();
            store.Completed += OnCompleted;
        }

        public async UniTask InitializeAsync()
        {
            ReconcileSteps();
            saves.Flush();
            var catalog = regularIds.Concat(stepIds).Distinct()
                .Select(id => new StoreProduct(id.ToString(), shop.Products.Get(id).layout == "NoAds"))
                .Append(new StoreProduct(WelcomeId, true));
            await store.InitializeAsync(catalog, Fulfill);
            Changed?.Invoke();
        }

        public int Count(int id) => ledger.Count(id.ToString());
        public bool IsOwned(int id)
        {
            var row = shop.Products.Get(id);
            var limit = row.layout == "NoAds" ? 1 : row.purchaseLimit;
            return limit > 0 && Count(id) >= limit;
        }
        public bool TryBuy(int id, out string message)
        {
            if (!regularIds.Contains(id)) { message = "This product is not available in the shop."; return false; }
            if (IsOwned(id)) { message = "Already purchased."; return false; }
            return Begin(id.ToString(), "", out message);
        }
        public bool TryBuyWelcome(out string message)
        {
            if (WelcomeOwned) { message = "Already purchased."; return false; }
            return Begin(WelcomeId, "", out message);
        }
        public bool TryBuyStep(in StepOfferCampaign campaign, out string message)
        {
            var current = offers.GetActive();
            if (current == null || current.Value.Current == null || !current.Value.Current.IsPaid
                || Context(current.Value) != Context(campaign))
            { message = "This offer has changed. Reopen it."; return false; }
            return Begin(current.Value.Current.ProductId, Context(current.Value), out message);
        }
        private bool Begin(string productId, string context, out string message)
        {
            if (!store.IsReady || store.IsBusy)
            { message = store.IsBusy ? "A purchase or save is still pending." : "Fake Store is available in the Unity Editor after products finish loading."; return false; }
            // Persist the exact step before opening the store. A callback may arrive after a popup closes or a week changes.
            intent.productId = productId; intent.context = context; intent.MarkDirty();
            if (!saves.Flush()) { message = "Could not save. Please retry before purchasing."; return false; }
            var started = store.TryPurchase(productId, out message);
            Changed?.Invoke();
            return started;
        }

        public ItemGrant[] RewardsFor(ShopProductRow row) => row.rewards.Select(reward =>
        {
            var definition = shop.Currencies.Get(reward.currencyId);
            var currency = string.IsNullOrEmpty(definition.boosterKind) ? reward.currencyId : definition.boosterKind;
            return new ItemGrant(currency, reward.amount);
        }).ToArray();

        private bool Fulfill(StorePurchase purchase)
        {
            var previous = ledger.Find(purchase.TransactionId);
            if (previous == null && intent.productId != purchase.ProductId) return false;
            ItemGrant[] rewards;
            if (purchase.ProductId == WelcomeId) rewards = WelcomeRewards;
            else
            {
                if (!int.TryParse(purchase.ProductId, out var id) || (!regularIds.Contains(id) && !stepIds.Contains(id))) return false;
                rewards = RewardsFor(shop.Products.Get(id));
            }
            ledger.Grant(purchase.TransactionId, purchase.ProductId, previous?.context ?? intent.context, rewards);
            // First persist wallet + receipt together. Only then advance the separate step-offer save.
            if (!saves.Flush()) return false;
            ReconcileSteps();
            if (!saves.Flush()) return false;
            Changed?.Invoke();
            return true;
        }

        private static string Context(in StepOfferCampaign c) => $"step:{c.WeekIndex}:{c.Definition.OfferId}:{c.NextStep}";
        private void ReconcileSteps()
        {
            var current = offers.GetActive();
            if (current?.Current == null || !current.Value.Current.IsPaid) return;
            var context = Context(current.Value);
            var receipt = ledger.Receipts.FirstOrDefault(r => r.context == context && r.productId == current.Value.Current.ProductId);
            if (receipt != null) offers.ConfirmPaid(current.Value, receipt.productId);
        }
        public void RetryPending() => store.RetryPending();
        private void OnCompleted(string message)
        {
            if (!store.IsBusy) { intent.productId = null; intent.context = null; intent.MarkDirty(); saves.Flush(); }
            Changed?.Invoke();
            Completed?.Invoke(message);
        }
        public void Dispose() { store.Completed -= OnCompleted; store.Dispose(); }
    }
}
