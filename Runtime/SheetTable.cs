using System.Collections.Generic;
using Kentavr.SheetsToSO.Core;
using UnityEngine;

namespace Kentavr.SheetsToSO
{
    /// <summary>
    /// Base ScriptableObject for one sheet tab: a serialized list of rows with lookup by id.
    /// Concrete tables close the generic type: <c>public sealed class UnitsTable : SheetTable&lt;UnitRow&gt; {}</c>,
    /// where <c>UnitRow</c> is a <c>[Serializable]</c> class implementing <see cref="ISheetRow"/>.
    /// </summary>
    public abstract class SheetTable<TRow> : ScriptableObject where TRow : ISheetRow
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
}
