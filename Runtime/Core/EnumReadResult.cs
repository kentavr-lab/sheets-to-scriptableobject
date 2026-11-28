using System;
using System.Collections.Generic;

namespace Kentavr.SheetsToSO.Core
{
    /// <summary>
    /// Members of an enum read from C# source by <see cref="CodeGenerator.ReadEnumMembers"/>.
    /// </summary>
    public sealed class EnumReadResult
    {
        private EnumReadResult(IReadOnlyList<EnumMember> members, string error)
        {
            Members = members;
            Error = error;
        }

        internal static EnumReadResult Success(IReadOnlyList<EnumMember> members) =>
            new EnumReadResult(members ?? throw new ArgumentNullException(nameof(members)), null);

        internal static EnumReadResult Failure(string error) =>
            new EnumReadResult(Array.Empty<EnumMember>(), error ?? throw new ArgumentNullException(nameof(error)));

        /// <summary>Members in file order; empty when <see cref="IsValid"/> is false.</summary>
        public IReadOnlyList<EnumMember> Members { get; }

        /// <summary>Why the source could not be read; <c>null</c> when <see cref="IsValid"/>.</summary>
        public string Error { get; }

        public bool IsValid => Error == null;
    }
}
