using System;

namespace Kentavr.SheetsToSO.Core
{
    /// <summary>
    /// Kind of a delimited text error.
    /// </summary>
    public enum CsvErrorCode
    {
        UnclosedQuote,
        TextAfterClosingQuote,
    }

    /// <summary>
    /// Delimited text error bound to a record and a physical line.
    /// </summary>
    public sealed class CsvError
    {
        public CsvError(CsvErrorCode code, int rowNumber, int lineNumber, string message)
        {
            Code = code;
            RowNumber = rowNumber;
            LineNumber = lineNumber;
            Message = message ?? throw new ArgumentNullException(nameof(message));
        }

        public CsvErrorCode Code { get; }

        /// <summary>1-based record number of the affected row.</summary>
        public int RowNumber { get; }

        /// <summary>1-based physical text line where the problem was found.</summary>
        public int LineNumber { get; }

        public string Message { get; }

        public override string ToString()
        {
            return $"line {LineNumber}: {Message}";
        }
    }
}
