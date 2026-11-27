using System;
using System.Collections.Generic;

namespace Kentavr.SheetsToSO.Core
{
    /// <summary>
    /// Result of parsing a data row.
    /// </summary>
    public sealed class RowParseResult
    {
        public RowParseResult(int rowNumber, IReadOnlyDictionary<string, object> values, IReadOnlyList<CellError> errors)
        {
            RowNumber = rowNumber;
            Values = values ?? throw new ArgumentNullException(nameof(values));
            Errors = errors ?? throw new ArgumentNullException(nameof(errors));
        }

        /// <summary>One-based row number.</summary>
        public int RowNumber { get; }

        /// <summary>Parsed values keyed by column name, one entry per column.</summary>
        public IReadOnlyDictionary<string, object> Values { get; }

        /// <summary>All errors found in the row, in column order.</summary>
        public IReadOnlyList<CellError> Errors { get; }

        public bool IsValid => Errors.Count == 0;
    }
}
