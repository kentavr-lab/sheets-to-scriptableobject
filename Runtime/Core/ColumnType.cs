namespace Kentavr.SheetsToSO.Core
{
    /// <summary>
    /// Base kind of a column type.
    /// </summary>
    public enum ColumnKind
    {
        String,
        Int,
        Float,
        Bool,
        Enum,
        Ref,
    }

    /// <summary>
    /// Immutable description of a column type, e.g. <c>int</c>, <c>enum&lt;Rarity&gt;</c>, <c>ref&lt;Weapons&gt;[]</c>.
    /// </summary>
    public sealed class ColumnType
    {
        public ColumnType(ColumnKind kind, string typeName, bool isArray)
        {
            Kind = kind;
            TypeName = kind == ColumnKind.Enum || kind == ColumnKind.Ref ? typeName : null;
            IsArray = isArray;
        }

        /// <summary>Base kind.</summary>
        public ColumnKind Kind { get; }

        /// <summary>Enum name for <see cref="ColumnKind.Enum"/>, sheet name for <see cref="ColumnKind.Ref"/>, otherwise <c>null</c>.</summary>
        public string TypeName { get; }

        /// <summary>Whether the type is an array (<c>type[]</c>).</summary>
        public bool IsArray { get; }

        /// <summary>Default type of a column without an explicit type.</summary>
        public static ColumnType DefaultString { get; } = new ColumnType(ColumnKind.String, null, false);

        /// <summary>Returns the header notation of the type, without the required marker.</summary>
        public override string ToString()
        {
            string baseName;
            switch (Kind)
            {
                case ColumnKind.String: baseName = "string"; break;
                case ColumnKind.Int: baseName = "int"; break;
                case ColumnKind.Float: baseName = "float"; break;
                case ColumnKind.Bool: baseName = "bool"; break;
                case ColumnKind.Enum: baseName = "enum<" + TypeName + ">"; break;
                case ColumnKind.Ref: baseName = "ref<" + TypeName + ">"; break;
                default: baseName = Kind.ToString(); break;
            }
            return IsArray ? baseName + "[]" : baseName;
        }
    }
}
