using System;
using System.Collections.Generic;
using R3;

namespace BK.Meta
{
    public enum StageFailReason { Unknown, TimeOver, HpZero, MovesZero, UserQuit, GameSpecific }

    /// <summary>One continue product for a fail reason. Supplied by the game.</summary>
    public sealed class ContinueOfferDefinition
    {
        public StageFailReason Reason;
        public string CurrencyId = "Gold";
        public long BasePrice;
        public long AddPrice;
        public long MaxPrice;
        public int AddMoves;
        public int AddTimeSeconds;
    }

    public interface IContinueOfferCatalog
    {
        IReadOnlyList<ContinueOfferDefinition> Offers { get; }
    }

    /// <summary>Pure continue rules.</summary>
    public static class FailContinueLogic
    {
        /// <summary>Price ladder indexed by continues already bought this attempt, capped.</summary>
        public static long ComputePrice(long basePrice, long addPrice, long maxPrice, int usedCount)
            => Math.Min(basePrice + usedCount * addPrice, maxPrice);

        public static bool CanAfford(long balance, long price) => balance >= price;

        /// <summary>Moves win over time when both are present.</summary>
        public static (bool isMoves, int amount) ResolveGrant(int addMoves, int addTimeSeconds)
            => addMoves > 0 ? (true, addMoves) : (false, addTimeSeconds);

        public static bool OffersContinue(StageFailReason reason)
            => reason == StageFailReason.TimeOver || reason == StageFailReason.MovesZero;
    }

    public readonly struct ContinueOffer
    {
        public readonly ContinueOfferDefinition Definition;
        public readonly long Price;
        public readonly bool CanAfford;

        public ContinueOffer(ContinueOfferDefinition definition, long price, bool canAfford)
        {
            Definition = definition;
            Price = price;
            CanAfford = canAfford;
        }
    }

    /// <summary>Per-attempt continue ladder. Resets whenever a stage attempt starts.</summary>
    public sealed class ContinueOffers : IDisposable
    {
        public const string ReasonContinue = "continue";

        private readonly IContinueOfferCatalog _catalog;
        private readonly IWallet _wallet;
        private readonly IDisposable _reset;

        public int UsedCount { get; private set; }

        public ContinueOffers(IContinueOfferCatalog catalog, IWallet wallet, IStageProgress progress)
        {
            _catalog = catalog;
            _wallet = wallet;
            _reset = progress.Started.Subscribe(_ => UsedCount = 0);
        }

        public bool TryGetOffer(StageFailReason reason, out ContinueOffer offer)
        {
            offer = default;
            if (!FailContinueLogic.OffersContinue(reason))
                return false;

            foreach (var definition in _catalog.Offers)
            {
                if (definition.Reason != reason)
                    continue;
                var price = FailContinueLogic.ComputePrice(definition.BasePrice, definition.AddPrice, definition.MaxPrice, UsedCount);
                offer = new ContinueOffer(definition, price, FailContinueLogic.CanAfford(_wallet.ValueOf(definition.CurrencyId), price));
                return true;
            }

            return false;
        }

        /// <summary>Charges the offer's price. False, with no charge, when unaffordable. Climbs the ladder on success.</summary>
        public bool TryPurchase(ContinueOffer offer)
        {
            if (!_wallet.CanAfford(offer.Definition.CurrencyId, offer.Price))
                return false;
            _wallet.Add(offer.Definition.CurrencyId, -offer.Price, ReasonContinue);
            UsedCount++;
            return true;
        }

        public void Dispose() => _reset.Dispose();
    }
}
