using System.Threading;
using Cysharp.Threading.Tasks;

namespace BK.UI
{
    /// <summary>
    /// Opens and closes views. Views are addressed by <see cref="BK.Assets.AssetKey"/>
    /// and resolved through the owning scope's asset scope, so closing a scene
    /// releases its view prefabs without extra bookkeeping.
    /// </summary>
    public interface IUIService
    {
        UniTask<TView> OpenAsync<TView>(
            BK.Assets.AssetKey key,
            CancellationToken cancellationToken = default)
            where TView : class, IUIView;

        UniTask<TView> OpenAsync<TView, TArgs>(
            BK.Assets.AssetKey key,
            TArgs args,
            CancellationToken cancellationToken = default)
            where TView : class, IUIView<TArgs>;

        UniTask CloseAsync(IUIView view, CancellationToken cancellationToken = default);

        /// <summary>Closes the topmost view of the layer. No-op when the layer is empty.</summary>
        UniTask CloseTopAsync(UILayer layer, CancellationToken cancellationToken = default);

        /// <summary>Closes every open view on the layer, topmost first.</summary>
        UniTask CloseAllAsync(UILayer layer, CancellationToken cancellationToken = default);

        /// <summary>Topmost view of the layer, or null.</summary>
        IUIView Peek(UILayer layer);

        /// <summary>
        /// Routes a back request to the topmost interactive view across all layers.
        /// Returns true when some view handled or was closed by it.
        /// </summary>
        bool HandleBackRequest();
    }
}
