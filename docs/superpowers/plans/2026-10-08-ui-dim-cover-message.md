# UI 계층: 팝업 딤 · 스크린 커버(리빌/씬 전환) · 메시지/토스트 구현 계획

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** sf의 PopupDim(ref-count 모달 딤), StageRevealCoordinator(단일 풀스크린 커버 + 준비 완료 대기), UIMessageManager(1/2버튼 메시지, 토스트 큐)와 씬 전환 페이드를 BK.UI 위에 재구현한다. 비주얼은 프레임워크가 프리팹에 의존하지 않도록 딤/커버는 런타임 생성, 메시지/토스트 프리팹은 에디터 메뉴로 생성해 Addressables에 등록한다.

**Architecture:** 순수 로직(`DimRefCounter`, `ScreenCover`, `MessageService`)과 Unity 뷰(`PopupDimView`, `ScreenCoverView`, `MessagePopupView`, `ToastView`)를 인터페이스로 분리해 EditMode에서 로직을 검증한다. `UIRoot`에 보조 캔버스(`PopupDim` 299, `Cover` 450)를 추가한다. `UIService`는 `IDimmedView`를 구현한 뷰를 열고 닫을 때 딤을 자동 Acquire/Release 한다. `ISceneFlow`가 커버→씬 전환→리빌을 한 호출로 묶는다. sf의 StagePreloaderRegistry는 스테이지 개념이라 D(메타 루프)로 미룬다.

**Tech Stack:** Unity 6000.3.21f1, VContainer, UniTask, R3, TextMeshPro(ugui 2.0 내장), Addressables, NUnit EditMode/PlayMode, 배치모드 검증(명령은 `2026-10-08-save-options-back.md` 상단과 동일, `addmeta.py`는 스크래치패드에 있음).

**사전 확인된 사실:**
- `UIService` 생성자를 프레임워크 밖에서 호출하는 곳 없음(시그니처 변경 안전).
- TMP 기본 폰트 `LiberationSans SDF`가 `Assets/TextMesh Pro/Resources`에 있어 `TextMeshProUGUI`는 폰트 지정 없이 동작.
- `VisualBootstrap` 씬이 EditorBuildSettings에 있고 Bounce PlayMode 테스트가 `SceneManager.LoadSceneAsync("VisualBootstrap")`로 부팅한다. 루트 스코프는 `IntegratedProjectScope : AppLifetimeScope`.
- Addressables 플레이모드 빌더 인덱스 3(기존 Bounce 테스트가 통과하므로 에디터 내 로드 경로는 동작).
- 기존 테스트: EditMode 32, PlayMode 11.

---

### Task 1: UIRoot 보조 캔버스 + 팝업 딤

**Files:**
- Modify: `Assets/BKFramework/Runtime/UI/UIRoot.cs` (보조 캔버스 API)
- Modify: `Assets/BKFramework/Runtime/UI/BK.UI.asmdef` (references에 `Unity.TextMeshPro` 추가 — Task 3에서 사용)
- Create: `Assets/BKFramework/Runtime/UI/Dim/DimLevel.cs`, `IDimView.cs`, `IDimScheduler.cs`, `DimRefCounter.cs`, `UniTaskDimScheduler.cs`, `PopupDimView.cs`, `IPopupDim.cs`, `PopupDim.cs`, `IDimmedView.cs`
- Modify: `Assets/BKFramework/Runtime/UI/UIViewBase.cs` (`IDimmedView` 구현, `_dim` 필드)
- Modify: `Assets/BKFramework/Runtime/UI/UIService.cs` (생성자에 `IPopupDim`, open/close 훅)
- Test: `Assets/BKFramework/Tests/EditMode/DimRefCounterTests.cs`

- [ ] **Step 1: 실패 테스트 — DimRefCounterTests.cs** (sf 테스트 이식)

```csharp
using System.Threading.Tasks;
using BK.UI;
using Cysharp.Threading.Tasks;
using NUnit.Framework;

namespace BK.Tests
{
    public sealed class DimRefCounterTests
    {
        private sealed class View : IDimView
        {
            public int Shows, Hides; public DimLevel Last = DimLevel.None;
            public void Show() => Shows++;
            public void Hide() => Hides++;
            public void ApplyLevel(DimLevel level) => Last = level;
        }
        private sealed class Scheduler : IDimScheduler
        {
            public UniTaskCompletionSource Grace = new(), Timeout = new();
            public UniTask GraceDelayAsync() => Grace.Task;
            public UniTask TransitionTimeoutAsync() => Timeout.Task;
        }

        [Test] public void AcquireManyShowsOnce()
        {
            var v = new View(); var c = new DimRefCounter(v, new Scheduler());
            c.Acquire(DimLevel.Soft); c.Acquire(DimLevel.Soft); c.Acquire(DimLevel.Soft);
            Assert.That(v.Shows, Is.EqualTo(1)); Assert.That(c.Count, Is.EqualTo(3));
        }

        [Test] public async Task ReleaseToZeroHidesAfterGrace()
        {
            var v = new View(); var s = new Scheduler(); var c = new DimRefCounter(v, s);
            c.Acquire(DimLevel.Soft); c.Release();
            Assert.That(v.Hides, Is.Zero);
            s.Grace.TrySetResult(); await c.Pending;
            Assert.That(v.Hides, Is.EqualTo(1));
        }

        [Test] public async Task ReacquireWithinGraceCancelsHide()
        {
            var v = new View(); var s = new Scheduler(); var c = new DimRefCounter(v, s);
            c.Acquire(DimLevel.Soft); c.Release(); c.Acquire(DimLevel.Soft);
            s.Grace.TrySetResult(); await c.Pending;
            Assert.That(v.Hides, Is.Zero); Assert.That(v.Shows, Is.EqualTo(1));
        }

        [Test] public async Task HoldForTransitionKeepsDimUntilNextAcquire()
        {
            var v = new View(); var s = new Scheduler(); var c = new DimRefCounter(v, s);
            c.Acquire(DimLevel.Soft); c.HoldForTransition(); c.Release();
            s.Grace.TrySetResult(); await UniTask.Yield();
            Assert.That(v.Hides, Is.Zero);
            c.Acquire(DimLevel.Soft);
            Assert.That(v.Shows, Is.EqualTo(1));
        }

        [Test] public async Task HoldTimeoutWithoutAcquireHides()
        {
            var v = new View(); var s = new Scheduler(); var c = new DimRefCounter(v, s);
            c.Acquire(DimLevel.Soft); c.HoldForTransition(); c.Release();
            s.Timeout.TrySetResult(); await c.Pending;
            Assert.That(v.Hides, Is.EqualTo(1));
        }

        [Test] public void ExplicitLevelWhileVisibleReappliesLevel()
        {
            var v = new View(); var c = new DimRefCounter(v, new Scheduler());
            c.Acquire(DimLevel.Soft); c.Acquire(DimLevel.Deep);
            Assert.That(v.Last, Is.EqualTo(DimLevel.Deep)); Assert.That(v.Shows, Is.EqualTo(1));
        }

        [Test] public void UnbalancedReleaseIsIgnoredAndResetHides()
        {
            var v = new View(); var c = new DimRefCounter(v, new Scheduler());
            c.Release(); Assert.That(c.Count, Is.Zero);
            c.Acquire(DimLevel.Soft); c.Acquire(DimLevel.Soft); c.Reset();
            Assert.That(c.Count, Is.Zero); Assert.That(v.Hides, Is.EqualTo(1));
        }
    }
}
```

