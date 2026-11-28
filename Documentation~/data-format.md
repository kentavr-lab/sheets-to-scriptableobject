# Data format

The format supported by the package.

Format version: **1** (`Kentavr.SheetsToSO.Core.FormatInfo.Version`).

## Sources

| Source | How it connects | Note |
|---|---|---|
| Google Sheets API v4 + API key | `values:batchGet`, `valueRenderOption=UNFORMATTED_VALUE`, `dateTimeRenderOption=SERIAL_NUMBER` | **Recommended.** The spreadsheet is shared as *Anyone with the link — Viewer*. Dates and times arrive as serial numbers (for example, `46349.5`), `bool` as `TRUE` / `FALSE`. |
| CSV export by link | `https://docs.google.com/spreadsheets/d/<ID>/export?format=csv&gid=<GID>` | One sheet per link. |
| Local files | CSV or TSV | One file = one sheet. |

- You can load the whole spreadsheet or only selected sheets.
- The API key is stored outside `Assets/` and outside git.
- The package does not use the Google SDK or Odin.

### CSV export by link

- No key is needed: the spreadsheet must be shared as *Anyone with the link — Viewer*. One sheet is loaded by the spreadsheet `ID` and the sheet `gid` (`CsvExportSource`).
- **HTML instead of CSV** is a `NotCsv` error with a hint about access. A private spreadsheet usually responds not with `403` but with the Google sign-in page and code `200`, so HTML is detected both by `Content-Type: text/html` and by `<` at the start of the response.
- **Unknown `gid`** is an `InvalidGid` error (Google responds with HTTP 400); the message contains the `gid` and the spreadsheet `ID`. Other HTTP codes are `HttpError` (for `401`/`403`/`404` with a hint about access); no network or a timeout is `NetworkError`.
- **Spreadsheet locale.** The export returns values formatted for the spreadsheet locale, so in locales with a comma `1.5` arrives as `1,5`. If an `int`/`float` column (including an array) contains values with `,`, the source emits a `CommaInNumber` warning — one per column, with the address of the first such cell and the number of values; when parsing a `float`, such a value is an error (see [Value parsing](#value-parsing)). The recommended locale is one with a dot, for example *United States* (*File → Settings → Locale*).

## Sheet rules

- **Sheet = data type.** The sheet name is the type name (and the name of the ScriptableObject config).
- **Skipping:** a sheet, column or row whose name (for a row, the `id` value) starts with `#` is not imported. Handy for comments and drafts.
- **Row 1 is the header**; data starts at row 2.
- Columns are matched to fields **by name, not by position**: you can reorder columns.
- **The first column is `id`:** required, unique within the sheet, made of Latin letters, digits and `_`.
- **Empty rows** — where all imported cells are empty — are skipped. `id` values are compared case-sensitively: `Sword` and `sword` are different rows.

## Where types come from: two modes

1. **Schema from the sheet.** The column header is `name:type`. Row classes and enums are generated from the headers.
2. **Schema from code.** You write your own row class; the sheet header contains only column names.

If a type is given both in the header and in code, they must match — otherwise it is an error.

### Schema from code

You write the row class; a column is matched to a field **by exact name, case-sensitively** (`hp` ≠ `Hp`), and the column type is taken from the field. Core call: `SheetParser.Parse("Units", rows, typeof(UnitRow))`; for a set of sheets — `SheetSetChecker.Check` with a row type for each sheet (`null` means schema from the sheet).

**How to enable it in the import window.** Write a row class and a derived table:

```csharp
public enum Rarity { Common, Epic }

[Serializable]
public class UnitRow : ISheetRow
{
    public string id;
    public string name;
    public int hp;
    [SerializeField] private float speed;
    public Rarity rarity;
    public List<string> tags;

    public string Id => id;
}

public class MyUnitsTable : SheetTable<UnitRow> { }
```

In the `Window → Sheets to SO → Import` window, each sheet (Google API, Export, *CSV files* («CSV-файлы»)) has a **Table type** («Тип таблицы») list: *Generate code* («Генерировать код», schema from the sheet) or any non-abstract `SheetTable<>` subclass in the project, by full name. The row class is taken from the selected table. The choice is stored by sheet name (for CSV, the file name without extension), shared by all sources, and survives a Unity restart. If you rename a sheet (including in the Export source's *Sheet name* («Имя вкладки») field), the choice does not follow it — select the type again. If the selected type no longer exists in the project, the list shows it as `<name> (not found)` / «<имя> (не найден)», and the check reports the error `Sheet 'Units': table type '…' not found`.

For such a sheet:

- no code is generated (neither the row class, nor the table class, nor the enums of its fields), and there is no recompilation; if the import has no sheets with code generation, the code folder is not created;
- the asset is `<assets folder>/<Sheet>.asset` of the selected type;
- the row class must be `[Serializable]` and have a public parameterless constructor — otherwise it is a sheet error (Unity would not save the rows);
- the names `<Sheet>` / `<Sheet>Table` do not take part in the type name conflict check;
- the "first column is `id` of type `string`" check applies as usual.

**enum.** The column type is the C# type of the field; the allowed values are its members. An unknown value is a cell error listing the allowed values: `Column 'rarity': 'Legendary' is not a value of enum 'Rarity'; allowed: Common, Epic`. Set errors:

- two different C# enums with the same short name in sheets with schema from code;
- a sheet with code generation uses `enum<X>`, and `X` is a C# enum of a sheet with schema from code (the generated code would get a second type `X`).

**References** work as usual: `ref<X>` is written in the header over a `string` / `string[]` / `List<string>` field; the target sheet can be in either mode.

**Changing a sheet's mode.** If the sheet was previously imported with code generation, its asset has the generated type `<Sheet>Table`, and importing with another type gives the error `existing asset of type '…' is not '…'; not overwritten`: delete the asset manually. Old generated files `<Sheet>.cs` / `<Sheet>Table.cs` are not deleted either — delete them yourself if you no longer need them. The first check after changing the mode (in either direction) parses the previous import's snapshot using the new mode, so its diff may show extra changes or report that the previous import cannot be read; this does not affect writing the data, and after applying the diff is accurate again.

**Which fields are taken into account** — the same ones Unity serializes:

- instance fields that are not `static`, `readonly` or `const`, without `[NonSerialized]`;
- `public` ones always, others (`private`, `protected`, `internal`) only with `[SerializeField]`;
- fields of base classes too (including their `[SerializeField] private` ones); when names coincide, the derived class field wins;
- properties are not taken into account.

**Type mapping:**

| C# field type | Column type |
|---|---|
| `string` | `string` |
| `int` | `int` |
| `float` | `float` |
| `bool` | `bool` |
| any `enum E` | `enum<E>` (short type name) |
| `T[]`, `List<T>` of the types above | `T[]`, for example `int[]`, `enum<Rarity>[]` |

Other types (`double`, `long`, `Vector3`, nested classes, arrays of arrays, `List<double>`, etc.) are not supported.

**The type in the header** is optional. If it is given, it must match the field type: base type, array or not, and for `enum` the name. The exception is **`ref<X>`**: a reference stores a row `id`, so `ref<X>` is allowed for a `string` field, and `ref<X>[]` for `string[]` and `List<string>`; the column stays `ref<X>` and is checked as a reference. Required (`!`) is set only in the header: `name:string!`.

**Fields without a column** are not an error: they keep their default value.

**`ref` properties** (the row of the referenced table, see [`ref<X>` properties](#refx-properties) in [Code generation](#code-generation)) are not generated in this mode: the row class is yours and the importer does not extend it. Look up a row of another table yourself with `TryGet`.

**Declare the `id` field** as `string`: the `id` column type is also taken from the field, and with `int id` text ids like `warrior` produce an `int` parsing error in addition to the `id` check.

**Errors** are reported on the header cell (`Units!B1`); a column with an error is not imported, the rest are parsed:

| Code | When | Example message |
|---|---|---|
| `TypeMismatch` | the header type does not match the field | `Column 'hp' has type 'float' in header, but field 'UnitRow.hp' is 'int'` |
| `UnknownField` | the column has no field | `Column 'hp' has no matching field in 'UnitRow'; did you mean 'Hp'?` (the hint appears if there is a field that differs only in case) |
| `UnsupportedFieldType` | the field type is not in the table above | `Field 'UnitRow.damage' for column 'damage' has unsupported type 'Double'` |

## Types v1

| Type | Header example | Cell example | Meaning |
|---|---|---|---|
| `string` (default) | `id`, `title` | `sword_01` | string; the type can be omitted |
| `int` | `hp:int` | `120` | integer |
| `float` | `speed:float` | `3.5` | fractional number, **always with a dot**, regardless of locale |
| `bool` | `stackable:bool` | `true` | boolean |
| `enum<Name>` | `rarity:enum<Rarity>` | `Epic` | a value of the `Rarity` enum |
| `ref<Sheet>` | `weapon:ref<Weapons>` | `sword_01` | a reference to a row `id` in the `Weapons` sheet |
| `type[]` | `tags:string[]` | `melee\|starter` | an array in one cell, elements separated by `\|` |

- **Arrays:** elements are separated by the `|` character. To use `|` inside an element, escape it: `\|`.
- **Required column:** `!` at the end of the type, for example `name:string!`. An empty cell in such a column is an error.
- **Empty cells** in other columns give the default value for the type.
- **Dates:** store them as strings in ISO 8601 (`2026-11-23`, `2026-11-23T16:05:00Z`).

### Value parsing

- **An empty cell** is empty or whitespace only. In a column with `!` it is an error, in others it gives the default value:

  | Type | Value | Default |
  |---|---|---|
  | `string` | text as is, whitespace is not trimmed | `""` |
  | `int` | `int` | `0` |
  | `float` | `float` | `0` |
  | `bool` | `bool` | `false` |
  | `enum<Name>` | value name (string) | no value (`null`) |
  | `ref<Sheet>` | row `id` (string) | no value (`null`) |
  | `type[]` | array of elements of the type | empty array |

- **`int`:** an integer with or without a sign; surrounding whitespace is ignored. `1.0`, `1 000`, `abc` and values out of `int` range are errors.
- **`float`:** a dot as the decimal separator; a sign and an exponent (`1.5e-2`) are allowed, regardless of locale. `NaN`, `Infinity` and values out of `float` range are errors. `1,5` is an error with the hint `use '.' as decimal separator: '1.5'`.
- **`bool`:** `true` / `false`, case-insensitive (Google Sheets checkboxes give `TRUE` / `FALSE`); anything else is an error.
- **`enum`, `ref`:** text without surrounding whitespace; whether the value is allowed and whether the row exists are checked separately (see [Reference and enum checks](#reference-and-enum-checks)).
- **Arrays:** elements are separated by `|`; `\|` is the `|` character inside an element, any other `\` is an ordinary character. `string[]` elements are not trimmed and can be empty (`a||b` → `a`, empty string, `b`). For other types each element is parsed as a single value (surrounding whitespace is ignored), and an empty element is an error. Errors of all elements are collected; the message contains the element's index and text.
- **Single `string`:** escaping does not apply, `a\|b` stays `a\|b`.
- Errors do not stop parsing: all errors of a row are collected, each with a cell address (`B7`).

### Reference and enum checks

After parsing, the set of sheets is checked as a whole; errors of all sheets are collected into one list with addresses `Sheet!B7`. Empty cells are not checked; for arrays each element is checked, and the message contains the element's index and text.

- **`ref<X>`** is checked within the loaded set of sheets. If sheet `X` is not among the loaded ones, there is one error on the column header (for example, `Units!E1`), and the column's cells are not checked. Otherwise the value must match the `id` of one of the rows of `X` (case-sensitively: `Sword` ≠ `sword`). A reference to the same sheet is allowed.
- **Source of `enum<E>` values:**
  - if the calling code passed a list of values of `E` (for example, from an existing C# enum), the value must match a list element exactly, case-sensitively; otherwise it is an error listing the allowed values;
  - if there is no list, any value that is a valid C# name is allowed (Latin letters, digits and `_`, not starting with a digit, not a keyword). The values found are collected without duplicates in order of appearance (sheets in order, rows top to bottom, columns left to right) and go into enum generation.

**When parsing fails**, reference and enum checks are still performed so that all errors are visible in one pass. Rules:

- A cell that failed to parse itself (`abc` in `int`, an empty required cell, an empty array element) is not checked again. A row with such an error remains a reference target, and its other `ref`/`enum` cells are checked.
- A row with an empty `id` is not a reference target (there is nothing to refer to); its other cells are checked.
- An invalid (`sword 1`) or duplicate `id` is treated as existing: a reference to it does not produce a "no row with this id" error, since the error is already shown on the `id` itself. After the `id` is fixed, a reference to the old value may break — the next check will show it. The other cells of such a row are checked.
- A column with an error in its header (except `id`) drops out of parsing: its cells are not checked. The other columns and the sheet's `id` are reliable.
- If a sheet has no parsed `id` column (an error in the first column header, the first column is not `id`, no columns), it is not a reference target: `ref<X>` cells pointing to it are not checked, and there is no "sheet not loaded" error. The sheet's own `ref`/`enum` cells are checked.
- An `id` column that is not of type `string` (`id:int`) is an error, but the sheet remains a reference target by the `id` text.
- With errors in sheet names (no name, a duplicate case-sensitively or case-insensitively) or an empty set, reference and enum checks are not performed: it is unclear which sheet a reference points to.

Example header of the `Units` sheet:

```
id | name:string! | hp:int | rarity:enum<Rarity> | weapon:ref<Weapons> | tags:string[] | #notes
```

## Output

- One ScriptableObject per sheet: a list of rows and lookup by `id`.
- Code generation templates can be replaced; the namespace and asmdef for the generated code are configurable.

### Code generation

From the sheet header, a row class is generated (`[global::System.Serializable] public sealed partial class`) — one `public` field per column, in column order, field name = column name. Columns with `#` are not included in the class. Each enum from `enum<Name>` is generated separately with its list of values; the values are C# identifiers, without duplicates, each with an explicit number (`Common = 0,`), see "Enum values" below. A generated file starts with a `// <auto-generated>` block: do not edit it manually, changes will be lost on the next generation. Line endings are always `\n`.

| Column type | C# field type |
|---|---|
| `string` | `string` |
| `int` | `int` |
| `float` | `float` |
| `bool` | `bool` |
| `enum<X>` | `X` |
| `ref<X>` | `string` — the row `id` in sheet `X` (with the comment `// ref<X>: row id in sheet X` above the field) |
| `type[]` | an array of the same type, for example `int[]`, `X[]`, `string[]` for `ref<X>[]` |

For each sheet, besides the row class, a table class is generated — a concrete ScriptableObject from which the asset is created. The same file contains the second part of the `partial` row class: the `ISheetRow` implementation (`Id` returns the `id` field, so the `id` column must be of type `string`), `ISheetTableRow` (the row knows its table; the `__table` field is not serialized) and properties for `ref<X>` columns:

```csharp
public sealed partial class Units : global::Kentavr.SheetsToSO.Core.ISheetRow, global::Kentavr.SheetsToSO.ISheetTableRow
{
    [global::System.NonSerialized] private global::Kentavr.SheetsToSO.SheetTableBase __table;

    string global::Kentavr.SheetsToSO.Core.ISheetRow.Id => id;

    void global::Kentavr.SheetsToSO.ISheetTableRow.SetTable(global::Kentavr.SheetsToSO.SheetTableBase table) => __table = table;

    // ref<Weapons>: row of sheet Weapons by 'weapon'; null if empty, not found or the table is not linked
    public global::SheetsData.Weapons Weapon => global::Kentavr.SheetsToSO.SheetRefs.Get<global::SheetsData.WeaponsTable, global::SheetsData.Weapons>(__table, weapon);
}

public sealed class UnitsTable : global::Kentavr.SheetsToSO.SheetTable<Units>
{
}
```

#### `ref<X>` properties

The field of a `ref<X>` column stays a string with an `id` (`public string weapon;`); storage in the asset does not change. In addition, for each `ref<X>` column a property is generated that returns the row of table `X`:

```csharp
Weapons w = unitsTable.Get("knight").Weapon; // row of the Weapons table or null
```

- **Name:** the column name with the first letter in upper case: `weapon` → `Weapon`, `dropItems` → `DropItems`.
- **Type:** `ref<X>` → `X`, `ref<X>[]` → `IReadOnlyList<X>`. Types in the code are fully qualified, so a property can have the same name as a type (`weapons:ref<Weapons>[]` → property `Weapons`).
- **`ref<X>`:** `null` if the `id` is empty, there is no row with this `id` in the table (for example, the asset was edited by hand) or the table is not linked. No exceptions and no messages in the Console — the property can be read in a loop.
- **`ref<X>[]`:** a list of the same length as the `id` array, with matching indices; `null` in place of an empty or missing `id`. An empty `id` array gives an empty list. An unlinked table gives `null`. Each call creates a new list: in a loop, store it in a variable.
- **The table is not linked** when the asset was imported by an older version and has not been reimported yet, when there is no asset `X` in the same assets folder or it is of another type (see [Assets and code](#assets-and-code)), and for a row created in code that is not in a table.
- **A name conflict** is a check error on the column header, and the import is not applied: the property name must not match the name of a column of the sheet (including its own: `Weapon:ref<Weapons>`, `_weapon:ref<Weapons>`), the name of the row class (`units:ref<X>` in the `Units` sheet) or the `__table` field. There are no automatic suffixes: the property name is always predictable, and a conflict is resolved by renaming the column.
- In schema from code mode, the properties are not generated.

Names and files (file name = class name — Unity requires this for ScriptableObject):

| What | Name | File |
|---|---|---|
| row class | sheet name: `Units` | `Units.cs` |
| table class | `<Sheet>Table`: `UnitsTable` | `UnitsTable.cs` |
| enum | name from `enum<Rarity>` | `Rarity.cs` |

Names of row classes, table classes and enums must not coincide with each other (for example, sheets `Units` and `UnitsTable` at the same time is an error).

**Enum values** are taken from the data: the distinct values of all imported `enum<X>` and `enum<X>[]` columns in order of first appearance (sheets in order, rows top to bottom, columns left to right). A value that is not a valid C# name is an error with a cell address (`Units!C5`). An empty enum cell adds no members, and in the asset gives the field's default value — the number 0 (usually the first member of the enum).

**Enum value numbers are fixed.** Unity stores an enum field as a number, so the numbers must not change between imports:

- Existing values are taken from the already generated file `<code folder>/<Enum>.cs` (it lives in `Assets/` and is committed, so the numbers are the same for the whole team). The order and numbers of these values are preserved and no longer depend on the row order in the sheet.
- New values are appended at the end in order of first appearance, with numbers `max + 1`, `max + 2`… (on the first generation — `0, 1, 2…`).
- A value that no longer exists in the sheets stays in the enum with its previous number; the import report and the Console show a warning.
- A file generated by an older version (without numbers: `Common,` `Epic,` `Rare,`) is read by C# rules — 0, 1, 2… in file order; the first import rewrites it with explicit numbers, and the meaning of the numbers does not change.
- If an existing file cannot be parsed (edited by hand, a non-standard template), the import stops with an error and nothing is written. `//` and `/* */` comments and `Name` and `Name = integer` members are parsed; a duplicate name or number is an error.
- To renumber values or remove a missing one, delete the enum file (or a line in it) manually — the generator takes what is in the file.

Template placeholders (plain text replacement; unknown `{{…}}` stay as is):

| Placeholder | Template | Value |
|---|---|---|
| `{{Header}}` | all | the `// <auto-generated>` block |
| `{{Namespace}}` | all | namespace from the settings |
| `{{ClassName}}` | row class | class name |
| `{{Fields}}` | row class | fields, each line indented by 8 spaces |
| `{{EnumName}}` | enum | enum name |
| `{{Members}}` | enum | values with explicit numbers separated by `,` (`Common = 0,`), each on its own line indented by 8 spaces; a custom template must keep `enum {{EnumName}} { {{Members}} }` so that the numbers can be read on the next import |
| `{{TableClassName}}` | table class | table class name |
| `{{RowClassName}}` | table class | row class name |
| `{{RefProperties}}` | table class | `ref<X>` properties; empty if there are no `ref` columns. Each property starts with an empty line and a comment, indented by 8 spaces, so the placeholder is placed right after the last member of the row class, on the same line. The properties access the `__table` field: together with the placeholder, keep the `__table` field and `ISheetTableRow.SetTable` from the default template in your template. Without the placeholder the properties are not generated (this is not an error) |

## Import

Pipeline: **load → parse → check → show diff → apply**.

1. Load the selected sheets from the source.
2. Parse headers and values by the rules above.
3. Check. Each error has a cell address in the form `Sheet!B7`.
4. Diff with the last applied import: added / removed / changed.
5. Apply — only after confirmation.

Reference and enum checks and the type name conflict check run together with parsing, even if parsing has errors: all errors are visible in one pass (rules — in [Reference and enum checks](#reference-and-enum-checks), *When parsing fails*). The exception is sheet name errors: then only they and parsing errors are shown.

Errors block the import — both parsing errors and cross-sheet ones (broken `ref<>`, the sheet from `ref<X>` not loaded, invalid enum values, a type name conflict). When run from the CLI, errors give a non-zero exit code: 0 — no errors, 1 — data errors, 2 — argument or access errors (see [Command-line check (CI)](../README.md#command-line-check-ci)).

**What the diff is compared with.** After a successful apply, the raw cells of the imported sheets are saved to `Library/Kentavr.SheetsToSO/snapshots/<assets folder, '/' → '_'>.json` — this is the "old" snapshot for the next check of the same assets folder. An import replaces only its own sheets in the snapshot (names compared case-insensitively); the rest remain from previous imports. Only the sheets of the current set take part in the diff: a sheet that was not selected this time is not considered removed; a sheet that is not in the snapshot is added. The snapshot is per machine (`Library/` is not in git): on another machine or after deleting `Library/`, the first diff shows all sheets as added. If a compared sheet of the snapshot cannot be parsed without errors (for example, under new rules), the diff is not built and an explanation is shown; this does not block applying. Cross-sheet checks are not applied to the snapshot: its sheets may come from different imports.

### Assets and code

- **Location.** A sheet's asset is `<assets folder>/<Sheet>.asset` (`Assets/Data/Units.asset`). Code goes to the code folder, by default `<assets folder>/Generated/`. Both folders are inside `Assets/`.
- **Errors.** If at least one sheet has errors (including cross-sheet ones), neither code nor assets are changed; errors with addresses are shown in the import window and in the console.
- **Code.** A file is rewritten only if its text has changed — without unnecessary recompilation. If the code has changed, the assets are updated automatically after scripts are recompiled (with a separate summary in the console). If the code has not changed, the assets are updated immediately. Stale code files are not deleted.
- **Reimport** updates the existing asset in place: rows are replaced, and the GUID and references to the asset from scenes and prefabs are preserved. An asset with the same path but of another type is not overwritten — this is an error.
- **Assets without a sheet.** Table assets directly in the assets folder (not in subfolders) that have no sheet in the import are not deleted: for each one there is a warning in the console with its path, and clicking it selects the asset.
- **Linked tables.** A table asset stores references to the table assets its `ref<X>` columns point to (the `linkedTables` field, visible in the Inspector in Debug mode). The importer sets them itself on every import, after all assets are created and updated: it looks for `<assets folder>/<X>.asset` of type `<Namespace>.XTable`; a reference to its own sheet is set too. The list is replaced as a whole; for a sheet without `ref` it is empty. If asset `X` is not found or is of another type, the reference is not set, and the report and the Console show a warning (clicking it selects the asset), for example: `Assets/Data/Units.asset: column 'weapon' refers to sheet 'Weapons', but 'Assets/Data/Weapons.asset' is not a 'SheetsData.WeaponsTable' asset; Units.Weapon will return null`. The warning does not stop the import. The link between rows and their table is restored when the asset is loaded, without global state, `Resources` or asset search at runtime. Links between tables from different assets folders are not set.
- **Old assets** (imported before `ref` properties appeared) are read as before: rows and `TryGet` work, and `ref` properties return `null` until reimported.
- If the code folder is under its own asmdef, it must reference `Kentavr.SheetsToSO` and `Kentavr.SheetsToSO.Core`.

### What counts as an error

- an empty `id`;
- a duplicate `id` within a sheet;
- an `id` not made of Latin letters, digits and `_`;
- a value that does not match the column type (for example, `abc` in `hp:int` or `3,5` in `float`);
- a value that is not in the passed enum list;
- an enum value without a passed list that is not a valid C# name (`1st`, `class`);
- a broken `ref<>` reference: the target sheet has no row with this `id`;
- the sheet from `ref<X>` is not among the loaded ones (one error on the column header);
- an invalid column name;
- an empty cell in a required (`!`) column;
- the property name of a `ref<X>` column matches the name of a column, the row class or the `__table` field (an error on the column header: `Units!E1: Column 'Weapon': property 'Weapon' generated for ref<Weapons> has the same name as column 'Weapon'; rename one of the columns`);
- a mismatch between the type in the header and in the row class (schema from code mode).

### Diff

Two snapshots of checked sheets (parse results without errors) are compared: the old one and the new one. A sheet with errors or a duplicate sheet name in one snapshot is a call error, and no diff is built from such data. An empty old snapshot (first import) means all sheets are added.

- **Sheets** are matched by name, **rows** by `id`. Case matters: `Sword` and `sword` are different `id` values, `Units` and `units` are different sheets.
- Changing an `id` is removing the row with the old `id` and adding a row with the new one; renames are not detected. The row number does not take part in the comparison: reordering rows is not a change.
- For a row present in both snapshots, fields are compared over the union of columns: first the columns of the new sheet in their order, then the removed columns in the order of the old one. Reordering columns is not a change. An added or removed column shows up as a field change in every row: on the side where the column is missing — `(missing)`.
- Values are equal if both type and value match: arrays element by element and in order, `float` exactly, without tolerance (`1.5` and `1.50` are equal, it is the same number). Empty `enum`/`ref` values (`null`) are equal to each other. When a column's type changes (`int` → `float`), values of different types are considered a change, even if they look the same: `120 → 120`.
- A sheet without changes is not included in the result. Order: sheets as in the new snapshot, then removed ones in the order of the old one; added and changed rows in the order of the new sheet, removed ones in the order of the old one.

Value text: a string in quotes (`"Sword"`), `int` and `bool` as is, `float` as the shortest exact form with a dot (`0.1`), empty `enum`/`ref` as `null`, an array as `["melee", "starter"]`, `[]`.

Example report:

```
Units: +1 -1 ~1
  + bow_03
  - axe_02
  ~ sword_01: hp: 120 → 150; name: "Sword" → "Long Sword"
+ Weapons (new sheet, 3 rows)
- Old (removed sheet, 2 rows)
```

A changed sheet shows counters of added (`+`), removed (`-`) and changed (`~`) rows, followed by the rows themselves. For an added or removed sheet the rows are not listed, only their count. If there are no changes — `No changes`.
