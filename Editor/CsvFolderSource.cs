using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Kentavr.SheetsToSO.Core;

namespace Kentavr.SheetsToSO.Editor
{
    /// <summary>
    /// Sheets from a folder on disk: every <c>.csv</c> / <c>.tsv</c> file is a sheet named after the file.
    /// </summary>
    internal static class CsvFolderSource
    {
        /// <summary>
        /// File names (with extension) of the sheet files directly in the folder, sorted ordinally.
        /// </summary>
        /// <param name="includeSkipped">Also list files starting with <c>#</c>.</param>
        public static List<string> ListFiles(string folder, bool includeSkipped)
        {
            return Directory.GetFiles(folder)
                .Select(path => Path.GetFileName(path))
                .Where(name => IsSheetFile(name) && (includeSkipped || !IsSkipped(name)))
                .OrderBy(name => name, StringComparer.Ordinal)
                .ToList();
        }

        /// <summary>Whether the file is skipped by default: its name starts with <c>#</c>.</summary>
        public static bool IsSkipped(string fileName) => fileName.StartsWith("#", StringComparison.Ordinal);

        /// <summary>Sheet name of a file: the file name without extension.</summary>
        public static string SheetName(string fileName) => Path.GetFileNameWithoutExtension(fileName);

        /// <summary>
        /// Reads a file as a sheet: tab-separated for <c>.tsv</c>, comma-separated otherwise.
        /// Returns <c>null</c> and adds errors prefixed with the file name when the file cannot be read.
        /// </summary>
        public static SheetImportSheet Read(string path, List<string> errors)
        {
            var fileName = Path.GetFileName(path);
            string text;
            try
            {
                text = File.ReadAllText(path, Encoding.UTF8);
            }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException)
            {
                errors.Add($"{fileName}: {e.Message}");
                return null;
            }

            var delimiter = string.Equals(Path.GetExtension(path), ".tsv", StringComparison.OrdinalIgnoreCase)
                ? CsvDelimiter.Tab
                : CsvDelimiter.Comma;
            var result = CsvReader.Read(text, delimiter);
            if (!result.IsValid)
            {
                foreach (var error in result.Errors)
                    errors.Add($"{fileName}: {error}");
                return null;
            }

            return ToSheet(SheetName(fileName), result.Rows);
        }

        /// <summary>Raw sheet from rows read by <see cref="CsvReader"/> or a sheet source.</summary>
        public static SheetImportSheet ToSheet(string name, IReadOnlyList<CsvRow> rows)
        {
            var sheet = new SheetImportSheet { name = name };
            foreach (var row in rows)
                sheet.rows.Add(new SheetImportRow { cells = row.Cells.ToList() });
            return sheet;
        }

        private static bool IsSheetFile(string fileName)
        {
            var extension = Path.GetExtension(fileName);
            return string.Equals(extension, ".csv", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(extension, ".tsv", StringComparison.OrdinalIgnoreCase);
        }
    }
}