- [ ] **Step 2: 로직 파일 작성**

`Dim/DimLevel.cs`:

```csharp
namespace BK.UI
{
    /// <summary>Darkness of the modal dim behind popups. None means "no dim for this view".</summary>
    public enum DimLevel { None = 0, Soft = 1, Medium = 2, Deep = 3, Deeper = 4, Full = 5 }
}
```

`Dim/IDimView.cs`:

```csharp
namespace BK.UI
{
    /// <summary>Visual side of the dim, abstracted so the ref-count logic is testable.</summary>
    public interface IDimView
    {
        void Show();
        void Hide();
        void ApplyLevel(DimLevel level);
    }
}
```

`Dim/IDimScheduler.cs`:

```csharp
using Cysharp.Threading.Tasks;

namespace BK.UI
{
    public interface IDimScheduler
    {
        /// <summary>Short wait after the count hits zero so a popup chain does not flicker.</summary>
        UniTask GraceDelayAsync();
        /// <summary>Upper bound for a transition hold that never gets its next Acquire.</summary>
        UniTask TransitionTimeoutAsync();
    }
}
```

`Dim/DimRefCounter.cs`:

```csharp
using Cysharp.Threading.Tasks;

namespace BK.UI
{
    /// <summary>
    /// Pure dim lifetime: ref-count, transition bridge, deferred hide, reset.
    /// A generation counter invalidates in-flight async hides on re-acquire/reset.
    /// </summary>
    public sealed class DimRefCounter
    {
        private readonly IDimView _view;
        private readonly IDimScheduler _scheduler;
        private int _count, _gen;
        private bool _pending, _visible;
        private DimLevel _level = DimLevel.Soft;

        public DimRefCounter(IDimView view, IDimScheduler scheduler) { _view = view; _scheduler = scheduler; }

        public int Count => _count;
        public bool IsVisible => _visible;
        /// <summary>Most recent deferred hide/timeout, for tests. Completed when idle.</summary>
        public UniTask Pending { get; private set; } = UniTask.CompletedTask;

        public void Acquire(DimLevel level)
        {
            if (level == DimLevel.None) return;
            _level = level;
            _pending = false;
            _gen++;
            _count++;
            if (!_visible) { _visible = true; _view.ApplyLevel(_level); _view.Show(); }
            else _view.ApplyLevel(_level);
        }

        public void Release()
        {
            if (_count <= 0) return;
            if (--_count == 0 && !_pending)
                Pending = HideAfterGraceAsync(_gen);
        }

        public void HoldForTransition()
        {
            _pending = true;
            Pending = WatchTimeoutAsync(++_gen);
        }

        public void Reset()
        {
            _count = 0; _pending = false; _gen++; _level = DimLevel.Soft;
            if (_visible) { _visible = false; _view.Hide(); }
        }

        private async UniTask HideAfterGraceAsync(int gen)
        {
            await _scheduler.GraceDelayAsync();
            if (_count == 0 && !_pending && gen == _gen && _visible) { _visible = false; _view.Hide(); _level = DimLevel.Soft; }
        }

        private async UniTask WatchTimeoutAsync(int gen)
        {
            await _scheduler.TransitionTimeoutAsync();
            if (gen != _gen) return;
            _pending = false;
            if (_count == 0 && _visible) { _visible = false; _view.Hide(); }
        }
    }
}
```

`Dim/UniTaskDimScheduler.cs`:

```csharp
using System;
using Cysharp.Threading.Tasks;

namespace BK.UI
{
    public sealed class UniTaskDimScheduler : IDimScheduler
    {
        private readonly int _graceFrames;
        private readonly float _timeoutSeconds;
        public UniTaskDimScheduler(int graceFrames = 1, float timeoutSeconds = 2f)
        { _graceFrames = graceFrames < 1 ? 1 : graceFrames; _timeoutSeconds = timeoutSeconds; }
        public UniTask GraceDelayAsync() => UniTask.DelayFrame(_graceFrames, PlayerLoopTiming.Update);
        public UniTask TransitionTimeoutAsync() => UniTask.Delay(TimeSpan.FromSeconds(_timeoutSeconds), ignoreTimeScale: true);
    }
}
```

`Dim/IPopupDim.cs`:

```csharp
using R3;

namespace BK.UI
{
    /// <summary>
    /// Shared modal dim under the Popup layer. Views that implement <see cref="IDimmedView"/>
    /// acquire it automatically; flows that span several popups can hold it explicitly.
    /// </summary>
    public interface IPopupDim
    {
        void Acquire(DimLevel level = DimLevel.Soft);
        void Release();
        /// <summary>Keep the dim through a gap (e.g. popup A closes, popup B loads) until the next Acquire or a timeout.</summary>
        void HoldForTransition();
        /// <summary>Drop everything (scene change).</summary>
        void Reset();
        ReadOnlyReactiveProperty<bool> IsActive { get; }
    }
}
```

`Dim/IDimmedView.cs`:

```csharp
namespace BK.UI
{
    /// <summary>Opt-in: the UI service acquires the popup dim at this level while the view is open.</summary>
    public interface IDimmedView
    {
        DimLevel DimLevel { get; }
    }
}
```

`Dim/PopupDimView.cs` (런타임 생성, 프리팹 없음):

