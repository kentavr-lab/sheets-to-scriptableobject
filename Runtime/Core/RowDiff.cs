using System;
using System.Collections.Generic;

namespace Kentavr.SheetsToSO.Core
{
    /// <summary>
    /// Added, removed or changed row, matched by <c>id</c>.
    /// </summary>
    public sealed class RowDiff
    {
        public RowDiff(string id, int oldRowNumber, int newRowNumber, IReadOnlyList<FieldChange> fields)
        {
            Id = id ?? throw new ArgumentNullException(nameof(id));
            OldRowNumber = oldRowNumber;
            NewRowNumber = newRowNumber;
            Fields = fields ?? throw new ArgumentNullException(nameof(fields));
        }

        public string Id { get; }

        /// <summary>One-based row number in the old sheet; 0 when the row is added.</summary>
        public int OldRowNumber { get; }

        /// <summary>One-based row number in the new sheet; 0 when the row is removed.</summary>
        public int NewRowNumber { get; }

        /// <summary>Changed fields; empty for added and removed rows.</summary>
        public IReadOnlyList<FieldChange> Fields { get; }
    }
}
