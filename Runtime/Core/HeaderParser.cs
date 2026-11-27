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

        // Reserved C# keywords (77). Contextual keywords such as var, value, record are allowed.
        private static readonly HashSet<string> CSharpKeywords = new HashSet<string>(StringComparer.Ordinal)
        {
            "abstract", "as", "base", "bool", "break", "byte", "case", "catch", "char", "checked",
            "class", "const", "continue", "decimal", "default", "delegate", "do", "double", "else", "enum",
            "event", "explicit", "extern", "false", "finally", "fixed", "float", "for", "foreach", "goto",
            "if", "implicit", "in", "int", "interface", "internal", "is", "lock", "long", "namespace",
            "new", "null", "object", "operator", "out", "override", "params", "private", "protected", "public",
            "readonly", "ref", "return", "sbyte", "sealed", "short", "sizeof", "stackalloc", "static", "string",
            "struct", "switch", "this", "throw", "true", "try", "typeof", "uint", "ulong", "unchecked",
            "unsafe", "ushort", "using", "virtual", "void", "volatile", "while",
        };

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
                    columns.Add(column);
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

            if (!IsIdentifier(name))
                return new HeaderError(HeaderErrorCode.InvalidName, address,
                    $"Invalid column name '{name}': use Latin letters, digits and '_', not starting with a digit");

            if (CSharpKeywords.Contains(name))
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
            seenNames.Add(name, address);

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
            if (!IsIdentifier(inner))
                return false;

            argument = inner;
            return true;
        }

        private static bool IsIdentifier(string value)
        {
            if (value.Length == 0)
                return false;

            for (var i = 0; i < value.Length; i++)
            {
                var c = value[i];
                var isLetter = (c >= 'A' && c <= 'Z') || (c >= 'a' && c <= 'z') || c == '_';
                var isDigit = c >= '0' && c <= '9';
                if (!(isLetter || (i > 0 && isDigit)))
                    return false;
            }
            return true;
        }

        private static string Normalize(string cell)
        {
            return cell == null ? string.Empty : cell.Trim();
        }
    }
}
