using System;
using System.Collections.Generic;

namespace Kentavr.SheetsToSO.Core
{
    /// <summary>
    /// Compares two snapshots of validated sheets: sheets by name, rows by <c>id</c>.
    /// See Documentation~/data-format.md, "Diff".
    /// </summary>
    public static class SnapshotDiffer
    {
        private static readonly IReadOnlyList<FieldChange> NoFields = Array.Empty<FieldChange>();

        /// <summary>
        /// Compares snapshots. Both must contain only valid sheets with unique names.
        /// An empty <paramref name="before"/> (first import) gives every sheet as added.
        /// </summary>
        public static SnapshotDiff Compare(IReadOnlyList<SheetParseResult> before, IReadOnlyList<SheetParseResult> after)
        {
            var oldSheets = Index(before, nameof(before));
            var newSheets = Index(after, nameof(after));

            var result = new List<SheetDiff>();

            foreach (var newSheet in after)
            {
                SheetDiff diff;
                if (oldSheets.TryGetValue(newSheet.SheetName, out var oldSheet))
                    diff = CompareSheet(oldSheet, newSheet);
                else
                    diff = WholeSheet(newSheet, SheetDiffKind.Added);

                if (diff != null)
                    result.Add(diff);
            }

            foreach (var oldSheet in before)
            {
                if (!newSheets.ContainsKey(oldSheet.SheetName))
                    result.Add(WholeSheet(oldSheet, SheetDiffKind.Removed));
            }

            return new SnapshotDiff(result.AsReadOnly());
        }

        private static Dictionary<string, SheetParseResult> Index(IReadOnlyList<SheetParseResult> sheets, string paramName)
        {
            if (sheets == null)
                throw new ArgumentNullException(paramName);

            var byName = new Dictionary<string, SheetParseResult>(StringComparer.Ordinal);
            for (var i = 0; i < sheets.Count; i++)
            {
                var sheet = sheets[i];
                if (sheet == null)
                    throw new ArgumentException($"Sheet {i} is null.", paramName);
                if (!sheet.IsValid)
                    throw new ArgumentException($"Sheet '{sheet.SheetName}' has errors; diff needs validated data.",
                        paramName);
                if (byName.ContainsKey(sheet.SheetName))
                    throw new ArgumentException($"Duplicate sheet name '{sheet.SheetName}'.", paramName);

                var ids = new HashSet<string>(StringComparer.Ordinal);
                foreach (var row in sheet.Rows)
                {
                    if (row == null)
                        throw new ArgumentException($"Sheet '{sheet.SheetName}' has a null row.", paramName);
                    if (row.Id.Length == 0 || !ids.Add(row.Id))
                        throw new ArgumentException(
                            $"Sheet '{sheet.SheetName}' has an empty or duplicate id '{row.Id}'.", paramName);
                }

                byName.Add(sheet.SheetName, sheet);
            }
            return byName;
        }

        private static SheetDiff WholeSheet(SheetParseResult sheet, SheetDiffKind kind)
        {
            var rows = new List<RowDiff>(sheet.Rows.Count);
            foreach (var row in sheet.Rows)
            {
                rows.Add(kind == SheetDiffKind.Added
                    ? new RowDiff(row.Id, 0, row.RowNumber, NoFields)
                    : new RowDiff(row.Id, row.RowNumber, 0, NoFields));
            }

            var empty = Array.Empty<RowDiff>();
            return kind == SheetDiffKind.Added
                ? new SheetDiff(sheet.SheetName, kind, rows.AsReadOnly(), empty, empty)
                : new SheetDiff(sheet.SheetName, kind, empty, rows.AsReadOnly(), empty);
        }

        // Returns null when the sheets have no differences.
        private static SheetDiff CompareSheet(SheetParseResult oldSheet, SheetParseResult newSheet)
        {
            var oldRows = new Dictionary<string, SheetRow>(StringComparer.Ordinal);
            foreach (var row in oldSheet.Rows)
                oldRows.Add(row.Id, row);

            var newIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (var row in newSheet.Rows)
                newIds.Add(row.Id);

            var columns = ColumnUnion(oldSheet, newSheet);

            var added = new List<RowDiff>();
            var changed = new List<RowDiff>();
            foreach (var newRow in newSheet.Rows)
            {
                if (!oldRows.TryGetValue(newRow.Id, out var oldRow))
                {
                    added.Add(new RowDiff(newRow.Id, 0, newRow.RowNumber, NoFields));
                    continue;
                }

                var fields = CompareRow(oldRow, newRow, columns);
                if (fields.Count > 0)
                    changed.Add(new RowDiff(newRow.Id, oldRow.RowNumber, newRow.RowNumber, fields.AsReadOnly()));
            }

            var removed = new List<RowDiff>();
            foreach (var oldRow in oldSheet.Rows)
            {
                if (!newIds.Contains(oldRow.Id))
                    removed.Add(new RowDiff(oldRow.Id, oldRow.RowNumber, 0, NoFields));
            }

            if (added.Count == 0 && removed.Count == 0 && changed.Count == 0)
                return null;

            return new SheetDiff(newSheet.SheetName, SheetDiffKind.Changed, added.AsReadOnly(), removed.AsReadOnly(),
                changed.AsReadOnly());
        }

        // New sheet columns in their order, then removed columns in old sheet order.
        private static List<string> ColumnUnion(SheetParseResult oldSheet, SheetParseResult newSheet)
        {
            var names = new List<string>();
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (var column in newSheet.Columns)
            {
                if (seen.Add(column.Name))
                    names.Add(column.Name);
            }
            foreach (var column in oldSheet.Columns)
            {
                if (seen.Add(column.Name))
                    names.Add(column.Name);
            }
            return names;
        }

        private static List<FieldChange> CompareRow(SheetRow oldRow, SheetRow newRow, List<string> columns)
        {
            var fields = new List<FieldChange>();
            foreach (var column in columns)
            {
                var oldMissing = !oldRow.Values.TryGetValue(column, out var oldValue);
                var newMissing = !newRow.Values.TryGetValue(column, out var newValue);

                if (oldMissing && newMissing)
                    continue;
                if (oldMissing || newMissing || !ValuesEqual(oldValue, newValue))
                    fields.Add(new FieldChange(column, oldValue, newValue, oldMissing, newMissing));
            }
            return fields;
        }

        // Same CLR type and equal value; arrays element by element; float compared exactly.
        internal static bool ValuesEqual(object a, object b)
        {
            if (a == null || b == null)
                return a == null && b == null;
            if (a.GetType() != b.GetType())
                return false;

            if (a is Array arrayA)
            {
                var arrayB = (Array)b;
                if (arrayA.Length != arrayB.Length)
                    return false;
                for (var i = 0; i < arrayA.Length; i++)
                {
                    if (!ValuesEqual(arrayA.GetValue(i), arrayB.GetValue(i)))
                        return false;
                }
                return true;
            }

            return a.Equals(b);
        }
    }
}
