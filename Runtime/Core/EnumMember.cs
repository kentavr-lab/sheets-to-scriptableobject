using System;

namespace Kentavr.SheetsToSO.Core
{
    /// <summary>
    /// A generated enum member: name and its explicit numeric value.
    /// </summary>
    public sealed class EnumMember : IEquatable<EnumMember>
    {
        public EnumMember(string name, int value)
        {
            Name = name ?? throw new ArgumentNullException(nameof(name));
            Value = value;
        }

        public string Name { get; }

        public int Value { get; }

        public bool Equals(EnumMember other) =>
            other != null && Value == other.Value && string.Equals(Name, other.Name, StringComparison.Ordinal);

        public override bool Equals(object obj) => Equals(obj as EnumMember);

        public override int GetHashCode() => StringComparer.Ordinal.GetHashCode(Name) * 31 + Value;

        public override string ToString() => $"{Name} = {Value}";
    }
}