```csharp
using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;

namespace BK.UI
{
    /// <summary>Full-screen black image whose darkness is the Image alpha and whose fade is the CanvasGroup alpha.</summary>
    [RequireComponent(typeof(CanvasGroup), typeof(Image))]
    public sealed class PopupDimView : MonoBehaviour, IDimView
    {
        public float FadeInSeconds = 0.2f;
        public float FadeOutSeconds = 0.2f;
        public float LevelTweenSeconds = 0.15f;

        private CanvasGroup _group;
        private Image _image;
        private CancellationTokenSource _fadeCts, _levelCts;

        public static PopupDimView Create(Transform parent)
        {
            var go = new GameObject("PopupDim", typeof(RectTransform), typeof(CanvasGroup), typeof(Image), typeof(PopupDimView));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.offsetMin = rt.offsetMax = Vector2.zero;
            var view = go.GetComponent<PopupDimView>();
            view._group = go.GetComponent<CanvasGroup>();
            view._image = go.GetComponent<Image>();
            view._image.color = new Color(0f, 0f, 0f, AlphaFor(DimLevel.Soft));
            view._image.raycastTarget = true;
            view._group.alpha = 0f;
            go.SetActive(false);
            return view;
        }

        public static float AlphaFor(DimLevel level) => level switch
        {
            DimLevel.Full => 1f, DimLevel.Deeper => 0.99f, DimLevel.Deep => 0.95f,
            DimLevel.Medium => 0.90f, DimLevel.Soft => 0.85f, _ => 0f,
        };

        public void ApplyLevel(DimLevel level)
        {
            if (this == null) return;
            var target = AlphaFor(level);
            Cancel(ref _levelCts);
            if (!gameObject.activeSelf || LevelTweenSeconds <= 0f) { _image.color = new Color(0, 0, 0, target); return; }
            _levelCts = new CancellationTokenSource();
            TweenAsync(_image.color.a, target, LevelTweenSeconds, a => _image.color = new Color(0, 0, 0, a), _levelCts.Token).Forget();
        }

        public void Show() => Fade(1f, FadeInSeconds, activate: true);
        public void Hide() => Fade(0f, FadeOutSeconds, activate: false);

        private void Fade(float target, float seconds, bool activate)
        {
            if (this == null) return;
            Cancel(ref _fadeCts);
            if (activate && !gameObject.activeSelf) gameObject.SetActive(true);
            _fadeCts = new CancellationTokenSource();
            var token = _fadeCts.Token;
            TweenAsync(_group.alpha, target, seconds, a => _group.alpha = a, token)
                .ContinueWith(() => { if (target <= 0f && !token.IsCancellationRequested) gameObject.SetActive(false); })
                .Forget();
        }

        private static async UniTask TweenAsync(float from, float to, float seconds, Action<float> apply, CancellationToken ct)
        {
            try
            {
                var elapsed = 0f;
                while (elapsed < seconds)
                {
                    elapsed += Time.unscaledDeltaTime;
                    apply(Mathf.Lerp(from, to, Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / seconds))));
                    await UniTask.Yield(PlayerLoopTiming.Update, ct);
                }
                apply(to);
            }
            catch (OperationCanceledException) { }
        }

        private static void Cancel(ref CancellationTokenSource cts)
        {
            if (cts == null) return;
            try { cts.Cancel(); } catch (ObjectDisposedException) { }
            cts.Dispose(); cts = null;
        }

        private void OnDestroy() { Cancel(ref _fadeCts); Cancel(ref _levelCts); }
    }
}
```

`Dim/PopupDim.cs`:

```csharp
using System;
using R3;

namespace BK.UI
{
    /// <inheritdoc cref="IPopupDim"/>
    public sealed class PopupDim : IPopupDim, IDisposable
    {
        private readonly ReactiveProperty<bool> _isActive = new(false);
        private readonly DimRefCounter _counter;

        public PopupDim(UIRoot root)
            : this(new Notifying(PopupDimView.Create(root.GetAuxiliaryRoot(UIRoot.DimCanvas, (int)UILayer.Popup - 1))), new UniTaskDimScheduler()) { }

        internal PopupDim(IDimView view, IDimScheduler scheduler)
        {
            if (view is Notifying n) n.Active = _isActive;
            _counter = new DimRefCounter(view, scheduler);
        }

        public ReadOnlyReactiveProperty<bool> IsActive => _isActive;
        public void Acquire(DimLevel level = DimLevel.Soft) => _counter.Acquire(level);
        public void Release() => _counter.Release();
        public void HoldForTransition() => _counter.HoldForTransition();
        public void Reset() => _counter.Reset();
        public void Dispose() => _isActive.Dispose();

        private sealed class Notifying : IDimView
        {
            private readonly IDimView _inner;
            public ReactiveProperty<bool> Active;
            public Notifying(IDimView inner) => _inner = inner;
            public void Show() { if (Active != null) Active.Value = true; _inner.Show(); }
            public void Hide() { if (Active != null) Active.Value = false; _inner.Hide(); }
            public void ApplyLevel(DimLevel level) => _inner.ApplyLevel(level);
        }
    }
}
```

- [ ] **Step 3: UIRoot 보조 캔버스**

`UIRoot.cs`에 추가:

```csharp
        public const string DimCanvas = "PopupDim";
        public const string CoverCanvas = "Cover";
        private readonly Dictionary<string, Transform> _auxRoots = new();

        /// <summary>A canvas outside the view stacks (dim, cover). Created on first use, same scaler as the layers.</summary>
        public Transform GetAuxiliaryRoot(string name, int sortingOrder)
        {
            if (_auxRoots.TryGetValue(name, out var existing)) return existing;
            var root = BuildCanvas(name, sortingOrder);
            _auxRoots.Add(name, root);
            return root;
        }
```

그리고 `BuildLayers`의 캔버스 생성 본문을 `private Transform BuildCanvas(string name, int sortingOrder)`로 추출해 양쪽이 쓴다.

- [ ] **Step 4: UIViewBase / UIService 훅**

`UIViewBase`: `public abstract class UIViewBase : MonoBehaviour, IUIView, IDimmedView`, 필드 `[SerializeField] private DimLevel _dim = DimLevel.None;`, `public DimLevel DimLevel => _dim;`.

`UIService`: 생성자에 `IPopupDim dim` 추가(필드 `_dim`). `OpenInternalAsync`에서 `applyArgs?.Invoke(view);` 다음에:

```csharp
            if (view is IDimmedView dimmed && dimmed.DimLevel != DimLevel.None)
                _dim.Acquire(dimmed.DimLevel);
```

`CloseAsync`에서 `Assets.ReleaseInstance(instance);` 다음에:

```csharp
            if (view is IDimmedView dimmed && dimmed.DimLevel != DimLevel.None)
                _dim.Release();
```

`Dispose`에서 `_dim.Reset()`을 마지막에 호출.

- [ ] **Step 5: AppLifetimeScope에 `builder.Register<PopupDim>(Lifetime.Singleton).As<IPopupDim>();`를 `UIRoot` 등록 다음, `UIService` 등록 전에 추가.** (Task 4에서 나머지 등록과 함께 커밋해도 되지만 컴파일을 위해 지금 넣는다.)

- [ ] **Step 6: meta → 컴파일 → 테스트 (`DimRefCounterTests` 7/7, 전체 EditMode 39)** → 커밋

```bash
git add Assets/BKFramework/Runtime/UI Assets/BKFramework/Runtime/Composition Assets/BKFramework/Tests
git commit -m "feat(ui): add ref-counted popup dim with auto acquire for dimmed views"
```

---

### Task 2: 스크린 커버(리빌) + 씬 플로우

**Files:**
- Create: `Assets/BKFramework/Runtime/UI/Cover/IRevealReady.cs`, `ICoverView.cs`, `IScreenCover.cs`, `ScreenCover.cs`, `ScreenCoverView.cs`
- Create: `Assets/BKFramework/Runtime/UI/Flow/ISceneFlow.cs`, `SceneFlow.cs`
- Test: `Assets/BKFramework/Tests/EditMode/ScreenCoverTests.cs`

- [ ] **Step 1: 실패 테스트 — ScreenCoverTests.cs**

