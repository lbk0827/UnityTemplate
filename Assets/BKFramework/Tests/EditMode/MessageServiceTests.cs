using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using BK.UI;
using Cysharp.Threading.Tasks;
using NUnit.Framework;

namespace BK.Tests
{
    public sealed class MessageServiceTests
    {
        private sealed class Presenter : IMessagePresenter
        {
            public readonly List<string> Shown = new();
            public readonly List<string> Toasts = new();
            public UniTaskCompletionSource<bool> Current;
            public UniTaskCompletionSource ToastDone;

            public UniTask<bool> ShowAsync(MessageRequest request, CancellationToken ct)
            {
                Shown.Add(request.Body);
                Current = new UniTaskCompletionSource<bool>();
                return Current.Task;
            }

            public UniTask ShowToastAsync(string text, CancellationToken ct)
            {
                Toasts.Add(text);
                ToastDone = new UniTaskCompletionSource();
                return ToastDone.Task;
            }
        }

        [Test]
        public async Task MessagesAreShownOneAtATimeInOrder()
        {
            var p = new Presenter();
            using var s = new MessageService(p);
            var a = s.ShowAsync("T", "A", "OK");
            var b = s.ConfirmAsync("T", "B", "Yes", "No");
            await UniTask.Yield();
            Assert.That(p.Shown, Is.EqualTo(new[] { "A" }));
            p.Current.TrySetResult(true);
            await a;
            await UniTask.Yield();
            Assert.That(p.Shown, Is.EqualTo(new[] { "A", "B" }));
            p.Current.TrySetResult(false);
            Assert.That(await b, Is.False);
        }

        [Test]
        public async Task CollapsedToastSkipsDuplicatesWhileQueuedOrShowing()
        {
            var p = new Presenter();
            using var s = new MessageService(p);
            s.Toast("same", collapseDuplicate: true);
            s.Toast("same", collapseDuplicate: true);
            s.Toast("other");
            s.Toast("other");
            await UniTask.Yield();
            Assert.That(p.Toasts, Is.EqualTo(new[] { "same" }));
            p.ToastDone.TrySetResult(); await UniTask.Yield(); await UniTask.Yield();
            p.ToastDone.TrySetResult(); await UniTask.Yield(); await UniTask.Yield();
            p.ToastDone.TrySetResult(); await UniTask.Yield(); await UniTask.Yield();
            Assert.That(p.Toasts, Is.EqualTo(new[] { "same", "other", "other" }));
            s.Toast("same", collapseDuplicate: true);
            await UniTask.Yield();
            Assert.That(p.Toasts.Count, Is.EqualTo(4), "key is released after the toast finished");
        }
    }
}
