using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Kentavr.SheetsToSO.Core
{
    /// <summary>
    /// Downloads one sheet of a Google spreadsheet shared as "Anyone with the link" through its CSV export link.
    /// Loading and data problems are collected into the result, never thrown.
    /// </summary>
    public sealed class CsvExportSource
    {
        private const string SharedAsViewer = "shared as 'Anyone with the link — Viewer'";
        private const string SharingHint = "check that the spreadsheet is " + SharedAsViewer;
        private const string ExistsAndSharingHint = "check that the spreadsheet exists and is " + SharedAsViewer;
        private const char ByteOrderMark = (char)0xFEFF;

        private static readonly Encoding Utf8 = new UTF8Encoding(false, false);

        private readonly HttpClient _httpClient;

        /// <param name="httpClient">Client used for requests; owned and disposed by the caller.</param>
        public CsvExportSource(HttpClient httpClient)
        {
            _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        }

        /// <summary>
        /// Returns <c>https://docs.google.com/spreadsheets/d/&lt;id&gt;/export?format=csv&amp;gid=&lt;gid&gt;</c>.
        /// </summary>
        /// <exception cref="ArgumentException">The id is not <c>[A-Za-z0-9_-]+</c> or the gid is not digits.</exception>
        public static Uri BuildExportUri(string spreadsheetId, string gid)
        {
            if (string.IsNullOrEmpty(spreadsheetId) || !IsSpreadsheetId(spreadsheetId))
                throw new ArgumentException("Spreadsheet id must be non-empty and contain only letters, digits, '_' and '-'", nameof(spreadsheetId));
            if (string.IsNullOrEmpty(gid) || !IsDigits(gid))
                throw new ArgumentException("Sheet gid must be non-empty and contain only digits", nameof(gid));

            return new Uri("https://docs.google.com/spreadsheets/d/" + spreadsheetId + "/export?format=csv&gid=" + gid);
        }

        /// <summary>
        /// Downloads the sheet and reads it as CSV.
        /// </summary>
        /// <exception cref="ArgumentException">Invalid id or gid, see <see cref="BuildExportUri"/>.</exception>
        /// <exception cref="OperationCanceledException"><paramref name="cancellationToken"/> was cancelled.</exception>
        public async Task<CsvExportResult> LoadAsync(string spreadsheetId, string gid, CancellationToken cancellationToken = default)
        {
            var uri = BuildExportUri(spreadsheetId, gid);

            HttpStatusCode status;
            string reason;
            string mediaType;
            byte[] body;
            try
            {
                using (var request = new HttpRequestMessage(HttpMethod.Get, uri))
                using (var response = await _httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false))
                {
                    status = response.StatusCode;
                    reason = response.ReasonPhrase;
                    mediaType = response.Content?.Headers.ContentType?.MediaType;
                    body = response.IsSuccessStatusCode && response.Content != null
                        ? await response.Content.ReadAsByteArrayAsync().ConfigureAwait(false)
                        : null;
                }
            }
            catch (HttpRequestException exception)
            {
                return Failed(SourceErrorCode.NetworkError, "Request to " + uri + " failed: " + exception.Message);
            }
            catch (TaskCanceledException exception) when (!cancellationToken.IsCancellationRequested)
            {
                return Failed(SourceErrorCode.NetworkError, "Request to " + uri + " timed out: " + exception.Message);
            }

            var code = (int)status;
            if (status == HttpStatusCode.BadRequest)
                return Failed(SourceErrorCode.InvalidGid,
                    $"Sheet with gid '{gid}' not found in spreadsheet '{spreadsheetId}' (HTTP 400)");

            if (code < 200 || code > 299)
            {
                var message = "HTTP " + code + (string.IsNullOrEmpty(reason) ? string.Empty : " " + reason) + " for " + uri;
                if (status == HttpStatusCode.Unauthorized || status == HttpStatusCode.Forbidden || status == HttpStatusCode.NotFound)
                    message += "; " + ExistsAndSharingHint;
                return Failed(SourceErrorCode.HttpError, message);
            }

            var text = body == null ? string.Empty : Utf8.GetString(body);
            if (string.Equals(mediaType, "text/html", StringComparison.OrdinalIgnoreCase) || StartsWithMarkup(text))
                return Failed(SourceErrorCode.NotCsv, "Expected CSV but received HTML from " + uri + "; " + SharingHint);

            var csv = CsvReader.Read(text, CsvDelimiter.Comma);
            return new CsvExportResult(null, csv, FindCommaInNumbers(csv.Rows).AsReadOnly());
        }

        private static CsvExportResult Failed(SourceErrorCode code, string message)
        {
            return new CsvExportResult(new SourceError(code, message), null, Array.Empty<SourceWarning>());
        }

        private static bool StartsWithMarkup(string text)
        {
            foreach (var c in text)
            {
                if (c == ByteOrderMark || char.IsWhiteSpace(c))
                    continue;
                return c == '<';
            }
            return false;
        }

        private static List<SourceWarning> FindCommaInNumbers(IReadOnlyList<CsvRow> rows)
        {
            var warnings = new List<SourceWarning>();
            if (rows.Count == 0)
                return warnings;

            var header = HeaderParser.Parse(rows[0].Cells);
            foreach (var column in header.Columns)
            {
                if (column.Type.Kind != ColumnKind.Int && column.Type.Kind != ColumnKind.Float)
                    continue;

                var count = 0;
                string firstAddress = null;
                string firstText = null;
                for (var i = 1; i < rows.Count; i++)
                {
                    var row = rows[i];
                    if (column.ColumnIndex >= row.Cells.Count)
                        continue;

                    var value = row.Cells[column.ColumnIndex];
                    if (value.IndexOf(',') < 0)
                        continue;

                    if (count == 0)
                    {
                        firstAddress = CellAddress.ToA1(column.ColumnIndex, row.RowNumber);
                        firstText = value;
                    }
                    count++;
                }

                if (count > 0)
                    warnings.Add(new SourceWarning(SourceWarningCode.CommaInNumber, firstAddress,
                        $"Column '{column.Name}' has {count} value(s) with ',' (first: '{firstText}'); " +
                        "the spreadsheet locale probably uses ',' as decimal separator — switch File → Settings → Locale to one with '.'"));
            }
            return warnings;
        }

        private static bool IsSpreadsheetId(string value)
        {
            foreach (var c in value)
            {
                var ok = (c >= 'A' && c <= 'Z') || (c >= 'a' && c <= 'z') || (c >= '0' && c <= '9') || c == '_' || c == '-';
                if (!ok)
                    return false;
            }
            return true;
        }

        private static bool IsDigits(string value)
        {
            foreach (var c in value)
            {
                if (c < '0' || c > '9')
                    return false;
            }
            return true;
        }
    }
}
