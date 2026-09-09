using System.Threading;
using Cysharp.Threading.Tasks;
using BK.Core.App;

namespace BK.Data
{
    /// <summary>Loads every table listed in the catalog before gameplay starts.</summary>
    public sealed class DataBootStep : IBootStep
    {
        private readonly ITableService _tableService;

        public DataBootStep(ITableService tableService) => _tableService = tableService;

        public int Order => BootOrder.Data;
        public string Name => "Tables";

        public UniTask ExecuteAsync(CancellationToken cancellationToken)
            => _tableService.LoadAllAsync(cancellationToken);
    }
}
