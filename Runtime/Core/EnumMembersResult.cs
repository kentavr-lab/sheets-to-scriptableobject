using System;
using System.Collections.Generic;

namespace Kentavr.SheetsToSO.Core
{
    /// <summary>
    /// Enum members collected from sheet data by <see cref="CodeGenerator.CollectEnumMembers"/>.
    /// </summary>
    public sealed class EnumMembersResult
    {
        public EnumMembersResult(IReadOnlyList<string> enumNames,
            IReadOnlyDictionary<string, IReadOnlyList<string>> members, IReadOnlyList<SheetError> errors)
        {
            EnumNames = enumNames ?? throw new ArgumentNullException(nameof(enumNames));
            Members = members ?? throw new ArgumentNullException(nameof(members));
            Errors = errors ?? throw new ArgumentNullException(nameof(errors));
        }

        /// <summary>Enum names in order of first appearance in the columns.</summary>
        public IReadOnlyList<string> EnumNames { get; }

        /// <summary>Members per enum name, in order of first appearance in the data; every name of <see cref="EnumNames"/> is present.</summary>
        public IReadOnlyDictionary<string, IReadOnlyList<string>> Members { get; }

        /// <summary>Values that are not valid C# names, with addresses like <c>Units!C5</c>.</summary>
        public IReadOnlyList<SheetError> Errors { get; }

        public bool IsValid => Errors.Count == 0;
    }
}
