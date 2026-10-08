using System.Threading;
using Cysharp.Threading.Tasks;
using R3;

namespace BK.UI
{
    /// <summary>
    /// One opaque cover above popups, below system dialogs. Hold it before tearing a
    /// scene down, lift it when the next scene is ready. Reentrant: Hold while held is a no-op.
    /// </summary>
    public interface IScreenCover
    {
        bool IsActive { get; }

        /// <summary>Opaque immediately.</summary>
        void Hold();

        /// <summary>Fade in from transparent (scene exit), then stay held.</summary>
        UniTask HoldAsync(float fadeInSeconds, CancellationToken cancellationToken = default);

        UniTask ReleaseAsync(CancellationToken cancellationToken = default);

        /// <summary>Waits for the registered <see cref="IRevealReady"/> (or one frame when none), then fades out.</summary>
        UniTask ReleaseWhenReadyAsync(CancellationToken cancellationToken = default);

        UniTask WaitUntilReleasedAsync(CancellationToken cancellationToken = default);

        Observable<Unit> Released { get; }

        /// <summary>The scene that is about to be revealed registers itself; the last registration wins.</summary>
        void RegisterRevealReady(IRevealReady ready);

        void UnregisterRevealReady(IRevealReady ready);
    }
}
