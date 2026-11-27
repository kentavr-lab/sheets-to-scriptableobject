using System;
using System.Collections.Generic;
using System.Reflection;

namespace Kentavr.SheetsToSO.Core
{
    /// <summary>
    /// Schema-from-code mode: column types come from the fields of a hand-written row class.
    /// Fields are selected the way Unity serializes them. See Documentation~/data-format.md.
    /// </summary>
    public static class CodeSchema
    {
        private const string SerializeFieldAttributeName = "UnityEngine.SerializeField";

        private const BindingFlags DeclaredInstanceFields =
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;

        /// <summary>
        /// Matches header columns to fields of <paramref name="rowType"/> by exact name and
        /// takes column types from the fields. A type written in the header must agree with the field.
        /// </summary>
        public static CodeSchemaResult Resolve(Type rowType, IReadOnlyList<ColumnDefinition> headerColumns)
        {
            if (rowType == null)
                throw new ArgumentNullException(nameof(rowType));
            if (headerColumns == null)
                throw new ArgumentNullException(nameof(headerColumns));

            var fields = GetSerializedFields(rowType);
            var byName = new Dictionary<string, FieldInfo>(StringComparer.Ordinal);
            foreach (var field in fields)
            {
                // A field of a derived class hides a base field with the same name.
                if (!byName.ContainsKey(field.Name))
                    byName.Add(field.Name, field);
            }

            var columns = new List<ColumnDefinition>();
            var errors = new List<HeaderError>();

            foreach (var column in headerColumns)
            {
                if (column == null)
                    throw new ArgumentException("Header columns must not contain null.", nameof(headerColumns));

                if (!byName.TryGetValue(column.Name, out var field))
                {
                    errors.Add(new HeaderError(HeaderErrorCode.UnknownField, column.Address,
                        UnknownFieldMessage(rowType, column.Name, fields)));
                    continue;
                }

                if (!TryGetColumnType(field.FieldType, out var fieldType))
                {
                    errors.Add(new HeaderError(HeaderErrorCode.UnsupportedFieldType, column.Address,
                        $"Field '{FieldDisplayName(field)}' for column '{column.Name}' has unsupported type " +
                        $"'{CSharpTypeName(field.FieldType)}'"));
                    continue;
                }

                var type = fieldType;
                if (column.IsTypeExplicit)
                {
                    if (IsRefCompatible(column.Type, fieldType))
                    {
                        type = column.Type;
                    }
                    else if (!SameType(column.Type, fieldType))
                    {
                        errors.Add(new HeaderError(HeaderErrorCode.TypeMismatch, column.Address,
                            $"Column '{column.Name}' has type '{column.Type}' in header, " +
                            $"but field '{FieldDisplayName(field)}' is '{fieldType}'"));
                        continue;
                    }
                }

                columns.Add(new ColumnDefinition(column.Name, type, column.IsRequired, column.IsTypeExplicit,
                    column.ColumnIndex, column.Address));
            }

            return new CodeSchemaResult(columns.AsReadOnly(), errors.AsReadOnly());
        }

        // Instance fields Unity would serialize, most derived class first.
        private static List<FieldInfo> GetSerializedFields(Type rowType)
        {
            var result = new List<FieldInfo>();
            for (var type = rowType; type != null && type != typeof(object); type = type.BaseType)
            {
                foreach (var field in type.GetFields(DeclaredInstanceFields))
                {
                    if (field.IsInitOnly || field.IsLiteral || field.IsNotSerialized)
                        continue;
                    if (!field.IsPublic && !HasSerializeField(field))
                        continue;
                    result.Add(field);
                }
            }
            return result;
        }

        // Compared by name so the core assembly does not reference UnityEngine.
        private static bool HasSerializeField(FieldInfo field)
        {
            foreach (var attribute in field.GetCustomAttributesData())
            {
                if (attribute.AttributeType.FullName == SerializeFieldAttributeName)
                    return true;
            }
            return false;
        }

        private static bool TryGetColumnType(Type type, out ColumnType columnType)
        {
            if (TryGetScalarType(type, false, out columnType))
                return true;

            Type element = null;
            if (type.IsArray && type.GetArrayRank() == 1)
                element = type.GetElementType();
            else if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(List<>))
                element = type.GetGenericArguments()[0];

            return element != null && TryGetScalarType(element, true, out columnType);
        }

        private static bool TryGetScalarType(Type type, bool isArray, out ColumnType columnType)
        {
            columnType = null;
            if (type == typeof(string))
                columnType = new ColumnType(ColumnKind.String, null, isArray);
            else if (type == typeof(int))
                columnType = new ColumnType(ColumnKind.Int, null, isArray);
            else if (type == typeof(float))
                columnType = new ColumnType(ColumnKind.Float, null, isArray);
            else if (type == typeof(bool))
                columnType = new ColumnType(ColumnKind.Bool, null, isArray);
            else if (type.IsEnum)
                columnType = new ColumnType(ColumnKind.Enum, type.Name, isArray);
            return columnType != null;
        }

        private static bool SameType(ColumnType header, ColumnType field)
        {
            return header.Kind == field.Kind && header.IsArray == field.IsArray &&
                   string.Equals(header.TypeName, field.TypeName, StringComparison.Ordinal);
        }

        // ref<X> holds a row id, so a string field can store it.
        private static bool IsRefCompatible(ColumnType header, ColumnType field)
        {
            return header.Kind == ColumnKind.Ref && field.Kind == ColumnKind.String && header.IsArray == field.IsArray;
        }

        private static string UnknownFieldMessage(Type rowType, string columnName, List<FieldInfo> fields)
        {
            var message = $"Column '{columnName}' has no matching field in '{rowType.Name}'";
            foreach (var field in fields)
            {
                if (string.Equals(field.Name, columnName, StringComparison.OrdinalIgnoreCase))
                    return message + $"; did you mean '{field.Name}'?";
            }
            return message;
        }

        private static string FieldDisplayName(FieldInfo field)
        {
            return field.DeclaringType.Name + "." + field.Name;
        }

        private static string CSharpTypeName(Type type)
        {
            if (type.IsArray)
                return CSharpTypeName(type.GetElementType()) + "[" + new string(',', type.GetArrayRank() - 1) + "]";
            if (type.IsGenericType)
            {
                var name = type.Name;
                var tick = name.IndexOf('`');
                if (tick >= 0)
                    name = name.Substring(0, tick);
                var args = type.GetGenericArguments();
                var parts = new string[args.Length];
                for (var i = 0; i < args.Length; i++)
                    parts[i] = CSharpTypeName(args[i]);
                return name + "<" + string.Join(", ", parts) + ">";
            }
            return type.Name;
        }
    }
}
