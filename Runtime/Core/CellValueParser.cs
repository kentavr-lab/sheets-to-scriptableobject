using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace Kentavr.SheetsToSO.Core
{
    /// <summary>
    /// Converts data cell text into values according to the column type.
    /// See Documentation~/data-format.md.
    /// </summary>
    public static class CellValueParser
    {
        private const char ArraySeparator = '|';
        private const char Escape = '\\';

        /// <summary>
        /// Parses a single cell. Data errors are collected into the result, never thrown.
        /// </summary>
        /// <param name="column">Column definition from <see cref="HeaderParser"/>.</param>
        /// <param name="text">Cell text; <c>null</c> is treated as an empty cell.</param>
        /// <param name="rowNumber">One-based row number used in error addresses.</param>
        public static CellParseResult Parse(ColumnDefinition column, string text, int rowNumber)
        {
            if (column == null)
                throw new ArgumentNullException(nameof(column));

            var address = CellAddress.ToA1(column.ColumnIndex, rowNumber);
            var errors = new List<CellError>();
            var value = ParseValue(column, text, address, errors);
            if (errors.Count > 0)
                value = DefaultValue(column.Type);

            return new CellParseResult(value, errors.AsReadOnly());
        }

        /// <summary>
        /// Parses a data row. A cell is taken by <see cref="ColumnDefinition.ColumnIndex"/>;
        /// missing cells (row shorter than the header) are empty. All errors are collected.
        /// </summary>
        public static RowParseResult ParseRow(IReadOnlyList<ColumnDefinition> columns, IReadOnlyList<string> cells, int rowNumber)
        {
            if (columns == null)
                throw new ArgumentNullException(nameof(columns));
            if (cells == null)
                throw new ArgumentNullException(nameof(cells));

            var values = new Dictionary<string, object>(StringComparer.Ordinal);
            var errors = new List<CellError>();

            foreach (var column in columns)
            {
                if (column == null)
                    throw new ArgumentException("Columns must not contain null.", nameof(columns));

                var index = column.ColumnIndex;
                var text = index >= 0 && index < cells.Count ? cells[index] : null;
                var result = Parse(column, text, rowNumber);
                values[column.Name] = result.Value;
                errors.AddRange(result.Errors);
            }

            return new RowParseResult(rowNumber, values, errors.AsReadOnly());
        }

        private static object ParseValue(ColumnDefinition column, string text, string address, List<CellError> errors)
        {
            var type = column.Type;

            if (IsEmpty(text))
            {
                if (column.IsRequired)
                    errors.Add(new CellError(CellErrorCode.RequiredEmpty, address,
                        $"Column '{column.Name}' is required, but the cell is empty"));
                return DefaultValue(type);
            }

            if (!type.IsArray)
                return ParseScalar(type.Kind, text, column.Name, address, null, errors);

            var elements = SplitArray(text);
            switch (type.Kind)
            {
                case ColumnKind.String:
                    return elements.ToArray();
                case ColumnKind.Int:
                    return ParseElements<int>(type.Kind, elements, column.Name, address, errors);
                case ColumnKind.Float:
                    return ParseElements<float>(type.Kind, elements, column.Name, address, errors);
                case ColumnKind.Bool:
                    return ParseElements<bool>(type.Kind, elements, column.Name, address, errors);
                default:
                    return ParseElements<string>(type.Kind, elements, column.Name, address, errors);
            }
        }

        private static T[] ParseElements<T>(ColumnKind kind, List<string> elements, string columnName, string address,
            List<CellError> errors)
        {
            var result = new T[elements.Count];
            for (var i = 0; i < elements.Count; i++)
            {
                var element = elements[i];
                var where = $"element {i + 1} '{element}'";
                if (IsEmpty(element))
                {
                    errors.Add(new CellError(CellErrorCode.EmptyArrayElement, address,
                        $"Column '{columnName}': {where} is empty"));
                    continue;
                }

                var value = ParseScalar(kind, element, columnName, address, where, errors);
                if (value is T typed)
                    result[i] = typed;
            }
            return result;
        }

        // Returns the parsed value, or null after adding an error. Text is not empty.
        private static object ParseScalar(ColumnKind kind, string text, string columnName, string address, string where,
            List<CellError> errors)
        {
            if (kind == ColumnKind.String)
                return text;

            var trimmed = text.Trim();
            var subject = $"Column '{columnName}': " + (where ?? $"'{text}'");

            switch (kind)
            {
                case ColumnKind.Int:
                    if (int.TryParse(trimmed, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var intValue))
                        return intValue;
                    errors.Add(new CellError(CellErrorCode.InvalidInt, address, subject + " is not a valid int"));
                    return null;

                case ColumnKind.Float:
                    if (TryParseFloat(trimmed, out var floatValue))
                        return floatValue;
                    var message = subject + " is not a valid float";
                    if (trimmed.IndexOf(',') >= 0)
                    {
                        var suggestion = trimmed.Replace(',', '.');
                        if (TryParseFloat(suggestion, out _))
                            message += $"; use '.' as decimal separator: '{suggestion}'";
                    }
                    errors.Add(new CellError(CellErrorCode.InvalidFloat, address, message));
                    return null;

                case ColumnKind.Bool:
                    if (string.Equals(trimmed, "true", StringComparison.OrdinalIgnoreCase))
                        return true;
                    if (string.Equals(trimmed, "false", StringComparison.OrdinalIgnoreCase))
                        return false;
                    errors.Add(new CellError(CellErrorCode.InvalidBool, address,
                        subject + " is not a valid bool: use 'true' or 'false'"));
                    return null;

                default:
                    // Enum names and ref ids are validated by separate checks.
                    return trimmed;
            }
        }

        private static bool TryParseFloat(string text, out float value)
        {
            return float.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out value) && float.IsFinite(value);
        }

        // Splits by '|'; "\|" yields a literal '|', any other '\' is kept as is.
        private static List<string> SplitArray(string text)
        {
            var elements = new List<string>();
            var current = new StringBuilder();
            for (var i = 0; i < text.Length; i++)
            {
                var c = text[i];
                if (c == Escape && i + 1 < text.Length && text[i + 1] == ArraySeparator)
                {
                    current.Append(ArraySeparator);
                    i++;
                }
                else if (c == ArraySeparator)
                {
                    elements.Add(current.ToString());
                    current.Clear();
                }
                else
                    current.Append(c);
            }
            elements.Add(current.ToString());
            return elements;
        }

        private static object DefaultValue(ColumnType type)
        {
            if (type.IsArray)
            {
                switch (type.Kind)
                {
                    case ColumnKind.Int: return Array.Empty<int>();
                    case ColumnKind.Float: return Array.Empty<float>();
                    case ColumnKind.Bool: return Array.Empty<bool>();
                    default: return Array.Empty<string>();
                }
            }

            switch (type.Kind)
            {
                case ColumnKind.String: return string.Empty;
                case ColumnKind.Int: return 0;
                case ColumnKind.Float: return 0f;
                case ColumnKind.Bool: return false;
                default: return null;
            }
        }

        private static bool IsEmpty(string text)
        {
            return string.IsNullOrWhiteSpace(text);
        }
    }
}
