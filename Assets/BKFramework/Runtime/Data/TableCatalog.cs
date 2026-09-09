using System.Collections.Generic;
using UnityEngine;

namespace BK.Data
{
    /// <summary>
    /// The list of tables to load at boot. Kept as an asset rather than discovered by
    /// reflection so load order and content are explicit and reviewable in a diff.
    /// </summary>
    [CreateAssetMenu(fileName = "TableCatalog", menuName = "BK/Data/Table Catalog")]
    public sealed class TableCatalog : ScriptableObject
    {
        [SerializeField, Tooltip("Addressable addresses of TableAsset instances.")]
        private List<string> _addresses = new();

        public IReadOnlyList<string> Addresses => _addresses;
    }
}
