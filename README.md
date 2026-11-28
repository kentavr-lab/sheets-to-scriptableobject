# Sheets to ScriptableObject

[Русская версия](README.ru.md)

A Unity package that imports Google Sheets and CSV tables into typed ScriptableObject configs. Column types are defined in the sheet header (then classes are generated) or in your code. Data is checked before import, and changes are shown as a diff — they are applied only after confirmation.

## Requirements

- Unity 2022.3 LTS or newer.
- No third-party dependencies (no Odin, no Google SDK, no Newtonsoft).

## Installation

**By git URL.** *Window → Package Manager → + → Add package from git URL…* and paste:

```
https://github.com/kentavr-lab/sheets-to-scriptableobject.git
```

A specific version, with a tag:

```
https://github.com/kentavr-lab/sheets-to-scriptableobject.git#v0.4.0
```

**Via `Packages/manifest.json`.** Add a line to `dependencies`:

```json
{
  "dependencies": {
    "com.kentavr-lab.sheets-to-scriptableobject": "https://github.com/kentavr-lab/sheets-to-scriptableobject.git"
  }
}
```

To pin a version, append `#v0.4.0` to the URL.

**From disk.** Clone the repository, then *Package Manager → + → Add package from disk…* and select `package.json` in the root of the clone.

**From `.unitypackage`.** Download `SheetsToSO-<version>.unitypackage` from the [Releases](https://github.com/kentavr-lab/sheets-to-scriptableobject/releases) page, then *Assets → Import Package → Custom Package…* and select the file. The files go to `Assets/SheetsToSO/`. To update, delete `Assets/SheetsToSO/` and import the new `.unitypackage`.

> Use only one installation method. The package via Package Manager and the `.unitypackage` in the same project give duplicate classes and GUIDs.

## Usage

The row class is `[Serializable]` with `ISheetRow`; the table is a subclass of `SheetTable<>`:

```csharp
using System;
using Kentavr.SheetsToSO;
using Kentavr.SheetsToSO.Core;
using UnityEngine;

[Serializable]
public class UnitRow : ISheetRow
{
    [SerializeField] private string id;
    public int hp;

    public string Id => id;
}

[CreateAssetMenu(menuName = "Tables/Units")]
public sealed class UnitsTable : SheetTable<UnitRow> {}
```

Lookup by `id` (case-sensitive):

```csharp
if (unitsTable.TryGet("knight", out var knight))
{
    Debug.Log(knight.hp);
}
```

Rows without an `id` are not included in the lookup; if an `id` is repeated, the first row is used. In both cases an error with a link to the asset is logged to the console.

A row of a referenced table is available through a property that the importer generates for each `ref<X>` column (the column name capitalized):

```csharp
Weapons w = unitsTable.Get("knight").Weapon; // row of the Weapons table or null
IReadOnlyList<Items> drops = unitsTable.Get("knight").Drops; // for ref<Items>[]
```

The `weapon` field still stores the `id`. The importer links the `Units.asset` asset to `Weapons.asset` from the same assets folder by itself. An empty or missing `id` and an unlinked table give `null` (old assets — until reimported). For your own row classes, like `UnitRow` above, and in schema from code mode, the properties are not generated. Details and naming rules are in the [data format](Documentation~/data-format.md#refx-properties).

### `id` dropdown for your own fields

Your own field that stores a table row `id` (in a `MonoBehaviour`, a prefab, your own ScriptableObject) can be marked with the `[SheetRef]` attribute — instead of a text input, the Inspector shows a list of `id` values from the table asset:

```csharp
using System.Collections.Generic;
using Kentavr.SheetsToSO;
using UnityEngine;

public class Unit : MonoBehaviour
{
    [SheetRef(typeof(WeaponsTable))] public string weaponId;
    [SheetRef(typeof(WeaponsTable))] public string[] spareWeapons;     // each element has its own list
    [SheetRef(typeof(ItemsTable))] public List<string> drops;

    // The project has several WeaponsTable assets — specify the one to use
    [SheetRef(typeof(WeaponsTable), Asset = "Assets/Data/Boss/Weapons.asset")] public string bossWeaponId;
}
```

- The type in the attribute is a non-abstract subclass of `SheetTable<>`; the field is `string`, `string[]` or `List<string>`. The attribute lives in the runtime assembly and does not pull the editor into the build.
- The asset is searched by exact type (subclasses do not count), in `Assets/` and `Packages/`. If there are several assets and `Asset` is not specified, the first one by path is used, and a warning with that path is shown above the field. If `Asset` is specified, only it is used; if there is no asset at that path, it is an error. The path in `Asset` is compared case-sensitively and must match the path in the Project window (`Assets/Data/Weapons.asset`, not `Assets/data/weapons.asset`).
- The list: the first item is `(empty)` / «(пусто)», then the `id` values in table row order (without empty ones and duplicates), with a search field on top. A selection is undone with `Ctrl+Z`.
- An empty value is allowed and is shown as `(empty)` / «(пусто)». A value that is not in the table is shown in red with a tooltip and is not erased.
- No table asset or a wrong type in the attribute — a plain text field with an error above it; the value can be edited by hand. The attribute on a non-`string` field — a plain field and a message that `string` is required.

**Checking in Unity.** There are no automated tests for the `[SheetRef]` drawer; it is checked manually:

1. A `MonoBehaviour` in a prefab with `[SheetRef(typeof(WeaponsTable))] public string weaponId;` — the field is shown as a dropdown button; the list contains `(empty)` / «(пусто)» and all `id` values from `Weapons.asset` in row order; searching by part of an `id` filters the list.
2. Selecting an `id` writes it to the field, is undone with `Ctrl+Z`, and stays in the prefab after saving.
3. An empty value — `(empty)` / «(пусто)» without an error.
4. A value that is not in the table (type it in the Debug mode of the Inspector, or delete the row from the sheet and reimport) — red with a tooltip; after opening and closing the list without selecting, the value is the same.
5. `string[]` and `List<string>` with the attribute — each element has its own list; adding and removing elements works as usual.
6. The same field in your own ScriptableObject works the same as in a prefab.
7. Two `WeaponsTable` assets: without `Asset` — a warning above the field with the path of the asset used; with `Asset = "<path to the second>"` — the list from the second asset without a warning; a wrong path in `Asset` — an error at the field, text input remains.
8. No `WeaponsTable` asset at all — a text field with an error; the value can be edited.
9. The attribute on an `int` field — a message that `string` is required, without exceptions in the Console.
10. The project builds without errors or warnings from the package, including a player build.

### Import window

*Window → Sheets to SO → Import*. From top to bottom:

- **Language** — the **EN** / **RU** switch on the right of the first line. It translates everything the package shows in the editor by itself: this window (labels, buttons, hints, results), the confirmation dialog, the *Import CSV folder here…* menu item and the `[SheetRef]` messages in the Inspector. Data errors and warnings, the diff and the import report in the Console stay in English, the same as in the [command-line check](#command-line-check-ci). The change applies at once, without a restart, and keeps the settings and the check result. The choice is stored per computer in `EditorPrefs` (key `Kentavr.SheetsToSO.Language`); the default is English. The Russian names are given below in «».
- **Source** («Источник»):
  - *Google API* — spreadsheet ID and API key (Google Sheets API, the spreadsheet is shared as *Anyone with the link — Viewer*). **Refresh sheet list** («Обновить список вкладок») reads the spreadsheet's sheets.
  - *Export* — spreadsheet ID and a manual list of sheets: the sheet name and its `gid` (the number after `gid=` in the sheet URL). No key is needed; each sheet is loaded by the CSV export link.
  - *CSV files* («CSV-файлы») — a folder on disk; each `.csv`/`.tsv` is a sheet named after the file without extension.
- **Assets folder, code folder** («Папка ассетов», «Папка кода»), **Namespace** — by default `Assets/Data`, `Assets/Data/Generated`, `SheetsData`.
- **Sheets** («Вкладки») with checkboxes; sheets and files starting with `#` are unchecked by default. **All** / **None** («Все» / «Ничего») buttons.
- **Table type** («Тип таблицы») for each sheet: *Generate code* («Генерировать код») or your own `SheetTable<>` subclass from the project — then no code is generated for the sheet, and the data is parsed by the fields of your row class (see [Schema from code](Documentation~/data-format.md#schema-from-code)).
- **Check** («Проверить») loads the selected sheets and checks them without writing anything: source and data errors with addresses (`Units!C5: …`), warnings (for example, `,` in numbers in the export) and the diff against the last applied import into this assets folder. During loading there is **Cancel** («Отмена»); closing the window also cancels loading.
- **Apply** («Применить») is active only after a check without errors and while the settings and checkboxes have not changed. It imports the same loaded data, without loading again. If the diff has removed rows or sheets, it asks for confirmation first. The result is shown in the window and in the Console; if the code has changed, the assets are updated after scripts are recompiled. Assets whose rows have not changed are not re-saved and are listed in the report as `Unchanged assets`.

**Where settings are stored.** The API key is stored in the editor user settings (`EditorPrefs`, key `Kentavr.SheetsToSO.ApiKey`): outside the project and git, in plain text in the user's registry / plist. The key does not get into reports, the log or project files; the **Clear** («Очистить») button removes it. Other window settings are in `UserSettings/Kentavr.SheetsToSO/ImportWindow.asset` (the `UserSettings/` folder is outside `Assets/` and in the standard Unity `.gitignore`).

### Import a CSV folder (temporary command)

The main way to import is the window above; the command is kept for the time being. Right-click a folder in the Project window → *Sheets to SO → Import CSV folder here…* (with the RU language — «Импортировать сюда папку CSV…») → select a folder on disk with `.csv`/`.tsv` files. Each file is a sheet (the file name without extension; files starting with `#` are skipped). Assets are created in the selected folder (`Units.asset`), code in its `Generated/` subfolder, the namespace is `SheetsData`. Running it again updates the assets in place. Details are in the [Import](Documentation~/data-format.md#import) section of the data format.

## Command-line check (CI)

The `Cli~/` application loads sheets, parses them and checks references and enums with the same core code as the editor, and prints a report to the console. It requires .NET SDK 8.0 or newer. From the repository root:

```
dotnet run --project "Cli~" -- --source csv --file Units.csv
```

Three sources:

```
# CSV/TSV files: one sheet = one file, the sheet name is the file name without extension; .tsv is tab-separated
dotnet run --project "Cli~" -- --source csv --file Data/Units.csv --file Data/Weapons.tsv

# Google Sheets API: without --sheet, all sheets are checked except those starting with #
dotnet run --project "Cli~" -- --source api --spreadsheet <ID> [--sheet Units --sheet Weapons]

# CSV export by link: the link has no sheet name, so a sheet is given as name=gid
dotnet run --project "Cli~" -- --source export --spreadsheet <ID> --sheet Units=0 --sheet Weapons=123456789
```

The API key for `--source api` is taken from the `SHEETS_TO_SO_API_KEY` environment variable (another name — `--api-key-env <NAME>`). The key is not accepted in arguments and does not get into the output. Help — `--help`.

The report has one line per problem and a summary at the end:

```
error: Units!C2: Column 'hp': 'abc' is not a valid int
error: Units!D2: Column 'weapon': 'axe': no row with this id in sheet 'Weapons'
Sheets: 2, errors: 2, warnings: 0
```

| Exit code | Meaning |
|---|---|
| `0` | No errors (warnings, for example about `,` in numbers, do not affect the code). |
| `1` | Data errors: header, cells, `id`, CSV syntax, `ref`/`enum`. |
| `2` | Argument or access errors: unknown option, missing file, API key not set, 403 response, HTML instead of CSV. The message goes to stderr; parsing is not performed. |

## Documentation

- [Data format](Documentation~/data-format.md) — sources, sheet rules, types, what counts as an error.

## Structure

| Assembly | Folder | Responsibility |
|---|---|---|
| `Kentavr.SheetsToSO.Core` | `Runtime/Core/` | The whole pipeline without Unity: sources, parsing, checks, diff. Does not reference `UnityEngine`/`UnityEditor` or third-party libraries, so it works in the editor, in the console and in CI. |
| `Kentavr.SheetsToSO` | `Runtime/` | Runtime: the base table ScriptableObject with lookup by `id`, the `[SheetRef]` attribute. |
| `Kentavr.SheetsToSO.Editor` | `Editor/` | Editor only: import window, code generation, API key storage, the `[SheetRef]` drawer. |
| — | `Cli~/` | The `sheets-to-so` console application for CI: checks sheets with an exit code. References the core via `Tests~/Core`; not included in the Unity package. |

Unity does not import the `Cli~/`, `Documentation~/`, `Tests~/` and `Tools~/` folders. `Tools~/release.py` builds the `.unitypackage` for releases; besides these folders, it leaves out `Tests/` (the Unity tests of the package).

## Tests

The core is tested without Unity: the `Tests~/Core` project compiles the same sources from `Runtime/Core/` as Unity does. `Tests~/Editor.Tests` checks the editor string tables from `Editor/Localization/` (every language has every text, with the same placeholders as English). From the repository root:

```
dotnet test "Tests~/Core.Tests"
dotnet test "Tests~/Cli.Tests"
dotnet test "Tests~/Editor.Tests"
```

Requires .NET SDK 8.0 or newer.

Unity EditMode tests (editor language, the Assets menu item, the import window) are in `Tests/Editor/`: add `"testables": ["com.kentavr-lab.sheets-to-scriptableobject"]` to `Packages/manifest.json` of the project and open *Window → General → Test Runner → EditMode*.

## License

[MIT](LICENSE)
