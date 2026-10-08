using System;
using BK.Save;

namespace BK.Kit
{
    [Serializable]
    public sealed class BounceProfileData : SaveData
    {
        public const string DefaultName = "BK Player";
        public override int CurrentVersion => 1;
        public string playerName = DefaultName;
        public override bool Validate() => !string.IsNullOrWhiteSpace(playerName) && playerName.Length <= 24;
    }

    /// <summary>Step progress of the lobby's step offers. Weekly rotation comes with the lobby rework.</summary>
    [Serializable]
    public sealed class BounceOfferData : SaveData
    {
        public override int CurrentVersion => 1;
        public int endlessOfferStep;
        public int endlessGiftStep;
        public override bool Validate() => endlessOfferStep >= 0 && endlessGiftStep >= 0;

        public int Step(OfferKind kind)
        {
            switch (kind)
            {
                case OfferKind.EndlessOffer: return endlessOfferStep;
                case OfferKind.EndlessGift: return endlessGiftStep;
                default: return 0;
            }
        }

        public void SetStep(OfferKind kind, int step)
        {
            switch (kind)
            {
                case OfferKind.EndlessOffer: endlessOfferStep = step; break;
                case OfferKind.EndlessGift: endlessGiftStep = step; break;
                default: throw new ArgumentOutOfRangeException(nameof(kind));
            }
            MarkDirty();
        }
    }
}
