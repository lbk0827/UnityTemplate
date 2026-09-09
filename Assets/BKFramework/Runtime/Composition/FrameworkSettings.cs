using UnityEngine;

namespace BK.Composition
{
    /// <summary>
    /// Everything the framework needs to know that varies per project. Held as an
    /// asset so a new game changes configuration without touching wiring code.
    /// </summary>
    [CreateAssetMenu(fileName = "FrameworkSettings", menuName = "BK/Framework Settings")]
    public sealed class FrameworkSettings : ScriptableObject
    {
        [Header("UI")]
        [SerializeField] private Vector2 _referenceResolution = new(1080f, 1920f);

        [Header("Data")]
        [SerializeField, Tooltip("Addressable address of the TableCatalog asset.")]
        private string _tableCatalogAddress = "Data/TableCatalog";

        [Header("Localization")]
        [SerializeField] private string _defaultLanguage = "en";
        [SerializeField, Tooltip("{0} is replaced with the language code.")]
        private string _localizationAddressFormat = "Localization/{0}";

        public Vector2 ReferenceResolution => _referenceResolution;
        public string TableCatalogAddress => _tableCatalogAddress;
        public string DefaultLanguage => _defaultLanguage;

        public string LocalizationAddressFor(string languageCode)
            => string.Format(_localizationAddressFormat, languageCode);
    }
}
