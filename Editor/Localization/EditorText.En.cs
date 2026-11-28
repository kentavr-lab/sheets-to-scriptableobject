using System.Collections.Generic;

namespace Kentavr.SheetsToSO.Editor
{
    internal static partial class EditorText
    {
        // English texts; also the fallback for keys missing from other tables.
        private static Dictionary<TextKey, string> CreateEnglishTable()
        {
            return new Dictionary<TextKey, string>
            {
                [TextKey.Source] = "Source",
                [TextKey.SourceGoogleApi] = "Google API",
                [TextKey.SourceExport] = "Export",
                [TextKey.SourceCsvFiles] = "CSV files",
                [TextKey.SpreadsheetId] = "Spreadsheet ID",
                [TextKey.ApiKey] = "API key",
                [TextKey.Clear] = "Clear",
                [TextKey.ApiKeyHint] = "The key is stored in the editor preferences (EditorPrefs), not in the project.",
                [TextKey.RefreshSheetList] = "Refresh sheet list",
                [TextKey.GoogleSheetsEmpty] = "Click “Refresh sheet list”.",
                [TextKey.ExportHint] = "No key needed: the spreadsheet must be shared as Anyone with the link — Viewer.",
                [TextKey.SheetName] = "Sheet name",
                [TextKey.TableType] = "Table type",
                [TextKey.GenerateCode] = "Generate code",
                [TextKey.TableTypeNotFound] = "{0} (not found)",
                [TextKey.AddSheet] = "Add sheet",
                [TextKey.CsvFolder] = "CSV folder",
                [TextKey.Browse] = "Browse…",
                [TextKey.CsvFolderPanelTitle] = "Folder with .csv / .tsv files",
                [TextKey.RefreshFileList] = "Refresh file list",
                [TextKey.CsvFilesEmpty] = "Choose a folder with .csv / .tsv files.",
                [TextKey.Sheets] = "Sheets",
                [TextKey.All] = "All",
                [TextKey.None] = "None",
                [TextKey.AssetsFolder] = "Assets folder",
                [TextKey.CodeFolder] = "Code folder",
                [TextKey.Check] = "Check",
                [TextKey.Apply] = "Apply",
                [TextKey.Cancel] = "Cancel",

                [TextKey.Loading] = "Loading…",
                [TextKey.LoadingSheetList] = "Loading the sheet list…",
                [TextKey.LoadingCancelled] = "Loading cancelled",
                [TextKey.CsvFolderNotFound] = "Folder '{0}' not found",
                [TextKey.CsvFolderUnreadable] = "Folder '{0}' cannot be read: {1}",
                [TextKey.CsvFolderNoFiles] = "No .csv / .tsv files in folder '{0}'",
                [TextKey.DiffHeader] = "Changes since the last import",
                [TextKey.ErrorCount] = "Errors: {0}. Apply is not available.",
                [TextKey.NoErrors] = "No errors — ready to apply.",
                [TextKey.ImportWaitingForReload] = "Code updated; the assets will be updated after scripts are recompiled.",
                [TextKey.ImportFinishedWithErrors] = "Import finished with errors; see the details below and in the Console.",
                [TextKey.ImportApplied] = "Import applied.",
                [TextKey.RemovalsDialog] = "The diff has removed rows or sheets. Removed rows will disappear from the assets; " +
                    "assets of removed sheets will stay in place.\n\nApply the import?",

                [TextKey.ApiKeyMissing] = "API key is not set",
                [TextKey.SpreadsheetIdMissing] = "Spreadsheet ID is not set",
                [TextKey.NetworkError] = "Network error: {0}",
                [TextKey.Timeout] = "The response timed out",

                [TextKey.SheetRefEmpty] = "(empty)",
                [TextKey.SheetRefNotString] = "[SheetRef] works only with string, string[] and List<string> fields",
                [TextKey.SheetRefInvalidType] = "[SheetRef]: type {0} must be a non-abstract subclass of SheetTable<>",
                [TextKey.SheetRefMissingRow] = "'{0}': no row in {1} ({2})",
                [TextKey.SheetRefNoAssetAtPath] = "no {0} asset at path '{1}'",
                [TextKey.SheetRefNoAsset] = "no {0} asset in the project",
                [TextKey.SheetRefSeveralAssets] = "found {0} {1} assets, using '{2}'; set Asset in the attribute",

                [TextKey.MenuImportCsvFolder] = "Import CSV folder here…",
            };
        }
    }
}
