using System;
using System.Collections.Generic;

namespace Kentavr.SheetsToSO.Core
{
    /// <summary>
    /// Result of parsing a single data cell.
    /// </summary>
    public sealed class CellParseResult
    {
        public CellParseResult(object value, IReadOnlyList<CellError> errors)
        {
            Value = value;
            Errors = errors ?? throw new ArgumentNullException(nameof(errors));
        }

        /// <summary>Parsed value; the default value of the column type when the cell has errors.</summary>
        public object Value { get; }

        /// <summary>All errors found in the cell.</summary>
        public IReadOnlyList<CellError> Errors { get; }

        public bool IsValid => Errors.Count == 0;
    }
}
