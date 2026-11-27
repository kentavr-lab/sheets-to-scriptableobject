using System;
using System.Collections.Generic;

namespace Kentavr.SheetsToSO.Core
{
    /// <summary>
    /// Converts the <c>values</c> array of a Sheets API ValueRange into <see cref="CsvRow"/>s,
    /// trimmed the same way as <see cref="CsvReader"/>.
    /// </summary>
    internal static class SheetValuesConverter
    {
        /// <exception cref="FormatException">The values are not an array of arrays of scalars.</exception>
        public static IReadOnlyList<CsvRow> ToRows(object values)
        {
            var rows = new List<CsvRow>();
            if (values == null)
                return rows.AsReadOnly();

            if (!(values is List<object> rawRows))
                throw new FormatException("'values' is not an array");

            for (var i = 0; i < rawRows.Count; i++)
            {
                var rowNumber = i + 1;
                var cells = new List<string>();

                if (rawRows[i] != null)
                {
                    if (!(rawRows[i] is List<object> rawCells))
                        throw new FormatException($"Row {rowNumber} is not an array");

                    foreach (var cell in rawCells)
                        cells.Add(ToText(cell, rowNumber));
                }

                var count = cells.Count;
                while (count > 0 && cells[count - 1].Length == 0)
                    count--;
                cells.RemoveRange(count, cells.Count - count);

                rows.Add(new CsvRow(rowNumber, rowNumber, cells.AsReadOnly()));
            }

            var rowCount = rows.Count;
            while (rowCount > 0 && rows[rowCount - 1].Cells.Count == 0)
                rowCount--;
            rows.RemoveRange(rowCount, rows.Count - rowCount);

            return rows.AsReadOnly();
        }

        private static string ToText(object cell, int rowNumber)
        {
            switch (cell)
            {
                case null: return "";
                case string s: return s;
                case JsonNumber n: return n.Text;
                // Same spelling as Google's CSV export, so both sources give identical strings.
                case bool b: return b ? "TRUE" : "FALSE";
                default: throw new FormatException($"Row {rowNumber} has a cell that is not a scalar value");
            }
        }
    }
}
