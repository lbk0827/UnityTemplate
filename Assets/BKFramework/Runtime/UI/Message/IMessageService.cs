using Cysharp.Threading.Tasks;

namespace BK.UI
{
    /// <summary>Queued modal messages (one at a time) and fire-and-forget toasts.</summary>
    public interface IMessageService
    {
        UniTask ShowAsync(string title, string body, string buttonText, bool forced = false);

        UniTask<bool> ConfirmAsync(string title, string body, string confirmText, string cancelText);

        /// <param name="collapseDuplicate">Skip when the same text is already queued or showing.</param>
        void Toast(string text, bool collapseDuplicate = false);
    }
}
