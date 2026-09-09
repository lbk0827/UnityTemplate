using System.Threading;
using Cysharp.Threading.Tasks;
using LitMotion;
using LitMotion.Extensions;
using UnityEngine;

namespace BK.Presentation
{
    /// <summary>
    /// Thin LitMotion wrappers shaped for <see cref="Sequence"/>. They exist so
    /// presentation code reads as a list of intents rather than motion plumbing.
    /// </summary>
    public static class Motions
    {
        public static UniTask FadeAsync(CanvasGroup target, float to, float duration, CancellationToken cancellationToken = default)
            => LMotion.Create(target.alpha, to, duration)
                .WithScheduler(MotionScheduler.UpdateIgnoreTimeScale)
                .BindToAlpha(target)
                .ToUniTask(cancellationToken);

        public static UniTask MoveAsync(Transform target, Vector3 to, float duration, CancellationToken cancellationToken = default)
            => LMotion.Create(target.localPosition, to, duration)
                .WithScheduler(MotionScheduler.UpdateIgnoreTimeScale)
                .BindToLocalPosition(target)
                .ToUniTask(cancellationToken);

        public static UniTask ScaleAsync(Transform target, Vector3 to, float duration, Ease ease = Ease.OutBack, CancellationToken cancellationToken = default)
            => LMotion.Create(target.localScale, to, duration)
                .WithEase(ease)
                .WithScheduler(MotionScheduler.UpdateIgnoreTimeScale)
                .BindToLocalScale(target)
                .ToUniTask(cancellationToken);
    }
}
