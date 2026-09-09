using System;

namespace BK.Data
{
    public sealed class TableRowNotFoundException : Exception
    {
        public TableRowNotFoundException(string tableName, object id)
            : base($"Row '{id}' not found in table '{tableName}'.") { }
    }

    public sealed class TableNotLoadedException : Exception
    {
        public TableNotLoadedException(Type tableType)
            : base($"Table '{tableType.Name}' was requested before it was loaded. "
                 + "Register it on a table set that runs during boot.") { }
    }
}
