using System;
using System.Collections.Generic;

namespace Kentavr.SheetsToSO.Core
{
    /// <summary>Cell values of one tab, in the same shape as <see cref="CsvReader"/> output.</summary>
    public sealed class SheetValues
    {
        public SheetValues(string title, IReadOnlyList<CsvRow> rows)
        {
            Title = title ?? throw new ArgumentNullException(nameof(title));
            Rows = rows ?? throw new ArgumentNullException(nameof(rows));
        }

        /// <summary>Tab name.</summary>
        public string Title { get; }

        /// <summary>Rows with trailing empty cells and trailing empty rows trimmed.</summary>
        public IReadOnlyList<CsvRow> Rows { get; }
    }
}
