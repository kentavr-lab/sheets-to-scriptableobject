using System;
using System.Collections.Generic;

namespace Kentavr.SheetsToSO.Core
{
    public enum SheetDiffKind
    {
        /// <summary>Sheet exists only in the new snapshot.</summary>
        Added,

        /// <summary>Sheet exists only in the old snapshot.</summary>
        Removed,

        /// <summary>Sheet exists in both snapshots and has changed rows.</summary>
        Changed,
    }

    /// <summary>
    /// Changes of one sheet, matched by name.
    /// </summary>
    public sealed class SheetDiff
    {
        public SheetDiff(string sheetName, SheetDiffKind kind, IReadOnlyList<RowDiff> addedRows,
            IReadOnlyList<RowDiff> removedRows, IReadOnlyList<RowDiff> changedRows)
        {
            SheetName = sheetName ?? throw new ArgumentNullException(nameof(sheetName));
            Kind = kind;
            AddedRows = addedRows ?? throw new ArgumentNullException(nameof(addedRows));
            RemovedRows = removedRows ?? throw new ArgumentNullException(nameof(removedRows));
            ChangedRows = changedRows ?? throw new ArgumentNullException(nameof(changedRows));
        }

        public string SheetName { get; }

        public SheetDiffKind Kind { get; }

        /// <summary>Rows only in the new sheet, in new sheet order. All rows for an added sheet.</summary>
        public IReadOnlyList<RowDiff> AddedRows { get; }

        /// <summary>Rows only in the old sheet, in old sheet order. All rows for a removed sheet.</summary>
        public IReadOnlyList<RowDiff> RemovedRows { get; }

        /// <summary>Rows in both sheets with changed fields, in new sheet order.</summary>
        public IReadOnlyList<RowDiff> ChangedRows { get; }
    }
}
