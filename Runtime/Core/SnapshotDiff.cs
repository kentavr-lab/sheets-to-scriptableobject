using System;
using System.Collections.Generic;

namespace Kentavr.SheetsToSO.Core
{
    /// <summary>
    /// Difference between two snapshots of parsed sheets.
    /// </summary>
    public sealed class SnapshotDiff
    {
        public SnapshotDiff(IReadOnlyList<SheetDiff> sheets)
        {
            Sheets = sheets ?? throw new ArgumentNullException(nameof(sheets));
        }

        /// <summary>Sheets with changes only: new snapshot order, then removed sheets in old order.</summary>
        public IReadOnlyList<SheetDiff> Sheets { get; }

        public bool HasChanges => Sheets.Count > 0;
    }
}
