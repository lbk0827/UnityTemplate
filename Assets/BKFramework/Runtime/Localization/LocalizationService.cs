using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Cysharp.Text;
using R3;
using BK.Assets;
using BK.Core.Diagnostics;

namespace BK.Localization
{
    /// <inheritdoc cref="ILocalizationService"/>
    public sealed class LocalizationService : ILocalizationService, IDisposable
    {
        private readonly ReactiveProperty<string> _currentLanguage = new(string.Empty);
        private readonly IAssetService _assetService;
        private readonly Func<string, AssetKey> _keyForLanguage;

        private IAssetScope _scope;
        private LocalizationTable _table;

        /// <param name="keyForLanguage">
        /// Maps a language code to its table address, e.g. code => $"Localization/{code}".
        /// </param>
        public LocalizationService(IAssetService assetService, Func<string, AssetKey> keyForLanguage)
        {
            _assetService = assetService;
            _keyForLanguage = keyForLanguage;
        }

        public ReadOnlyReactiveProperty<string> CurrentLanguage => _currentLanguage;

        public async UniTask SetLanguageAsync(string languageCode, CancellationToken cancellationToken = default)
        {
            if (string.Equals(_currentLanguage.Value, languageCode, StringComparison.Ordinal))
                return;

            // Load into a fresh scope, then swap. If the load fails the previously
            // loaded language stays intact rather than leaving the UI stringless.
            var incoming = _assetService.CreateScope($"loc:{languageCode}");
            LocalizationTable table;
            try
            {
                table = await incoming.LoadAsync<LocalizationTable>(
                    _keyForLanguage(languageCode), cancellationToken);
            }
            catch
            {
                incoming.Dispose();
                throw;
            }

            _scope?.Dispose();
            _scope = incoming;
            _table = table;
            _currentLanguage.Value = languageCode;

            BKLog.Info(BKLog.Loc, $"language set to '{languageCode}'");
        }

        public string Get(string key)
        {
            if (string.IsNullOrEmpty(key))
                return string.Empty;

            if (_table != null && _table.TryGet(key, out var value))
                return value;

            BKLog.Warn(BKLog.Loc, $"missing key '{key}' for language '{_currentLanguage.Value}'");
            return key;
        }

        public string Format<T0>(string key, T0 arg0)
            => ZString.Format(Get(key), arg0);

        public string Format<T0, T1>(string key, T0 arg0, T1 arg1)
            => ZString.Format(Get(key), arg0, arg1);

        public void Dispose()
        {
            _scope?.Dispose();
            _scope = null;
            _table = null;
            _currentLanguage.Dispose();
        }
    }
}
