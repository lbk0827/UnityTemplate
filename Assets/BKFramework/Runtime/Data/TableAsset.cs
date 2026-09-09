using System.Collections.Generic;
using UnityEngine;

namespace BK.Data
{
    /// <summary>
    /// Non-generic handle so the service can hold tables of mixed row types in one
    /// collection and force index construction without knowing the type arguments.
    /// </summary>
    public abstract class TableAsset : ScriptableObject
    {
        /// <summary>Row type this table serves. Used as the service's lookup key.</summary>
        public abstract System.Type RowType { get; }

        internal abstract void BuildIndex();
    }

    /// <summary>
    /// Authored table data. Rows live in a serialized list so they are diffable and
    /// editable in the inspector; the dictionary is built once on load.
    /// </summary>
    public abstract class TableAsset<TKey, TRow> : TableAsset, ITable<TKey, TRow>
        where TRow : ITableRow<TKey>
    {
        [SerializeField] private List<TRow> _rows = new();

        private Dictionary<TKey, TRow> _index;

        public override System.Type RowType => typeof(TRow);

        public int Count => _rows.Count;
        public IReadOnlyList<TRow> Rows => _rows;

        public TRow Get(TKey id)
        {
            EnsureIndex();
            if (!_index.TryGetValue(id, out var row))
                throw new TableRowNotFoundException(name, id);
            return row;
        }

        public bool TryGet(TKey id, out TRow row)
        {
            EnsureIndex();
            return _index.TryGetValue(id, out row);
        }

        public bool Contains(TKey id)
        {
            EnsureIndex();
            return _index.ContainsKey(id);
        }

        internal override void BuildIndex()
        {
            _index = new Dictionary<TKey, TRow>(_rows.Count);
            foreach (var row in _rows)
            {
                if (_index.ContainsKey(row.Id))
                {
                    Debug.LogError($"[{BK.Core.Diagnostics.BKLog.Data}] duplicate id '{row.Id}' in table '{name}'; keeping the first.");
                    continue;
                }
                _index.Add(row.Id, row);
            }
        }

        private void EnsureIndex()
        {
            if (_index == null)
                BuildIndex();
        }
    }
}
