using System;
using System.Collections.Generic;

namespace Kentavr.SheetsToSO.Core
{
    /// <summary>
    /// Result of parsing a header row.
    /// </summary>
    public sealed class HeaderParseResult
    {
        public HeaderParseResult(IReadOnlyList<ColumnDefinition> columns, IReadOnlyList<HeaderError> errors)
        {
            Columns = columns ?? throw new ArgumentNullException(nameof(columns));
            Errors = errors ?? throw new ArgumentNullException(nameof(errors));
        }

        /// <summary>Valid, non-skipped columns in source order.</summary>
        public IReadOnlyList<ColumnDefinition> Columns { get; }

        /// <summary>All errors found in the row.</summary>
        public IReadOnlyList<HeaderError> Errors { get; }

        public bool IsValid => Errors.Count == 0;
    }
}
