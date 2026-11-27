using System;
using System.Collections.Generic;

namespace Kentavr.SheetsToSO.Core
{
    /// <summary>
    /// Parses a whole sheet: header, data rows and <c>id</c> checks.
    /// See Documentation~/data-format.md.
    /// </summary>
    public static class SheetParser
    {
        private const string IdColumnName = "id";
        private const char SkipPrefix = '#';

        /// <summary>
        /// Parses a sheet. Data errors are collected into the result, never thrown.
        /// </summary>
        /// <param name="sheetName">Sheet name used as the error address prefix (<c>"Units!B7"</c>).</param>
        /// <param name="rows">Sheet rows; <c>rows[i]</c> is row number <c>i + 1</c>, row 1 is the header.</param>
        public static SheetParseResult Parse(string sheetName, IReadOnlyList<IReadOnlyList<string>> rows)
        {
            if (string.IsNullOrEmpty(sheetName))
                throw new ArgumentException("Sheet name must not be null or empty.", nameof(sheetName));
            if (rows == null)
                throw new ArgumentNullException(nameof(rows));
            for (var i = 0; i < rows.Count; i++)
            {
                if (rows[i] == null)
                    throw new ArgumentException($"Row {i + 1} is null.", nameof(rows));
            }

            var errors = new List<SheetError>();
            var sheetRows = new List<SheetRow>();

            var header = HeaderParser.Parse(rows.Count > 0 ? rows[0] : Array.Empty<string>());
            foreach (var error in header.Errors)
                errors.Add(new SheetError(sheetName, error));

            var columns = header.Columns;
            ColumnDefinition idColumn = null;
            foreach (var column in columns)
            {
                if (column.Name == IdColumnName)
                {
                    idColumn = column;
                    break;
                }
            }

            // Address of the first occurrence of each id.
            var seenIds = new Dictionary<string, string>(StringComparer.Ordinal);

            for (var i = 1; i < rows.Count; i++)
            {
                var cells = rows[i];
                var rowNumber = i + 1;

                if (IsEmptyRow(columns, cells))
                    continue;

                var idText = idColumn != null ? CellText(cells, idColumn.ColumnIndex) : null;
                if (idText != null && idText.Trim().Length > 0 && idText.Trim()[0] == SkipPrefix)
                    continue;

                var row = CellValueParser.ParseRow(columns, cells, rowNumber);
                var emptyId = false;

                if (idColumn != null)
                {
                    var idAddress = CellAddress.ToA1(idColumn.ColumnIndex, rowNumber);
                    var idError = CheckId(idText, idAddress, rowNumber, sheetName, seenIds);
                    if (idError != null)
                    {
                        errors.Add(new SheetError(sheetName, idError));
                        emptyId = idError.Code == CellErrorCode.EmptyId;
                    }
                }

                foreach (var error in row.Errors)
                {
                    // Empty id is already reported; skip the duplicate RequiredEmpty for "id:string!".
                    if (emptyId && error.Code == CellErrorCode.RequiredEmpty &&
                        error.Address == CellAddress.ToA1(idColumn.ColumnIndex, rowNumber))
                        continue;
                    errors.Add(new SheetError(sheetName, error));
                }

                sheetRows.Add(new SheetRow(rowNumber, idText ?? string.Empty, row.Values));
            }

            return new SheetParseResult(sheetName, columns, sheetRows.AsReadOnly(), errors.AsReadOnly());
        }

        private static CellError CheckId(string idText, string address, int rowNumber, string sheetName,
            Dictionary<string, string> seenIds)
        {
            if (string.IsNullOrWhiteSpace(idText))
                return new CellError(CellErrorCode.EmptyId, address, $"Row {rowNumber} has data but empty 'id'");

            if (!IsValidId(idText))
                return new CellError(CellErrorCode.InvalidId, address,
                    $"Invalid id '{idText}': use Latin letters, digits and '_'");

            if (seenIds.TryGetValue(idText, out var firstAddress))
                return new CellError(CellErrorCode.DuplicateId, address,
                    $"Duplicate id '{idText}', already defined in {sheetName}!{firstAddress}");

            seenIds.Add(idText, address);
            return null;
        }

        private static bool IsValidId(string text)
        {
            foreach (var c in text)
            {
                var ok = (c >= 'A' && c <= 'Z') || (c >= 'a' && c <= 'z') || (c >= '0' && c <= '9') || c == '_';
                if (!ok)
                    return false;
            }
            return true;
        }

        // A row is empty when every imported cell is empty or whitespace.
        private static bool IsEmptyRow(IReadOnlyList<ColumnDefinition> columns, IReadOnlyList<string> cells)
        {
            foreach (var column in columns)
            {
                if (!string.IsNullOrWhiteSpace(CellText(cells, column.ColumnIndex)))
                    return false;
            }
            return true;
        }

        private static string CellText(IReadOnlyList<string> cells, int index)
        {
            return index >= 0 && index < cells.Count ? cells[index] ?? string.Empty : string.Empty;
        }
    }
}
