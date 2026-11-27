using System;
using System.Reflection;

namespace Kentavr.SheetsToSO.Core
{
    /// <summary>
    /// Copies parsed values of a sheet row into a new instance of a row class, field by field.
    /// </summary>
    public static class RowMapper
    {
        /// <summary>
        /// Creates an instance of <paramref name="rowType"/> and assigns each value of <see cref="SheetRow.Values"/>
        /// to the public instance field with the same name.
        /// <c>string</c>, <c>int</c>, <c>float</c>, <c>bool</c> and their arrays are assigned as is;
        /// an enum field gets <see cref="Enum.Parse(Type, string)"/> of the text, an empty (<c>null</c>) enum keeps the field default;
        /// an enum array field gets the parsed elements of a <c>string[]</c>.
        /// </summary>
        /// <param name="rowType">Row class with a public parameterless constructor.</param>
        /// <param name="row">Parsed row.</param>
        /// <param name="sheetName">Sheet name used in error messages; may be <c>null</c>.</param>
        /// <exception cref="ArgumentException">The row class has no matching field or the field type does not fit the value.</exception>
        public static object Map(Type rowType, SheetRow row, string sheetName = null)
        {
            if (rowType == null)
                throw new ArgumentNullException(nameof(rowType));
            if (row == null)
                throw new ArgumentNullException(nameof(row));

            var instance = Activator.CreateInstance(rowType);
            foreach (var pair in row.Values)
            {
                var where = (sheetName != null ? $"Sheet '{sheetName}', " : string.Empty) +
                    $"row {row.RowNumber}, column '{pair.Key}'";

                var field = rowType.GetField(pair.Key, BindingFlags.Public | BindingFlags.Instance);
                if (field == null)
                    throw new ArgumentException($"{where}: type '{rowType.FullName}' has no public field '{pair.Key}'",
                        nameof(rowType));

                if (!TryConvert(field.FieldType, pair.Value, out var converted, out var skip))
                    throw new ArgumentException(
                        $"{where}: value of type '{pair.Value?.GetType().Name ?? "null"}' does not fit field " +
                        $"'{rowType.FullName}.{field.Name}' of type '{field.FieldType.Name}'",
                        nameof(rowType));

                if (!skip)
                    field.SetValue(instance, converted);
            }
            return instance;
        }

        private static bool TryConvert(Type fieldType, object value, out object converted, out bool skip)
        {
            converted = null;
            skip = false;

            if (fieldType.IsEnum)
            {
                if (value == null)
                {
                    skip = true;
                    return true;
                }
                return value is string text && TryParseEnum(fieldType, text, out converted);
            }

            if (fieldType.IsArray && fieldType.GetElementType().IsEnum)
            {
                if (value == null)
                {
                    converted = Array.CreateInstance(fieldType.GetElementType(), 0);
                    return true;
                }
                if (!(value is string[] elements))
                    return false;

                var elementType = fieldType.GetElementType();
                var array = Array.CreateInstance(elementType, elements.Length);
                for (var i = 0; i < elements.Length; i++)
                {
                    if (elements[i] == null)
                        continue;
                    if (!TryParseEnum(elementType, elements[i], out var element))
                        return false;
                    array.SetValue(element, i);
                }
                converted = array;
                return true;
            }

            if (value == null)
            {
                // Empty ref cells are null; value-type fields cannot hold null.
                if (fieldType.IsValueType)
                    return false;
                return true;
            }

            if (!fieldType.IsInstanceOfType(value))
                return false;
            converted = value;
            return true;
        }

        private static bool TryParseEnum(Type enumType, string text, out object value)
        {
            value = null;
            // Only declared member names; numeric text like "5" is not accepted.
            if (!Enum.IsDefined(enumType, text))
                return false;
            value = Enum.Parse(enumType, text);
            return true;
        }
    }
}
