using System;

namespace Kentavr.SheetsToSO.Core
{
    /// <summary>
    /// Immutable description of a header column.
    /// </summary>
    public sealed class ColumnDefinition
    {
        public ColumnDefinition(string name, ColumnType type, bool isRequired, bool isTypeExplicit, int columnIndex, string address)
        {
            Name = name ?? throw new ArgumentNullException(nameof(name));
            Type = type ?? throw new ArgumentNullException(nameof(type));
            IsRequired = isRequired;
            IsTypeExplicit = isTypeExplicit;
            ColumnIndex = columnIndex;
            Address = address ?? throw new ArgumentNullException(nameof(address));
        }

        /// <summary>Column name.</summary>
        public string Name { get; }

        /// <summary>Column type; <c>string</c> when not written in the header.</summary>
        public ColumnType Type { get; }

        /// <summary>Whether the type ends with <c>!</c>.</summary>
        public bool IsRequired { get; }

        /// <summary>Whether the type was written in the header (<c>name:type</c>).</summary>
        public bool IsTypeExplicit { get; }

        /// <summary>Zero-based position in the source row, skipped columns included.</summary>
        public int ColumnIndex { get; }

        /// <summary>Header cell address, e.g. <c>"B1"</c>.</summary>
        public string Address { get; }

        public override string ToString()
        {
            return Address + ": " + Name + ":" + Type + (IsRequired ? "!" : string.Empty);
        }
    }
}
