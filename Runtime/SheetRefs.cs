using System.Collections.Generic;
using Kentavr.SheetsToSO.Core;

namespace Kentavr.SheetsToSO
{
    /// <summary>
    /// Lookups behind the generated <c>ref&lt;X&gt;</c> properties: a row id of sheet <c>X</c> to the row
    /// of the linked table <c>XTable</c>.
    /// </summary>
    public static class SheetRefs
    {
        /// <summary>
        /// Row of the linked table <typeparamref name="TTable"/> with the id; <c>null</c> when <paramref name="owner"/>
        /// is <c>null</c>, the table is not linked, the id is empty or there is no such row.
        /// </summary>
        public static TRow Get<TTable, TRow>(SheetTableBase owner, string id)
            where TTable : SheetTable<TRow>
            where TRow : class, ISheetRow
        {
            if (string.IsNullOrEmpty(id) || owner == null)
                return null;

            var table = owner.GetLinkedTable<TTable>();
            if (table == null)
                return null;

            return table.TryGet(id, out var row) ? row : null;
        }

        /// <summary>
        /// Rows of the linked table <typeparamref name="TTable"/> for the ids, index by index: an empty id or an id
        /// with no row gives <c>null</c> at its place. <c>null</c> when <paramref name="owner"/> is <c>null</c>
        /// or the table is not linked; empty when <paramref name="ids"/> is <c>null</c> or empty. A new list on every call.
        /// </summary>
        public static IReadOnlyList<TRow> GetMany<TTable, TRow>(SheetTableBase owner, string[] ids)
            where TTable : SheetTable<TRow>
            where TRow : class, ISheetRow
        {
            if (owner == null)
                return null;

            var table = owner.GetLinkedTable<TTable>();
            if (table == null)
                return null;

            if (ids == null || ids.Length == 0)
                return System.Array.Empty<TRow>();

            var rows = new TRow[ids.Length];
            for (var i = 0; i < ids.Length; i++)
            {
                if (!string.IsNullOrEmpty(ids[i]) && table.TryGet(ids[i], out var row))
                    rows[i] = row;
            }

            return rows;
        }
    }
}
