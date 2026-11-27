using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Kentavr.SheetsToSO.Editor
{
    /// <summary>
    /// Temporary command: imports every .csv/.tsv file of a folder on disk into the selected project folder.
    /// The main way to import is <see cref="SheetImportWindow"/>.
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

            var files = CsvFolderSource.ListFiles(source, false);
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

            var errors = new List<string>();
            foreach (var file in files)
            {
                var sheet = CsvFolderSource.Read(Path.Combine(source, file), errors);
                if (sheet != null)
                    request.sheets.Add(sheet);
            }

            if (errors.Count > 0)
            {
                foreach (var error in errors)
                    Debug.LogError(LogPrefix + error);
                Debug.LogError(LogPrefix + "import cancelled: fix the CSV errors above");
                return;
            }

            SheetAssetImporter.Import(request);
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
