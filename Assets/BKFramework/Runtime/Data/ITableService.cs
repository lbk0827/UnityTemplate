using System.Threading;
using Cysharp.Threading.Tasks;

namespace BK.Data
{
    /// <summary>
    /// Loads table assets during boot and serves them for the rest of the session.
    /// Tables are authored as ScriptableObjects so they are inspectable in-editor and
    /// need no parsing at runtime; the import side (CSV/JSON to asset) is editor-only.
    /// </summary>
    public interface ITableService
    {
        bool IsLoaded { get; }

        UniTask LoadAllAsync(CancellationToken cancellationToken = default);

        /// <exception cref="TableNotLoadedException">Requested before <see cref="LoadAllAsync"/> completed.</exception>
        ITable<TKey, TRow> Get<TKey, TRow>() where TRow : ITableRow<TKey>;
    }
}
