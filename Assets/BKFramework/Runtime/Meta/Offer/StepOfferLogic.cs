using System;
using System.Collections.Generic;

namespace BK.Meta
{
    public readonly struct StepOfferCampaign
    {
        public readonly StepOfferDefinition Definition;
        public readonly int WeekIndex;
        public readonly int NextStep;
        public readonly DateTime EndUtc;
        public readonly bool AutoOpened;

        public StepOfferCampaign(StepOfferDefinition definition, int weekIndex, int nextStep, DateTime endUtc, bool autoOpened)
        {
            Definition = definition;
            WeekIndex = weekIndex;
            NextStep = nextStep;
            EndUtc = endUtc;
            AutoOpened = autoOpened;
        }

        public bool IsCompleted => NextStep > Definition.MaxStep;
        public StepOfferStep Current => IsCompleted ? null : Definition.Steps[NextStep - 1];
        public TimeSpan Remaining(DateTime nowUtc) => EndUtc > nowUtc ? EndUtc - nowUtc : TimeSpan.Zero;
    }

    public readonly struct StepOfferSlotView
    {
        public readonly StepOfferStep Step;
        public readonly bool IsCurrent;
        public readonly bool IsLocked;

        public StepOfferSlotView(StepOfferStep step, bool isCurrent)
        {
            Step = step;
            IsCurrent = isCurrent;
            IsLocked = !isCurrent;
        }
    }

    /// <summary>Pure rules for weekly step offers.</summary>
    public static class StepOfferLogic
    {
        public static StepOfferType TypeOfWeek(int weekIndex)
            => weekIndex < 0 ? StepOfferType.None : (weekIndex % 2 == 0 ? StepOfferType.Chain : StepOfferType.Vertical);

        /// <summary>Lowest OfferId of the type with at least one step, or null.</summary>
        public static StepOfferDefinition Select(IReadOnlyList<StepOfferDefinition> all, StepOfferType type)
        {
            StepOfferDefinition best = null;
            foreach (var offer in all)
            {
                if (offer.Type != type || offer.MaxStep <= 0)
                    continue;
                if (best == null || offer.OfferId < best.OfferId)
                    best = offer;
            }
            return best;
        }

        /// <summary>Starts a fresh round when the slot belongs to another (offer, week) or is corrupt. Returns true when reset.</summary>
        public static bool BeginRound(StepOfferData.Slot slot, StepOfferDefinition definition, int weekIndex, DateTime weekStartUtc)
        {
            var isNew = slot.offerId != definition.OfferId || slot.weekIndex != weekIndex || slot.nextStep <= 0;
            if (!isNew)
                return false;
            slot.type = (int)definition.Type;
            slot.offerId = definition.OfferId;
            slot.weekIndex = weekIndex;
            slot.nextStep = 1;
            slot.startedTicks = weekStartUtc.Ticks; // the week start, not "now": every player shares the same expiry
            slot.autoOpened = false;
            return true;
        }

        /// <summary>Cards from the current step onward; exactly one is current, the rest locked. Past steps are not rendered.</summary>
        public static IReadOnlyList<StepOfferSlotView> VisibleWindow(StepOfferDefinition definition, int nextStep, int slotCount)
        {
            var result = new List<StepOfferSlotView>();
            for (var step = nextStep; step <= definition.MaxStep && result.Count < slotCount; step++)
                result.Add(new StepOfferSlotView(definition.Steps[step - 1], isCurrent: step == nextStep));
            return result;
        }
    }
}
