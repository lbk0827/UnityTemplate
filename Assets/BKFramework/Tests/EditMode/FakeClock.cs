using System;
using BK.Core.Time;

namespace BK.Tests
{
    public sealed class FakeClock : IClock
    {
        public DateTime UtcNow { get; set; }
        public FakeClock(DateTime start) => UtcNow = start;
        public void Advance(TimeSpan by) => UtcNow += by;
    }
}
