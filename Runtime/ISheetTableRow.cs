namespace Kentavr.SheetsToSO
{
    /// <summary>
    /// A row that knows its table, so that generated <c>ref&lt;X&gt;</c> properties can reach the linked tables.
    /// <see cref="SheetTable{TRow}"/> calls <see cref="SetTable"/> for every row when its index is rebuilt
    /// (on load, on validate and when rows are replaced). Implemented by generated row classes.
    /// </summary>
    public interface ISheetTableRow
    {
        /// <summary>Sets the table that owns the row. Not serialized.</summary>
        void SetTable(SheetTableBase table);
    }
}
