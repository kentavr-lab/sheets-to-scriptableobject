using System;
using System.Collections.Generic;

namespace Kentavr.SheetsToSO.Core
{
    /// <summary>
    /// Enum members after <see cref="CodeGenerator.MergeEnumMembers"/>: previous members keep their order and values.
    /// </summary>
    public sealed class EnumMergeResult
    {
        public EnumMergeResult(IReadOnlyList<EnumMember> members, IReadOnlyList<string> removed)
        {
            Members = members ?? throw new ArgumentNullException(nameof(members));
            Removed = removed ?? throw new ArgumentNullException(nameof(removed));
        }

        /// <summary>Previous members in their order, then new ones in order of appearance.</summary>
        public IReadOnlyList<EnumMember> Members { get; }

        /// <summary>Previous members no longer in the data; still present in <see cref="Members"/>.</summary>
        public IReadOnlyList<string> Removed { get; }
    }
}
