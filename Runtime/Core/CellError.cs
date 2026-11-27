using System;

namespace Kentavr.SheetsToSO.Core
{
    /// <summary>
    /// Kind of a data cell error.
    /// </summary>
    public enum CellErrorCode
    {
        RequiredEmpty,
        InvalidInt,
        InvalidFloat,
        InvalidBool,
        EmptyArrayElement,
    }

    /// <summary>
    /// Data cell error bound to a cell address.
    /// </summary>
    public sealed class CellError
    {
        public CellError(CellErrorCode code, string address, string message)
        {
            Code = code;
            Address = address ?? throw new ArgumentNullException(nameof(address));
            Message = message ?? throw new ArgumentNullException(nameof(message));
        }

        public CellErrorCode Code { get; }

        /// <summary>Cell address, e.g. <c>"B7"</c>.</summary>
        public string Address { get; }

        /// <summary>Human-readable message containing the column name and the source cell (or array element) text.</summary>
        public string Message { get; }

        public override string ToString()
        {
            return Address + ": " + Message;
        }
    }
}
