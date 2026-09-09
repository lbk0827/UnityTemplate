using System.Threading;
using Cysharp.Threading.Tasks;
using BK.Core.App;

namespace BK.Localization
{
    /// <summary>Loads the starting language so no view renders before strings exist.</summary>
    public sealed class LocalizationBootStep : IBootStep
    {
        private readonly ILocalizationService _localization;
        private readonly string _defaultLanguage;

        public LocalizationBootStep(ILocalizationService localization, string defaultLanguage)
        {
            _localization = localization;
            _defaultLanguage = defaultLanguage;
        }

        public int Order => BootOrder.Localization;
        public string Name => "Localization";

        public UniTask ExecuteAsync(CancellationToken cancellationToken)
            => _localization.SetLanguageAsync(_defaultLanguage, cancellationToken);
    }
}
