using System;
using System.Collections;

namespace Kentavr.SheetsToSO.Core
{
    /// <summary>
    /// Copies parsed values of a sheet row into a new instance of a row class, field by field.
    /// </summary>
    public static class RowMapper
    {
        /// <summary>
        /// Creates an instance of <paramref name="rowType"/> and assigns each value of <see cref="SheetRow.Values"/>
        /// to the field with the same name. Fields are found as in schema-from-code mode (<see cref="CodeSchema"/>):
        /// public and <c>[SerializeField]</c> non-public instance fields, base classes included, a derived field wins.
        /// <c>string</c>, <c>int</c>, <c>float</c>, <c>bool</c> and their arrays are assigned as is;
        /// an enum field gets <see cref="Enum.Parse(Type, string)"/> of the text, an empty (<c>null</c>) enum keeps the field default;
        /// an enum array field gets the parsed elements of a <c>string[]</c>;
        /// a <c>List&lt;T&gt;</c> field gets a new list with the elements of the array value (enums parsed as above).
        /// A <c>null</c> value of an array or list field gives an empty collection.
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

            var fields = CodeSchema.GetSerializedFieldsByName(rowType);
            var instance = Activator.CreateInstance(rowType);
            foreach (var pair in row.Values)
            {
                var where = (sheetName != null ? $"Sheet '{sheetName}', " : string.Empty) +
                    $"row {row.RowNumber}, column '{pair.Key}'";

                if (!fields.TryGetValue(pair.Key, out var field))
                    throw new ArgumentException($"{where}: type '{rowType.FullName}' has no serialized field '{pair.Key}'",
                        nameof(rowType));

                if (!TryConvert(field.FieldType, pair.Value, out var converted, out var skip))
                    throw new ArgumentException(
                        $"{where}: value of type '{pair.Value?.GetType().Name ?? "null"}' does not fit field " +
                        $"'{field.DeclaringType?.FullName}.{field.Name}' of type '{field.FieldType.Name}'",
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

            var elementType = CodeSchema.GetCollectionElementType(fieldType);
            if (elementType != null)
                return TryConvertCollection(fieldType, elementType, value, out converted);

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

        private static bool TryConvertCollection(Type fieldType, Type elementType, object value, out object converted)
        {
            converted = null;

            Array elements;
            if (value == null)
            {
                elements = Array.CreateInstance(elementType, 0);
            }
            else if (elementType.IsEnum)
            {
                if (!(value is string[] texts))
                    return false;

                elements = Array.CreateInstance(elementType, texts.Length);
                for (var i = 0; i < texts.Length; i++)
                {
                    if (texts[i] == null)
                        continue;
                    if (!TryParseEnum(elementType, texts[i], out var element))
                        return false;
                    elements.SetValue(element, i);
                }
            }
            else
            {
                if (!(value is Array array) || array.GetType().GetElementType() != elementType)
                    return false;
                elements = array;
            }

            if (fieldType.IsArray)
            {
                converted = elements;
                return true;
            }

            var list = (IList)Activator.CreateInstance(fieldType, elements.Length);
            foreach (var element in elements)
                list.Add(element);
            converted = list;
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
