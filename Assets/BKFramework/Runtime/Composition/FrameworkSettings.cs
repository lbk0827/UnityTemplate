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
        [SerializeField] private Vector2 _referenceResolution = new(900f, 1600f);

        [Header("Data")]
        [SerializeField, Tooltip("Addressable address of the TableCatalog asset.")]
        private string _tableCatalogAddress = "Data/TableCatalog";

        [Header("Localization")]
        [SerializeField] private string _defaultLanguage = "en";
        [SerializeField, Tooltip("{0} is replaced with the language code.")]
        private string _localizationAddressFormat = "Localization/{0}";

        [Header("Messages")]
        [SerializeField, Tooltip("Addressable address of the generated message popup prefab.")]
        private string _messagePopupAddress = "BK/UI/MessagePopup";
        [SerializeField, Tooltip("Addressable address of the generated toast prefab.")]
        private string _toastAddress = "BK/UI/Toast";
        [SerializeField, Min(0.2f)] private float _toastSeconds = 1.2f;

        public Vector2 ReferenceResolution => _referenceResolution;
        public string MessagePopupAddress => _messagePopupAddress;
        public string ToastAddress => _toastAddress;
        public float ToastSeconds => _toastSeconds;
        public string TableCatalogAddress => _tableCatalogAddress;
        public string DefaultLanguage => _defaultLanguage;

        public string LocalizationAddressFor(string languageCode)
            => string.Format(_localizationAddressFormat, languageCode);
    }
}
