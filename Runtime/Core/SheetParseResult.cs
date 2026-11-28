using System;
using System.Collections.Generic;

namespace Kentavr.SheetsToSO.Core
{
    /// <summary>
    /// Result of parsing a whole sheet.
    /// </summary>
    public sealed class SheetParseResult
    {
        public SheetParseResult(string sheetName, IReadOnlyList<ColumnDefinition> columns, IReadOnlyList<SheetRow> rows,
            IReadOnlyList<SheetError> errors)
        {
            SheetName = sheetName ?? throw new ArgumentNullException(nameof(sheetName));
            Columns = columns ?? throw new ArgumentNullException(nameof(columns));
            Rows = rows ?? throw new ArgumentNullException(nameof(rows));
            Errors = errors ?? throw new ArgumentNullException(nameof(errors));
        }

        public string SheetName { get; }

        /// <summary>Valid, non-skipped columns from the header.</summary>
        public IReadOnlyList<ColumnDefinition> Columns { get; }

        /// <summary>
        /// Data rows except comment and empty rows. With errors, cells that failed to parse hold the default value
        /// (<c>null</c> or an empty array) and columns with header errors are missing.
        /// </summary>
        public IReadOnlyList<SheetRow> Rows { get; }

        /// <summary>All errors: header first, then rows top to bottom.</summary>
        public IReadOnlyList<SheetError> Errors { get; }

        public bool IsValid => Errors.Count == 0;
    }
}
