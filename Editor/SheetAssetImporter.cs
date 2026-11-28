using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Kentavr.SheetsToSO.Core;
using UnityEditor;
using UnityEditor.Callbacks;
using UnityEngine;

namespace Kentavr.SheetsToSO.Editor
{
    /// <summary>
    /// Creates or updates table assets from sheets and writes the generated code into the project.
    /// When the code changes, the import continues by itself after the scripts recompile.
    /// </summary>
    public static class SheetAssetImporter
    {
        /// <summary>Request saved before a script reload; read and deleted after it.</summary>
        internal const string PendingImportPath = "Library/Kentavr.SheetsToSO/pending-import.json";

        /// <summary>
        /// Raw sheets as last applied, one file per asset folder; the "before" side of the diff.
        /// Each import replaces its own sheets and keeps the others.
        /// </summary>
        internal const string SnapshotFolder = "Library/Kentavr.SheetsToSO/snapshots";

        private const string LogPrefix = "Sheets to SO: ";
        private const string AssetsRoot = "Assets";

        private static readonly UTF8Encoding Utf8NoBom = new UTF8Encoding(false);

        /// <summary>Raised with the report of an import that continued after a script reload.</summary>
        internal static event Action<SheetImportReport> ImportContinued;

        private sealed class PreparedImport
        {
            public SheetImportRequest Request;
            public CodeGenOptions Options;
            public SheetSetCheckResult Checked;

            /// <summary>Table types of the sheets in schema-from-code mode, by sheet name.</summary>
            public Dictionary<string, Type> CodeTableTypes = new Dictionary<string, Type>(StringComparer.Ordinal);

            public IReadOnlyList<SheetParseResult> Sheets => Checked.Sheets;
            public EnumMembersResult Enums => Checked.Enums;
        }

        [Serializable]
        private sealed class Snapshot
        {
            public List<SheetImportSheet> sheets = new List<SheetImportSheet>();
        }

        /// <summary>
        /// Checks the request as <see cref="Import"/> would, without writing anything, and builds the diff
        /// with the last import applied to the same asset folder on this machine.
        /// </summary>
        public static SheetImportCheck Check(SheetImportRequest request)
        {
            if (request == null)
                throw new ArgumentNullException(nameof(request));

            var check = new SheetImportCheck();
            var prepared = Prepare(request, check.Errors);
            if (check.HasErrors)
                return check;

            List<SheetImportSheet> previous;
            try
            {
                previous = ReadSnapshot(prepared.Request.assetFolder);
            }
            catch (Exception e)
            {
                check.DiffText = SheetSetChecker.PreviousUnreadableText +
                    $": {SnapshotPath(prepared.Request.assetFolder)}: {e.Message}";
                return check;
            }

            var diff = SheetSetChecker.Diff(ToCheckInput(previous), prepared.Checked);
            check.DiffText = diff.Text;
            check.HasRemovals = diff.HasRemovals;
            return check;
        }

        /// <summary>
        /// Imports the sheets. Nothing is written when any sheet has errors.
        /// If the generated code is unchanged and already compiled, assets are updated right away;
        /// otherwise the code is written and assets are updated after the scripts recompile
        /// (<see cref="SheetImportReport.WaitingForReload"/>), with a separate report in the Console.
        /// </summary>
        public static SheetImportReport Import(SheetImportRequest request)
        {
            if (request == null)
                throw new ArgumentNullException(nameof(request));

            // A newer import replaces any import still waiting for a reload.
            DeletePending();

            var report = new SheetImportReport();
            var prepared = Prepare(request, report.Errors);
            if (report.HasErrors)
            {
                Log(report);
                return report;
            }

            var files = GenerateCode(prepared, report);
            if (report.HasErrors)
            {
                Log(report);
                return report;
            }

            var changed = WriteFiles(prepared.Request.codeFolder, files, report);
            if (!changed)
            {
                if (TryFindTableTypes(prepared, report, false, out var tableTypes))
                    ApplyAssets(prepared, tableTypes, report);
                Log(report);
                return report;
            }

            SavePending(prepared.Request);
            report.WaitingForReload = true;
            AssetDatabase.Refresh();
            Log(report);
            return report;
        }

