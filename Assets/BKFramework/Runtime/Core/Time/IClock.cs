using System;

namespace BK.Core.Time
{
    /// <summary>
    /// UTC wall clock. Logic never calls DateTime.UtcNow directly, so a game that later
    /// trusts a server clock swaps this implementation and tests can move time by hand.
    /// </summary>
    public interface IClock
    {
        DateTime UtcNow { get; }
    }
}
