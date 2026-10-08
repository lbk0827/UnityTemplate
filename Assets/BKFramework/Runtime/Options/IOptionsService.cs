using R3;

namespace BK.Options
{
    /// <summary>
    /// Player-facing settings as reactive properties. Writing a value persists it
    /// through <see cref="BK.Save.ISaveService"/>; audio/haptic systems subscribe to apply it.
    /// </summary>
    public interface IOptionsService
    {
        ReactiveProperty<bool> Music { get; }
        ReactiveProperty<bool> Sfx { get; }
        ReactiveProperty<bool> Haptics { get; }
        /// <summary>Language code. Never empty: the project default is substituted.</summary>
        ReactiveProperty<string> Language { get; }
    }
}
