using System;
using System.Collections.Generic;
using UnityEngine;

namespace BK.Localization
{
    /// <summary>
    /// One language's strings. Splitting per language means only the active language
    /// is resident, and adding a language is a new asset rather than an edit to a
    /// shared one.
    /// </summary>
    [CreateAssetMenu(fileName = "LocalizationTable", menuName = "BK/Localization/Table")]
    public sealed class LocalizationTable : ScriptableObject
    {
        [Serializable]
        public struct Entry
        {
            public string Key;
            [TextArea] public string Value;
        }

        [SerializeField] private string _languageCode = "en";
        [SerializeField] private List<Entry> _entries = new();

        private Dictionary<string, string> _index;

        public string LanguageCode => _languageCode;

        public bool TryGet(string key, out string value)
        {
            if (_index == null)
                BuildIndex();
            return _index.TryGetValue(key, out value);
        }

        private void BuildIndex()
        {
            _index = new Dictionary<string, string>(_entries.Count, StringComparer.Ordinal);
            foreach (var entry in _entries)
            {
                if (string.IsNullOrEmpty(entry.Key))
                    continue;
                _index[entry.Key] = entry.Value;
            }
        }
    }
}
