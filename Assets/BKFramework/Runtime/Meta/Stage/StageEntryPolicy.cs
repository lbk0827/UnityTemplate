namespace BK.Meta
{
    /// <summary>Which currency a stage attempt costs and which early stages are free.</summary>
    public sealed class StageEntryPolicy
    {
        public string EnergyId { get; }

        /// <summary>Stages at or below this number are free: no charge and no refund (symmetric).</summary>
        public int FreeUntilStage { get; }

        public StageEntryPolicy(string energyId, int freeUntilStage)
        {
            EnergyId = energyId;
            FreeUntilStage = freeUntilStage;
        }

        public bool ShouldCharge(int stage) => stage > FreeUntilStage;
    }
}
