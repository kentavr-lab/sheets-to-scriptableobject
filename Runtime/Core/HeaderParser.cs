using System;
using System.Collections.Generic;

namespace Kentavr.SheetsToSO.Core
{
    /// <summary>
    /// Parses the header row (row 1) of a sheet into column definitions.
    /// See Documentation~/data-format.md.
    /// </summary>
    public static class HeaderParser
    {
        private const int HeaderRow = 1;
        private const string IdColumnName = "id";

        /// <summary>
        /// Parses header cells. Data errors are collected into the result, never thrown.
        /// </summary>
        public static HeaderParseResult Parse(IReadOnlyList<string> cells)
        {
            if (cells == null)
                throw new ArgumentNullException(nameof(cells));

            var columns = new List<ColumnDefinition>();
            var errors = new List<HeaderError>();
            var seenNames = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            var count = cells.Count;
            while (count > 0 && Normalize(cells[count - 1]).Length == 0)
                count--;

            var firstColumnSeen = false;

            for (var index = 0; index < count; index++)
            {
                var text = Normalize(cells[index]);
                if (text.StartsWith("#", StringComparison.Ordinal))
                    continue;

                var address = CellAddress.ToA1(index, HeaderRow);
                var isFirstColumn = !firstColumnSeen;
                firstColumnSeen = true;

                var error = ParseCell(text, index, address, seenNames, out var column);
                if (error == null && isFirstColumn && column.Name != IdColumnName)
                {
                    error = new HeaderError(HeaderErrorCode.MissingIdColumn, address,
                        $"First column must be '{IdColumnName}', found '{text}'");
                }

                if (error != null)
                    errors.Add(error);
                else
                {
                    seenNames.Add(column.Name, column.Address);
                    columns.Add(column);
                }
            }

            if (!firstColumnSeen)
            {
                errors.Add(new HeaderError(HeaderErrorCode.MissingIdColumn, CellAddress.ToA1(0, HeaderRow),
                    $"Header has no columns; first column must be '{IdColumnName}'"));
            }

            return new HeaderParseResult(columns.AsReadOnly(), errors.AsReadOnly());
        }

        private static HeaderError ParseCell(string text, int index, string address,
            Dictionary<string, string> seenNames, out ColumnDefinition column)
        {
            column = null;

            if (text.Length == 0)
                return new HeaderError(HeaderErrorCode.EmptyName, address, "Empty header cell");

            var separator = text.IndexOf(':');
            var isTypeExplicit = separator >= 0;
            var name = isTypeExplicit ? text.Substring(0, separator).Trim() : text;
            var typeText = isTypeExplicit ? text.Substring(separator + 1).Trim() : null;

            if (name.Length == 0)
                return new HeaderError(HeaderErrorCode.EmptyName, address, $"Empty column name in '{text}'");

            if (!CSharpIdentifier.IsIdentifier(name))
                return new HeaderError(HeaderErrorCode.InvalidName, address,
                    $"Invalid column name '{name}': use Latin letters, digits and '_', not starting with a digit");

            if (CSharpIdentifier.IsKeyword(name))
                return new HeaderError(HeaderErrorCode.CSharpKeyword, address,
                    $"Column name '{name}' is a reserved C# keyword");

            var type = ColumnType.DefaultString;
            var isRequired = false;
            if (isTypeExplicit && !TryParseType(typeText, out type, out isRequired))
                return new HeaderError(HeaderErrorCode.UnknownType, address,
                    $"Unknown type '{typeText}' in column '{name}'");

            if (seenNames.TryGetValue(name, out var firstAddress))
                return new HeaderError(HeaderErrorCode.DuplicateName, address,
                    $"Duplicate column name '{name}', already defined in {firstAddress}");

            column = new ColumnDefinition(name, type, isRequired, isTypeExplicit, index, address);
            return null;
        }

        private static bool TryParseType(string text, out ColumnType type, out bool isRequired)
        {
            type = null;
            isRequired = false;

            if (text.EndsWith("!", StringComparison.Ordinal))
            {
                isRequired = true;
                text = text.Substring(0, text.Length - 1);
            }

            var isArray = false;
            if (text.EndsWith("[]", StringComparison.Ordinal))
            {
                isArray = true;
                text = text.Substring(0, text.Length - 2);
            }

            switch (text)
            {
                case "string": type = new ColumnType(ColumnKind.String, null, isArray); return true;
                case "int": type = new ColumnType(ColumnKind.Int, null, isArray); return true;
                case "float": type = new ColumnType(ColumnKind.Float, null, isArray); return true;
                case "bool": type = new ColumnType(ColumnKind.Bool, null, isArray); return true;
            }

            if (TryParseGeneric(text, "enum", out var enumName))
            {
                type = new ColumnType(ColumnKind.Enum, enumName, isArray);
                return true;
            }

            if (TryParseGeneric(text, "ref", out var sheetName))
            {
                type = new ColumnType(ColumnKind.Ref, sheetName, isArray);
                return true;
            }

            return false;
        }

        private static bool TryParseGeneric(string text, string keyword, out string argument)
        {
            argument = null;
            var prefix = keyword + "<";
            if (!text.StartsWith(prefix, StringComparison.Ordinal) || !text.EndsWith(">", StringComparison.Ordinal))
                return false;

            var inner = text.Substring(prefix.Length, text.Length - prefix.Length - 1);
            if (!CSharpIdentifier.IsIdentifier(inner))
                return false;

            argument = inner;
            return true;
        }

        private static string Normalize(string cell)
        {
            return cell == null ? string.Empty : cell.Trim();
        }
    }
}
