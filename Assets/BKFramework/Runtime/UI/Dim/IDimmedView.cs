namespace BK.UI
{
    /// <summary>Opt-in: the UI service acquires the popup dim at this level while the view is open.</summary>
    public interface IDimmedView
    {
        DimLevel DimLevel { get; }
    }
}
