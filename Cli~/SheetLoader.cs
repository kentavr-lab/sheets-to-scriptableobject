using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Kentavr.SheetsToSO.Core;

namespace Kentavr.SheetsToSO.Cli
{
    /// <summary>Rows of one loaded sheet plus problems found while reading it.</summary>
    public sealed class LoadedSheet
    {
        public LoadedSheet(string name, IReadOnlyList<CsvRow> rows, IReadOnlyList<CsvError> csvErrors,
            IReadOnlyList<SourceWarning> warnings)
        {
            Name = name;
            Rows = rows;
            CsvErrors = csvErrors;
            Warnings = warnings;
        }

        public string Name { get; }

        /// <summary>Records in sheet order; <c>Rows[i]</c> is sheet row <c>i + 1</c>.</summary>
        public IReadOnlyList<CsvRow> Rows { get; }

        /// <summary>CSV syntax errors; they are data errors.</summary>
        public IReadOnlyList<CsvError> CsvErrors { get; }

        /// <summary>Warnings of the source; they do not affect the exit code.</summary>
        public IReadOnlyList<SourceWarning> Warnings { get; }
    }

    /// <summary>Result of loading all requested sheets.</summary>
    public sealed class LoadResult
    {
        public LoadResult(IReadOnlyList<LoadedSheet> sheets, IReadOnlyList<string> accessErrors)
        {
            Sheets = sheets;
            AccessErrors = accessErrors;
        }

        public IReadOnlyList<LoadedSheet> Sheets { get; }

        /// <summary>Problems that prevented reading data: missing files, HTTP and API errors.</summary>
        public IReadOnlyList<string> AccessErrors { get; }
    }

    /// <summary>Brings every source to the same list of sheets with string rows.</summary>
    public static class SheetLoader
    {
        private const char SkipPrefix = '#';

        private static readonly Encoding Utf8 = new UTF8Encoding(false, false);

        /// <param name="apiKey">Required for <see cref="SourceKind.Api"/>; never written anywhere.</param>
        public static async Task<LoadResult> LoadAsync(CliOptions options, string apiKey, HttpMessageHandler handler)
        {
            if (options == null)
                throw new ArgumentNullException(nameof(options));

            switch (options.Source)
            {
                case SourceKind.Csv:
                    return LoadFiles(options.Sheets);
                case SourceKind.Api:
                    using (var http = new HttpClient(handler, false))
                        return await LoadApiAsync(http, apiKey, options.SpreadsheetId, options.Sheets).ConfigureAwait(false);
                case SourceKind.Export:
                    using (var http = new HttpClient(handler, false))
                        return await LoadExportAsync(http, options.SpreadsheetId, options.Sheets).ConfigureAwait(false);
                default:
                    throw new ArgumentOutOfRangeException(nameof(options), options.Source, "Unknown source");
            }
        }

        private static LoadResult LoadFiles(IReadOnlyList<SheetSpec> specs)
        {
            var sheets = new List<LoadedSheet>();
            var errors = new List<string>();
            foreach (var spec in specs)
            {
                string text;
                try
                {
                    text = File.ReadAllText(spec.Path, Utf8);
                }
                catch (Exception e) when (e is IOException || e is UnauthorizedAccessException
                                          || e is ArgumentException || e is NotSupportedException)
                {
                    errors.Add(e is FileNotFoundException || e is DirectoryNotFoundException
                        ? $"file '{spec.Path}' not found"
                        : $"cannot read file '{spec.Path}': {e.Message}");
                    continue;
                }

                var csv = CsvReader.Read(text, spec.Delimiter);
                sheets.Add(new LoadedSheet(spec.Name, csv.Rows, csv.Errors, Array.Empty<SourceWarning>()));
            }
            return new LoadResult(sheets.AsReadOnly(), errors.AsReadOnly());
        }

        private static async Task<LoadResult> LoadApiAsync(HttpClient http, string apiKey, string spreadsheetId,
            IReadOnlyList<SheetSpec> specs)
        {
            var client = new GoogleSheetsClient(http, apiKey);
            try
            {
                var titles = new List<string>();
                if (specs.Count > 0)
                {
                    foreach (var spec in specs)
                        titles.Add(spec.Name);
                }
                else
                {
                    foreach (var info in await client.GetSheetsAsync(spreadsheetId).ConfigureAwait(false))
                    {
                        if (info.Title.Length > 0 && info.Title[0] != SkipPrefix)
                            titles.Add(info.Title);
                    }
                }

                var sheets = new List<LoadedSheet>();
                foreach (var values in await client.GetValuesAsync(spreadsheetId, titles).ConfigureAwait(false))
                    sheets.Add(new LoadedSheet(values.Title, values.Rows, Array.Empty<CsvError>(), Array.Empty<SourceWarning>()));
                return new LoadResult(sheets.AsReadOnly(), Array.Empty<string>());
            }
            catch (GoogleSheetsException e)
            {
                return Failed(e.Message);
            }
            catch (HttpRequestException e)
            {
                return Failed("request to Google Sheets API failed: " + Redact(e.Message, apiKey));
            }
            catch (TaskCanceledException e)
            {
                return Failed("request to Google Sheets API timed out: " + Redact(e.Message, apiKey));
            }
        }

        private static async Task<LoadResult> LoadExportAsync(HttpClient http, string spreadsheetId,
            IReadOnlyList<SheetSpec> specs)
        {
            var source = new CsvExportSource(http);
            var sheets = new List<LoadedSheet>();
            var errors = new List<string>();
            foreach (var spec in specs)
            {
                CsvExportResult result;
                try
                {
                    result = await source.LoadAsync(spreadsheetId, spec.Gid).ConfigureAwait(false);
                }
                catch (ArgumentException e)
                {
                    errors.Add(e.Message);
                    continue;
                }

                if (result.Error != null)
                {
                    errors.Add($"{spec.Name}: {result.Error.Message}");
                    continue;
                }

                sheets.Add(new LoadedSheet(spec.Name, result.Csv.Rows, result.Csv.Errors, result.Warnings));
            }
            return new LoadResult(sheets.AsReadOnly(), errors.AsReadOnly());
        }

        private static LoadResult Failed(string message) =>
            new LoadResult(Array.Empty<LoadedSheet>(), new[] { message });

        private static string Redact(string text, string apiKey) =>
            string.IsNullOrEmpty(apiKey) ? text : text.Replace(apiKey, "***");
    }
}
