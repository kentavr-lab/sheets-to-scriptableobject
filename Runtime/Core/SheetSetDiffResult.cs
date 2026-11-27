using System;

namespace Kentavr.SheetsToSO.Core
{
    /// <summary>
    /// Result of <see cref="SheetSetChecker.Diff"/>: the diff with the previous snapshot, or why it is not available.
    /// </summary>
    public sealed class SheetSetDiffResult
    {
        public SheetSetDiffResult(SnapshotDiff diff, string text)
        {
            Diff = diff;
            Text = text ?? throw new ArgumentNullException(nameof(text));
        }

        /// <summary>The diff; <c>null</c> when the previous snapshot cannot be read.</summary>
        public SnapshotDiff Diff { get; }

        /// <summary><see cref="DiffReport.Format"/> text, or the reason the diff is not available.</summary>
        public string Text { get; }

        public bool IsAvailable => Diff != null;

        /// <summary>Whether the diff removes a sheet or a row.</summary>
        public bool HasRemovals
        {
            get
            {
                if (Diff == null)
                    return false;
                foreach (var sheet in Diff.Sheets)
                {
                    if (sheet.Kind == SheetDiffKind.Removed || sheet.RemovedRows.Count > 0)
                        return true;
                }
                return false;
            }
        }
    }
}
