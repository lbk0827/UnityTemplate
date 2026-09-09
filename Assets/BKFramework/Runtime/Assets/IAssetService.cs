using System.Threading;
using Cysharp.Threading.Tasks;

namespace BK.Assets
{
    /// <summary>
    /// Entry point for content loading. Callers work through scopes; the service
    /// itself only bootstraps the backend and hands out scopes.
    /// </summary>
    public interface IAssetService
    {
        UniTask InitializeAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Creates an independently disposable scope. <paramref name="name"/> is used
        /// in diagnostics to attribute leaked handles to their owner.
        /// </summary>
        IAssetScope CreateScope(string name);

        /// <summary>Scope that lives for the whole session; never disposed by the framework.</summary>
        IAssetScope GlobalScope { get; }
    }
}
