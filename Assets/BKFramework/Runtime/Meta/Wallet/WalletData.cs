using System;
using System.Collections.Generic;
using BK.Save;

namespace BK.Meta
{
    [Serializable]
    public sealed class WalletData : SaveData
    {
        [Serializable]
        public sealed class Entry
        {
            public string id;
            public long value;
            /// <summary>Rechargeable: cycle anchor. Buff: end time. 0 = none.</summary>
            public long stampTicks;
        }

        public override int CurrentVersion => 1;
        public List<Entry> entries = new();

        public Entry Find(string id)
        {
            foreach (var e in entries)
                if (e.id == id) return e;
            return null;
        }
    }
}
