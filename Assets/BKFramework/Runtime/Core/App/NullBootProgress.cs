using System;

namespace BK.Core.App
{
    /// <summary>
    /// Default <see cref="IProgress{T}"/> registration so boot works before a splash
    /// screen exists. Replace the registration to drive real UI.
    /// </summary>
    public sealed class NullBootProgress : IProgress<BootProgress>
    {
        public void Report(BootProgress value) { }
    }
}