        [DidReloadScripts]
        private static void OnScriptsReloaded()
        {
            if (!File.Exists(PendingImportPath))
                return;

            SheetImportRequest request;
            try
            {
                request = JsonUtility.FromJson<SheetImportRequest>(File.ReadAllText(PendingImportPath, Utf8NoBom));
            }
            catch (Exception e)
            {
                Debug.LogError(LogPrefix + $"cannot read the pending import '{PendingImportPath}': {e.Message}");
                request = null;
            }
            finally
            {
                DeletePending();
            }

            if (request == null)
                return;

            // Let the editor finish the reload before touching assets.
            EditorApplication.delayCall += () => ContinueAfterReload(request);
        }

        private static void ContinueAfterReload(SheetImportRequest request)
        {
            var report = new SheetImportReport();
            var prepared = Prepare(request, report.Errors);
            if (!report.HasErrors && TryFindTableTypes(prepared, report, true, out var tableTypes))
                ApplyAssets(prepared, tableTypes, report);
            Log(report);
            ImportContinued?.Invoke(report);
        }

        private static PreparedImport Prepare(SheetImportRequest request, List<string> errors)
        {
            var prepared = new PreparedImport
            {
                Request = new SheetImportRequest
                {
                    assetFolder = NormalizeFolder(request.assetFolder, "Asset folder", errors),
                    codeFolder = NormalizeFolder(request.codeFolder, "Code folder", errors),
                    @namespace = request.@namespace,
                    sheets = request.sheets ?? new List<SheetImportSheet>(),
                },
            };

            try
            {
                prepared.Options = new CodeGenOptions(request.@namespace ?? string.Empty);
            }
            catch (ArgumentException e)
            {
                errors.Add(e.Message);
            }

            ResolveTableTypes(prepared.Request.sheets, prepared.CodeTableTypes, errors);

            var input = prepared.Request.sheets
                .Select(sheet => (
                    sheet?.name,
                    Rows(sheet),
                    sheet?.name != null && prepared.CodeTableTypes.TryGetValue(sheet.name, out var tableType)
                        ? GetRowType(tableType)
                        : null))
                .ToList();
            prepared.Checked = SheetSetChecker.Check(input);
            errors.AddRange(prepared.Checked.Errors);
            return prepared;
        }

        // Table types picked for the sheets (schema from code), by sheet name. A type that is not a
        // non-abstract SheetTable<> subclass of the project is an error; the sheet is then checked as generated.
        // A repeated sheet name keeps the first type: the checker reports the repeated name anyway.
        private static void ResolveTableTypes(List<SheetImportSheet> sheets, Dictionary<string, Type> result,
            List<string> errors)
        {
            Dictionary<string, Type> byName = null;
            foreach (var sheet in sheets)
            {
                if (sheet == null || string.IsNullOrEmpty(sheet.name) || string.IsNullOrWhiteSpace(sheet.tableType) ||
                    result.ContainsKey(sheet.name))
                    continue;

                byName = byName ?? FindTableTypes().ToDictionary(type => type.FullName, StringComparer.Ordinal);
                var fullName = sheet.tableType.Trim();
                if (byName.TryGetValue(fullName, out var type))
                    result.Add(sheet.name, type);
                else
                    errors.Add($"Sheet '{sheet.name}': table type '{fullName}' not found: it must be a non-abstract " +
                        "subclass of SheetTable<> in the project; check compile errors in the Console or pick another type");
            }
        }

        /// <summary>Non-abstract <see cref="SheetTable{TRow}"/> subclasses of the project, ordered by full name.</summary>
        internal static List<Type> FindTableTypes()
        {
            var byName = new Dictionary<string, Type>(StringComparer.Ordinal);
            foreach (var type in TypeCache.GetTypesDerivedFrom<ScriptableObject>())
            {
                if (!type.IsAbstract && !type.ContainsGenericParameters && type.FullName != null &&
                    GetRowType(type) != null && !byName.ContainsKey(type.FullName))
                    byName.Add(type.FullName, type);
            }
            return byName.Values.OrderBy(type => type.FullName, StringComparer.Ordinal).ToList();
        }

