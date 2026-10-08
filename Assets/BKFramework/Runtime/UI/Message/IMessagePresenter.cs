using System.Threading;
using Cysharp.Threading.Tasks;

namespace BK.UI
{
    /// <summary>Shows one message/toast at a time. Abstracted so the queue is testable.</summary>
    public interface IMessagePresenter
    {
        /// <returns>true for confirm/OK, false for cancel/dismiss.</returns>
        UniTask<bool> ShowAsync(MessageRequest request, CancellationToken cancellationToken);

        UniTask ShowToastAsync(string text, CancellationToken cancellationToken);
    }
}
