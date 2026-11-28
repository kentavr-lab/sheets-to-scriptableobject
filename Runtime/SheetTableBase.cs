using System.Collections.Generic;
using UnityEngine;

namespace Kentavr.SheetsToSO
{
    /// <summary>
    /// Non-generic base of every <see cref="SheetTable{TRow}"/>: keeps serialized references to the tables
    /// its <c>ref&lt;X&gt;</c> columns point to. The importer sets them on every import.
    /// </summary>
    public abstract class SheetTableBase : ScriptableObject
    {
        [SerializeField] private List<SheetTableBase> linkedTables = new List<SheetTableBase>();

        /// <summary>Tables the <c>ref&lt;X&gt;</c> columns point to, as set by the last import.</summary>
        public IReadOnlyList<SheetTableBase> LinkedTables => linkedTables;

        /// <summary>
        /// First linked table of type <typeparamref name="T"/>, or <c>null</c> when none is linked.
        /// The table class <c>XTable</c> is unique per sheet <c>X</c>, so the type identifies the sheet.
        /// </summary>
        public T GetLinkedTable<T>() where T : SheetTableBase
        {
            if (linkedTables == null)
                return null;

            foreach (var table in linkedTables)
            {
                // Unity's == also catches destroyed or missing assets.
                if (table != null && table is T typed)
                    return typed;
            }

            return null;
        }

        /// <summary>Replaces the linked tables; <c>null</c> items are skipped.</summary>
        internal void ReplaceLinkedTables(IList<SheetTableBase> tables)
        {
            var list = new List<SheetTableBase>(tables == null ? 0 : tables.Count);
            if (tables != null)
            {
                foreach (var table in tables)
                {
                    if (table != null)
                        list.Add(table);
                }
            }

            linkedTables = list;
        }
    }
}