        private static IReadOnlyList<(string Name, IReadOnlyList<IReadOnlyList<string>> Rows)> ToCheckInput(
            List<SheetImportSheet> sheets)
        {
            return sheets.Select(sheet => (sheet?.name, Rows(sheet))).ToList();
        }

        private static IReadOnlyList<IReadOnlyList<string>> Rows(SheetImportSheet sheet)
        {
            return (sheet?.rows ?? new List<SheetImportRow>())
                .Select(row => (IReadOnlyList<string>)(row?.cells ?? new List<string>()))
                .ToList();
        }

        private static string NormalizeFolder(string folder, string what, List<string> errors)
        {
            if (string.IsNullOrWhiteSpace(folder))
            {
                errors.Add($"{what} is not set");
                return folder;
            }

            var path = folder.Trim().Replace('\\', '/').TrimEnd('/');
            var parts = path.Split('/');
            var valid = parts[0] == AssetsRoot && parts.All(part => part.Length > 0 && part != "." && part != "..");
            if (!valid)
                errors.Add($"{what} '{folder}' must be inside '{AssetsRoot}/'");
            return path;
        }

        private static List<KeyValuePair<string, string>> GenerateCode(PreparedImport prepared, SheetImportReport report)
        {
            var files = new List<KeyValuePair<string, string>>();
            var folder = prepared.Request.codeFolder;

            foreach (var sheet in prepared.Sheets)
            {
                // Schema from code: the classes are written by the user.
                if (prepared.CodeTableTypes.ContainsKey(sheet.SheetName))
                    continue;

                var rowClass = sheet.SheetName;
                var tableClass = rowClass + CodeGenerator.TableSuffix;
                try
                {
                    files.Add(new KeyValuePair<string, string>($"{folder}/{rowClass}.cs",
                        CodeGenerator.GenerateRowClass(rowClass, sheet.Columns, prepared.Options)));
                    files.Add(new KeyValuePair<string, string>($"{folder}/{tableClass}.cs",
                        CodeGenerator.GenerateTableClass(tableClass, rowClass, sheet.Columns, prepared.Options)));
                }
                catch (ArgumentException e)
                {
                    report.Errors.Add($"Sheet '{sheet.SheetName}': {e.Message}");
                }
            }

            foreach (var enumName in prepared.Enums.EnumNames)
            {
                var path = $"{folder}/{enumName}.cs";
                if (!TryReadPreviousEnum(path, enumName, report, out var previous))
                    continue;

                try
                {
                    var merged = CodeGenerator.MergeEnumMembers(previous, prepared.Enums.Members[enumName]);
                    files.Add(new KeyValuePair<string, string>(path,
                        CodeGenerator.GenerateEnum(enumName, merged.Members, prepared.Options)));

                    foreach (var name in merged.Removed)
                    {
                        var value = merged.Members.First(member => member.Name == name).Value;
                        report.Warnings.Add($"Enum '{enumName}': value '{name}' is no longer in the sheets; " +
                            $"kept as {name} = {value} to preserve serialized values");
                    }
                }
                catch (ArgumentException e)
                {
                    report.Errors.Add($"Enum '{enumName}': {e.Message}");
                }
            }

            return files;
        }

        // Members of the enum as already generated, so that their values survive a new import.
        // No file: no previous members. An unreadable file is an import error.
        private static bool TryReadPreviousEnum(string path, string enumName, SheetImportReport report,
            out IReadOnlyList<EnumMember> members)
        {
            members = Array.Empty<EnumMember>();
            if (!File.Exists(path))
                return true;

            string reason;
            try
            {
                var read = CodeGenerator.ReadEnumMembers(File.ReadAllText(path, Utf8NoBom), enumName);
                if (read.IsValid)
                {
                    members = read.Members;
                    return true;
                }
                reason = read.Error;
            }
            catch (ArgumentException e)
            {
                // Invalid enum name (e.g. a C# keyword): the same error the generator would give.
                report.Errors.Add($"Enum '{enumName}': {e.Message}");
                return false;
            }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException)
            {
                reason = e.Message;
            }

