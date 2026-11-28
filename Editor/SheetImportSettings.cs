using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Kentavr.SheetsToSO.Editor
{
    /// <summary>Where the import window loads sheets from. The window shows translated names, see <see cref="TextKey"/>.</summary>
    internal enum SheetSourceKind
    {
        [InspectorName("Google API")] GoogleApi,
        [InspectorName("Export")] Export,
        [InspectorName("CSV files")] CsvFiles,
    }

    /// <summary>Sheet in a list with a check box.</summary>
    [Serializable]
    internal sealed class SheetToggle
    {
        public string name;
        public bool selected;
    }

    /// <summary>Sheet of the export source: name given by the user and the tab <c>gid</c>.</summary>
    [Serializable]
    internal sealed class ExportSheetEntry
    {
        public string name;
        public string gid;
        public bool selected = true;
    }

    /// <summary>Table type picked for a sheet (schema from code); see <see cref="SheetImportSheet.tableType"/>.</summary>
    [Serializable]
    internal sealed class SheetTableBinding
    {
        public string sheet;
        public string tableType;
    }

    /// <summary>
    /// Settings of the import window, per project and per user: <c>UserSettings/</c> is outside <c>Assets/</c>
    /// and ignored by the standard Unity .gitignore. The API key is not stored here, see <see cref="ApiKeyStore"/>.
    /// </summary>
    [FilePath("UserSettings/Kentavr.SheetsToSO/ImportWindow.asset", FilePathAttribute.Location.ProjectFolder)]
    internal sealed class SheetImportSettings : ScriptableSingleton<SheetImportSettings>
    {
        public SheetSourceKind source = SheetSourceKind.GoogleApi;
        public string spreadsheetId = string.Empty;
        public string assetFolder = "Assets/Data";
        public string codeFolder = "Assets/Data/Generated";
        public string @namespace = "SheetsData";

        /// <summary>Tabs read from Google Sheets API.</summary>
        public List<SheetToggle> googleSheets = new List<SheetToggle>();

        /// <summary>Sheets of the export source, entered by hand.</summary>
        public List<ExportSheetEntry> exportSheets = new List<ExportSheetEntry>();

        /// <summary>Folder on disk with .csv / .tsv files.</summary>
        public string csvFolder = string.Empty;

        /// <summary>Files of <see cref="csvFolder"/>, names with extension.</summary>
        public List<SheetToggle> csvFiles = new List<SheetToggle>();

        /// <summary>
        /// Table types picked for sheets, by sheet name (ordinal), shared by all sources. Sheets without a binding
        /// are in code generation mode. Bindings of sheets missing from the lists are kept: a sheet may be gone for a while.
        /// </summary>
        public List<SheetTableBinding> tableBindings = new List<SheetTableBinding>();

        public void SaveSettings() => Save(true);

        /// <summary>Full name of the table type picked for the sheet; empty for code generation mode.</summary>
        public string GetTableType(string sheet)
        {
            if (string.IsNullOrEmpty(sheet))
                return string.Empty;
            foreach (var binding in tableBindings)
            {
                if (binding != null && binding.sheet == sheet)
                    return binding.tableType ?? string.Empty;
            }
            return string.Empty;
        }

        /// <summary>Picks a table type for the sheet; empty or <c>null</c> switches it back to code generation.</summary>
        public void SetTableType(string sheet, string tableType)
        {
            if (string.IsNullOrEmpty(sheet))
                return;

            tableBindings.RemoveAll(binding => binding == null || binding.sheet == sheet);
            if (!string.IsNullOrEmpty(tableType))
                tableBindings.Add(new SheetTableBinding { sheet = sheet, tableType = tableType });
        }

        /// <summary>
        /// Replaces the list with the given names, in their order. Known names keep their check box;
        /// new names are checked unless they start with <c>#</c>.
        /// </summary>
        public static void Merge(List<SheetToggle> list, IEnumerable<string> names)
        {
            var previous = new Dictionary<string, bool>(StringComparer.Ordinal);
            foreach (var item in list)
            {
                if (item?.name != null && !previous.ContainsKey(item.name))
                    previous.Add(item.name, item.selected);
            }

            list.Clear();
            foreach (var name in names)
            {
                var selected = previous.TryGetValue(name, out var was) ? was : !name.StartsWith("#", StringComparison.Ordinal);
                list.Add(new SheetToggle { name = name, selected = selected });
            }
        }
    }

    /// <summary>
    /// Google API key in <see cref="EditorPrefs"/>: per user of the editor, outside the project and git.
    /// Stored as plain text by the OS (registry / plist).
    /// </summary>
    internal static class ApiKeyStore
    {
        private const string PrefsKey = "Kentavr.SheetsToSO.ApiKey";

        public static string Load() => EditorPrefs.GetString(PrefsKey, string.Empty);

        public static void Save(string key)
        {
            if (string.IsNullOrEmpty(key))
                EditorPrefs.DeleteKey(PrefsKey);
            else
                EditorPrefs.SetString(PrefsKey, key);
        }
    }
}
