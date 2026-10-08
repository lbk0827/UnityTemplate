using System;
using System.Collections.Generic;
using R3;
using BK.Save;

namespace BK.Meta
{
    public sealed class WinStreakTier
    {
        public int Threshold;
        public ItemGrant[] Rewards = Array.Empty<ItemGrant>();
    }

    public interface IWinStreakCatalog
    {
        /// <summary>Streak cap; 0 or less falls back to 10.</summary>
        int Cap { get; }

        /// <summary>Clears below this stage reset instead of accumulating.</summary>
        int UnlockStage { get; }

        IReadOnlyList<WinStreakTier> Tiers { get; }
    }

    [Serializable]
    public sealed class WinStreakData : SaveData
    {
        public override int CurrentVersion => 1;
        public int streak;
        public bool attemptInProgress;
        public List<ItemGrant> pending = new();
        public override bool Validate() => streak >= 0;
    }

    /// <summary>Pure streak rules.</summary>
    public static class WinStreakLogic
    {
        public const int DefaultCap = 10;

        public static int Increment(int streak, int cap)
            => Math.Min(Math.Max(streak, 0) + 1, cap <= 0 ? DefaultCap : cap);

        /// <summary>Tier with the highest threshold at or below the streak, order independent; null when none.</summary>
        public static WinStreakTier RewardFor(int streak, IReadOnlyList<WinStreakTier> tiers)
        {
            WinStreakTier best = null;
            foreach (var tier in tiers)
                if (tier.Threshold <= streak && (best == null || tier.Threshold > best.Threshold))
                    best = tier;
            return best;
        }

        /// <summary>Piecewise-linear gauge: equal visual segments per tier, 1 at or above the last tier.</summary>
        public static float GaugeFill(int streak, IReadOnlyList<int> thresholdsAscending)
        {
            if (thresholdsAscending.Count == 0)
                return 0f;
            if (streak <= 0)
                return 0f;
            var segment = 1f / thresholdsAscending.Count;
            var previous = 0;
            for (var i = 0; i < thresholdsAscending.Count; i++)
            {
                var threshold = thresholdsAscending[i];
                if (streak >= threshold)
                {
                    previous = threshold;
                    continue;
                }
                var span = Math.Max(1, threshold - previous);
                return segment * i + segment * (streak - previous) / span;
            }
            return 1f;
        }
    }

    /// <summary>
    /// Consecutive-win counter with tier rewards handed to the next attempt. Hooks on
    /// <see cref="IStageProgress"/>: Started marks an attempt (and resets if one was already
    /// in progress: retry or force-quit), Cleared increments. Explicit quit calls <see cref="Abandon"/>.
    /// </summary>
    public sealed class WinStreak : IDisposable
    {
        private readonly IWinStreakCatalog _catalog;
        private readonly WinStreakData _data;
        private readonly ReactiveProperty<int> _current;
        private readonly CompositeDisposable _subscriptions = new();
        private readonly List<int> _thresholds = new();

        public WinStreak(IWinStreakCatalog catalog, ISaveService saves, IStageProgress progress)
        {
            _catalog = catalog;
            _data = saves.Get<WinStreakData>();
            foreach (var tier in catalog.Tiers) _thresholds.Add(tier.Threshold);
            _thresholds.Sort();

            // Force-quit mid-attempt: the marker survived the restart, so the streak is forfeited now.
            Abandon();
            _current = new ReactiveProperty<int>(_data.streak);

            progress.Started.Subscribe(OnStarted).AddTo(_subscriptions);
            progress.Cleared.Subscribe(OnCleared).AddTo(_subscriptions);
        }

        public ReadOnlyReactiveProperty<int> Current => _current;
        public bool AttemptInProgress => _data.attemptInProgress;
        public IReadOnlyList<ItemGrant> Pending => _data.pending;
        public float GaugeFill => WinStreakLogic.GaugeFill(_data.streak, _thresholds);

        /// <summary>One-shot: returns pending grants and clears them. The game applies them as the next attempt's boosters.</summary>
        public IReadOnlyList<ItemGrant> ConsumePending()
        {
            if (_data.pending.Count == 0)
                return Array.Empty<ItemGrant>();
            var grants = _data.pending.ToArray();
            _data.pending.Clear();
            _data.MarkDirty();
            return grants;
        }

        /// <summary>Quit or retry mid-attempt. Idempotent: no effect when no attempt is in progress.</summary>
        public void Abandon()
        {
            if (!_data.attemptInProgress)
                return;
            _data.attemptInProgress = false;
            Reset();
            _data.MarkDirty();
        }

        private void OnStarted(StageStarted started)
        {
            if (_data.attemptInProgress)
                Reset(); // a Started while in progress is a retry (or a quit without Abandon): the streak breaks
            _data.attemptInProgress = true;
            _data.MarkDirty();
        }

        private void OnCleared(StageCleared cleared)
        {
            _data.attemptInProgress = false;
            if (cleared.Stage < _catalog.UnlockStage)
            {
                Reset();
                _data.MarkDirty();
                return;
            }

            _data.streak = WinStreakLogic.Increment(_data.streak, _catalog.Cap);
            _data.pending.Clear();
            var tier = WinStreakLogic.RewardFor(_data.streak, _catalog.Tiers);
            if (tier != null)
                _data.pending.AddRange(tier.Rewards);
            _data.MarkDirty();
            if (_current != null) _current.Value = _data.streak;
        }

        private void Reset()
        {
            if (_data.streak == 0 && _data.pending.Count == 0)
                return;
            _data.streak = 0;
            _data.pending.Clear();
            if (_current != null) _current.Value = 0;
        }

        public void Dispose()
        {
            _subscriptions.Dispose();
            _current.Dispose();
        }
    }
}
