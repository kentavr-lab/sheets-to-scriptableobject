using System;

namespace Kentavr.SheetsToSO.Core
{
    /// <summary>
    /// Google Sheets API request failed. The message is user-facing and never contains the API key.
    /// </summary>
    public sealed class GoogleSheetsException : Exception
    {
        public GoogleSheetsException(GoogleSheetsErrorCode code, int? statusCode, string message)
            : base(message)
        {
            Code = code;
            StatusCode = statusCode;
        }

        public GoogleSheetsErrorCode Code { get; }

        /// <summary>HTTP status of the last response; <c>null</c> when no status applies.</summary>
        public int? StatusCode { get; }
    }
}
