using System;
using System.Collections.Generic;

namespace BK.Meta
{
    /// <summary>Even weeks run the Chain type, odd weeks the Vertical type. Only one is live per week.</summary>
    public enum StepOfferType { None = 0, Vertical = 1, Chain = 2 }

    public sealed class StepOfferStep
    {
        /// <summary>1-based.</summary>
        public int Step;
        public bool IsPaid;
        /// <summary>Store product for paid steps; the store grants its contents.</summary>
        public string ProductId;
        /// <summary>Granted on a free claim.</summary>
        public ItemGrant[] Rewards = Array.Empty<ItemGrant>();
    }

    /// <summary>One campaign table. The same offer returns every week its type is scheduled; a round is (OfferId, WeekIndex).</summary>
    public sealed class StepOfferDefinition
    {
        public int OfferId;
        public StepOfferType Type;
        /// <summary>Ordered by Step, 1..MaxStep.</summary>
        public StepOfferStep[] Steps = Array.Empty<StepOfferStep>();
        public int MaxStep => Steps.Length;
    }

    public interface IStepOfferCatalog
    {
        /// <summary>Start of week 0 (UTC).</summary>
        DateTime EpochUtc { get; }

        /// <summary>Hidden while the player's current stage is below this.</summary>
        int UnlockStage { get; }

        IReadOnlyList<StepOfferDefinition> Offers { get; }
    }
}
