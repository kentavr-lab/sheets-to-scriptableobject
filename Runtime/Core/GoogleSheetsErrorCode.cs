namespace Kentavr.SheetsToSO.Core
{
    /// <summary>Kind of a Google Sheets API failure.</summary>
    public enum GoogleSheetsErrorCode
    {
        /// <summary>403: the sheet is not shared for viewing or the API is disabled for the key.</summary>
        AccessDenied,

        /// <summary>400 with reason <c>API_KEY_INVALID</c>.</summary>
        InvalidApiKey,

        /// <summary>404: no spreadsheet with this id.</summary>
        NotFound,

        /// <summary>429 after all retries.</summary>
        QuotaExceeded,

        /// <summary>5xx after all retries.</summary>
        ServerError,

        /// <summary>Other 4xx, for example a range with a missing tab.</summary>
        BadRequest,

        /// <summary>Response body is not valid JSON or has an unexpected structure.</summary>
        InvalidResponse,
    }
}
