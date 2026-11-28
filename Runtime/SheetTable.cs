using System.Collections.Generic;
using Kentavr.SheetsToSO.Core;
using UnityEngine;

namespace Kentavr.SheetsToSO
{
    /// <summary>
    /// Base ScriptableObject for one sheet tab: a serialized list of rows with lookup by id.
    /// Concrete tables close the generic type: <c>public sealed class UnitsTable : SheetTable&lt;UnitRow&gt; {}</c>,
    /// where <c>UnitRow</c> is a <c>[Serializable]</c> class implementing <see cref="ISheetRow"/>.
    /// Rows implementing <see cref="ISheetTableRow"/> are given this table when the index is rebuilt.
    /// </summary>
    public abstract class SheetTable<TRow> : SheetTableBase, ISheetTableData where TRow : ISheetRow
    {
        [SerializeField] private List<TRow> rows = new List<TRow>();

        private RowIndex<TRow> index;

        /// <summary>All rows in list order, including ones without an id or with a repeated id.</summary>
        public IReadOnlyList<TRow> Rows => rows;

        /// <summary>Number of rows in the list.</summary>
        public int Count => rows == null ? 0 : rows.Count;

        /// <summary>Finds a row by id (case-sensitive); <c>null</c> id returns <c>false</c>.</summary>
        public bool TryGet(string id, out TRow row)
        {
            return GetIndex().TryGet(id, out row);
        }

        /// <summary>Whether a row with the id exists; <c>null</c> id returns <c>false</c>.</summary>
        public bool Contains(string id)
        {
            return GetIndex().Contains(id);
        }

        /// <summary>Returns the row with the id or throws <see cref="KeyNotFoundException"/>.</summary>
        public TRow Get(string id)
        {
            if (GetIndex().TryGet(id, out var row))
            {
                return row;
            }

            throw new KeyNotFoundException($"Table '{name}' has no row with id '{id}'.");
        }

        System.Type ISheetTableData.RowType => typeof(TRow);

        System.Collections.IList ISheetTableData.RowsData => rows;

        void ISheetTableData.SetRows(System.Collections.IList newRows)
        {
            var list = new List<TRow>(newRows == null ? 0 : newRows.Count);
            if (newRows != null)
            {
                foreach (var row in newRows)
                {
                    list.Add((TRow)row);
                }
            }

            rows = list;
            RebuildIndex();
        }

        void ISheetTableData.SetLinkedTables(IList<SheetTableBase> tables)
        {
            ReplaceLinkedTables(tables);
        }

        protected virtual void OnEnable()
        {
            RebuildIndex();
        }

        protected virtual void OnValidate()
        {
            RebuildIndex();
        }

        private RowIndex<TRow> GetIndex()
        {
            if (index == null)
            {
                RebuildIndex();
            }

            return index;
        }

        private void RebuildIndex()
        {
            index = RowIndex<TRow>.Build(rows);

            if (rows != null)
            {
                foreach (var row in rows)
                {
                    if (row is ISheetTableRow tableRow)
                        tableRow.SetTable(this);
                }
            }

            foreach (var duplicateId in index.DuplicateIds)
            {
                Debug.LogError($"Table '{name}': duplicate id '{duplicateId}', only the first row is used.", this);
            }

            foreach (var position in index.InvalidRows)
            {
                Debug.LogError($"Table '{name}': row {position} has no id and is not searchable.", this);
            }
        }
    }

    /// <summary>
    /// Write access to the rows of a <see cref="SheetTable{TRow}"/> for the editor importer.
    /// </summary>
    internal interface ISheetTableData
    {
        /// <summary>Row class of the table.</summary>
        System.Type RowType { get; }

        /// <summary>Current rows in list order; <c>null</c> when the list is not set.</summary>
        System.Collections.IList RowsData { get; }

        /// <summary>Replaces all rows and rebuilds the id index. Every item must be a <see cref="RowType"/>.</summary>
        void SetRows(System.Collections.IList rows);

        /// <summary>Replaces the tables the <c>ref&lt;X&gt;</c> columns point to; <c>null</c> items are skipped.</summary>
        void SetLinkedTables(IList<SheetTableBase> tables);
    }
}
