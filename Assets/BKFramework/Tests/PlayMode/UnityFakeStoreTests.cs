#if UNITY_EDITOR
using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using BK.Purchasing;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace BK.Tests
{
    public sealed class UnityFakeStoreTests
    {
        [UnityTest]
        public IEnumerator OfficialDialogCancelThenBuyAndRetryUnsavedOrder()
        {
            using var store = new UnityFakeStore();
            int deliveries = 0;
            bool saved = false;
            StorePurchase purchase = default;
            string result = null;
            store.Completed += message => result = message;
            yield return store.InitializeAsync(new[] { new StoreProduct("bk.test.coins") }, order =>
            { deliveries++; purchase = order; return saved; }).ToCoroutine();
            yield return WaitFor(() => store.IsReady);

            Assert.That(store.TryPurchase("bk.test.coins", out var error), Is.True, error);
            Assert.That(store.TryPurchase("bk.test.coins", out _), Is.False, "Double click must not start another payment.");
            yield return WaitFor(() => Dialog() != null);
            Click("OnCancelClicked");
            yield return WaitFor(() => !store.IsBusy);
            Assert.That(deliveries, Is.Zero);
            StringAssert.Contains("cancelled", result);
            yield return null; // Unity destroys the previous dialog at end of frame.

            Assert.That(store.TryPurchase("bk.test.coins", out error), Is.True, error);
            yield return WaitFor(() => Dialog() != null);
            Click("OnOkClicked");
            yield return WaitFor(() => deliveries > 0);
            Assert.That(store.IsBusy, Is.True, "A failed save must not confirm the order.");
            Assert.That(purchase.ProductId, Is.EqualTo("bk.test.coins"));
            Assert.That(purchase.TransactionId, Is.Not.Empty);
            saved = true;
            store.RetryPending();
            yield return WaitFor(() => !store.IsBusy);
            StringAssert.Contains("complete", result);
        }

        private static MonoBehaviour Dialog() => UnityEngine.Object.FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None)
            .FirstOrDefault(x => x.GetType().FullName == "UnityEngine.Purchasing.UIFakeStoreWindow");
        private static void Click(string method)
        {
            var dialog = Dialog();
            Assert.That(dialog, Is.Not.Null);
            dialog.GetType().GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(dialog, null);
        }
        private static IEnumerator WaitFor(Func<bool> condition)
        {
            var until = Time.realtimeSinceStartup + 20;
            while (!condition() && Time.realtimeSinceStartup < until) yield return null;
            Assert.That(condition(), Is.True, "Timed out waiting for Unity Fake Store.");
        }
    }
}
#endif