```csharp
using System.Threading;
using System.Threading.Tasks;
using BK.UI;
using Cysharp.Threading.Tasks;
using NUnit.Framework;

namespace BK.Tests
{
    public sealed class ScreenCoverTests
    {
        private sealed class View : ICoverView
        {
            public bool Visible; public float Alpha; public int Fades;
            public void SetVisible(bool visible) => Visible = visible;
            public void SetAlpha(float alpha) => Alpha = alpha;
            public UniTask FadeAsync(float to, float seconds, CancellationToken ct) { Fades++; Alpha = to; return UniTask.CompletedTask; }
        }
        private sealed class Ready : IRevealReady
        {
            public readonly UniTaskCompletionSource Source = new();
            public bool IsReady => Source.Task.Status.IsCompleted();
            public UniTask ReadyTask => Source.Task;
        }

        [Test] public async Task HoldShowsAndReleaseHidesAfterFade()
        {
            var v = new View(); var c = new ScreenCover(v, fadeOutSeconds: 0.1f, readyTimeoutSeconds: 1f);
            c.Hold();
            Assert.That(c.IsActive, Is.True); Assert.That(v.Visible, Is.True); Assert.That(v.Alpha, Is.EqualTo(1f));
            var waited = false; var wait = c.WaitUntilReleasedAsync().ContinueWith(() => waited = true);
            await c.ReleaseAsync();
            await wait;
            Assert.That(waited, Is.True); Assert.That(c.IsActive, Is.False); Assert.That(v.Visible, Is.False); Assert.That(v.Fades, Is.EqualTo(1));
        }

        [Test] public async Task ReleaseWhenReadyWaitsForRegisteredReady()
        {
            var v = new View(); var c = new ScreenCover(v, 0f, readyTimeoutSeconds: 5f);
            var ready = new Ready(); c.RegisterRevealReady(ready);
            c.Hold();
            var release = c.ReleaseWhenReadyAsync();
            await UniTask.Yield();
            Assert.That(c.IsActive, Is.True, "still held until ready");
            ready.Source.TrySetResult();
            await release;
            Assert.That(c.IsActive, Is.False);
        }

        [Test] public async Task ReleaseWhenReadyTimesOutWithoutReady()
        {
            var v = new View(); var c = new ScreenCover(v, 0f, readyTimeoutSeconds: 0.05f);
            c.RegisterRevealReady(new Ready());
            c.Hold();
            await c.ReleaseWhenReadyAsync();
            Assert.That(c.IsActive, Is.False);
        }

        [Test] public async Task ReleaseWithoutHoldIsNoOpAndUnregisterClearsReady()
        {
            var v = new View(); var c = new ScreenCover(v, 0f, 1f);
            await c.ReleaseAsync();
            Assert.That(v.Fades, Is.Zero);
            var ready = new Ready(); c.RegisterRevealReady(ready); c.UnregisterRevealReady(ready);
            c.Hold(); await c.ReleaseWhenReadyAsync();   // no ready registered → fallback path
            Assert.That(c.IsActive, Is.False);
        }
    }
}
```

- [ ] **Step 2: 구현**

`Cover/IRevealReady.cs`:

```csharp
using Cysharp.Threading.Tasks;

namespace BK.UI
{
    /// <summary>Optional: a scene/core publishes "first frame is ready to show" so the cover lifts exactly then.</summary>
    public interface IRevealReady
    {
        bool IsReady { get; }
        UniTask ReadyTask { get; }
    }
}
```

`Cover/ICoverView.cs`:

```csharp
using System.Threading;
using Cysharp.Threading.Tasks;

namespace BK.UI
{
    public interface ICoverView
    {
        void SetVisible(bool visible);
        void SetAlpha(float alpha);
        UniTask FadeAsync(float to, float seconds, CancellationToken cancellationToken);
    }
}
```

`Cover/IScreenCover.cs`:

```csharp
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
        void Hold();
        /// <summary>Fade in from transparent (scene exit), then stay held.</summary>
        UniTask HoldAsync(float fadeInSeconds, CancellationToken cancellationToken = default);
        UniTask ReleaseAsync(CancellationToken cancellationToken = default);
        /// <summary>Waits for the registered <see cref="IRevealReady"/> (or a short fallback), then fades out.</summary>
        UniTask ReleaseWhenReadyAsync(CancellationToken cancellationToken = default);
        UniTask WaitUntilReleasedAsync(CancellationToken cancellationToken = default);
        Observable<Unit> Released { get; }
        void RegisterRevealReady(IRevealReady ready);
        void UnregisterRevealReady(IRevealReady ready);
    }
}
```

`Cover/ScreenCover.cs`:

```csharp
using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using R3;
using BK.Core.Diagnostics;

namespace BK.UI
{
    /// <inheritdoc cref="IScreenCover"/>
    public sealed class ScreenCover : IScreenCover, IDisposable
    {
        private readonly ICoverView _view;
        private readonly float _fadeOutSeconds;
        private readonly float _readyTimeoutSeconds;
        private readonly Subject<Unit> _released = new();
        private IRevealReady _ready;
        private UniTaskCompletionSource _releaseSource;

        public ScreenCover(ICoverView view, float fadeOutSeconds = 0.15f, float readyTimeoutSeconds = 3f)
        { _view = view; _fadeOutSeconds = fadeOutSeconds; _readyTimeoutSeconds = readyTimeoutSeconds; }

        public bool IsActive { get; private set; }
        public Observable<Unit> Released => _released;

        public void RegisterRevealReady(IRevealReady ready) => _ready = ready;
        public void UnregisterRevealReady(IRevealReady ready) { if (ReferenceEquals(_ready, ready)) _ready = null; }

        public void Hold()
        {
            _view.SetVisible(true);
            _view.SetAlpha(1f);
            if (IsActive) return;
            IsActive = true;
            _releaseSource = new UniTaskCompletionSource();
        }

        public async UniTask HoldAsync(float fadeInSeconds, CancellationToken cancellationToken = default)
        {
            if (IsActive) return;
            IsActive = true;
            _releaseSource = new UniTaskCompletionSource();
            _view.SetVisible(true);
            _view.SetAlpha(0f);
            await _view.FadeAsync(1f, fadeInSeconds, cancellationToken);
        }

        public async UniTask ReleaseAsync(CancellationToken cancellationToken = default)
        {
            if (!IsActive) return;
            try { await _view.FadeAsync(0f, _fadeOutSeconds, cancellationToken); }
            finally { Finish(); }
        }

        public async UniTask ReleaseWhenReadyAsync(CancellationToken cancellationToken = default)
        {
            if (!IsActive) return;
            try
            {
                var ready = _ready;
                if (ready != null)
                {
                    using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                    timeout.CancelAfter(TimeSpan.FromSeconds(_readyTimeoutSeconds));
                    try { await ready.ReadyTask.AttachExternalCancellation(timeout.Token); }
                    catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
                    { BKLog.Warn(BKLog.UI, $"reveal ready timed out after {_readyTimeoutSeconds}s; releasing cover"); }
                }
                else
                {
                    try { await UniTask.NextFrame(PlayerLoopTiming.LastPostLateUpdate, cancellationToken); }
                    catch (OperationCanceledException) { }
                }
                await _view.FadeAsync(0f, _fadeOutSeconds, cancellationToken);
            }
            finally { Finish(); }
        }

        public UniTask WaitUntilReleasedAsync(CancellationToken cancellationToken = default)
        {
            var source = _releaseSource;
            return !IsActive || source == null ? UniTask.CompletedTask : source.Task.AttachExternalCancellation(cancellationToken);
        }

        private void Finish()
        {
            if (!IsActive) return;
            _view.SetVisible(false);
            IsActive = false;
            _releaseSource?.TrySetResult();
            _releaseSource = null;
            _released.OnNext(Unit.Default);
        }

        public void Dispose() { _view.SetVisible(false); _released.Dispose(); }
    }
}
```

