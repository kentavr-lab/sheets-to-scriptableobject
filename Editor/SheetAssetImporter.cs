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

        private const string LogPrefix = "Sheets to SO: ";
        private const string TableSuffix = "Table";
        private const string AssetsRoot = "Assets";

        private static readonly UTF8Encoding Utf8NoBom = new UTF8Encoding(false);

        private sealed class PreparedImport
        {
            public SheetImportRequest Request;
            public CodeGenOptions Options;
            public List<SheetParseResult> Sheets = new List<SheetParseResult>();
            public EnumMembersResult Enums;
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
            var prepared = Prepare(request, report);
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
            var prepared = Prepare(request, report);
            if (!report.HasErrors && TryFindTableTypes(prepared, report, true, out var tableTypes))
                ApplyAssets(prepared, tableTypes, report);
            Log(report);
        }

        private static PreparedImport Prepare(SheetImportRequest request, SheetImportReport report)
        {
            var prepared = new PreparedImport
            {
                Request = new SheetImportRequest
                {
                    assetFolder = NormalizeFolder(request.assetFolder, "Asset folder", report),
                    codeFolder = NormalizeFolder(request.codeFolder, "Code folder", report),
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
                report.Errors.Add(e.Message);
            }

            if (prepared.Request.sheets.Count == 0)
                report.Errors.Add("No sheets to import");

            var names = new HashSet<string>(StringComparer.Ordinal);
            foreach (var source in prepared.Request.sheets)
            {
                if (source == null || string.IsNullOrEmpty(source.name))
                {
                    report.Errors.Add("Sheet without a name");
                    continue;
                }
                if (!names.Add(source.name))
                {
                    report.Errors.Add($"Duplicate sheet name '{source.name}'");
                    continue;
                }

                var rows = (source.rows ?? new List<SheetImportRow>())
                    .Select(row => (IReadOnlyList<string>)(row?.cells ?? new List<string>()))
                    .ToList();
                var sheet = SheetParser.Parse(source.name, rows);
                foreach (var error in sheet.Errors)
                    report.Errors.Add(error.ToString());

                if (sheet.Columns.Count > 0)
                {
                    var id = sheet.Columns[0];
                    if (id.Type.Kind != ColumnKind.String || id.Type.IsArray)
                        report.Errors.Add($"{sheet.SheetName}!{id.Address}: column 'id' must be of type 'string', found '{id.Type}'");
                }

                prepared.Sheets.Add(sheet);
            }

            if (report.HasErrors)
                return prepared;

            prepared.Enums = CodeGenerator.CollectEnumMembers(prepared.Sheets);
            foreach (var error in prepared.Enums.Errors)
                report.Errors.Add(error.ToString());

            // Row classes, table classes and enums share one namespace.
            var typeOwners = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var sheet in prepared.Sheets)
            {
                AddTypeName(typeOwners, sheet.SheetName, $"row class of sheet '{sheet.SheetName}'", report);
                AddTypeName(typeOwners, sheet.SheetName + TableSuffix, $"table class of sheet '{sheet.SheetName}'", report);
            }
            foreach (var enumName in prepared.Enums.EnumNames)
                AddTypeName(typeOwners, enumName, $"enum '{enumName}'", report);

            return prepared;
        }

        private static void AddTypeName(Dictionary<string, string> owners, string name, string owner, SheetImportReport report)
        {
            if (owners.TryGetValue(name, out var other))
                report.Errors.Add($"Type name '{name}' is used by both the {other} and the {owner}");
            else
                owners.Add(name, owner);
        }

        private static string NormalizeFolder(string folder, string what, SheetImportReport report)
        {
            if (string.IsNullOrWhiteSpace(folder))
            {
                report.Errors.Add($"{what} is not set");
                return folder;
            }

            var path = folder.Trim().Replace('\\', '/').TrimEnd('/');
            var parts = path.Split('/');
            var valid = parts[0] == AssetsRoot && parts.All(part => part.Length > 0 && part != "." && part != "..");
            if (!valid)
                report.Errors.Add($"{what} '{folder}' must be inside '{AssetsRoot}/'");
            return path;
        }

        private static List<KeyValuePair<string, string>> GenerateCode(PreparedImport prepared, SheetImportReport report)
        {
            var files = new List<KeyValuePair<string, string>>();
            var folder = prepared.Request.codeFolder;

            foreach (var sheet in prepared.Sheets)
            {
                var rowClass = sheet.SheetName;
                var tableClass = rowClass + TableSuffix;
                try
                {
                    files.Add(new KeyValuePair<string, string>($"{folder}/{rowClass}.cs",
                        CodeGenerator.GenerateRowClass(rowClass, sheet.Columns, prepared.Options)));
                    files.Add(new KeyValuePair<string, string>($"{folder}/{tableClass}.cs",
                        CodeGenerator.GenerateTableClass(tableClass, rowClass, prepared.Options)));
                }
                catch (ArgumentException e)
                {
                    report.Errors.Add($"Sheet '{sheet.SheetName}': {e.Message}");
                }
            }

            foreach (var enumName in prepared.Enums.EnumNames)
            {
                try
                {
                    files.Add(new KeyValuePair<string, string>($"{folder}/{enumName}.cs",
                        CodeGenerator.GenerateEnum(enumName, prepared.Enums.Members[enumName], prepared.Options)));
                }
                catch (ArgumentException e)
                {
                    report.Errors.Add($"Enum '{enumName}': {e.Message}");
                }
            }

            return files;
        }

        // Writes only files whose text differs, so unchanged code does not trigger a recompile.
        private static bool WriteFiles(string folder, List<KeyValuePair<string, string>> files, SheetImportReport report)
        {
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
            var byName = new Dictionary<string, Type>(StringComparer.Ordinal);
            foreach (var type in TypeCache.GetTypesDerivedFrom<ScriptableObject>())
            {
                if (!type.IsAbstract && type.FullName != null && GetRowType(type) != null && !byName.ContainsKey(type.FullName))
                    byName.Add(type.FullName, type);
            }

            tableTypes = new Dictionary<string, Type>(StringComparer.Ordinal);
            foreach (var sheet in prepared.Sheets)
            {
                var fullName = prepared.Options.Namespace + "." + sheet.SheetName + TableSuffix;
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

                    ((ISheetTableData)existing).SetRows(rows);
                    EditorUtility.SetDirty(existing);
                    report.UpdatedAssets.Add(path);
                }
                else if (File.Exists(path))
                {
                    report.Errors.Add($"{path}: file exists but cannot be loaded as an asset; not overwritten");
                }
                else
                {
                    var asset = ScriptableObject.CreateInstance(tableType);
                    ((ISheetTableData)asset).SetRows(rows);
                    AssetDatabase.CreateAsset(asset, path);
                    report.CreatedAssets.Add(path);
                }
            }

            AssetDatabase.SaveAssets();
            FindAssetsWithoutSheet(prepared, report);
        }

        // Table assets directly in the asset folder (subfolders excluded) whose name matches no sheet.
        private static void FindAssetsWithoutSheet(PreparedImport prepared, SheetImportReport report)
        {
            var folder = prepared.Request.assetFolder;
            var names = new HashSet<string>(prepared.Sheets.Select(sheet => sheet.SheetName), StringComparer.Ordinal);
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

            foreach (var error in report.Errors)
                Debug.LogError(LogPrefix + error);
        }
    }
}
