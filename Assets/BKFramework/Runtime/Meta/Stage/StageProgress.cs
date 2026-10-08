using System;
using R3;
using BK.Save;

namespace BK.Meta
{
    [Serializable]
    public sealed class StageProgressData : SaveData
    {
        public override int CurrentVersion => 1;
        public int currentStage = 1;
        public int attempts;
        public override bool Validate() => currentStage >= 1 && attempts >= 0;
    }

    /// <inheritdoc cref="IStageProgress"/>
    public sealed class StageProgress : IStageProgress, IDisposable
    {
        public const string ReasonEntry = "stage_entry";
        public const string ReasonRefund = "stage_refund";

        private readonly StageProgressData _data;
        private readonly IWallet _wallet;
        private readonly StageEntryPolicy _policy;
        private readonly ReactiveProperty<int> _current;
        private readonly Subject<StageStarted> _started = new();
        private readonly Subject<StageCleared> _cleared = new();
        private int _playingStage;

        public StageProgress(ISaveService saves, IWallet wallet, StageEntryPolicy policy)
        {
            _data = saves.Get<StageProgressData>();
            _wallet = wallet;
            _policy = policy;
            _current = new ReactiveProperty<int>(_data.currentStage);
            _playingStage = _data.currentStage;
        }

        public ReadOnlyReactiveProperty<int> CurrentStage => _current;
        public int AttemptsOnCurrentStage => _data.attempts;
        public Observable<StageStarted> Started => _started;
        public Observable<StageCleared> Cleared => _cleared;

        public bool CanEnter(int stage)
            => !_policy.ShouldCharge(stage) || _wallet.IsInfinite(_policy.EnergyId) || _wallet.ValueOf(_policy.EnergyId) > 0;

        public bool TryStart(int stage)
        {
            if (_policy.ShouldCharge(stage) && !_wallet.IsInfinite(_policy.EnergyId))
            {
                if (_wallet.ValueOf(_policy.EnergyId) <= 0)
                    return false;
                _wallet.Add(_policy.EnergyId, -1, ReasonEntry);
            }

            // Attempts count retries of the same stage; switching stage (replaying an older one) starts over.
            if (stage != _playingStage)
            {
                _playingStage = stage;
                _data.attempts = 0;
            }
            _data.attempts++;
            _data.MarkDirty();
            _started.OnNext(new StageStarted(stage, _data.attempts));
            return true;
        }

        public void Clear(int stage)
        {
            // Pointer first: a crash during the clear presentation must not replay the stage.
            if (stage >= _data.currentStage)
            {
                _data.currentStage = stage + 1;
                _current.Value = _data.currentStage;
            }
            _data.attempts = 0;
            _data.MarkDirty();

            if (_policy.ShouldCharge(stage) && !_wallet.IsInfinite(_policy.EnergyId))
            {
                var energy = _wallet.Get(_policy.EnergyId);
                if (energy.Value.CurrentValue < energy.Definition.RechargeMax)
                    _wallet.Add(_policy.EnergyId, +1, ReasonRefund);
            }

            _cleared.OnNext(new StageCleared(stage));
        }

        public void SetCurrentStage(int stage)
        {
            if (stage < 1)
                throw new ArgumentOutOfRangeException(nameof(stage));
            _data.currentStage = stage;
            _data.attempts = 0;
            _playingStage = stage;
            _data.MarkDirty();
            _current.Value = stage;
        }

        public void Dispose()
        {
            _current.Dispose();
            _started.Dispose();
            _cleared.Dispose();
        }
    }
}