`Cover/ScreenCoverView.cs`:

```csharp
using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;

namespace BK.UI
{
    /// <summary>Runtime-built opaque black cover on the Cover canvas (above Popup/Overlay, below System).</summary>
    [RequireComponent(typeof(CanvasGroup), typeof(Image))]
    public sealed class ScreenCoverView : MonoBehaviour, ICoverView
    {
        private CanvasGroup _group;

        public static ScreenCoverView Create(UIRoot root)
        {
            var parent = root.GetAuxiliaryRoot(UIRoot.CoverCanvas, (int)UILayer.System - 50);
            var go = new GameObject("ScreenCover", typeof(RectTransform), typeof(CanvasGroup), typeof(Image), typeof(ScreenCoverView));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.offsetMin = rt.offsetMax = Vector2.zero;
            var image = go.GetComponent<Image>();
            image.color = Color.black; image.raycastTarget = true;
            var view = go.GetComponent<ScreenCoverView>();
            view._group = go.GetComponent<CanvasGroup>();
            go.SetActive(false);
            return view;
        }

        public void SetVisible(bool visible) { if (this != null) gameObject.SetActive(visible); }
        public void SetAlpha(float alpha) { if (this != null) _group.alpha = alpha; }

        public async UniTask FadeAsync(float to, float seconds, CancellationToken cancellationToken)
        {
            if (this == null) return;
            var from = _group.alpha;
            try
            {
                await UniTask.NextFrame(PlayerLoopTiming.LastPostLateUpdate, cancellationToken);
                var elapsed = 0f;
                while (elapsed < seconds)
                {
                    elapsed += Time.unscaledDeltaTime;
                    _group.alpha = Mathf.Lerp(from, to, Mathf.Clamp01(elapsed / seconds));
                    await UniTask.Yield(PlayerLoopTiming.Update, cancellationToken);
                }
            }
            catch (OperationCanceledException) { }
            if (this != null) _group.alpha = to;
        }
    }
}
```

`Flow/ISceneFlow.cs` / `Flow/SceneFlow.cs`:

```csharp
using System.Threading;
using Cysharp.Threading.Tasks;
using BK.Assets;
using BK.Scene;

namespace BK.UI
{
    /// <summary>Cover → close content views → scene transition → reveal, in one call.</summary>
    public interface ISceneFlow
    {
        bool IsBusy { get; }
        UniTask<ISceneScope> TransitionAsync(AssetKey scene, CancellationToken cancellationToken = default);
    }
}
```

```csharp
using System.Threading;
using Cysharp.Threading.Tasks;
using BK.Assets;
using BK.Scene;

namespace BK.UI
{
    /// <inheritdoc cref="ISceneFlow"/>
    public sealed class SceneFlow : ISceneFlow
    {
        private readonly ISceneService _scenes;
        private readonly IUIService _ui;
        private readonly IScreenCover _cover;
        private readonly IPopupDim _dim;
        private readonly float _fadeInSeconds;

        public SceneFlow(ISceneService scenes, IUIService ui, IScreenCover cover, IPopupDim dim, float fadeInSeconds = 0.15f)
        { _scenes = scenes; _ui = ui; _cover = cover; _dim = dim; _fadeInSeconds = fadeInSeconds; }

        public bool IsBusy { get; private set; }

        public async UniTask<ISceneScope> TransitionAsync(AssetKey scene, CancellationToken cancellationToken = default)
        {
            if (IsBusy) throw new System.InvalidOperationException("A scene flow is already running.");
            IsBusy = true;
            try
            {
                await _cover.HoldAsync(_fadeInSeconds, cancellationToken);
                _dim.Reset();
                await _ui.CloseAllAsync(UILayer.Popup, cancellationToken);
                await _ui.CloseAllAsync(UILayer.Content, cancellationToken);
                var scope = await _scenes.TransitionToAsync(scene, null, cancellationToken);
                await _cover.ReleaseWhenReadyAsync(cancellationToken);
                return scope;
            }
            finally { IsBusy = false; }
        }
    }
}
```

- [ ] **Step 3: AppLifetimeScope 등록** (리뷰 반영: `SceneFlow`는 float 기본 인자가 있어 VContainer가 자동 해석 못 함 → 팩토리 람다. `PopupDim` public 생성자에는 `[Inject]` 필수 — VContainer는 internal 생성자도 후보에 넣고 매개변수가 많은 쪽을 고른다.)

```csharp
            builder.Register<IScreenCover>(container => new ScreenCover(ScreenCoverView.Create(container.Resolve<UIRoot>())), Lifetime.Singleton);
            builder.Register<ISceneFlow>(container => new SceneFlow(container.Resolve<ISceneService>(), container.Resolve<IUIService>(), container.Resolve<IScreenCover>(), container.Resolve<IPopupDim>()), Lifetime.Singleton);
```

리뷰 반영 2: `ScreenCover`의 ready 대기는 `CancelAfter`(스레드풀 타이머) 대신 `UniTask.WhenAny(ready.ReadyTask, UniTask.Delay(...))`로, fallback은 `UniTask.NextFrame`(EditMode에서 영원히 안 끝남) 대신 `UniTask.Yield(PlayerLoopTiming.LastPostLateUpdate)`로 구현한다.

- [ ] **Step 4: meta → 컴파일 → 테스트 (`ScreenCoverTests` 4/4)** → 커밋 `feat(ui): add screen cover with reveal-ready gating and SceneFlow`

---

### Task 3: 메시지 팝업 / 토스트

**Files:**
- Create: `Assets/BKFramework/Runtime/UI/Message/MessageRequest.cs`, `IMessagePresenter.cs`, `IMessageService.cs`, `MessageService.cs`, `UIMessagePresenter.cs`, `MessagePopupView.cs`, `ToastView.cs`
- Create: `Assets/BKFramework/Editor/BK.Editor.asmdef`, `Assets/BKFramework/Editor/FrameworkUISetup.cs`
- Create (생성 결과): `Assets/BKFramework/Content/UI/MessagePopup.prefab`, `Toast.prefab` + Addressables 그룹 `BK Framework`
- Modify: `FrameworkSettings.cs` (주소/토스트 시간), `AppLifetimeScope.cs`
- Test: `Assets/BKFramework/Tests/EditMode/MessageServiceTests.cs`

- [ ] **Step 1: 실패 테스트 — MessageServiceTests.cs**