            report.Errors.Add($"Enum '{enumName}': cannot read existing values from '{path}': {reason}; " +
                "fix or delete the file to renumber");
            return false;
        }

        // Writes only files whose text differs, so unchanged code does not trigger a recompile.
        private static bool WriteFiles(string folder, List<KeyValuePair<string, string>> files, SheetImportReport report)
        {
            // Only sheets with a picked table type: no code folder is needed.
            if (files.Count == 0)
                return false;

            Directory.CreateDirectory(folder);
            var changed = false;
            foreach (var file in files)
            {
                if (File.Exists(file.Key) && File.ReadAllText(file.Key, Utf8NoBom) == file.Value)
                {
                    report.UnchangedFiles.Add(file.Key);
                    continue;
                }

                File.WriteAllText(file.Key, file.Value, Utf8NoBom);
                report.WrittenFiles.Add(file.Key);
                changed = true;
            }
            return changed;
        }

        private static bool TryFindTableTypes(PreparedImport prepared, SheetImportReport report, bool afterReload,
            out Dictionary<string, Type> tableTypes)
        {
            var byName = FindTableTypes().ToDictionary(type => type.FullName, StringComparer.Ordinal);

            tableTypes = new Dictionary<string, Type>(StringComparer.Ordinal);
            foreach (var sheet in prepared.Sheets)
            {
                if (prepared.CodeTableTypes.TryGetValue(sheet.SheetName, out var codeType))
                {
                    tableTypes.Add(sheet.SheetName, codeType);
                    continue;
                }

                var fullName = prepared.Options.Namespace + "." + sheet.SheetName + CodeGenerator.TableSuffix;
                if (byName.TryGetValue(fullName, out var type))
                {
                    tableTypes.Add(sheet.SheetName, type);
                    continue;
                }

                report.Errors.Add(afterReload
                    ? $"Type '{fullName}' not found after the scripts recompiled: check the Console for compile errors; " +
                      $"if '{prepared.Request.codeFolder}' is under an asmdef, it must reference 'Kentavr.SheetsToSO' and 'Kentavr.SheetsToSO.Core'"
                    : $"Generated code is up to date, but type '{fullName}' is not compiled: fix compile errors in the Console " +
                      "and import again");
            }
            return !report.HasErrors;
        }

        private static void ApplyAssets(PreparedImport prepared, Dictionary<string, Type> tableTypes, SheetImportReport report)
        {
            var folder = prepared.Request.assetFolder;
            EnsureFolder(folder);

            var applied = new Dictionary<string, UnityEngine.Object>(StringComparer.Ordinal);
            foreach (var sheet in prepared.Sheets)
            {
                var path = $"{folder}/{sheet.SheetName}.asset";
                var tableType = tableTypes[sheet.SheetName];
                var rowType = GetRowType(tableType);

                List<object> rows;
                try
                {
                    rows = sheet.Rows.Select(row => RowMapper.Map(rowType, row, sheet.SheetName)).ToList();
                }
                catch (ArgumentException e)
                {
                    report.Errors.Add($"{path}: not updated: {e.Message}");
                    continue;
                }

                var existing = AssetDatabase.LoadMainAssetAtPath(path);
                if (existing != null)
                {
                    if (existing.GetType() != tableType)
                    {
                        report.Errors.Add(
                            $"{path}: existing asset of type '{existing.GetType().FullName}' is not '{tableType.FullName}'; not overwritten");
                        continue;
                    }

                    var table = (ISheetTableData)existing;
                    if (SameRows(table.RowsData, rows))
                    {
                        report.UnchangedAssets.Add(path);
                        // Rows are the same, but the linked tables may still need an update.
                        applied.Add(sheet.SheetName, existing);
                        continue;
                    }

                    table.SetRows(rows);
                    EditorUtility.SetDirty(existing);
                    report.UpdatedAssets.Add(path);
                    applied.Add(sheet.SheetName, existing);
                }
                else if (File.Exists(path))
                {
                    report.Errors.Add($"{path}: file exists but cannot be loaded as an asset; not overwritten");
                }
                else
                {
                    var asset = ScriptableObject.CreateInstance(tableType);
                    // Name it before SetRows so index messages name the table.
                    asset.name = sheet.SheetName;
                    ((ISheetTableData)asset).SetRows(rows);
                    AssetDatabase.CreateAsset(asset, path);
                    report.CreatedAssets.Add(path);
                    applied.Add(sheet.SheetName, asset);
                }
            }

            LinkTables(prepared, applied, report);

            AssetDatabase.SaveAssets();
            FindAssetsWithoutSheet(prepared, report);

            if (!report.HasErrors)
                SaveSnapshot(prepared.Request);
        }

