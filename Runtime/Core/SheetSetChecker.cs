using System;
using System.Collections.Generic;

namespace Kentavr.SheetsToSO.Core
{
    /// <summary>
    /// Checks a set of sheets before an import and compares it with the previous snapshot.
    /// See Documentation~/data-format.md, "Import".
    /// </summary>
    public static class SheetSetChecker
    {
        /// <summary>Shown instead of a diff when the previous snapshot has errors.</summary>
        public const string PreviousUnreadableText = "Previous snapshot cannot be read, diff is not available";

        /// <summary>Error of an empty sheet set.</summary>
        public const string NoSheetsError = "No sheets to import";

        private static readonly EnumMembersResult NoEnums = new EnumMembersResult(
            Array.Empty<string>(), new Dictionary<string, IReadOnlyList<string>>(), Array.Empty<SheetError>());

        /// <summary>
        /// Parses and checks the sheets: names (empty, repeated ignoring case), headers and values,
        /// <c>id</c> as the first <c>string</c> column, <c>ref&lt;&gt;</c> and <c>enum&lt;&gt;</c> values across sheets,
        /// type name conflicts of the generated code and names of the generated <c>ref&lt;&gt;</c> properties.
        /// Errors are collected, never thrown. Cross-sheet, type name and property name checks run even with
        /// parse errors, so all errors are seen at once; only sheet name errors skip them.
        /// </summary>
        /// <param name="sheets">Sheet names and raw cells; <c>Rows[0]</c> is the header. A <c>null</c> row counts as empty.</param>
        public static SheetSetCheckResult Check(IReadOnlyList<(string Name, IReadOnlyList<IReadOnlyList<string>> Rows)> sheets)
        {
            if (sheets == null)
                throw new ArgumentNullException(nameof(sheets));

            var input = new List<(string, IReadOnlyList<IReadOnlyList<string>>, Type)>(sheets.Count);
            foreach (var sheet in sheets)
                input.Add((sheet.Name, sheet.Rows, null));
            return Check(input);
        }

        /// <summary>
        /// Same as <see cref="Check(IReadOnlyList{ValueTuple{string, IReadOnlyList{IReadOnlyList{string}}}})"/>,
        /// with an optional row class per sheet. A sheet with a <c>RowType</c> is in schema-from-code mode:
        /// it is parsed by the fields of the class (see <see cref="SheetParser.Parse(string, IReadOnlyList{IReadOnlyList{string}}, Type)"/>),
        /// no code is generated for it, so its row and table class names take no part in the type name check,
        /// and its enums are the C# enum types of the fields: values are checked against their members and
        /// they are not in <see cref="SheetSetCheckResult.Enums"/>. The class must be <c>[Serializable]</c> and
        /// have a public parameterless constructor. Two different C# enums with the same name, or an
        /// <c>enum&lt;X&gt;</c> column of a generated sheet where <c>X</c> is a C# enum of a code sheet, are errors.
        /// </summary>
        /// <param name="sheets">
        /// Sheet names, raw cells (<c>Rows[0]</c> is the header; a <c>null</c> row counts as empty) and
        /// the row class, <c>null</c> for code generation mode.
        /// </param>
        public static SheetSetCheckResult Check(
            IReadOnlyList<(string Name, IReadOnlyList<IReadOnlyList<string>> Rows, Type RowType)> sheets)
        {
            if (sheets == null)
                throw new ArgumentNullException(nameof(sheets));

            var errors = new List<string>();
            var parsed = new List<SheetParseResult>();
            var rowTypes = new Dictionary<string, Type>(StringComparer.Ordinal);
            var nameErrors = false;

            if (sheets.Count == 0)
            {
                errors.Add(NoSheetsError);
                nameErrors = true;
            }

            // Ignore case: each sheet becomes files, and file names are case-insensitive on Windows and macOS.
            var names = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var source in sheets)
            {
                if (string.IsNullOrEmpty(source.Name))
                {
                    errors.Add("Sheet without a name");
                    nameErrors = true;
                    continue;
                }
                if (names.TryGetValue(source.Name, out var first))
                {
                    errors.Add(first == source.Name
                        ? $"Duplicate sheet name '{source.Name}'"
                        : $"Duplicate sheet name '{source.Name}': same as '{first}' ignoring case");
                    nameErrors = true;
                    continue;
                }
                names.Add(source.Name, source.Name);

                SheetParseResult sheet;
                if (source.RowType != null)
                {
                    CheckRowType(source.Name, source.RowType, errors);
                    rowTypes.Add(source.Name, source.RowType);
                    sheet = SheetParser.Parse(source.Name, Rows(source.Rows), source.RowType);
                }
                else
                {
                    sheet = SheetParser.Parse(source.Name, Rows(source.Rows));
                }

                foreach (var error in sheet.Errors)
                    errors.Add(error.ToString());

                if (sheet.Columns.Count > 0)
                {
                    var id = sheet.Columns[0];
                    if (id.Type.Kind != ColumnKind.String || id.Type.IsArray)
                        errors.Add($"{sheet.SheetName}!{id.Address}: column 'id' must be of type 'string', found '{id.Type}'");
                }

                parsed.Add(sheet);
            }

