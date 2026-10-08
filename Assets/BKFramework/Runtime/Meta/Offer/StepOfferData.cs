using System;
using System.Collections.Generic;
using BK.Save;

namespace BK.Meta
{
    [Serializable]
    public sealed class StepOfferData : SaveData
    {
        [Serializable]
        public sealed class Slot
        {
            public int type;
            public int offerId;
            public int weekIndex = -1;
            /// <summary>1-based; greater than MaxStep means completed.</summary>
            public int nextStep;
            public long startedTicks;
            public bool autoOpened;
        }

        public override int CurrentVersion => 1;
        public List<Slot> slots = new();

        public Slot Find(StepOfferType type)
        {
            foreach (var slot in slots)
                if (slot.type == (int)type) return slot;
            return null;
        }
    }
}
