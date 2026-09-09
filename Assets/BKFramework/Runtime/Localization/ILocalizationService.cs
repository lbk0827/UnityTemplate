using System.Threading;
using Cysharp.Threading.Tasks;
using R3;

namespace BK.Localization
{
    /// <summary>
    /// Resolves string keys for the active language. Missing keys never throw — a
    /// visible key in the UI is a better failure mode than a crash mid-scene.
    /// </summary>
    public interface ILocalizationService
    {
        /// <summary>Active language code. Emits on change so views can re-render.</summary>
        ReadOnlyReactiveProperty<string> CurrentLanguage { get; }

        UniTask SetLanguageAsync(string languageCode, CancellationToken cancellationToken = default);

        /// <summary>Returns the localized string, or the key itself when unmapped.</summary>
        string Get(string key);

        /// <summary>Localizes then formats. Uses ZString, so no intermediate allocations.</summary>
        string Format<T0>(string key, T0 arg0);
        string Format<T0, T1>(string key, T0 arg0, T1 arg1);
    }
}
