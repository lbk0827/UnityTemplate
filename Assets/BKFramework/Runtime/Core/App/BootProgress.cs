namespace BK.Core.App
{
    /// <summary>Snapshot of boot progress, suitable for driving a splash/loading view.</summary>
    public readonly struct BootProgress
    {
        public readonly int CompletedSteps;
        public readonly int TotalSteps;
        public readonly string CurrentStepName;

        public BootProgress(int completedSteps, int totalSteps, string currentStepName)
        {
            CompletedSteps = completedSteps;
            TotalSteps = totalSteps;
            CurrentStepName = currentStepName;
        }

        /// <summary>0..1. Returns 1 when there is nothing to do.</summary>
        public float Normalized => TotalSteps <= 0 ? 1f : (float)CompletedSteps / TotalSteps;
    }
}
