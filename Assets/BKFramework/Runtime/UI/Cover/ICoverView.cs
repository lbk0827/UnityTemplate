using System.Threading;
using Cysharp.Threading.Tasks;

namespace BK.UI
{
    /// <summary>Visual side of the screen cover, abstracted so the hold/release logic is testable.</summary>
    public interface ICoverView
    {
        void SetVisible(bool visible);
        void SetAlpha(float alpha);
        UniTask FadeAsync(float to, float seconds, CancellationToken cancellationToken);
    }
}
