using System;
using System.Collections.Generic;
using Kentavr.SheetsToSO.Core;

namespace Kentavr.SheetsToSO.Cli
{
    /// <summary>Where the sheets come from.</summary>
    public enum SourceKind
    {
        Csv,
        Api,
        Export,
    }

    /// <summary>One sheet to check: a local file, an API tab name or an export gid.</summary>
    public sealed class SheetSpec
    {
        public SheetSpec(string name, string path, CsvDelimiter delimiter, string gid)
        {
            Name = name;
            Path = path;
            Delimiter = delimiter;
            Gid = gid;
        }

        /// <summary>Sheet name used in error addresses.</summary>
        public string Name { get; }

        /// <summary>File path for <see cref="SourceKind.Csv"/>; otherwise <c>null</c>.</summary>
        public string Path { get; }

        /// <summary>Delimiter for <see cref="SourceKind.Csv"/>.</summary>
        public CsvDelimiter Delimiter { get; }

        /// <summary>Sheet gid for <see cref="SourceKind.Export"/>; otherwise <c>null</c>.</summary>
        public string Gid { get; }
    }

    /// <summary>Command line options of <c>sheets-to-so</c>.</summary>
    public sealed class CliOptions
    {
        public const string DefaultApiKeyVariable = "SHEETS_TO_SO_API_KEY";

        public const string Usage =
            "Usage:\n" +
            "  sheets-to-so --source csv    --file <path> [--file <path> ...]\n" +
            "  sheets-to-so --source api    --spreadsheet <id> [--sheet <name> ...] [--api-key-env <VARIABLE>]\n" +
            "  sheets-to-so --source export --spreadsheet <id> --sheet <name>=<gid> [--sheet <name>=<gid> ...]\n" +
            "  sheets-to-so --help\n" +
            "\n" +
            "  csv     each file is one sheet named after the file without extension; .tsv is tab-separated\n" +
            "  api     Google Sheets API; the key is read from " + DefaultApiKeyVariable + "\n" +
            "          (or the variable named by --api-key-env); without --sheet all tabs except '#...' are checked\n" +
            "  export  CSV export link of a spreadsheet shared as 'Anyone with the link'\n" +
            "\n" +
            "Exit codes: 0 - no errors, 1 - data errors, 2 - argument or access errors.\n";

        private CliOptions(bool help, SourceKind source, string spreadsheetId, IReadOnlyList<SheetSpec> sheets,
            string apiKeyVariable)
        {
            Help = help;
            Source = source;
            SpreadsheetId = spreadsheetId;
            Sheets = sheets;
            ApiKeyVariable = apiKeyVariable;
        }

        /// <summary><c>--help</c> was given; other options are not set.</summary>
        public bool Help { get; }

        public SourceKind Source { get; }

        /// <summary>Spreadsheet id for <c>api</c> and <c>export</c>.</summary>
        public string SpreadsheetId { get; }

        /// <summary>Sheets in command line order; empty for <c>api</c> means all tabs.</summary>
        public IReadOnlyList<SheetSpec> Sheets { get; }

        /// <summary>Environment variable with the API key; set only for <c>api</c>.</summary>
        public string ApiKeyVariable { get; }

        /// <summary>Parses the arguments. Returns <c>null</c> and sets <paramref name="error"/> when they are invalid.</summary>
        public static CliOptions Parse(string[] args, out string error)
        {
            if (args == null)
                throw new ArgumentNullException(nameof(args));

            foreach (var arg in args)
            {
                if (arg == "--help" || arg == "-h")
                {
                    error = null;
                    return new CliOptions(true, default, null, Array.Empty<SheetSpec>(), null);
                }
            }

            string source = null;
            string spreadsheetId = null;
            string apiKeyVariable = null;
            var files = new List<string>();
            var sheets = new List<string>();

            for (var i = 0; i < args.Length; i++)
            {
                var key = args[i];
                switch (key)
                {
                    case "--source":
                    case "--spreadsheet":
                    case "--api-key-env":
                    case "--file":
                    case "--sheet":
                        break;
                    default:
                        error = key.StartsWith("-", StringComparison.Ordinal)
                            ? $"unknown option '{key}'"
                            : $"unexpected argument '{key}'";
                        return null;
                }

                if (i + 1 >= args.Length || args[i + 1].StartsWith("--", StringComparison.Ordinal))
                {
                    error = $"option '{key}' needs a value";
                    return null;
                }

                var value = args[++i];
                switch (key)
                {
                    case "--source":
                        if (!SetOnce(ref source, key, value, out error))
                            return null;
                        break;
                    case "--spreadsheet":
                        if (!SetOnce(ref spreadsheetId, key, value, out error))
                            return null;
                        break;
                    case "--api-key-env":
                        if (!SetOnce(ref apiKeyVariable, key, value, out error))
                            return null;
                        break;
                    case "--file":
                        files.Add(value);
                        break;
                    case "--sheet":
                        sheets.Add(value);
                        break;
                }
            }

            switch (source)
            {
                case null:
                    error = "option '--source' is required";
                    return null;
                case "csv":
                    return ParseCsv(files, spreadsheetId, sheets, apiKeyVariable, out error);
                case "api":
                    return ParseApi(files, spreadsheetId, sheets, apiKeyVariable, out error);
                case "export":
                    return ParseExport(files, spreadsheetId, sheets, apiKeyVariable, out error);
                default:
                    error = $"unknown source '{source}'; expected csv, api or export";
                    return null;
            }
        }

