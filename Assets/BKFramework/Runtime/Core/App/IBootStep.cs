using System.Threading;
using Cysharp.Threading.Tasks;

namespace BK.Core.App
{
    /// <summary>
    /// One unit of application start-up work. Steps are resolved from the root
    /// container and executed in ascending <see cref="Order"/>.
    /// </summary>
    public interface IBootStep
    {
        /// <summary>Lower runs first. Use the constants on <see cref="BootOrder"/>.</summary>
        int Order { get; }

        /// <summary>Human readable name, surfaced in boot logs and on failure.</summary>
        string Name { get; }

        UniTask ExecuteAsync(CancellationToken cancellationToken);
    }

    /// <summary>Conventional slots so feature steps can position themselves without magic numbers.</summary>
    public static class BootOrder
    {
        public const int Diagnostics = -1000;
        public const int Assets = 0;
        public const int Data = 100;
        public const int Localization = 200;
        public const int Presentation = 300;
        public const int UI = 400;
        public const int Gameplay = 1000;
    }
}
