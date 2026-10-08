using R3;

namespace BK.Meta
{
    public readonly struct StageStarted
    {
        public readonly int Stage;
        public readonly int Attempt;
        public StageStarted(int stage, int attempt) { Stage = stage; Attempt = attempt; }
    }

    public readonly struct StageCleared
    {
        public readonly int Stage;
        public StageCleared(int stage) => Stage = stage;
    }

    /// <summary>
    /// Saved stage pointer plus the single entry gate. One energy charge per attempt,
    /// refunded on clear, nothing on fail/continue; a retry goes through the gate again.
    /// </summary>
    public interface IStageProgress
    {
        /// <summary>Next stage to play. Advances the moment a stage is cleared.</summary>
        ReadOnlyReactiveProperty<int> CurrentStage { get; }

        int AttemptsOnCurrentStage { get; }

        bool CanEnter(int stage);

        /// <summary>The gate. False, with no side effects, when the entry cost cannot be paid.</summary>
        bool TryStart(int stage);

        /// <summary>Saves the new pointer first, then refunds the entry cost when it was charged.</summary>
        void Clear(int stage);

        Observable<StageStarted> Started { get; }
        Observable<StageCleared> Cleared { get; }

        /// <summary>Tools and tests only: moves the saved pointer without charging, refunding or raising events.</summary>
        void SetCurrentStage(int stage);
    }
}
