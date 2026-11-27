using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Kentavr.SheetsToSO.Editor
{
    /// <summary>Where the import window loads sheets from.</summary>
    internal enum SheetSourceKind
    {
        [InspectorName("Google API")] GoogleApi,
        [InspectorName("Export")] Export,
        [InspectorName("CSV-файлы")] CsvFiles,
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

        public void SaveSettings() => Save(true);

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
