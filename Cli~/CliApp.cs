using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using Kentavr.SheetsToSO.Core;

namespace Kentavr.SheetsToSO.Cli
{
    /// <summary>Load → parse → cross-sheet check → report.</summary>
    public static class CliApp
    {
        public const int ExitOk = 0;
        public const int ExitDataErrors = 1;
        public const int ExitAccessErrors = 2;

        /// <returns>0 — no errors, 1 — data errors, 2 — argument or access errors.</returns>
        public static async Task<int> RunAsync(string[] args, TextWriter stdout, TextWriter stderr,
            Func<string, string> getEnvironmentVariable, HttpMessageHandler handler)
        {
            if (stdout == null)
                throw new ArgumentNullException(nameof(stdout));
            if (stderr == null)
                throw new ArgumentNullException(nameof(stderr));
            if (getEnvironmentVariable == null)
                throw new ArgumentNullException(nameof(getEnvironmentVariable));

            var options = CliOptions.Parse(args ?? Array.Empty<string>(), out var argumentError);
            if (options == null)
                return ArgumentFailure(stderr, argumentError);

            if (options.Help)
            {
                stdout.Write(CliOptions.Usage);
                return ExitOk;
            }

            string apiKey = null;
            if (options.Source == SourceKind.Api)
            {
                apiKey = getEnvironmentVariable(options.ApiKeyVariable);
                if (string.IsNullOrWhiteSpace(apiKey))
                    return ArgumentFailure(stderr,
                        $"environment variable {options.ApiKeyVariable} with the Google API key is not set");
            }

            var loaded = await SheetLoader.LoadAsync(options, apiKey, handler).ConfigureAwait(false);
            if (loaded.AccessErrors.Count > 0)
            {
                foreach (var error in loaded.AccessErrors)
                    stderr.WriteLine("error: " + error);
                return ExitAccessErrors;
            }

            var parsed = new List<SheetParseResult>();
            foreach (var sheet in loaded.Sheets)
            {
                var rows = new List<IReadOnlyList<string>>(sheet.Rows.Count);
                foreach (var row in sheet.Rows)
                    rows.Add(row.Cells);
                parsed.Add(SheetParser.Parse(sheet.Name, rows));
            }

            var cross = CrossSheetValidator.Validate(parsed);

            var errorCount = 0;
            var warningCount = 0;
            for (var i = 0; i < loaded.Sheets.Count; i++)
            {
                var sheet = loaded.Sheets[i];
                foreach (var error in sheet.CsvErrors)
                {
                    stdout.WriteLine($"error: {sheet.Name} {error}");
                    errorCount++;
                }
                foreach (var error in parsed[i].Errors)
                {
                    stdout.WriteLine("error: " + error);
                    errorCount++;
                }
                foreach (var error in cross.Errors)
                {
                    if (error.SheetName != sheet.Name)
                        continue;
                    stdout.WriteLine("error: " + error);
                    errorCount++;
                }
                // Same rule as the import: a generated ref<X> property must not clash with row class members.
                foreach (var column in parsed[i].Columns)
                {
                    if (column.Type.Kind != ColumnKind.Ref)
                        continue;
                    var conflict = CodeGenerator.FindRefPropertyConflict(column, parsed[i].Columns, sheet.Name);
                    if (conflict == null)
                        continue;
                    stdout.WriteLine($"error: {sheet.Name}!{column.Address}: Column '{column.Name}': {conflict}");
                    errorCount++;
                }
                foreach (var warning in sheet.Warnings)
                {
                    stdout.WriteLine($"warning: {sheet.Name}!{warning}");
                    warningCount++;
                }
            }

            stdout.WriteLine($"Sheets: {loaded.Sheets.Count}, errors: {errorCount}, warnings: {warningCount}");
            return errorCount > 0 ? ExitDataErrors : ExitOk;
        }

        private static int ArgumentFailure(TextWriter stderr, string message)
        {
            stderr.WriteLine("error: " + message);
            stderr.WriteLine();
            stderr.Write(CliOptions.Usage);
            return ExitAccessErrors;
        }
    }
}
