using System;
using System.Collections.Generic;

namespace Kentavr.SheetsToSO.Core
{
    /// <summary>
    /// Case-sensitive lookup of rows by <see cref="ISheetRow.Id"/>.
    /// Rows that are <c>null</c> or have an empty id are skipped; on a repeated id the first row wins.
    /// </summary>
    public sealed class RowIndex<TRow> where TRow : ISheetRow
    {
        private readonly Dictionary<string, TRow> byId;

        private RowIndex(Dictionary<string, TRow> byId, IReadOnlyList<int> invalidRows, IReadOnlyList<string> duplicateIds)
        {
            this.byId = byId;
            InvalidRows = invalidRows;
            DuplicateIds = duplicateIds;
        }

        /// <summary>Number of indexed rows.</summary>
        public int Count => byId.Count;

        /// <summary>Positions (in the source sequence) of rows that are <c>null</c> or have an empty id.</summary>
        public IReadOnlyList<int> InvalidRows { get; }

        /// <summary>Ids that occur more than once, each listed once, in order of the first repeat.</summary>
        public IReadOnlyList<string> DuplicateIds { get; }

        /// <summary>Builds an index; <c>null</c> gives an empty index.</summary>
        public static RowIndex<TRow> Build(IEnumerable<TRow> rows)
        {
            var byId = new Dictionary<string, TRow>(StringComparer.Ordinal);
            var invalidRows = new List<int>();
            var duplicateIds = new List<string>();
            var reported = new HashSet<string>(StringComparer.Ordinal);

            if (rows != null)
            {
                var position = 0;
                foreach (var row in rows)
                {
                    var id = row == null ? null : row.Id;
                    if (string.IsNullOrEmpty(id))
                    {
                        invalidRows.Add(position);
                    }
                    else if (byId.ContainsKey(id))
                    {
                        if (reported.Add(id))
                        {
                            duplicateIds.Add(id);
                        }
                    }
                    else
                    {
                        byId.Add(id, row);
                    }

                    position++;
                }
            }

            return new RowIndex<TRow>(byId, invalidRows, duplicateIds);
        }

        /// <summary>Finds a row by id; <c>null</c> id returns <c>false</c>.</summary>
        public bool TryGet(string id, out TRow row)
        {
            if (id == null)
            {
                row = default;
                return false;
            }

            return byId.TryGetValue(id, out row);
        }

        /// <summary>Whether a row with the id exists; <c>null</c> id returns <c>false</c>.</summary>
        public bool Contains(string id)
        {
            return id != null && byId.ContainsKey(id);
        }
    }
}