        private static CliOptions ParseCsv(List<string> files, string spreadsheetId, List<string> sheets,
            string apiKeyVariable, out string error)
        {
            if (!NotAllowed("csv", spreadsheetId != null, "--spreadsheet", out error)
                || !NotAllowed("csv", sheets.Count > 0, "--sheet", out error)
                || !NotAllowed("csv", apiKeyVariable != null, "--api-key-env", out error))
                return null;
            if (files.Count == 0)
            {
                error = "source csv needs at least one '--file'";
                return null;
            }

            var specs = new List<SheetSpec>();
            foreach (var file in files)
            {
                var name = System.IO.Path.GetFileNameWithoutExtension(file);
                if (string.IsNullOrEmpty(name))
                {
                    error = $"cannot take a sheet name from file path '{file}'";
                    return null;
                }

                var delimiter = string.Equals(System.IO.Path.GetExtension(file), ".tsv", StringComparison.OrdinalIgnoreCase)
                    ? CsvDelimiter.Tab
                    : CsvDelimiter.Comma;
                specs.Add(new SheetSpec(name, file, delimiter, null));
            }

            return Create(SourceKind.Csv, null, specs, null, out error);
        }

        private static CliOptions ParseApi(List<string> files, string spreadsheetId, List<string> sheets,
            string apiKeyVariable, out string error)
        {
            if (!NotAllowed("api", files.Count > 0, "--file", out error)
                || !Required("api", spreadsheetId, "--spreadsheet", out error))
                return null;

            var specs = new List<SheetSpec>();
            foreach (var sheet in sheets)
                specs.Add(new SheetSpec(sheet, null, CsvDelimiter.Comma, null));

            return Create(SourceKind.Api, spreadsheetId, specs, apiKeyVariable ?? DefaultApiKeyVariable, out error);
        }

        private static CliOptions ParseExport(List<string> files, string spreadsheetId, List<string> sheets,
            string apiKeyVariable, out string error)
        {
            if (!NotAllowed("export", files.Count > 0, "--file", out error)
                || !NotAllowed("export", apiKeyVariable != null, "--api-key-env", out error)
                || !Required("export", spreadsheetId, "--spreadsheet", out error))
                return null;
            if (sheets.Count == 0)
            {
                error = "source export needs at least one '--sheet <name>=<gid>'";
                return null;
            }

            var specs = new List<SheetSpec>();
            foreach (var sheet in sheets)
            {
                var separator = sheet.LastIndexOf('=');
                var name = separator < 0 ? sheet : sheet.Substring(0, separator);
                var gid = separator < 0 ? string.Empty : sheet.Substring(separator + 1);
                if (name.Length == 0 || !IsDigits(gid))
                {
                    error = $"'--sheet {sheet}': expected <name>=<gid>, where gid is the number after 'gid=' in the sheet URL";
                    return null;
                }
                specs.Add(new SheetSpec(name, null, CsvDelimiter.Comma, gid));
            }

            return Create(SourceKind.Export, spreadsheetId, specs, null, out error);
        }

        private static CliOptions Create(SourceKind source, string spreadsheetId, List<SheetSpec> specs,
            string apiKeyVariable, out string error)
        {
            var names = new HashSet<string>(StringComparer.Ordinal);
            foreach (var spec in specs)
            {
                if (!names.Add(spec.Name))
                {
                    error = $"sheet '{spec.Name}' is given more than once";
                    return null;
                }
            }

            error = null;
            return new CliOptions(false, source, spreadsheetId, specs.AsReadOnly(), apiKeyVariable);
        }

        private static bool SetOnce(ref string field, string key, string value, out string error)
        {
            if (field != null)
            {
                error = $"option '{key}' is given more than once";
                return false;
            }
            field = value;
            error = null;
            return true;
        }

        private static bool NotAllowed(string source, bool present, string key, out string error)
        {
            error = present ? $"option '{key}' is not used with source {source}" : null;
            return !present;
        }

        private static bool Required(string source, string value, string key, out string error)
        {
            var missing = string.IsNullOrWhiteSpace(value);
            error = missing ? $"source {source} needs '{key}'" : null;
            return !missing;
        }

        private static bool IsDigits(string value)
        {
            if (value.Length == 0)
                return false;
            foreach (var c in value)
            {
                if (c < '0' || c > '9')
                    return false;
            }
            return true;
        }
    }
}