        // Second pass, after all assets exist: each table gets the assets of the sheets its ref<X> columns point to,
        // looked up as '<asset folder>/<X>.asset' of type '<Namespace>.XTable'. The list is replaced on every import.
        private static void LinkTables(PreparedImport prepared, Dictionary<string, UnityEngine.Object> applied,
            SheetImportReport report)
        {
            var folder = prepared.Request.assetFolder;
            var found = new Dictionary<string, SheetTableBase>(StringComparer.Ordinal);

            foreach (var sheet in prepared.Sheets)
            {
                if (!applied.TryGetValue(sheet.SheetName, out var asset))
                    continue;

                var path = $"{folder}/{sheet.SheetName}.asset";
                var linked = new List<SheetTableBase>();
                foreach (var column in sheet.Columns)
                {
                    if (column.Type.Kind != ColumnKind.Ref)
                        continue;

                    var target = column.Type.TypeName;
                    var targetPath = $"{folder}/{target}.asset";
                    // A sheet in schema-from-code mode has a hand-written table class.
                    var expectedType = prepared.CodeTableTypes.TryGetValue(target, out var codeTableType)
                        ? codeTableType.FullName
                        : prepared.Options.Namespace + "." + target + CodeGenerator.TableSuffix;
                    if (!found.TryGetValue(target, out var table))
                    {
                        var loaded = AssetDatabase.LoadMainAssetAtPath(targetPath);
                        table = loaded != null && loaded.GetType().FullName == expectedType ? loaded as SheetTableBase : null;
                        found.Add(target, table);
                    }

                    if (table == null)
                    {
                        report.AddWarning(
                            $"{path}: column '{column.Name}' refers to sheet '{target}', but '{targetPath}' is not " +
                            $"a '{expectedType}' asset; {sheet.SheetName}.{CodeGenerator.RefPropertyName(column.Name)} will return null",
                            path);
                        continue;
                    }

                    if (!linked.Contains(table))
                        linked.Add(table);
                }

                if (asset is SheetTableBase current && current.LinkedTables.SequenceEqual(linked))
                    continue;

                ((ISheetTableData)asset).SetLinkedTables(linked);
                EditorUtility.SetDirty(asset);
            }
        }

        // Rows are [Serializable] classes: compare them as the asset serializer sees them.
        private static bool SameRows(System.Collections.IList current, List<object> rows)
        {
            var count = current?.Count ?? 0;
            if (count != rows.Count)
                return false;

            for (var i = 0; i < count; i++)
            {
                if (current[i] == null || rows[i] == null)
                {
                    if (!ReferenceEquals(current[i], rows[i]))
                        return false;
                    continue;
                }
                if (JsonUtility.ToJson(current[i]) != JsonUtility.ToJson(rows[i]))
                    return false;
            }

            return true;
        }

        private static string SnapshotPath(string assetFolder) =>
            $"{SnapshotFolder}/{assetFolder.Replace('/', '_')}.json";

        // Sheets of the last import applied to the folder; empty when there was none.
        private static List<SheetImportSheet> ReadSnapshot(string assetFolder)
        {
            var path = SnapshotPath(assetFolder);
            if (!File.Exists(path))
                return new List<SheetImportSheet>();

            var snapshot = JsonUtility.FromJson<Snapshot>(File.ReadAllText(path, Utf8NoBom));
            return snapshot?.sheets ?? new List<SheetImportSheet>();
        }

