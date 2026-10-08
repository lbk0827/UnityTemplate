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
