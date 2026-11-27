using System;
using System.Collections.Generic;

namespace Kentavr.SheetsToSO.Core
{
    /// <summary>
    /// Result of matching header columns to the fields of a row class.
    /// </summary>
    public sealed class CodeSchemaResult
    {
        public CodeSchemaResult(IReadOnlyList<ColumnDefinition> columns, IReadOnlyList<HeaderError> errors)
        {
            Columns = columns ?? throw new ArgumentNullException(nameof(columns));
            Errors = errors ?? throw new ArgumentNullException(nameof(errors));
        }

        /// <summary>Columns matched to fields, with types taken from the fields; columns with errors are left out.</summary>
        public IReadOnlyList<ColumnDefinition> Columns { get; }

        /// <summary>Errors bound to header cells.</summary>
        public IReadOnlyList<HeaderError> Errors { get; }

        public bool IsValid => Errors.Count == 0;
    }
}