            // With a missing or repeated sheet name it is unclear which sheet a ref points to. Parse errors do not stop
            // the checks below: see the rules in Documentation~/data-format.md, "Проверка ссылок и enum".
            if (nameErrors)
                return new SheetSetCheckResult(parsed.AsReadOnly(), NoEnums, errors.AsReadOnly(), rowTypes);

            var generated = new List<SheetParseResult>();
            foreach (var sheet in parsed)
            {
                if (!rowTypes.ContainsKey(sheet.SheetName))
                    generated.Add(sheet);
            }

            var codeEnums = CollectCodeEnums(parsed, rowTypes, errors);
            CheckGeneratedEnumNames(generated, codeEnums, errors);

            var knownEnums = new Dictionary<string, IReadOnlyCollection<string>>(StringComparer.Ordinal);
            foreach (var pair in codeEnums)
                knownEnums.Add(pair.Key, Enum.GetNames(pair.Value.Type));

            // Invalid enum values are reported by both the validator and CollectEnumMembers: take them from the validator only.
            var cross = CrossSheetValidator.Validate(parsed, knownEnums);
            foreach (var error in cross.Errors)
                errors.Add(error.ToString());

            var enums = CodeGenerator.CollectEnumMembers(generated);

            // Row classes, table classes and enums share one namespace and one code folder (one file per type,
            // so names are compared ignoring case). Sheets in schema-from-code mode generate no code.
            var typeOwners = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var sheet in generated)
            {
                AddTypeName(typeOwners, sheet.SheetName, $"row class of sheet '{sheet.SheetName}'", errors);
                AddTypeName(typeOwners, sheet.SheetName + CodeGenerator.TableSuffix,
                    $"table class of sheet '{sheet.SheetName}'", errors);
            }
            foreach (var enumName in enums.EnumNames)
                AddTypeName(typeOwners, enumName, $"enum '{enumName}'", errors);

            // Generated ref<X> properties must not clash with members of the row class
            // (sheets in schema-from-code mode get no generated properties).
            foreach (var sheet in generated)
            {
                foreach (var column in sheet.Columns)
                {
                    if (column.Type.Kind != ColumnKind.Ref)
                        continue;
                    var conflict = CodeGenerator.FindRefPropertyConflict(column, sheet.Columns, sheet.SheetName);
                    if (conflict != null)
                        errors.Add($"{sheet.SheetName}!{column.Address}: Column '{column.Name}': {conflict}");
                }
            }

