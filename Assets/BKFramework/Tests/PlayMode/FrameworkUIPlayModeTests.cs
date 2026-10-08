using System.Collections;
using System.Linq;
using BK.UI;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using VContainer;
using VContainer.Unity;

namespace BK.Tests
{
    public sealed class FrameworkUIPlayModeTests
    {
        [UnityTest]
        public IEnumerator MessagePopupToastAndDimWorkThroughTheAppScope()
        {
            yield return SceneManager.LoadSceneAsync("VisualBootstrap");

            LifetimeScope scope = null;
            var deadline = Time.realtimeSinceStartup + 20f;
            while (scope == null && Time.realtimeSinceStartup < deadline)
            {
                scope = Object.FindFirstObjectByType<LifetimeScope>();
                yield return null;
            }
            Assert.That(scope, Is.Not.Null, "bootstrap scene must contain the app scope");

            var messages = scope.Container.Resolve<IMessageService>();
            var dim = scope.Container.Resolve<IPopupDim>();

            deadline = Time.realtimeSinceStartup + 20f;
            while (Object.FindFirstObjectByType<UIRoot>() == null && Time.realtimeSinceStartup < deadline)
                yield return null;
            yield return new WaitForSecondsRealtime(1f);

            bool? result = null;
            messages.ConfirmAsync("Title", "Body", "Yes", "No").ContinueWith(r => result = r).Forget();

            MessagePopupView popup = null;
            deadline = Time.realtimeSinceStartup + 10f;
            while (popup == null && Time.realtimeSinceStartup < deadline)
            {
                popup = Object.FindFirstObjectByType<MessagePopupView>();
                yield return null;
            }
            Assert.That(popup, Is.Not.Null, "message popup prefab must load from Addressables");
            yield return new WaitForSecondsRealtime(0.3f);
            Assert.That(dim.IsActive.CurrentValue, Is.True, "dimmed view acquires the popup dim");

            var confirm = popup.GetComponentsInChildren<Button>(true).First(b => b.name == "Confirm");
            confirm.onClick.Invoke();
            deadline = Time.realtimeSinceStartup + 5f;
            while (result == null && Time.realtimeSinceStartup < deadline)
                yield return null;
            Assert.That(result, Is.True);
            yield return new WaitForSecondsRealtime(0.5f);
            Assert.That(dim.IsActive.CurrentValue, Is.False);

            messages.Toast("hello");
            ToastView toast = null;
            deadline = Time.realtimeSinceStartup + 5f;
            while (toast == null && Time.realtimeSinceStartup < deadline)
            {
                toast = Object.FindFirstObjectByType<ToastView>();
                yield return null;
            }
            Assert.That(toast, Is.Not.Null, "toast prefab must load from Addressables");
        }
    }
}
