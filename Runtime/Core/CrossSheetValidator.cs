using System;
using System.Collections.Generic;
using System.Linq;

namespace Kentavr.SheetsToSO.Core
{
    /// <summary>
    /// Checks <c>ref&lt;X&gt;</c> and <c>enum&lt;E&gt;</c> values across a set of parsed sheets.
    /// </summary>
    public static class CrossSheetValidator
    {
        /// <summary>
        /// Validates references and enum values of all sheets and collects all errors at once.
        /// </summary>
        /// <param name="sheets">Parsed sheets; names must be unique. Parse errors of a sheet are not copied to the result.</param>
        /// <param name="knownEnums">
        /// Allowed values per enum name. Values of listed enums must match exactly (ordinal);
        /// values of other enums must be valid C# names and are returned in <see cref="CrossSheetResult.CollectedEnums"/>.
        /// </param>
        public static CrossSheetResult Validate(IReadOnlyList<SheetParseResult> sheets,
            IReadOnlyDictionary<string, IReadOnlyCollection<string>> knownEnums = null)
        {
            if (sheets == null)
                throw new ArgumentNullException(nameof(sheets));

            var idsBySheet = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);
            foreach (var sheet in sheets)
            {
                if (sheet == null)
                    throw new ArgumentException("Sheets must not contain null.", nameof(sheets));
                if (idsBySheet.ContainsKey(sheet.SheetName))
                    throw new ArgumentException($"Duplicate sheet name '{sheet.SheetName}'.", nameof(sheets));

                var ids = new HashSet<string>(StringComparer.Ordinal);
                foreach (var row in sheet.Rows)
                {
                    if (!string.IsNullOrEmpty(row.Id))
                        ids.Add(row.Id);
                }
                idsBySheet.Add(sheet.SheetName, ids);
            }

            var known = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);
            var knownLists = new Dictionary<string, string>(StringComparer.Ordinal);
            if (knownEnums != null)
            {
                foreach (var pair in knownEnums)
                {
                    if (pair.Value == null)
                        throw new ArgumentException($"Known enum '{pair.Key}' has no value list.", nameof(knownEnums));
                    known.Add(pair.Key, new HashSet<string>(pair.Value, StringComparer.Ordinal));
                    knownLists.Add(pair.Key, string.Join(", ", pair.Value));
                }
            }

            var errors = new List<SheetError>();
            var collected = new Dictionary<string, List<string>>(StringComparer.Ordinal);
            var collectedSeen = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);

            foreach (var sheet in sheets)
            {
                // Columns whose cells are checked: refs to loaded sheets and enums.
                var checkedColumns = new List<ColumnDefinition>();
                foreach (var column in sheet.Columns)
                {
                    var type = column.Type;
                    if (type.Kind == ColumnKind.Ref)
                    {
                        if (!idsBySheet.ContainsKey(type.TypeName))
                        {
                            errors.Add(new SheetError(sheet.SheetName, new CellError(CellErrorCode.UnknownRefSheet,
                                column.Address,
                                $"Column '{column.Name}': sheet '{type.TypeName}' from {type} is not among the loaded sheets")));
                            continue;
                        }
                        checkedColumns.Add(column);
                    }
                    else if (type.Kind == ColumnKind.Enum)
                    {
                        if (!known.ContainsKey(type.TypeName) && !collected.ContainsKey(type.TypeName))
                        {
                            collected.Add(type.TypeName, new List<string>());
                            collectedSeen.Add(type.TypeName, new HashSet<string>(StringComparer.Ordinal));
                        }
                        checkedColumns.Add(column);
                    }
                }

                if (checkedColumns.Count == 0)
                    continue;

                foreach (var row in sheet.Rows)
                {
                    foreach (var column in checkedColumns)
                    {
                        if (!row.Values.TryGetValue(column.Name, out var value) || value == null)
                            continue;

                        var address = CellAddress.ToA1(column.ColumnIndex, row.RowNumber);
                        if (value is string[] elements)
                        {
                            for (var i = 0; i < elements.Length; i++)
                            {
                                if (elements[i] != null)
                                    CheckValue(sheet.SheetName, column, elements[i], $"element {i + 1} '{elements[i]}'",
                                        address, idsBySheet, known, knownLists, collected, collectedSeen, errors);
                            }
                        }
                        else if (value is string text)
                        {
                            CheckValue(sheet.SheetName, column, text, $"'{text}'",
                                address, idsBySheet, known, knownLists, collected, collectedSeen, errors);
                        }
                    }
                }
            }

            var collectedResult = collected.ToDictionary(
                pair => pair.Key, pair => (IReadOnlyList<string>)pair.Value.AsReadOnly(), StringComparer.Ordinal);
            return new CrossSheetResult(errors.AsReadOnly(), collectedResult);
        }

        private static void CheckValue(string sheetName, ColumnDefinition column, string value, string subject,
            string address, Dictionary<string, HashSet<string>> idsBySheet, Dictionary<string, HashSet<string>> known,
            Dictionary<string, string> knownLists, Dictionary<string, List<string>> collected,
            Dictionary<string, HashSet<string>> collectedSeen, List<SheetError> errors)
        {
            var typeName = column.Type.TypeName;
            var prefix = $"Column '{column.Name}': {subject}";

            if (column.Type.Kind == ColumnKind.Ref)
            {
                if (value.Length == 0 || idsBySheet[typeName].Contains(value))
                    return;
                errors.Add(new SheetError(sheetName, new CellError(CellErrorCode.BrokenRef, address,
                    $"{prefix}: no row with this id in sheet '{typeName}'")));
                return;
            }

            if (value.Length == 0)
                return;

            if (known.TryGetValue(typeName, out var allowed))
            {
                if (!allowed.Contains(value))
                    errors.Add(new SheetError(sheetName, new CellError(CellErrorCode.UnknownEnumValue, address,
                        $"{prefix} is not a value of enum '{typeName}'; allowed: {knownLists[typeName]}")));
                return;
            }

            if (!CSharpIdentifier.IsValidName(value))
            {
                errors.Add(new SheetError(sheetName, new CellError(CellErrorCode.InvalidEnumValue, address,
                    $"{prefix} is not a valid enum '{typeName}' value: use Latin letters, digits and '_', " +
                    "not starting with a digit, and not a C# keyword")));
                return;
            }

            if (collectedSeen[typeName].Add(value))
                collected[typeName].Add(value);
        }
    }
}
