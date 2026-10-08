using System;
using System.Collections.Generic;
using BK.Meta;

namespace BK.Kit
{
    /// <summary>Bounce's currencies: gold, hearts with a 30-minute recharge, the infinite-heart buff and four boosters.</summary>
    public sealed class BounceCurrencies : ICurrencyCatalog
    {
        public const string Gold = "Gold";
        public const string Heart = "Heart";
        public const string InfiniteHeart = "InfiniteHeart";
        public const int HeartMax = 5;
        public static readonly TimeSpan HeartInterval = TimeSpan.FromMinutes(30);

        public IReadOnlyList<CurrencyDefinition> Definitions { get; } = Build();

        private static CurrencyDefinition[] Build()
        {
            var list = new List<CurrencyDefinition>
            {
                new() { Id = Gold },
                new()
                {
                    Id = Heart, Kind = CurrencyKind.Rechargeable, InitialValue = HeartMax, RechargeMax = HeartMax,
                    RechargeAmount = 1, RechargeInterval = HeartInterval, InfiniteBuffIds = new[] { InfiniteHeart },
                },
                new() { Id = InfiniteHeart, Kind = CurrencyKind.Buff },
            };
            foreach (var offer in BoosterCatalog.All)
                list.Add(new CurrencyDefinition { Id = offer.Kind.ToString() });
            return list.ToArray();
        }
    }

    /// <summary>Continue after running out of balls: +5 balls on a gold ladder sized to Bounce's 50-gold clear reward.</summary>
    public sealed class BounceContinueOffers : IContinueOfferCatalog
    {
        public const long BasePrice = 150;
        public const long AddPrice = 150;
        public const long MaxPrice = 600;
        public const int ExtraBalls = 5;

        public IReadOnlyList<ContinueOfferDefinition> Offers { get; } = new[]
        {
            new ContinueOfferDefinition
            {
                Reason = StageFailReason.MovesZero, CurrencyId = BounceCurrencies.Gold,
                BasePrice = BasePrice, AddPrice = AddPrice, MaxPrice = MaxPrice, AddMoves = ExtraBalls,
            },
        };
    }

    public static class BounceEntry
    {
        /// <summary>Stages 1..3 are free; from 4 on a heart is charged and refunded on clear.</summary>
        public const int FreeUntilStage = 3;
        public static StageEntryPolicy Policy => new(BounceCurrencies.Heart, FreeUntilStage);
    }
}