```csharp
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
            { Shown.Add(request.Body); Current = new UniTaskCompletionSource<bool>(); return Current.Task; }
            public UniTask ShowToastAsync(string text, CancellationToken ct)
            { Toasts.Add(text); ToastDone = new UniTaskCompletionSource(); return ToastDone.Task; }
        }

        [Test] public async Task MessagesAreShownOneAtATimeInOrder()
        {
            var p = new Presenter(); using var s = new MessageService(p);
            var a = s.ShowAsync("T", "A", "OK");
            var b = s.ConfirmAsync("T", "B", "Yes", "No");
            await UniTask.Yield();
            Assert.That(p.Shown, Is.EqualTo(new[] { "A" }));
            p.Current.TrySetResult(true); await a; await UniTask.Yield();
            Assert.That(p.Shown, Is.EqualTo(new[] { "A", "B" }));
            p.Current.TrySetResult(false);
            Assert.That(await b, Is.False);
        }

        [Test] public async Task CollapsedToastSkipsDuplicatesWhileQueuedOrShowing()
        {
            var p = new Presenter(); using var s = new MessageService(p);
            s.Toast("same", collapseDuplicate: true);
            s.Toast("same", collapseDuplicate: true);
            s.Toast("other");
            s.Toast("other");
            await UniTask.Yield();
            Assert.That(p.Toasts, Is.EqualTo(new[] { "same" }));
            p.ToastDone.TrySetResult(); await UniTask.Yield(); await UniTask.Yield();
            p.ToastDone.TrySetResult(); await UniTask.Yield(); await UniTask.Yield();
            p.ToastDone.TrySetResult(); await UniTask.Yield();
            Assert.That(p.Toasts, Is.EqualTo(new[] { "same", "other", "other" }));
            s.Toast("same", collapseDuplicate: true);
            await UniTask.Yield();
            Assert.That(p.Toasts.Count, Is.EqualTo(4), "key is released after the toast finished");
        }
    }
}
```

- [ ] **Step 2: 런타임 구현**

`Message/MessageRequest.cs`:

```csharp
namespace BK.UI
{
    public sealed class MessageRequest
    {
        public string Title;
        public string Body;
        public string ConfirmText;
        /// <summary>Null means a single-button message.</summary>
        public string CancelText;
        /// <summary>No background tap / back to dismiss.</summary>
        public bool Forced;
        public bool IsConfirm => CancelText != null;
    }
}
```

`Message/IMessagePresenter.cs`:

```csharp
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
```

`Message/IMessageService.cs`:

```csharp
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
```

`Message/MessageService.cs`:

```csharp
using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;

namespace BK.UI
{
    /// <inheritdoc cref="IMessageService"/>
    public sealed class MessageService : IMessageService, IDisposable
    {
        private sealed class Pending { public MessageRequest Request; public UniTaskCompletionSource<bool> Result = new(); }
        private sealed class Toast_ { public string Text; public string CollapseKey; }

        private readonly IMessagePresenter _presenter;
        private readonly CancellationTokenSource _cts = new();
        private readonly Queue<Pending> _messages = new();
        private readonly Queue<Toast_> _toasts = new();
        private readonly HashSet<string> _collapsed = new();
        private bool _messageLoop, _toastLoop;

        public MessageService(IMessagePresenter presenter) => _presenter = presenter;

        public UniTask ShowAsync(string title, string body, string buttonText, bool forced = false)
            => Enqueue(new MessageRequest { Title = title, Body = body, ConfirmText = buttonText, Forced = forced }).AsUniTask();

        public UniTask<bool> ConfirmAsync(string title, string body, string confirmText, string cancelText)
            => Enqueue(new MessageRequest { Title = title, Body = body, ConfirmText = confirmText, CancelText = cancelText ?? "" });

        public void Toast(string text, bool collapseDuplicate = false)
        {
            if (collapseDuplicate && !_collapsed.Add(text)) return;
            _toasts.Enqueue(new Toast_ { Text = text, CollapseKey = collapseDuplicate ? text : null });
            if (!_toastLoop) RunToastsAsync().Forget();
        }

        private UniTask<bool> Enqueue(MessageRequest request)
        {
            var pending = new Pending { Request = request };
            _messages.Enqueue(pending);
            if (!_messageLoop) RunMessagesAsync().Forget();
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
            finally { _messageLoop = false; }
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
                    finally { if (toast.CollapseKey != null) _collapsed.Remove(toast.CollapseKey); }
                }
            }
            finally { _toastLoop = false; _collapsed.Clear(); }
        }

        public void Dispose() { _cts.Cancel(); _cts.Dispose(); }
    }
}
```

`Message/UIMessagePresenter.cs`:

```csharp
using System.Threading;
using Cysharp.Threading.Tasks;
using BK.Assets;

namespace BK.UI
{
    /// <summary>Presents messages through the UI service on the System layer.</summary>
    public sealed class UIMessagePresenter : IMessagePresenter
    {
        private readonly IUIService _ui;
        private readonly AssetKey _popupKey, _toastKey;
        private readonly float _toastSeconds;

        public UIMessagePresenter(IUIService ui, AssetKey popupKey, AssetKey toastKey, float toastSeconds)
        { _ui = ui; _popupKey = popupKey; _toastKey = toastKey; _toastSeconds = toastSeconds; }

        public async UniTask<bool> ShowAsync(MessageRequest request, CancellationToken cancellationToken)
        {
            var view = await _ui.OpenAsync<MessagePopupView, MessageRequest>(_popupKey, request, cancellationToken);
            try { return await view.Result.AttachExternalCancellation(cancellationToken); }
            finally { await _ui.CloseAsync(view); }
        }

        public async UniTask ShowToastAsync(string text, CancellationToken cancellationToken)
        {
            var view = await _ui.OpenAsync<ToastView, string>(_toastKey, text, cancellationToken);
            try { await UniTask.Delay(System.TimeSpan.FromSeconds(_toastSeconds), ignoreTimeScale: true, cancellationToken: cancellationToken); }
            finally { await _ui.CloseAsync(view); }
        }
    }
}
```

`Message/MessagePopupView.cs`:

```csharp
using System.Threading;
using Cysharp.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace BK.UI
{
    /// <summary>One- or two-button modal. Prefab generated by BK > Framework > Generate UI Prefabs.</summary>
    public sealed class MessagePopupView : UIViewBase, IUIView<MessageRequest>
    {
        [SerializeField] private TMP_Text _title;
        [SerializeField] private TMP_Text _body;
        [SerializeField] private Button _confirm;
        [SerializeField] private TMP_Text _confirmLabel;
        [SerializeField] private Button _cancel;
        [SerializeField] private TMP_Text _cancelLabel;
        [SerializeField] private Button _background;

        private readonly UniTaskCompletionSource<bool> _result = new();
        private MessageRequest _request;

        public UniTask<bool> Result => _result.Task;

        public void SetArgs(MessageRequest args) => _request = args;

        public override UniTask OnInitializeAsync(CancellationToken cancellationToken)
        {
            _title.text = _request.Title ?? "";
            _body.text = _request.Body ?? "";
            _confirmLabel.text = _request.ConfirmText ?? "OK";
            _cancel.gameObject.SetActive(_request.IsConfirm);
            if (_request.IsConfirm) _cancelLabel.text = _request.CancelText;
            _confirm.onClick.AddListener(() => _result.TrySetResult(true));
            _cancel.onClick.AddListener(() => _result.TrySetResult(false));
            _background.onClick.AddListener(() => { if (!_request.Forced) _result.TrySetResult(false); });
            return base.OnInitializeAsync(cancellationToken);
        }

        public override bool OnBackRequested()
        {
            if (!_request.Forced) _result.TrySetResult(false);
            return true; // the presenter closes us; never let the service pop us mid-await
        }

        public void Bind(TMP_Text title, TMP_Text body, Button confirm, TMP_Text confirmLabel, Button cancel, TMP_Text cancelLabel, Button background)
        { _title = title; _body = body; _confirm = confirm; _confirmLabel = confirmLabel; _cancel = cancel; _cancelLabel = cancelLabel; _background = background; }
    }
}
```