            return new SheetSetCheckResult(parsed.AsReadOnly(), errors.Count == 0 ? enums : NoEnums, errors.AsReadOnly(),
                rowTypes);
        }

        // Unity stores the rows of a table only for a [Serializable] class, and the mapper needs a constructor.
        private static void CheckRowType(string sheetName, Type rowType, List<string> errors)
        {
            var name = rowType.FullName ?? rowType.Name;
            if (rowType.IsAbstract || rowType.IsInterface || rowType.GetConstructor(Type.EmptyTypes) == null)
                errors.Add($"Sheet '{sheetName}': row class '{name}' must be a non-abstract class with a public " +
                    "parameterless constructor");
            if (!rowType.IsSerializable)
                errors.Add($"Sheet '{sheetName}': row class '{name}' must be marked [Serializable], " +
                    "otherwise Unity does not save the rows");
        }

        // C# enum types of the enum columns of sheets in schema-from-code mode, by short name (ordinal).
        private static Dictionary<string, (Type Type, string Sheet)> CollectCodeEnums(List<SheetParseResult> parsed,
            Dictionary<string, Type> rowTypes, List<string> errors)
        {
            var result = new Dictionary<string, (Type Type, string Sheet)>(StringComparer.Ordinal);
            foreach (var sheet in parsed)
            {
                if (!rowTypes.TryGetValue(sheet.SheetName, out var rowType))
                    continue;

                var fields = CodeSchema.GetSerializedFieldsByName(rowType);
                foreach (var column in sheet.Columns)
                {
                    if (column.Type.Kind != ColumnKind.Enum || !fields.TryGetValue(column.Name, out var field))
                        continue;

                    var enumType = field.FieldType.IsEnum
                        ? field.FieldType
                        : CodeSchema.GetCollectionElementType(field.FieldType);
                    if (enumType == null || !enumType.IsEnum)
                        continue;

                    if (!result.TryGetValue(enumType.Name, out var first))
                    {
                        result.Add(enumType.Name, (enumType, sheet.SheetName));
                        continue;
                    }
                    if (first.Type != enumType)
                        errors.Add($"{sheet.SheetName}!{column.Address}: enum name '{enumType.Name}' is used by two C# enums: " +
                            $"'{TypeName(first.Type)}' (sheet '{first.Sheet}') and '{TypeName(enumType)}' (sheet '{sheet.SheetName}')");
                }
            }
            return result;
        }

        // A generated enum<X> next to a C# enum X would be a second type X, or the generated code would not find it.
        private static void CheckGeneratedEnumNames(List<SheetParseResult> generated,
            Dictionary<string, (Type Type, string Sheet)> codeEnums, List<string> errors)
        {
            if (codeEnums.Count == 0)
                return;

            foreach (var sheet in generated)
            {
                foreach (var column in sheet.Columns)
                {
                    if (column.Type.Kind != ColumnKind.Enum || !codeEnums.TryGetValue(column.Type.TypeName, out var code))
                        continue;
                    errors.Add($"{sheet.SheetName}!{column.Address}: column '{column.Name}' uses {column.Type}, but " +
                        $"'{column.Type.TypeName}' is the C# enum '{TypeName(code.Type)}' of sheet '{code.Sheet}' " +
                        "(schema from code); rename the enum or pick a table type for this sheet too");
                }
            }
        }

        private static string TypeName(Type type) => type.FullName ?? type.Name;

        /// <summary>
        /// Compares the previous snapshot with checked sheets. Only previous sheets whose names (ordinal) are in
        /// <paramref name="current"/> take part: a sheet left out of this import is not shown as removed.
        /// A sheet missing from the snapshot (or an empty / <c>null</c> snapshot) is shown as added.
        /// When a compared previous sheet does not parse without errors, no diff is built and
        /// <see cref="SheetSetDiffResult.Text"/> says why; this is not an import error.
        /// Cross-sheet checks are not repeated for the snapshot: its sheets may come from different imports.
        /// </summary>
        /// <param name="previous">Raw sheets of the previous imports, as for <see cref="Check"/>.</param>
        /// <param name="current">Result of <see cref="Check"/> without errors.</param>
        /// <exception cref="ArgumentException"><paramref name="current"/> has errors.</exception>
        public static SheetSetDiffResult Diff(IReadOnlyList<(string Name, IReadOnlyList<IReadOnlyList<string>> Rows)> previous,
            SheetSetCheckResult current)
        {
            if (current == null)
                throw new ArgumentNullException(nameof(current));
            if (!current.IsValid)
                throw new ArgumentException("Sheets have errors; diff needs checked data.", nameof(current));

            var currentNames = new HashSet<string>(StringComparer.Ordinal);
            foreach (var sheet in current.Sheets)
                currentNames.Add(sheet.SheetName);

            var before = new List<SheetParseResult>();
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (var source in previous ?? Array.Empty<(string, IReadOnlyList<IReadOnlyList<string>>)>())
            {
                if (string.IsNullOrEmpty(source.Name) || !currentNames.Contains(source.Name))
                    continue;
                if (!seen.Add(source.Name))
                    return Unreadable($"Duplicate sheet name '{source.Name}'");

                // A sheet in schema-from-code mode has its types in the class, not in the header.
                var sheet = current.RowTypes.TryGetValue(source.Name, out var rowType)
                    ? SheetParser.Parse(source.Name, Rows(source.Rows), rowType)
                    : SheetParser.Parse(source.Name, Rows(source.Rows));
                if (!sheet.IsValid)
                    return Unreadable(sheet.Errors[0].ToString());
                before.Add(sheet);
            }

            var diff = SnapshotDiffer.Compare(before, current.Sheets);
            return new SheetSetDiffResult(diff, DiffReport.Format(diff));
        }

        private static SheetSetDiffResult Unreadable(string reason) =>
            new SheetSetDiffResult(null, PreviousUnreadableText + ": " + reason);

        private static IReadOnlyList<IReadOnlyList<string>> Rows(IReadOnlyList<IReadOnlyList<string>> rows)
        {
            if (rows == null)
                return Array.Empty<IReadOnlyList<string>>();

            var result = new IReadOnlyList<string>[rows.Count];
            for (var i = 0; i < rows.Count; i++)
                result[i] = rows[i] ?? Array.Empty<string>();
            return result;
        }

        private static void AddTypeName(Dictionary<string, string> owners, string name, string owner, List<string> errors)
        {
            if (owners.TryGetValue(name, out var other))
                errors.Add($"Type name '{name}' is used by both the {other} and the {owner}");
            else
                owners.Add(name, owner);
        }
    }
}
