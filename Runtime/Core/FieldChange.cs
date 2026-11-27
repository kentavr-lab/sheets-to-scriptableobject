using System;

namespace Kentavr.SheetsToSO.Core
{
    /// <summary>
    /// Changed field of a row present in both snapshots.
    /// </summary>
    public sealed class FieldChange
    {
        public FieldChange(string column, object oldValue, object newValue, bool oldMissing, bool newMissing)
        {
            if (oldMissing && newMissing)
                throw new ArgumentException("Column cannot be missing on both sides.", nameof(newMissing));

            Column = column ?? throw new ArgumentNullException(nameof(column));
            OldValue = oldMissing ? null : oldValue;
            NewValue = newMissing ? null : newValue;
            OldMissing = oldMissing;
            NewMissing = newMissing;
            OldText = DiffReport.FormatValue(OldValue, oldMissing);
            NewText = DiffReport.FormatValue(NewValue, newMissing);
        }

        /// <summary>Column name.</summary>
        public string Column { get; }

        /// <summary>Value in the old snapshot, typed as in <see cref="SheetRow.Values"/>.</summary>
        public object OldValue { get; }

        /// <summary>Value in the new snapshot, typed as in <see cref="SheetRow.Values"/>.</summary>
        public object NewValue { get; }

        /// <summary>Display text of the old value, e.g. <c>"Sword"</c> (with quotes) or <c>(missing)</c>.</summary>
        public string OldText { get; }

        /// <summary>Display text of the new value.</summary>
        public string NewText { get; }

        /// <summary>The old sheet has no such column.</summary>
        public bool OldMissing { get; }

        /// <summary>The new sheet has no such column.</summary>
        public bool NewMissing { get; }

        public override string ToString()
        {
            return Column + ": " + OldText + " → " + NewText;
        }
    }
}
