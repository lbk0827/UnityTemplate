using System.Threading;
using Cysharp.Threading.Tasks;

namespace BK.UI
{
    /// <summary>
    /// A view owns its own presentation lifecycle. The UI service drives these
    /// callbacks; views never manage their own instantiation or destruction.
    /// </summary>
    public interface IUIView
    {
        UILayer Layer { get; }
        bool IsOpen { get; }

        /// <summary>
        /// Called once after instantiation and dependency injection, before the
        /// open transition. Do resource-independent wiring here.
        /// </summary>
        UniTask OnInitializeAsync(CancellationToken cancellationToken);

        /// <summary>Runs the show transition. Completes when the view is interactive.</summary>
        UniTask OnOpenAsync(CancellationToken cancellationToken);

        /// <summary>Runs the hide transition. Completes when the view is fully hidden.</summary>
        UniTask OnCloseAsync(CancellationToken cancellationToken);

        /// <summary>
        /// Raised by the back gesture / hardware back button while this view is
        /// topmost. Return true to consume; false lets the service pop it.
        /// </summary>
        bool OnBackRequested();
    }

    /// <summary>Implemented by views that receive a typed argument on open.</summary>
    public interface IUIView<in TArgs> : IUIView
    {
        void SetArgs(TArgs args);
    }
}
