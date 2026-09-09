using System;
using BK.Assets;

namespace BK.Scene
{
    /// <summary>
    /// Everything a loaded scene owns: its DI child container, its asset scope, and
    /// its open views. Unloading the scene disposes the scope, which releases all
    /// three together. This is the single place scene lifetime is enforced.
    /// </summary>
    public interface ISceneScope : IDisposable
    {
        string SceneName { get; }
        bool IsDisposed { get; }

        /// <summary>Assets loaded here are released when the scene unloads.</summary>
        IAssetScope Assets { get; }

        /// <summary>Resolves a service from the scene container, falling back to the root.</summary>
        T Resolve<T>();
    }
}
