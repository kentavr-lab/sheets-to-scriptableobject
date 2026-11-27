using System;

namespace Kentavr.SheetsToSO.Core
{
    /// <summary>
    /// Kind of a header row error.
    /// </summary>
    public enum HeaderErrorCode
    {
        EmptyName,
        InvalidName,
        CSharpKeyword,
        DuplicateName,
        UnknownType,
        MissingIdColumn,

        /// <summary>Schema from code: the header type differs from the field type.</summary>
        TypeMismatch,

        /// <summary>Schema from code: the row class has no field with the column name.</summary>
        UnknownField,

        /// <summary>Schema from code: the field type is not one of the v1 types.</summary>
        UnsupportedFieldType,
    }

    /// <summary>
    /// Header row error bound to a cell address.
    /// </summary>
    public sealed class HeaderError
    {
        public HeaderError(HeaderErrorCode code, string address, string message)
        {
            Code = code;
            Address = address ?? throw new ArgumentNullException(nameof(address));
            Message = message ?? throw new ArgumentNullException(nameof(message));
        }

        public HeaderErrorCode Code { get; }

        /// <summary>Cell address, e.g. <c>"B1"</c>.</summary>
        public string Address { get; }

        /// <summary>Human-readable message containing the source cell text.</summary>
        public string Message { get; }

        public override string ToString()
        {
            return Address + ": " + Message;
        }
    }
}
