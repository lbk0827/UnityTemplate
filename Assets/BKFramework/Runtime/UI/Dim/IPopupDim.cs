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

        /// <summary>Keep the dim through a gap (popup A closes, popup B loads) until the next Acquire or a timeout.</summary>
        void HoldForTransition();

        /// <summary>Drop everything (scene change).</summary>
        void Reset();

        ReadOnlyReactiveProperty<bool> IsActive { get; }
    }
}
