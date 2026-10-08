# Table pipeline (xlsx → JSON + C# → TableAsset)

```
python -I tools/tables/export_tables.py --xlsx <folder or file> \
    --json-out Assets/_Project/Data/Tables/Generated \
    --cs-out   Assets/_Project/Scripts/Tables/Generated \
    --namespace Project.Tables
```

Then in Unity: **BK > Data > Import Tables** fills (or creates) `<Sheet>Table.asset`
from each JSON, registers it in the `BK Tables` Addressables group at `Data/Tables/<Sheet>`
and appends it to the project's `TableCatalog`.

## Sheet layout (same as the sf/RootBox exporter)

| Row | Column A | Other columns |
|---|---|---|
| 1 | (optional sub-folder, ignored) | |
| 2 | `#data` | column names; `name[]` marks an array column |
| 3 | | type/options, comma separated |
| 4 | | export marker: only `data` columns are exported |
| 5+ | | rows; the first empty key cell ends the sheet |

Options for row 3: `key` (exactly one, `int` or `string`), `int`, `long`, `float`, `double`, `bool`, `string`,
`type<ENUM>` (generates `enum ENUM` in `TableEnums.cs`; members from the `types` sheet or from the data in
order of appearance), `unique`. `check<...>`, `localtext`, `stream`, `origin`, `null` are accepted with a warning
and ignored.

A `types` sheet lists enum members: row 2 = enum names, rows 3+ = members in declaration order.

Create a sample: `python -I tools/tables/export_tables.py --write-sample tools/tables/samples/SampleTables.xlsx`
Run the tests: `python -I tools/tables/test_export_tables.py`