        // Replaces the imported sheets in the snapshot and keeps the others, so a partial import
        // does not hide the earlier state of the sheets left out.
        private static void SaveSnapshot(SheetImportRequest request)
        {
            var path = SnapshotPath(request.assetFolder);

            List<SheetImportSheet> previous;
            try
            {
                previous = ReadSnapshot(request.assetFolder);
            }
            catch (Exception)
            {
                // Unreadable snapshot: start over with this import.
                previous = new List<SheetImportSheet>();
            }

            // Same rule as for sheet names in an import: one asset file per name, case-insensitive.
            var imported = new HashSet<string>(request.sheets.Select(sheet => sheet.name), StringComparer.OrdinalIgnoreCase);
            var sheets = previous
                .Where(sheet => sheet != null && !string.IsNullOrEmpty(sheet.name) && !imported.Contains(sheet.name))
                .Concat(request.sheets)
                .ToList();

            try
            {
                Directory.CreateDirectory(SnapshotFolder);
                File.WriteAllText(path, JsonUtility.ToJson(new Snapshot { sheets = sheets }), Utf8NoBom);
            }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException)
            {
                // Assets are already updated; only the next diff is affected.
                Debug.LogWarning(LogPrefix + $"cannot save the import snapshot '{path}': {e.Message}");
            }
        }

        // Table assets directly in the asset folder (subfolders excluded) whose name matches no sheet.
        private static void FindAssetsWithoutSheet(PreparedImport prepared, SheetImportReport report)
        {
            var folder = prepared.Request.assetFolder;
            var names = new HashSet<string>(prepared.Sheets.Select(sheet => sheet.SheetName), StringComparer.OrdinalIgnoreCase);
            var files = Directory.GetFiles(folder, "*.asset", SearchOption.TopDirectoryOnly)
                .Select(file => file.Replace('\\', '/'))
                .OrderBy(file => file, StringComparer.Ordinal);

            foreach (var path in files)
            {
                if (names.Contains(Path.GetFileNameWithoutExtension(path)))
                    continue;

                var asset = AssetDatabase.LoadMainAssetAtPath(path);
                if (asset != null && GetRowType(asset.GetType()) != null)
                    report.AssetsWithoutSheet.Add(path);
            }
        }

        private static void EnsureFolder(string folder)
        {
            if (AssetDatabase.IsValidFolder(folder))
                return;

            var slash = folder.LastIndexOf('/');
            var parent = folder.Substring(0, slash);
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, folder.Substring(slash + 1));
        }

        /// <summary>Row type of a <see cref="SheetTable{TRow}"/> subclass, or <c>null</c> for other types.</summary>
        internal static Type GetRowType(Type type)
        {
            for (var current = type; current != null; current = current.BaseType)
            {
                if (current.IsGenericType && current.GetGenericTypeDefinition() == typeof(SheetTable<>))
                    return current.GetGenericArguments()[0];
            }
            return null;
        }

        private static void SavePending(SheetImportRequest request)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(PendingImportPath));
            File.WriteAllText(PendingImportPath, JsonUtility.ToJson(request), Utf8NoBom);
        }

        private static void DeletePending()
        {
            if (File.Exists(PendingImportPath))
                File.Delete(PendingImportPath);
        }

        private static void Log(SheetImportReport report)
        {
            Debug.Log(report.ToString());

            foreach (var path in report.AssetsWithoutSheet)
                Debug.LogWarning(LogPrefix + $"asset '{path}' has no sheet in this import; it was not deleted",
                    AssetDatabase.LoadMainAssetAtPath(path));

            foreach (var warning in report.Warnings)
            {
                var context = report.WarningAssets.TryGetValue(warning, out var assetPath)
                    ? AssetDatabase.LoadMainAssetAtPath(assetPath)
                    : null;
                Debug.LogWarning(LogPrefix + warning, context);
            }

            foreach (var error in report.Errors)
                Debug.LogError(LogPrefix + error);
        }
    }
}
