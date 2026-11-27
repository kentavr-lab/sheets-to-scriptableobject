using System;
using System.Collections.Generic;

namespace Kentavr.SheetsToSO.Core
{
    /// <summary>
    /// Parsed data row of a sheet.
    /// </summary>
    public sealed class SheetRow
    {
        public SheetRow(int rowNumber, string id, IReadOnlyDictionary<string, object> values)
        {
            RowNumber = rowNumber;
            Id = id ?? throw new ArgumentNullException(nameof(id));
            Values = values ?? throw new ArgumentNullException(nameof(values));
        }

        /// <summary>One-based row number in the sheet.</summary>
        public int RowNumber { get; }

        /// <summary>Text of the <c>id</c> cell; empty when the sheet has no <c>id</c> column.</summary>
        public string Id { get; }

        /// <summary>Parsed values keyed by column name, <c>id</c> included.</summary>
        public IReadOnlyDictionary<string, object> Values { get; }
    }
}
