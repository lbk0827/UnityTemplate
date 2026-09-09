using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using BK.Assets;
using BK.Core.Diagnostics;

namespace BK.Data
{
    /// <inheritdoc cref="ITableService"/>
    public sealed class TableService : ITableService
    {
        private readonly Dictionary<Type, TableAsset> _tables = new();
        private readonly IAssetService _assetService;
        private readonly AssetKey _catalogKey;

        public TableService(IAssetService assetService, AssetKey catalogKey)
        {
            _assetService = assetService;
            _catalogKey = catalogKey;
        }

        public bool IsLoaded { get; private set; }

        public async UniTask LoadAllAsync(CancellationToken cancellationToken = default)
        {
            if (IsLoaded)
                return;

            // Tables live for the whole session, so they belong to the global scope
            // rather than any scene's.
            var scope = _assetService.GlobalScope;
            var catalog = await scope.LoadAsync<TableCatalog>(_catalogKey, cancellationToken);

            foreach (var address in catalog.Addresses)
            {
                var table = await scope.LoadAsync<TableAsset>(address, cancellationToken);
                table.BuildIndex();

                if (!_tables.TryAdd(table.RowType, table))
                {
                    BKLog.Warn(BKLog.Data,
                        $"two tables serve row type '{table.RowType.Name}'; ignoring '{address}'");
                }
            }

            IsLoaded = true;
            BKLog.Info(BKLog.Data, $"loaded {_tables.Count} tables");
        }

        public ITable<TKey, TRow> Get<TKey, TRow>() where TRow : ITableRow<TKey>
        {
            if (!IsLoaded)
                throw new TableNotLoadedException(typeof(TRow));

            if (!_tables.TryGetValue(typeof(TRow), out var table))
                throw new TableNotLoadedException(typeof(TRow));

            return (ITable<TKey, TRow>)table;
        }
    }
}
