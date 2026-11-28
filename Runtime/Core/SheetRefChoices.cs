using System;
using System.Collections.Generic;
using System.Linq;

namespace Kentavr.SheetsToSO.Core
{
    /// <summary>State of a <c>[SheetRef]</c> field value against the ids of its table.</summary>
    internal enum SheetRefState
    {
        /// <summary>The value is empty or <c>null</c>: allowed, no error.</summary>
        Empty,

        /// <summary>The table has a row with the value as id.</summary>
        Valid,

        /// <summary>The table has no row with the value as id.</summary>
        Missing,
    }

    /// <summary>Which table asset a <c>[SheetRef]</c> field uses.</summary>
    internal readonly struct SheetRefAssetPick
    {
        public SheetRefAssetPick(string path, string error, string warning)
        {
            Path = path;
            Error = error;
            Warning = warning;
        }

        /// <summary>Path of the picked asset; <c>null</c> when there is <see cref="Error"/>.</summary>
        public string Path { get; }

        /// <summary>Why no asset was picked; <c>null</c> when <see cref="Path"/> is set.</summary>
        public string Error { get; }

        /// <summary>Set when several assets were found and the first was taken; <c>null</c> otherwise.</summary>
        public string Warning { get; }
    }

    /// <summary>
    /// Editor-independent logic of the <c>[SheetRef]</c> drawer: the id list, value state and asset choice.
    /// </summary>
    internal static class SheetRefChoices
    {
        /// <summary>Ids in row order without empty or <c>null</c> ones and without repeats (first wins).</summary>
        public static IReadOnlyList<string> BuildIds(IEnumerable<string> ids)
        {
            var result = new List<string>();
            if (ids == null)
                return result;

            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (var id in ids)
            {
                if (!string.IsNullOrEmpty(id) && seen.Add(id))
                    result.Add(id);
            }

            return result;
        }

        /// <summary>
        /// <see cref="SheetRefState.Empty"/> for an empty value, otherwise whether <paramref name="ids"/> contains it.
        /// The comparison is case-sensitive, as in table lookup; pass a set with ordinal comparison.
        /// </summary>
        public static SheetRefState Classify(string value, ICollection<string> ids)
        {
            if (string.IsNullOrEmpty(value))
                return SheetRefState.Empty;

            return ids != null && ids.Contains(value) ? SheetRefState.Valid : SheetRefState.Missing;
        }

        /// <summary>
        /// Picks the asset of <paramref name="tableName"/> among <paramref name="paths"/> (assets of exactly that type).
        /// An explicit <paramref name="requested"/> path must be among them; it is never replaced by another asset.
        /// Without it a single asset is taken as is, and of several the first by ordinal path order, with a warning.
        /// </summary>
        public static SheetRefAssetPick PickAsset(IReadOnlyList<string> paths, string requested, string tableName)
        {
            var found = (paths ?? Array.Empty<string>())
                .Where(path => !string.IsNullOrEmpty(path))
                .Distinct(StringComparer.Ordinal)
                .OrderBy(path => path, StringComparer.Ordinal)
                .ToList();

            if (!string.IsNullOrWhiteSpace(requested))
            {
                var wanted = requested.Trim();
                return found.Contains(wanted, StringComparer.Ordinal)
                    ? new SheetRefAssetPick(wanted, null, null)
                    : new SheetRefAssetPick(null, $"нет ассета {tableName} по пути '{wanted}'", null);
            }

            if (found.Count == 0)
                return new SheetRefAssetPick(null, $"нет ассета {tableName} в проекте", null);

            if (found.Count == 1)
                return new SheetRefAssetPick(found[0], null, null);

            return new SheetRefAssetPick(found[0], null,
                $"найдено {found.Count} ассетов {tableName}, используется '{found[0]}'; укажите Asset в атрибуте");
        }
    }
}
