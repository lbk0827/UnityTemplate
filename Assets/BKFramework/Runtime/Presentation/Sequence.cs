using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;

namespace BK.Presentation
{
    /// <summary>
    /// Composes async steps into an ordered set of groups. Steps inside a group run
    /// together; groups run one after another. This covers the "play these three at
    /// once, then that one" shape that presentation code is almost entirely made of,
    /// without a bespoke timeline type.
    /// </summary>
    public sealed class Sequence
    {
        private readonly List<List<Func<CancellationToken, UniTask>>> _groups = new();

        public static Sequence Create() => new();

        /// <summary>Starts a new group that runs after everything queued so far.</summary>
        public Sequence Then(Func<CancellationToken, UniTask> step)
        {
            _groups.Add(new List<Func<CancellationToken, UniTask>> { step });
            return this;
        }

        /// <summary>Adds a step to the current group, running alongside its siblings.</summary>
        public Sequence With(Func<CancellationToken, UniTask> step)
        {
            if (_groups.Count == 0)
                return Then(step);

            _groups[^1].Add(step);
            return this;
        }

        /// <summary>Unscaled delay, so presentation keeps running while the game is paused.</summary>
        public Sequence Wait(float seconds)
            => Then(ct => UniTask.Delay(TimeSpan.FromSeconds(seconds), DelayType.UnscaledDeltaTime, cancellationToken: ct));

        public async UniTask PlayAsync(CancellationToken cancellationToken = default)
        {
            foreach (var group in _groups)
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (group.Count == 1)
                {
                    await group[0](cancellationToken);
                    continue;
                }

                var running = new UniTask[group.Count];
                for (var i = 0; i < group.Count; i++)
                    running[i] = group[i](cancellationToken);

                await UniTask.WhenAll(running);
            }
        }
    }
}