`Message/ToastView.cs`:

```csharp
using System.Threading;
using Cysharp.Threading.Tasks;
using TMPro;
using UnityEngine;

namespace BK.UI
{
    public sealed class ToastView : UIViewBase, IUIView<string>
    {
        [SerializeField] private TMP_Text _text;
        private string _message;
        public void SetArgs(string args) => _message = args;
        public override UniTask OnInitializeAsync(CancellationToken cancellationToken)
        { _text.text = _message ?? ""; return base.OnInitializeAsync(cancellationToken); }
        public override bool OnBackRequested() => true;
        public void Bind(TMP_Text text) => _text = text;
    }
}
```

`UIViewBase`의 `_layer`를 에디터 생성기가 설정할 수 있도록 `public void SetLayer(UILayer layer) => _layer = layer;` 추가 (System 레이어로 저장). `_dim`도 `public void SetDimLevel(DimLevel level) => _dim = level;`.

- [ ] **Step 3: FrameworkSettings 확장**

```csharp
        [Header("Messages")]
        [SerializeField] private string _messagePopupAddress = "BK/UI/MessagePopup";
        [SerializeField] private string _toastAddress = "BK/UI/Toast";
        [SerializeField, Min(0.2f)] private float _toastSeconds = 1.2f;
        public string MessagePopupAddress => _messagePopupAddress;
        public string ToastAddress => _toastAddress;
        public float ToastSeconds => _toastSeconds;
```

AppLifetimeScope:

```csharp
            builder.Register<IMessagePresenter>(container => new UIMessagePresenter(
                container.Resolve<IUIService>(), new AssetKey(_settings.MessagePopupAddress),
                new AssetKey(_settings.ToastAddress), _settings.ToastSeconds), Lifetime.Singleton);
            builder.Register<MessageService>(Lifetime.Singleton).As<IMessageService>();
```

- [ ] **Step 4: 에디터 생성기**

`Assets/BKFramework/Editor/BK.Editor.asmdef`:

```json
{
  "name": "BK.Editor",
  "rootNamespace": "BK.Editor",
  "references": ["BK.Core", "BK.UI", "Unity.TextMeshPro", "UnityEngine.UI", "Unity.Addressables", "Unity.Addressables.Editor", "Unity.ResourceManager"],
  "includePlatforms": ["Editor"],
  "excludePlatforms": [],
  "allowUnsafeCode": false,
  "overrideReferences": false,
  "precompiledReferences": [],
  "autoReferenced": true,
  "defineConstraints": [],
  "versionDefines": [],
  "noEngineReferences": false
}
```

`Assets/BKFramework/Editor/FrameworkUISetup.cs`:

```csharp
using System.IO;
using BK.UI;
using TMPro;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.AddressableAssets.Settings.GroupSchemas;
using UnityEngine;
using UnityEngine.UI;

namespace BK.Editor
{
    /// <summary>Builds the framework's own UI prefabs (message popup, toast) and registers them as Addressables.</summary>
    public static class FrameworkUISetup
    {
        public const string Folder = "Assets/BKFramework/Content/UI";
        public const string GroupName = "BK Framework";
        private static readonly Color Panel = new(0.13f, 0.14f, 0.18f, 1f);
        private static readonly Color Button = new(0.30f, 0.55f, 0.95f, 1f);
        private static readonly Color ButtonMuted = new(0.35f, 0.36f, 0.42f, 1f);

        [MenuItem("BK/Framework/Generate UI Prefabs")]
        public static void Generate()
        {
            Directory.CreateDirectory(Folder);
            var popup = SavePrefab(BuildMessagePopup(), Folder + "/MessagePopup.prefab");
            var toast = SavePrefab(BuildToast(), Folder + "/Toast.prefab");
            Register((popup, "BK/UI/MessagePopup"), (toast, "BK/UI/Toast"));
            AssetDatabase.SaveAssets();
            Debug.Log("BK_FRAMEWORK_UI_OK");
        }

        private static GameObject BuildMessagePopup()
        {
            var root = Root("MessagePopup");
            var view = root.AddComponent<MessagePopupView>();
            view.SetLayer(UILayer.System);
            view.SetDimLevel(DimLevel.Soft);

            var background = Image(root.transform, "Background", new Color(0, 0, 0, 0.001f));
            Stretch(background.rectTransform);
            var backgroundButton = background.gameObject.AddComponent<Button>();
            backgroundButton.transition = Selectable.Transition.None;

            var panel = Image(root.transform, "Panel", Panel);
            Place(panel.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(720, 460));

            var title = Text(panel.transform, "Title", 44, FontStyles.Bold);
            Place(title.rectTransform, new Vector2(0.5f, 1f), new Vector2(0, -40), new Vector2(640, 70));
            var body = Text(panel.transform, "Body", 32, FontStyles.Normal);
            Place(body.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0, 20), new Vector2(640, 200));

            var confirm = ButtonWithLabel(panel.transform, "Confirm", Button, out var confirmLabel);
            Place(((RectTransform)confirm.transform), new Vector2(0.5f, 0f), new Vector2(150, 50), new Vector2(260, 90));
            var cancel = ButtonWithLabel(panel.transform, "Cancel", ButtonMuted, out var cancelLabel);
            Place(((RectTransform)cancel.transform), new Vector2(0.5f, 0f), new Vector2(-150, 50), new Vector2(260, 90));

            view.Bind(title, body, confirm, confirmLabel, cancel, cancelLabel, backgroundButton);
            return root;
        }

        private static GameObject BuildToast()
        {
            var root = Root("Toast");
            var view = root.AddComponent<ToastView>();
            view.SetLayer(UILayer.System);
            root.GetComponent<CanvasGroup>().blocksRaycasts = false;
            var panel = Image(root.transform, "Panel", new Color(0, 0, 0, 0.75f));
            panel.raycastTarget = false;
            Place(panel.rectTransform, new Vector2(0.5f, 0f), new Vector2(0, 220), new Vector2(700, 90));
            var text = Text(panel.transform, "Text", 30, FontStyles.Normal);
            text.raycastTarget = false;
            Stretch(text.rectTransform);
            view.Bind(text);
            return root;
        }

        private static GameObject Root(string name)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasGroup));
            Stretch((RectTransform)go.transform);
            return go;
        }

        private static Image Image(Transform parent, string name, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            var image = go.GetComponent<Image>();
            image.color = color;
            image.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
            image.type = UnityEngine.UI.Image.Type.Sliced;
            return image;
        }

        private static TMP_Text Text(Transform parent, string name, float size, FontStyles style)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
            go.transform.SetParent(parent, false);
            var text = go.GetComponent<TextMeshProUGUI>();
            text.fontSize = size; text.fontStyle = style; text.alignment = TextAlignmentOptions.Center;
            text.color = Color.white; text.enableWordWrapping = true; text.text = name;
            return text;
        }

        private static Button ButtonWithLabel(Transform parent, string name, Color color, out TMP_Text label)
        {
            var image = Image(parent, name, color);
            var button = image.gameObject.AddComponent<Button>();
            label = Text(image.transform, "Label", 32, FontStyles.Bold);
            Stretch(label.rectTransform);
            return button;
        }

        private static void Stretch(RectTransform rt)
        { rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.offsetMin = rt.offsetMax = Vector2.zero; }

        private static void Place(RectTransform rt, Vector2 anchor, Vector2 position, Vector2 size)
        { rt.anchorMin = rt.anchorMax = rt.pivot = anchor; rt.anchoredPosition = position; rt.sizeDelta = size; }

        private static string SavePrefab(GameObject go, string path)
        {
            PrefabUtility.SaveAsPrefabAsset(go, path);
            Object.DestroyImmediate(go);
            return path;
        }

        private static void Register(params (string path, string address)[] entries)
        {
            var settings = AddressableAssetSettingsDefaultObject.GetSettings(true);
            var group = settings.FindGroup(GroupName) ?? settings.CreateGroup(GroupName, false, false, true, null,
                typeof(BundledAssetGroupSchema), typeof(ContentUpdateGroupSchema));
            var bundle = group.GetSchema<BundledAssetGroupSchema>();
            bundle.BuildPath.SetVariableByName(settings, AddressableAssetSettings.kLocalBuildPath);
            bundle.LoadPath.SetVariableByName(settings, AddressableAssetSettings.kLocalLoadPath);
            foreach (var (path, address) in entries)
            {
                var entry = settings.CreateOrMoveEntry(AssetDatabase.AssetPathToGUID(path), group, false, false);
                entry.address = address;
            }
            settings.SetDirty(AddressableAssetSettings.ModificationEvent.EntryMoved, null, true, true);
        }
    }
}
```

