using System;
using System.Collections.Generic;
using System.Text;
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

    /// <summary>Grant helpers shared by the catalogs and the views.</summary>
    public static class BounceItems
    {
        public static ItemGrant Gold(long amount) => new(BounceCurrencies.Gold, amount);
        public static ItemGrant Booster(BoosterKind kind, long amount) => new(kind.ToString(), amount);
        public static bool IsGold(in ItemGrant grant) => grant.itemId == BounceCurrencies.Gold;
        public static bool TryGetBooster(in ItemGrant grant, out BoosterKind kind)
            => Enum.TryParse(grant.itemId, out kind) && BoosterCatalog.Find(kind) != null;

        /// <summary>"200 Gold, Missile x1" for toasts and feedback lines.</summary>
        public static string Describe(IReadOnlyList<ItemGrant> grants)
        {
            var text = new StringBuilder();
            for (var i = 0; i < grants.Count; i++)
            {
                var grant = grants[i];
                if (text.Length > 0) text.Append(", ");
                if (IsGold(grant)) text.Append(grant.amount).Append(" Gold");
                else text.Append(TryGetBooster(grant, out var kind) ? BoosterCatalog.Find(kind).Title : grant.itemId).Append(" x").Append(grant.amount);
            }
            return text.ToString();
        }
    }

    /// <summary>
    /// Weekly step offers (sf "endless offer"). Even weeks run the 7-step chain, odd weeks the 4-step vertical ladder.
    /// Paid steps point at the endless_* rows of the shop table; the store is not connected, so they only display a price.
    /// </summary>
    public sealed class BounceStepOffers : IStepOfferCatalog
    {
        public const int VerticalOfferId = 1;
        public const int ChainOfferId = 2;
        public DateTime EpochUtc { get; } = new(2026, 1, 5, 0, 0, 0, DateTimeKind.Utc); // a Monday
        public int UnlockStage => 2;

        public IReadOnlyList<StepOfferDefinition> Offers { get; } = new[]
        {
            new StepOfferDefinition
            {
                OfferId = VerticalOfferId, Type = StepOfferType.Vertical,
                Steps = new[]
                {
                    Free(1, BounceItems.Gold(100)),
                    Paid(2, 1001),
                    Free(3, BounceItems.Booster(BoosterKind.Bomb, 1)),
                    Paid(4, 1002),
                },
            },
            new StepOfferDefinition
            {
                OfferId = ChainOfferId, Type = StepOfferType.Chain,
                Steps = new[]
                {
                    Free(1, BounceItems.Gold(50)),
                    Paid(2, 1011),
                    Free(3, BounceItems.Booster(BoosterKind.ExtraBall, 1)),
                    Paid(4, 1012),
                    Free(5, BounceItems.Gold(100)),
                    Paid(6, 1013),
                    Free(7, BounceItems.Booster(BoosterKind.Laser, 1)),
                },
            },
        };

        private static StepOfferStep Free(int step, params ItemGrant[] rewards) => new() { Step = step, Rewards = rewards };
        private static StepOfferStep Paid(int step, int productId) => new() { Step = step, IsPaid = true, ProductId = productId.ToString() };
    }

    /// <summary>Seven-day login cycle, three daily bonus slots and an hourly gift (sf template rules).</summary>
    public sealed class BounceDailyRewards : IDailyRewardCatalog
    {
        public IReadOnlyList<ItemGrant[]> Days { get; } = new[]
        {
            new[] { BounceItems.Gold(200) },
            new[] { BounceItems.Gold(300) },
            new[] { BounceItems.Booster(BoosterKind.Missile, 1) },
            new[] { BounceItems.Gold(500) },
            new[] { BounceItems.Booster(BoosterKind.Bomb, 1) },
            new[] { BounceItems.Gold(800) },
            new[] { BounceItems.Gold(1500), BounceItems.Booster(BoosterKind.ExtraBall, 1) },
        };

        public IReadOnlyList<ItemGrant[]> BonusSlots { get; } = new[]
        {
            new[] { BounceItems.Gold(50) },
            new[] { BounceItems.Gold(50) },
            new[] { BounceItems.Gold(100) },
        };

        public ItemGrant[] Hourly { get; } = { BounceItems.Gold(20) };
    }

    /// <summary>Consecutive clears up to 10; milestones hand boosters to the next attempt (sf 3/5/10).</summary>
    public sealed class BounceWinStreak : IWinStreakCatalog
    {
        public int Cap => 10;
        public int UnlockStage => 1;

        public IReadOnlyList<WinStreakTier> Tiers { get; } = new[]
        {
            new WinStreakTier { Threshold = 3, Rewards = new[] { BounceItems.Booster(BoosterKind.Missile, 1) } },
            new WinStreakTier { Threshold = 5, Rewards = new[] { BounceItems.Booster(BoosterKind.Bomb, 1) } },
            new WinStreakTier { Threshold = 10, Rewards = new[] { BounceItems.Booster(BoosterKind.ExtraBall, 2) } },
        };
    }
}
