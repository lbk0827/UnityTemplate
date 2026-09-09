using System.Threading;
using Cysharp.Threading.Tasks;
using BK.Core.App;

namespace BK.Assets
{
    /// <summary>Brings Addressables up before anything tries to load content.</summary>
    public sealed class AssetBootStep : IBootStep
    {
        private readonly IAssetService _assetService;

        public AssetBootStep(IAssetService assetService) => _assetService = assetService;

        public int Order => BootOrder.Assets;
        public string Name => "Assets";

        public UniTask ExecuteAsync(CancellationToken cancellationToken)
            => _assetService.InitializeAsync(cancellationToken);
    }
}
