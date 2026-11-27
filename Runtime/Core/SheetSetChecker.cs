using System;
using System.Collections.Generic;

namespace Kentavr.SheetsToSO.Core
{
    /// <summary>
    /// Checks a set of sheets before an import and compares it with the previous snapshot.
    /// See Documentation~/data-format.md, "Import".
    /// </summary>
    public static class SheetSetChecker
    {
        /// <summary>Shown instead of a diff when the previous snapshot has errors.</summary>
        public const string PreviousUnreadableText = "Предыдущий снимок не читается, diff недоступен";

        private const string TableSuffix = "Table";

        private static readonly EnumMembersResult NoEnums = new EnumMembersResult(
            Array.Empty<string>(), new Dictionary<string, IReadOnlyList<string>>(), Array.Empty<SheetError>());

        /// <summary>
        /// Parses and checks the sheets: names (empty, repeated ignoring case), headers and values,
        /// <c>id</c> as the first <c>string</c> column, <c>ref&lt;&gt;</c> and <c>enum&lt;&gt;</c> values across sheets
        /// and type name conflicts of the generated code. Errors are collected, never thrown.
        /// </summary>
        /// <param name="sheets">Sheet names and raw cells; <c>Rows[0]</c> is the header. A <c>null</c> row counts as empty.</param>
        public static SheetSetCheckResult Check(IReadOnlyList<(string Name, IReadOnlyList<IReadOnlyList<string>> Rows)> sheets)
        {
            if (sheets == null)
                throw new ArgumentNullException(nameof(sheets));

            var errors = new List<string>();
            var parsed = new List<SheetParseResult>();

            if (sheets.Count == 0)
                errors.Add("No sheets to import");

            // Ignore case: each sheet becomes files, and file names are case-insensitive on Windows and macOS.
            var names = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var source in sheets)
            {
                if (string.IsNullOrEmpty(source.Name))
                {
                    errors.Add("Sheet without a name");
                    continue;
                }
                if (names.TryGetValue(source.Name, out var first))
                {
                    errors.Add(first == source.Name
                        ? $"Duplicate sheet name '{source.Name}'"
                        : $"Duplicate sheet name '{source.Name}': same as '{first}' ignoring case");
                    continue;
                }
                names.Add(source.Name, source.Name);

                var sheet = SheetParser.Parse(source.Name, Rows(source.Rows));
                foreach (var error in sheet.Errors)
                    errors.Add(error.ToString());

                if (sheet.Columns.Count > 0)
                {
                    var id = sheet.Columns[0];
                    if (id.Type.Kind != ColumnKind.String || id.Type.IsArray)
                        errors.Add($"{sheet.SheetName}!{id.Address}: column 'id' must be of type 'string', found '{id.Type}'");
                }

                parsed.Add(sheet);
            }

            // Rows of a sheet with errors are not reliable for cross-sheet checks.
            if (errors.Count > 0)
                return new SheetSetCheckResult(parsed.AsReadOnly(), NoEnums, errors.AsReadOnly());

            // Invalid enum values are reported by both the validator and CollectEnumMembers: take them from the validator only.
            var cross = CrossSheetValidator.Validate(parsed);
            foreach (var error in cross.Errors)
                errors.Add(error.ToString());

            var enums = CodeGenerator.CollectEnumMembers(parsed);

            // Row classes, table classes and enums share one namespace and one code folder (one file per type,
            // so names are compared ignoring case).
            var typeOwners = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var sheet in parsed)
            {
                AddTypeName(typeOwners, sheet.SheetName, $"row class of sheet '{sheet.SheetName}'", errors);
                AddTypeName(typeOwners, sheet.SheetName + TableSuffix, $"table class of sheet '{sheet.SheetName}'", errors);
            }
            foreach (var enumName in enums.EnumNames)
                AddTypeName(typeOwners, enumName, $"enum '{enumName}'", errors);

            return new SheetSetCheckResult(parsed.AsReadOnly(), enums, errors.AsReadOnly());
        }

        /// <summary>
        /// Compares the previous snapshot with checked sheets. An empty or <c>null</c> snapshot gives every sheet as added.
        /// When the previous snapshot has errors, no diff is built and <see cref="SheetSetDiffResult.Text"/> says why;
        /// this is not an import error.
        /// </summary>
        /// <param name="previous">Raw sheets of the previous import, as for <see cref="Check"/>.</param>
        /// <param name="current">Result of <see cref="Check"/> without errors.</param>
        /// <exception cref="ArgumentException"><paramref name="current"/> has errors.</exception>
        public static SheetSetDiffResult Diff(IReadOnlyList<(string Name, IReadOnlyList<IReadOnlyList<string>> Rows)> previous,
            SheetSetCheckResult current)
        {
            if (current == null)
                throw new ArgumentNullException(nameof(current));
            if (!current.IsValid)
                throw new ArgumentException("Sheets have errors; diff needs checked data.", nameof(current));

            IReadOnlyList<SheetParseResult> before = Array.Empty<SheetParseResult>();
            if (previous != null && previous.Count > 0)
            {
                var old = Check(previous);
                if (!old.IsValid)
                    return new SheetSetDiffResult(null, PreviousUnreadableText + ": " + old.Errors[0]);
                before = old.Sheets;
            }

            var diff = SnapshotDiffer.Compare(before, current.Sheets);
            return new SheetSetDiffResult(diff, DiffReport.Format(diff));
        }

        private static IReadOnlyList<IReadOnlyList<string>> Rows(IReadOnlyList<IReadOnlyList<string>> rows)
        {
            if (rows == null)
                return Array.Empty<IReadOnlyList<string>>();

            var result = new IReadOnlyList<string>[rows.Count];
            for (var i = 0; i < rows.Count; i++)
                result[i] = rows[i] ?? Array.Empty<string>();
            return result;
        }

        private static void AddTypeName(Dictionary<string, string> owners, string name, string owner, List<string> errors)
        {
            if (owners.TryGetValue(name, out var other))
                errors.Add($"Type name '{name}' is used by both the {other} and the {owner}");
            else
                owners.Add(name, owner);
        }
    }
}
