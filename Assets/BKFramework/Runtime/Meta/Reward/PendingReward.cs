namespace BK.Meta
{
    public enum PendingRewardKind { Claimed, ClaimedDouble }

    /// <summary>
    /// A reward whose arrival effect still has to play. <see cref="AlreadyCredited"/> says
    /// whether the wallet already holds it (credit-first policy) or the presenter credits on arrival.
    /// </summary>
    public readonly struct PendingReward
    {
        public readonly string CurrencyId;
        public readonly long Amount;
        public readonly long From;
        public readonly long To;
        public readonly PendingRewardKind Kind;
        public readonly bool AlreadyCredited;

        public PendingReward(string currencyId, long amount, long from, long to,
            PendingRewardKind kind = PendingRewardKind.Claimed, bool alreadyCredited = false)
        {
            CurrencyId = currencyId;
            Amount = amount;
            From = from;
            To = to;
            Kind = kind;
            AlreadyCredited = alreadyCredited;
        }
    }
}
