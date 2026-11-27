using System;
using System.Collections.Generic;

namespace Kentavr.SheetsToSO.Core
{
    /// <summary>
    /// Result of reading delimited text.
    /// </summary>
    public sealed class CsvReadResult
    {
        public CsvReadResult(IReadOnlyList<CsvRow> rows, IReadOnlyList<CsvError> errors)
        {
            Rows = rows ?? throw new ArgumentNullException(nameof(rows));
            Errors = errors ?? throw new ArgumentNullException(nameof(errors));
        }

        /// <summary>Records in source order; empty rows inside the text are kept, trailing ones removed.</summary>
        public IReadOnlyList<CsvRow> Rows { get; }

        /// <summary>All errors found in the text.</summary>
        public IReadOnlyList<CsvError> Errors { get; }

        public bool IsValid => Errors.Count == 0;
    }
}