`TMP_Text.enableWordWrapping`이 ugui 2.0 TMP에서 obsolete면 `textWrappingMode = TextWrappingModes.Normal`로 바꾼다.

- [ ] **Step 5: 생성기 실행(배치모드 `-executeMethod BK.Editor.FrameworkUISetup.Generate`) → 프리팹 2개 + `Assets/AddressableAssetsData/AssetGroups/BK Framework.asset` 생성 확인**

```bash
"$UNITY" -batchmode -nographics -quit -projectPath "D:/UnityTemplate" -executeMethod BK.Editor.FrameworkUISetup.Generate -logFile "$SP/gen.log"; echo exit=$?; grep -E "BK_FRAMEWORK_UI_OK|error CS|Exception" "$SP/gen.log" | head
ls Assets/BKFramework/Content/UI "Assets/AddressableAssetsData/AssetGroups" | grep -i -E "prefab|BK Framework"
```

- [ ] **Step 6: EditMode 테스트 (`MessageServiceTests` 2/2) → 커밋** `feat(ui): add queued message popups and toasts with generated prefabs`

---

### Task 4: PlayMode 통합 테스트 + 문서

**Files:**
- Create: `Assets/BKFramework/Tests/PlayMode/BK.Framework.PlayModeTests.asmdef`, `FrameworkUIPlayModeTests.cs`
- Modify: `CLAUDE.md`, 로드맵

- [ ] **Step 1: PlayMode asmdef** (EditMode 것과 같되 `includePlatforms: []`, references에 `BK.Composition`, `UnityEngine.UI`, `Unity.TextMeshPro` 추가, `UnityEditor.TestRunner` 제거)

- [ ] **Step 2: 테스트**

```csharp
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
            float deadline = Time.realtimeSinceStartup + 20f;
            while (scope == null && Time.realtimeSinceStartup < deadline)
            { scope = Object.FindFirstObjectByType<LifetimeScope>(); yield return null; }
            Assert.That(scope, Is.Not.Null);
            var messages = scope.Container.Resolve<IMessageService>();
            var dim = scope.Container.Resolve<IPopupDim>();
            // Wait for boot (UIRoot exists once UI is usable).
            deadline = Time.realtimeSinceStartup + 20f;
            while (Object.FindFirstObjectByType<UIRoot>() == null && Time.realtimeSinceStartup < deadline) yield return null;
            yield return new WaitForSecondsRealtime(1f);

            bool? result = null;
            messages.ConfirmAsync("Title", "Body", "Yes", "No").ContinueWith(r => result = r).Forget();
            deadline = Time.realtimeSinceStartup + 10f;
            MessagePopupView popup = null;
            while (popup == null && Time.realtimeSinceStartup < deadline)
            { popup = Object.FindFirstObjectByType<MessagePopupView>(); yield return null; }
            Assert.That(popup, Is.Not.Null, "message popup prefab must load from Addressables");
            yield return new WaitForSecondsRealtime(0.3f);
            Assert.That(dim.IsActive.CurrentValue, Is.True, "dimmed view acquires the popup dim");
            var yes = popup.GetComponentsInChildren<Button>(true).First(b => b.name == "Confirm");
            yes.onClick.Invoke();
            deadline = Time.realtimeSinceStartup + 5f;
            while (result == null && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(result, Is.True);
            yield return new WaitForSecondsRealtime(0.5f);
            Assert.That(dim.IsActive.CurrentValue, Is.False);

            messages.Toast("hello");
            deadline = Time.realtimeSinceStartup + 5f;
            ToastView toast = null;
            while (toast == null && Time.realtimeSinceStartup < deadline)
            { toast = Object.FindFirstObjectByType<ToastView>(); yield return null; }
            Assert.That(toast, Is.Not.Null);
        }
    }
}
```

Bounce 테스트가 `KitApp.Instance`를 전역으로 두므로, 이 테스트 끝에 `if (BK.Kit.KitApp.Instance != null) Object.Destroy(...)`는 하지 않는다(BK.Kit 의존 금지). 테스트 순서 간섭이 나면 `[Order]`가 아니라 Bounce 테스트가 이미 하는 것처럼 각 테스트가 씬을 새로 로드하므로 문제 없어야 한다.

- [ ] **Step 3: EditMode 전체(45 = 32 + 7 + 4 + 2) + PlayMode 전체(12) → 통과 확인**

- [ ] **Step 4: 문서**

CLAUDE.md Layout 블록 `UI/` 줄을:

```
  UI/        layered view stack, per-view scopes, popup dim (IPopupDim), screen cover + SceneFlow, message/toast (IMessageService)
```

Rules 추가:

```
- 팝업은 `UIViewBase`의 Dim 레벨(인스펙터)로 딤을 자동 취득한다. 플로우가 여러 팝업을 잇는 경우 `IPopupDim.HoldForTransition()`. 씬 전환은 `ISceneFlow.TransitionAsync`(커버→닫기→전환→리빌). 메시지/토스트 프리팹은 `BK > Framework > Generate UI Prefabs`로 재생성한다(`Assets/BKFramework/Content/UI`, Addressables 그룹 `BK Framework`).
```

로드맵 C 행 완료 + "StagePreloaderRegistry는 D에서". 커밋 `docs: record popup dim, screen cover, scene flow and message service`.
