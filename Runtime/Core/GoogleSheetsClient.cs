using System;
using System.Collections.Generic;
using System.Globalization;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Kentavr.SheetsToSO.Core
{
    /// <summary>
    /// Reads a spreadsheet shared "anyone with the link can view" through Google Sheets API v4 with an API key.
    /// The key is sent in the <c>X-goog-api-key</c> header, so it never appears in URLs or exception messages.
    /// </summary>
    public sealed class GoogleSheetsClient
    {
        private const string BaseUrl = "https://sheets.googleapis.com/v4/spreadsheets/";
        private const string ApiKeyHeader = "X-goog-api-key";
        private const string SheetFields = "sheets.properties(sheetId,title,index,hidden)";
        private const string RedactedKey = "***";

        private readonly HttpClient _http;
        private readonly string _apiKey;
        private readonly GoogleSheetsRetryOptions _options;

        /// <param name="http">Owned by the caller; the client does not dispose it.</param>
        /// <param name="apiKey">Google API key with Google Sheets API enabled.</param>
        /// <param name="options">Retry policy; defaults when <c>null</c>.</param>
        public GoogleSheetsClient(HttpClient http, string apiKey, GoogleSheetsRetryOptions options = null)
        {
            if (string.IsNullOrWhiteSpace(apiKey))
                throw new ArgumentException("API key is empty", nameof(apiKey));

            _http = http ?? throw new ArgumentNullException(nameof(http));
            _apiKey = apiKey;
            _options = options ?? new GoogleSheetsRetryOptions();
        }

        /// <summary>Lists the tabs of the spreadsheet in the order returned by the API.</summary>
        public async Task<IReadOnlyList<SheetInfo>> GetSheetsAsync(string spreadsheetId, CancellationToken ct = default)
        {
            var url = SpreadsheetUrl(spreadsheetId) + "?fields=" + Uri.EscapeDataString(SheetFields);
            var root = await GetJsonAsync(url, spreadsheetId, ct).ConfigureAwait(false);

            try
            {
                return ParseSheets(root);
            }
            catch (FormatException e)
            {
                throw InvalidResponse(e.Message);
            }
        }

        /// <summary>
        /// Reads the values of the given tabs with a single <c>values:batchGet</c> request.
        /// The result follows the order of <paramref name="sheetTitles"/>.
        /// </summary>
        public async Task<IReadOnlyList<SheetValues>> GetValuesAsync(string spreadsheetId,
            IReadOnlyList<string> sheetTitles, CancellationToken ct = default)
        {
            var baseUrl = SpreadsheetUrl(spreadsheetId);
            if (sheetTitles == null)
                throw new ArgumentNullException(nameof(sheetTitles));
            for (var i = 0; i < sheetTitles.Count; i++)
            {
                if (string.IsNullOrEmpty(sheetTitles[i]))
                    throw new ArgumentException($"Sheet title at index {i} is empty", nameof(sheetTitles));
            }

            if (sheetTitles.Count == 0)
                return Array.Empty<SheetValues>();

            var url = new StringBuilder(baseUrl).Append("/values:batchGet?");
            foreach (var title in sheetTitles)
                url.Append("ranges=").Append(Uri.EscapeDataString(ToRange(title))).Append('&');
            url.Append("majorDimension=ROWS")
                .Append("&valueRenderOption=UNFORMATTED_VALUE")
                .Append("&dateTimeRenderOption=SERIAL_NUMBER");

            var root = await GetJsonAsync(url.ToString(), spreadsheetId, ct).ConfigureAwait(false);

            try
            {
                return ParseValues(root, sheetTitles);
            }
            catch (FormatException e)
            {
                throw InvalidResponse(e.Message);
            }
        }

        /// <summary>A1 range covering a whole tab: the name in single quotes, inner quotes doubled.</summary>
        internal static string ToRange(string sheetTitle) => "'" + sheetTitle.Replace("'", "''") + "'";

        private static string SpreadsheetUrl(string spreadsheetId)
        {
            if (string.IsNullOrWhiteSpace(spreadsheetId))
                throw new ArgumentException("Spreadsheet id is empty", nameof(spreadsheetId));
            return BaseUrl + Uri.EscapeDataString(spreadsheetId);
        }

        private static IReadOnlyList<SheetInfo> ParseSheets(object root)
        {
            var result = new List<SheetInfo>();
            var sheets = Optional<List<object>>(AsObject(root, "response"), "sheets");
            if (sheets == null)
                return result.AsReadOnly();

            foreach (var sheet in sheets)
            {
                var properties = Required<Dictionary<string, object>>(AsObject(sheet, "sheet"), "properties");
                var title = Required<string>(properties, "title");
                // Proto3 JSON may omit fields with default values (0, false).
                var sheetId = ToInt(Optional<JsonNumber>(properties, "sheetId"), "sheetId");
                var index = ToInt(Optional<JsonNumber>(properties, "index"), "index");
                properties.TryGetValue("hidden", out var hiddenValue);
                bool hidden;
                if (hiddenValue == null)
                    hidden = false;
                else if (hiddenValue is bool b)
                    hidden = b;
                else
                    throw new FormatException("'hidden' is not a boolean");

                result.Add(new SheetInfo(title, sheetId, index, hidden));
            }

            return result.AsReadOnly();
        }

        private static IReadOnlyList<SheetValues> ParseValues(object root, IReadOnlyList<string> sheetTitles)
        {
            var ranges = Optional<List<object>>(AsObject(root, "response"), "valueRanges");
            var count = ranges?.Count ?? 0;
            if (count != sheetTitles.Count)
                throw new FormatException($"Expected {sheetTitles.Count} value ranges, got {count}");

            var result = new List<SheetValues>(count);
            for (var i = 0; i < count; i++)
            {
                var range = AsObject(ranges[i], "valueRange");
                range.TryGetValue("values", out var values);
                result.Add(new SheetValues(sheetTitles[i], SheetValuesConverter.ToRows(values)));
            }

            return result.AsReadOnly();
        }

        private static Dictionary<string, object> AsObject(object value, string name) =>
            value as Dictionary<string, object> ?? throw new FormatException($"'{name}' is not an object");

        private static T Optional<T>(Dictionary<string, object> obj, string key) where T : class
        {
            if (!obj.TryGetValue(key, out var value) || value == null)
                return null;
            return value as T ?? throw new FormatException($"'{key}' has an unexpected type");
        }

        private static T Required<T>(Dictionary<string, object> obj, string key) where T : class =>
            Optional<T>(obj, key) ?? throw new FormatException($"'{key}' is missing");

        private static int ToInt(JsonNumber number, string name)
        {
            if (number == null)
                return 0;
            if (!int.TryParse(number.Text, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var value))
                throw new FormatException($"'{name}' is not an integer");
            return value;
        }

        private async Task<object> GetJsonAsync(string url, string spreadsheetId, CancellationToken ct)
        {
            var quotaRetries = 0;
            var serverRetries = 0;

            while (true)
            {
                ct.ThrowIfCancellationRequested();

                using (var request = new HttpRequestMessage(HttpMethod.Get, url))
                {
                    request.Headers.TryAddWithoutValidation(ApiKeyHeader, _apiKey);

                    using (var response = await _http.SendAsync(request, ct).ConfigureAwait(false))
                    {
                        var status = (int)response.StatusCode;
                        var body = response.Content == null
                            ? ""
                            : await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                        // netstandard2.1 has no ReadAsStringAsync(CancellationToken); check right after reading.
                        ct.ThrowIfCancellationRequested();

                        if (status >= 200 && status < 300)
                        {
                            try
                            {
                                return JsonReader.Parse(body);
                            }
                            catch (FormatException e)
                            {
                                throw InvalidResponse("JSON не разобран: " + e.Message);
                            }
                        }

                        if (status == 429)
                        {
                            if (quotaRetries >= _options.MaxQuotaRetries)
                                throw Error(GoogleSheetsErrorCode.QuotaExceeded, status,
                                    "Превышена квота запросов Google Sheets API. Повторите позже", body);

                            quotaRetries++;
                            await _options.Delay(QuotaPause(response), ct).ConfigureAwait(false);
                            continue;
                        }

                        if (status >= 500 && status < 600)
                        {
                            if (serverRetries >= _options.MaxServerRetries)
                                throw Error(GoogleSheetsErrorCode.ServerError, status,
                                    $"Сервер Google Sheets API ответил ошибкой {status}. Повторите позже", body);

                            var pause = Backoff(serverRetries);
                            serverRetries++;
                            await _options.Delay(pause, ct).ConfigureAwait(false);
                            continue;
                        }

                        throw ClientError(status, body, spreadsheetId);
                    }
                }
            }
        }

        private TimeSpan QuotaPause(HttpResponseMessage response)
        {
            var retryAfter = response.Headers.RetryAfter;
            TimeSpan? pause = null;
            if (retryAfter?.Delta != null)
                pause = retryAfter.Delta.Value;
            else if (retryAfter?.Date != null)
                pause = retryAfter.Date.Value - DateTimeOffset.UtcNow;

            if (pause == null)
                return _options.QuotaWindow;
            return pause.Value < TimeSpan.Zero ? TimeSpan.Zero : pause.Value;
        }

        private TimeSpan Backoff(int attempt)
        {
            var ticks = _options.InitialBackoff.Ticks * Math.Pow(2, attempt);
            var max = _options.MaxBackoff.Ticks;
            return ticks >= max ? _options.MaxBackoff : TimeSpan.FromTicks((long)ticks);
        }

        private GoogleSheetsException ClientError(int status, string body, string spreadsheetId)
        {
            switch (status)
            {
                case 400 when HasReason(body, "API_KEY_INVALID", "keyInvalid"):
                    return Error(GoogleSheetsErrorCode.InvalidApiKey, status,
                        "API-ключ не принят Google. Проверьте ключ", body);

                case 403:
                    return Error(GoogleSheetsErrorCode.AccessDenied, status,
                        "Нет доступа к таблице. Откройте её «всем, у кого есть ссылка — просмотр» " +
                        "и проверьте, что для ключа включён Google Sheets API", body);

                case 404:
                    return Error(GoogleSheetsErrorCode.NotFound, status,
                        $"Таблица {spreadsheetId} не найдена. Проверьте ID", body);

                default:
                    if (status >= 400 && status < 500)
                        return Error(GoogleSheetsErrorCode.BadRequest, status,
                            $"Google Sheets API отклонил запрос ({status}). Проверьте ID таблицы и имена вкладок", body);
                    return Error(GoogleSheetsErrorCode.InvalidResponse, status,
                        $"Неожиданный ответ Google Sheets API ({status})", body);
            }
        }

        private GoogleSheetsException InvalidResponse(string detail) =>
            new GoogleSheetsException(GoogleSheetsErrorCode.InvalidResponse, null,
                Redact("Неожиданный ответ Google Sheets API: " + detail));

        private GoogleSheetsException Error(GoogleSheetsErrorCode code, int status, string message, string body)
        {
            var googleMessage = ErrorMessage(body);
            if (!string.IsNullOrWhiteSpace(googleMessage))
                message += ". Ответ Google: " + googleMessage;
            return new GoogleSheetsException(code, status, Redact(message));
        }

        private string Redact(string text) => text.Replace(_apiKey, RedactedKey);

        /// <summary>Reads <c>error.message</c> from a Google error body; <c>null</c> if the body is not one.</summary>
        private static string ErrorMessage(string body) => ParseError(body)?.TryGetValue("message", out var m) == true
            ? m as string
            : null;

        private static Dictionary<string, object> ParseError(string body)
        {
            if (string.IsNullOrWhiteSpace(body))
                return null;
            try
            {
                return JsonReader.Parse(body) is Dictionary<string, object> root
                    && root.TryGetValue("error", out var error)
                    ? error as Dictionary<string, object>
                    : null;
            }
            catch (FormatException)
            {
                return null;
            }
        }

        /// <summary>Looks for a reason in <c>error.details[].reason</c> and <c>error.errors[].reason</c>.</summary>
        private static bool HasReason(string body, params string[] reasons)
        {
            var error = ParseError(body);
            if (error == null)
                return false;

            foreach (var listKey in new[] { "details", "errors" })
            {
                if (!error.TryGetValue(listKey, out var list) || !(list is List<object> items))
                    continue;
                foreach (var item in items)
                {
                    if (item is Dictionary<string, object> entry
                        && entry.TryGetValue("reason", out var reason)
                        && reason is string text
                        && Array.IndexOf(reasons, text) >= 0)
                        return true;
                }
            }

            return false;
        }
    }
}
