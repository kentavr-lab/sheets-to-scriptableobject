using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Kentavr.SheetsToSO.Core;

namespace Kentavr.SheetsToSO.Editor
{
    /// <summary>Sheets loaded from a source, with the problems of the source itself.</summary>
    internal sealed class SheetLoadResult
    {
        public List<SheetImportSheet> Sheets { get; } = new List<SheetImportSheet>();

        /// <summary>Loading errors; any error blocks the import.</summary>
        public List<string> Errors { get; } = new List<string>();

        /// <summary>Non-blocking problems of the data, e.g. ',' in numbers.</summary>
        public List<string> Warnings { get; } = new List<string>();
    }

    /// <summary>Tab names of a spreadsheet, or why they could not be read.</summary>
    internal sealed class SheetListResult
    {
        public List<string> Titles { get; } = new List<string>();

        public string Error { get; set; }
    }

    /// <summary>
    /// Loads sheets for the import window from Google Sheets API, CSV export links or a folder of CSV files.
    /// Owns one <see cref="HttpClient"/>; dispose it with the window.
    /// Errors are returned in the results; only <see cref="OperationCanceledException"/> is thrown.
    /// </summary>
    internal sealed class SheetSourceLoader : IDisposable
    {
        private readonly HttpClient _http = new HttpClient();

        public void Dispose() => _http.Dispose();

        /// <summary>Lists the tabs through Google Sheets API.</summary>
        public async Task<SheetListResult> ListGoogleSheetsAsync(string spreadsheetId, string apiKey, CancellationToken ct)
        {
            var result = new SheetListResult();
            try
            {
                var client = CreateClient(apiKey, spreadsheetId);
                var sheets = await client.GetSheetsAsync(spreadsheetId.Trim(), ct);
                result.Titles.AddRange(sheets.OrderBy(sheet => sheet.Index).Select(sheet => sheet.Title));
            }
            catch (Exception e) when (!(e is OperationCanceledException) || !ct.IsCancellationRequested)
            {
                result.Error = Describe(e);
            }
            return result;
        }

        /// <summary>Loads the tabs with one <c>values:batchGet</c> request.</summary>
        public async Task<SheetLoadResult> LoadGoogleAsync(string spreadsheetId, string apiKey, IReadOnlyList<string> titles,
            CancellationToken ct)
        {
            var result = new SheetLoadResult();
            try
            {
                var client = CreateClient(apiKey, spreadsheetId);
                var values = await client.GetValuesAsync(spreadsheetId.Trim(), titles, ct);
                foreach (var sheet in values)
                    result.Sheets.Add(CsvFolderSource.ToSheet(sheet.Title, sheet.Rows));
            }
            catch (Exception e) when (!(e is OperationCanceledException) || !ct.IsCancellationRequested)
            {
                result.Errors.Add(Describe(e));
            }
            return result;
        }

        /// <summary>Loads the sheets one by one through CSV export links; no API key needed.</summary>
        public async Task<SheetLoadResult> LoadExportAsync(string spreadsheetId, IReadOnlyList<ExportSheetEntry> sheets,
            CancellationToken ct)
        {
            var result = new SheetLoadResult();
            var source = new CsvExportSource(_http);
            var id = (spreadsheetId ?? string.Empty).Trim();

            foreach (var sheet in sheets)
            {
                ct.ThrowIfCancellationRequested();
                var name = (sheet.name ?? string.Empty).Trim();
                var gid = (sheet.gid ?? string.Empty).Trim();
                if (gid.Length == 0 || !gid.All(c => c >= '0' && c <= '9'))
                {
                    result.Errors.Add($"{name}: {SourceErrorCode.InvalidGid}: gid '{gid}' must be a number from the sheet URL (gid=…)");
                    continue;
                }

                CsvExportResult loaded;
                try
                {
                    loaded = await source.LoadAsync(id, gid, ct);
                }
                catch (ArgumentException e)
                {
                    result.Errors.Add($"{name}: {FirstLine(e.Message)}");
                    continue;
                }

                if (loaded.Error != null)
                {
                    result.Errors.Add($"{name}: {loaded.Error}");
                    continue;
                }
                foreach (var warning in loaded.Warnings)
                    result.Warnings.Add($"{name}!{warning}");
                if (!loaded.Csv.IsValid)
                {
                    foreach (var error in loaded.Csv.Errors)
                        result.Errors.Add($"{name}: {error}");
                    continue;
                }

                result.Sheets.Add(CsvFolderSource.ToSheet(name, loaded.Csv.Rows));
            }
            return result;
        }

        /// <summary>Reads the given files of a folder on disk.</summary>
        public static SheetLoadResult LoadCsvFolder(string folder, IReadOnlyList<string> fileNames)
        {
            var result = new SheetLoadResult();
            if (string.IsNullOrWhiteSpace(folder) || !Directory.Exists(folder))
            {
                result.Errors.Add($"CSV folder '{folder}' not found");
                return result;
            }

            foreach (var fileName in fileNames)
            {
                var sheet = CsvFolderSource.Read(Path.Combine(folder, fileName), result.Errors);
                if (sheet != null)
                    result.Sheets.Add(sheet);
            }
            return result;
        }

        private GoogleSheetsClient CreateClient(string apiKey, string spreadsheetId)
        {
            if (string.IsNullOrWhiteSpace(apiKey))
                throw new ArgumentException("API-ключ не задан");
            if (string.IsNullOrWhiteSpace(spreadsheetId))
                throw new ArgumentException("ID таблицы не задан");
            return new GoogleSheetsClient(_http, apiKey.Trim());
        }

        // Messages of GoogleSheetsException never contain the key; other exceptions carry no request headers.
        private static string Describe(Exception e)
        {
            switch (e)
            {
                case GoogleSheetsException google:
                    return $"{google.Code}: {google.Message}";
                case ArgumentException argument:
                    return FirstLine(argument.Message);
                case HttpRequestException http:
                    return "Сетевая ошибка: " + http.Message;
                case OperationCanceledException _:
                    return "Время ожидания ответа истекло";
                default:
                    return e.GetType().Name + ": " + e.Message;
            }
        }

        // ArgumentException appends " (Parameter 'x')" on a new line in some runtimes.
        private static string FirstLine(string message)
        {
            var index = message.IndexOfAny(new[] { '\r', '\n' });
            return index < 0 ? message : message.Substring(0, index);
        }
    }
}
