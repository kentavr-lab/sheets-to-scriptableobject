# Changelog

All notable changes to this package. The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/), and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

## [0.3.0] — 2026-11-28

### Added

- Sheet header row parsing: column types (`string`, `int`, `float`, `bool`, `enum<X>`, `ref<X>`), `type[]` arrays, required `!`, skipping columns starting with `#`, errors with a cell address (`Units!C5`).
- CSV/TSV reading: RFC 4180, BOM, `\r\n`/`\n`, row numbers for error addresses.
- Cell value parsing by column type: defaults for empty cells, required columns, arrays with `\|` escaping, numbers in the invariant culture, all errors of a row with a cell address.
- Base table ScriptableObject `SheetTable<TRow>`: a serializable list of rows and lookup by `id`.
- Code generation from the sheet header: row class, table class `<Sheet>Table` and an enum for each `enum<X>`.
- Data sources: Google Sheets API by spreadsheet ID and API key (sheet list, retries on `429` and `5xx`); CSV export by link without a key (sheet by `gid`, detection of HTML instead of CSV, warning about `,` in numbers); local CSV/TSV files — one file per sheet.
- Data checks: empty, duplicate and invalid `id` values; broken `ref<>` references between sheets and a sheet from `ref<X>` that is not loaded; invalid `enum<>` values; name conflicts of generated types.
- Diff with the last applied import into the same assets folder: added, removed and changed sheets, rows and fields.
- Import into ScriptableObject assets: one asset per sheet in the assets folder, code in the code folder; reimport updates assets in place, preserving GUIDs; on any error nothing is changed. Temporary command *Sheets to SO → Import CSV folder here…* in the folder context menu of the Project window.
- *Window → Sheets to SO → Import* window: source (*Google API*, *Export*, *CSV files* («CSV-файлы»)), sheet selection, **Check** («Проверить»: errors, warnings, diff) and **Apply** («Применить») with confirmation of removals; the API key is stored in `EditorPrefs`, outside the project and git.
- Console application `Cli~` (`sheets-to-so`) for checking sheets in CI with the same core code: `csv`, `api`, `export` sources, API key from an environment variable, exit codes `0` / `1` / `2`.
- Schema from code mode: for a sheet you can select your own `SheetTable<>` subclass — no code is generated, the data is parsed by the fields of your row class, and a type mismatch with the header is an error.
- Properties for `ref<X>` and `ref<X>[]` columns in generated row classes: they return the row of the referenced table or `null`; the importer links table assets from the same assets folder by itself.
- `[SheetRef]` attribute for your own `string`, `string[]`, `List<string>` fields: the Inspector shows a searchable dropdown of `id` values from the table asset; the asset to use can be specified via `Asset`.

### Changed

- Minimum Unity version is 2022.3 LTS (was 2021.3).
- Generated enum value numbers are fixed between imports: existing values are taken from the already generated file, new ones are appended at the end, and values that disappeared from the sheets keep their previous number with a warning.
- The check shows all errors in one pass: references and enums are checked even when parsing fails.
- Assets whose rows have not changed are not re-saved and are listed in the report as `Unchanged assets`.

### Fixed

- False `ref` errors on rows with an invalid or duplicate `id`: such an `id` is treated as existing, and the error is shown only on it.
- Import window: sheet names of the *CSV files* («CSV-файлы») source have no file extension; the `No sheets to import` error is no longer repeated on loading errors.

## [0.1.0]

- Package skeleton: `package.json`, assemblies `Kentavr.SheetsToSO`, `Kentavr.SheetsToSO.Core`, `Kentavr.SheetsToSO.Editor`, dotnet tests for the core, data format description. No import logic yet.
