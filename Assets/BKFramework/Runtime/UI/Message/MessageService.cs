using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;

namespace BK.UI
{
    /// <inheritdoc cref="IMessageService"/>
    public sealed class MessageService : IMessageService, IDisposable
    {
        private sealed class PendingMessage
        {
            public MessageRequest Request;
            public readonly UniTaskCompletionSource<bool> Result = new();
        }

        private sealed class PendingToast
        {
            public string Text;
            public string CollapseKey;
        }

        private readonly IMessagePresenter _presenter;
        private readonly CancellationTokenSource _cts = new();
        private readonly Queue<PendingMessage> _messages = new();
        private readonly Queue<PendingToast> _toasts = new();
        private readonly HashSet<string> _collapsed = new();
        private bool _messageLoop, _toastLoop;

        public MessageService(IMessagePresenter presenter) => _presenter = presenter;

        public UniTask ShowAsync(string title, string body, string buttonText, bool forced = false)
            => Enqueue(new MessageRequest { Title = title, Body = body, ConfirmText = buttonText, Forced = forced }).AsUniTask();

        public UniTask<bool> ConfirmAsync(string title, string body, string confirmText, string cancelText)
            => Enqueue(new MessageRequest { Title = title, Body = body, ConfirmText = confirmText, CancelText = cancelText ?? "" });

        public void Toast(string text, bool collapseDuplicate = false)
        {
            if (collapseDuplicate && !_collapsed.Add(text))
                return;
            _toasts.Enqueue(new PendingToast { Text = text, CollapseKey = collapseDuplicate ? text : null });
            if (!_toastLoop)
                RunToastsAsync().Forget();
        }

        private UniTask<bool> Enqueue(MessageRequest request)
        {
            var pending = new PendingMessage { Request = request };
            _messages.Enqueue(pending);
            if (!_messageLoop)
                RunMessagesAsync().Forget();
            return pending.Result.Task;
        }

        private async UniTaskVoid RunMessagesAsync()
        {
            _messageLoop = true;
            try
            {
                while (_messages.Count > 0 && !_cts.IsCancellationRequested)
                {
                    var pending = _messages.Dequeue();
                    try { pending.Result.TrySetResult(await _presenter.ShowAsync(pending.Request, _cts.Token)); }
                    catch (OperationCanceledException) { pending.Result.TrySetCanceled(); }
                    catch (Exception exception) { pending.Result.TrySetException(exception); }
                }
            }
            finally
            {
                _messageLoop = false;
            }
        }

        private async UniTaskVoid RunToastsAsync()
        {
            _toastLoop = true;
            try
            {
                while (_toasts.Count > 0 && !_cts.IsCancellationRequested)
                {
                    var toast = _toasts.Dequeue();
                    try { await _presenter.ShowToastAsync(toast.Text, _cts.Token); }
                    catch (OperationCanceledException) { }
                    finally
                    {
                        // Released after display so the same text can be shown again later.
                        if (toast.CollapseKey != null)
                            _collapsed.Remove(toast.CollapseKey);
                    }
                }
            }
            finally
            {
                _toastLoop = false;
                // Leaving the loop means the queue is empty (or we were cancelled): never leave a key behind.
                _collapsed.Clear();
            }
        }

        public void Dispose()
        {
            _cts.Cancel();
            _cts.Dispose();
        }
    }
}
