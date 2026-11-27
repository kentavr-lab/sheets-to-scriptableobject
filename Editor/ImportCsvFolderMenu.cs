using System;
using System.IO;
using System.Linq;
using System.Text;
using Kentavr.SheetsToSO.Core;
using UnityEditor;
using UnityEngine;

namespace Kentavr.SheetsToSO.Editor
{
    /// <summary>
    /// Temporary command to try the importer before the import window exists:
    /// imports every .csv/.tsv file of a folder on disk into the selected project folder.
    /// </summary>
    internal static class ImportCsvFolderMenu
    {
        private const string MenuPath = "Assets/Sheets to SO/Import CSV folder here…";
        private const string Namespace = "SheetsData";
        private const string CodeSubfolder = "Generated";
        private const string LogPrefix = "Sheets to SO: ";

        [MenuItem(MenuPath, true)]
        private static bool CanImport()
        {
            return GetSelectedFolder() != null;
        }

        [MenuItem(MenuPath, false)]
        private static void ImportHere()
        {
            var target = GetSelectedFolder();
            if (target == null)
                return;

            var source = EditorUtility.OpenFolderPanel("Folder with .csv / .tsv files", string.Empty, string.Empty);
            if (string.IsNullOrEmpty(source))
                return;

            var files = Directory.GetFiles(source)
                .Where(IsSheetFile)
                .OrderBy(file => Path.GetFileName(file), StringComparer.Ordinal)
                .ToList();
            if (files.Count == 0)
            {
                Debug.LogError(LogPrefix + $"no .csv or .tsv files in '{source}'");
                return;
            }

            var request = new SheetImportRequest
            {
                assetFolder = target,
                codeFolder = target + "/" + CodeSubfolder,
                @namespace = Namespace,
            };

            var hasErrors = false;
            foreach (var file in files)
            {
                var delimiter = string.Equals(Path.GetExtension(file), ".tsv", StringComparison.OrdinalIgnoreCase)
                    ? CsvDelimiter.Tab
                    : CsvDelimiter.Comma;
                var result = CsvReader.Read(File.ReadAllText(file, Encoding.UTF8), delimiter);
                if (!result.IsValid)
                {
                    foreach (var error in result.Errors)
                        Debug.LogError(LogPrefix + $"{Path.GetFileName(file)}: {error}");
                    hasErrors = true;
                    continue;
                }

                var sheet = new SheetImportSheet { name = Path.GetFileNameWithoutExtension(file) };
                foreach (var row in result.Rows)
                    sheet.rows.Add(new SheetImportRow { cells = row.Cells.ToList() });
                request.sheets.Add(sheet);
            }

            if (hasErrors)
            {
                Debug.LogError(LogPrefix + "import cancelled: fix the CSV errors above");
                return;
            }

            SheetAssetImporter.Import(request);
        }

        private static bool IsSheetFile(string path)
        {
            var name = Path.GetFileName(path);
            if (name.StartsWith("#", StringComparison.Ordinal))
                return false;
            var extension = Path.GetExtension(path);
            return string.Equals(extension, ".csv", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(extension, ".tsv", StringComparison.OrdinalIgnoreCase);
        }

        private static string GetSelectedFolder()
        {
            var guids = Selection.assetGUIDs;
            if (guids == null || guids.Length != 1)
                return null;
            var path = AssetDatabase.GUIDToAssetPath(guids[0]);
            return AssetDatabase.IsValidFolder(path) ? path : null;
        }
    }
}
