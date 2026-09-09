using System.Collections.Generic;

namespace BK.Data
{
    /// <summary>Row keyed by an id that is unique within its table.</summary>
    public interface ITableRow<out TKey>
    {
        TKey Id { get; }
    }

    /// <summary>
    /// Read-only lookup over one table's rows. Tables are immutable once loaded;
    /// nothing in the framework mutates them, so they are safe to share freely.
    /// </summary>
    public interface ITable<TKey, TRow> where TRow : ITableRow<TKey>
    {
        int Count { get; }
        IReadOnlyList<TRow> Rows { get; }

        /// <summary>Throws <see cref="TableRowNotFoundException"/> when absent.</summary>
        TRow Get(TKey id);

        bool TryGet(TKey id, out TRow row);
        bool Contains(TKey id);
    }
}
