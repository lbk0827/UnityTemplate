using System.Collections.Generic;

namespace BK.Meta
{
    /// <summary>The game's currency list. Registered by the game; the framework never reads tables for it.</summary>
    public interface ICurrencyCatalog
    {
        IReadOnlyList<CurrencyDefinition> Definitions { get; }
    }
}
