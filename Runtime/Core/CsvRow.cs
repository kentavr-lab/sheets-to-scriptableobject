using System;
using System.Collections.Generic;

namespace Kentavr.SheetsToSO.Core
{
    /// <summary>
    /// One record of delimited text.
    /// </summary>
    public sealed class CsvRow
    {
        public CsvRow(int rowNumber, int lineNumber, IReadOnlyList<string> cells)
        {
            if (rowNumber < 1)
                throw new ArgumentOutOfRangeException(nameof(rowNumber), rowNumber, "Row number starts at 1");
            if (lineNumber < 1)
                throw new ArgumentOutOfRangeException(nameof(lineNumber), lineNumber, "Line number starts at 1");

            RowNumber = rowNumber;
            LineNumber = lineNumber;
            Cells = cells ?? throw new ArgumentNullException(nameof(cells));
        }

        /// <summary>1-based record number; matches the sheet row and <see cref="CellAddress.ToA1"/>.</summary>
        public int RowNumber { get; }

        /// <summary>1-based physical text line where the record starts.</summary>
        public int LineNumber { get; }

        /// <summary>Unquoted cell values; trailing empty cells are trimmed.</summary>
        public IReadOnlyList<string> Cells { get; }
    }
}
