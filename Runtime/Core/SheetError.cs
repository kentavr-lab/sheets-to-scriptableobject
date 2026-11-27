using System;

namespace Kentavr.SheetsToSO.Core
{
    /// <summary>
    /// Sheet error with a full address, e.g. <c>"Units!B7"</c>.
    /// Wraps either a header error or a data cell error.
    /// </summary>
    public sealed class SheetError
    {
        public SheetError(string sheetName, HeaderError error)
            : this(sheetName, (error ?? throw new ArgumentNullException(nameof(error))).Address, error.Message)
        {
            HeaderCode = error.Code;
        }

        public SheetError(string sheetName, CellError error)
            : this(sheetName, (error ?? throw new ArgumentNullException(nameof(error))).Address, error.Message)
        {
            CellCode = error.Code;
        }

        private SheetError(string sheetName, string cellAddress, string message)
        {
            SheetName = sheetName ?? throw new ArgumentNullException(nameof(sheetName));
            CellAddress = cellAddress;
            Message = message;
        }

        /// <summary>Sheet name as given to the parser, without quotes.</summary>
        public string SheetName { get; }

        /// <summary>Cell address within the sheet, e.g. <c>"B7"</c>.</summary>
        public string CellAddress { get; }

        /// <summary>Full address, e.g. <c>"Units!B7"</c>.</summary>
        public string Address => SheetName + "!" + CellAddress;

        /// <summary>Human-readable message.</summary>
        public string Message { get; }

        /// <summary>Set when the error comes from the header row.</summary>
        public HeaderErrorCode? HeaderCode { get; }

        /// <summary>Set when the error comes from a data row.</summary>
        public CellErrorCode? CellCode { get; }

        public override string ToString()
        {
            return Address + ": " + Message;
        }
    }
}
