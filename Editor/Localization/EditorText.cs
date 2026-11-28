using System.Collections.Generic;
using System.Globalization;

namespace Kentavr.SheetsToSO.Editor
{
    /// <summary>One string of the editor interface; texts are in the tables of <see cref="EditorText"/>.</summary>
    internal enum TextKey
    {
        // Import window: fields and buttons.
        Source,
        SourceGoogleApi,
        SourceExport,
        SourceCsvFiles,
        SpreadsheetId,
        ApiKey,
        Clear,
        ApiKeyHint,
        RefreshSheetList,
        GoogleSheetsEmpty,
        ExportHint,
        SheetName,
        TableType,
        GenerateCode,
        TableTypeNotFound,
        AddSheet,
        CsvFolder,
        Browse,
        CsvFolderPanelTitle,
        RefreshFileList,
        CsvFilesEmpty,
        Sheets,
        All,
        None,
        AssetsFolder,
        CodeFolder,
        Check,
        Apply,
        Cancel,

        // Import window: loading, results and messages.
        Loading,
        LoadingSheetList,
        LoadingCancelled,
        CsvFolderNotFound,
        CsvFolderUnreadable,
        CsvFolderNoFiles,
        DiffHeader,
        ErrorCount,
        NoErrors,
        ImportWaitingForReload,
        ImportFinishedWithErrors,
        ImportApplied,
        RemovalsDialog,

        // Source loader.
        ApiKeyMissing,
        SpreadsheetIdMissing,
        NetworkError,
        Timeout,

        // [SheetRef] drawer.
        SheetRefEmpty,
        SheetRefNotString,
        SheetRefInvalidType,
        SheetRefMissingRow,
        SheetRefNoAssetAtPath,
        SheetRefNoAsset,
        SheetRefSeveralAssets,

        // Assets menu item (under "Assets/Sheets to SO/").
        MenuImportCsvFolder,
    }

    /// <summary>Language of the editor interface: its code and the table of texts.</summary>
    internal sealed class EditorLanguage
    {
        public EditorLanguage(string code, string name, IReadOnlyDictionary<TextKey, string> table)
        {
            Code = code;
            Name = name;
            Table = table;
        }

        /// <summary>Short code, stored in the editor preferences and shown in the language switch.</summary>
        public string Code { get; }

        /// <summary>Name of the language in that language.</summary>
        public string Name { get; }

        public IReadOnlyDictionary<TextKey, string> Table { get; }
    }

    /// <summary>
    /// Texts of the editor interface, one table per language (<c>EditorText.*.cs</c>). Free of Unity, so the
    /// tables are checked by <c>dotnet test</c>. To add a language, add a table file and list it in <see cref="Languages"/>.
    /// </summary>
    internal static partial class EditorText
    {
        // Tables are built by methods: the order of static field initializers across partial files is undefined.
        public static readonly EditorLanguage English = new EditorLanguage("en", "English", CreateEnglishTable());

        public static readonly EditorLanguage Russian = new EditorLanguage("ru", RussianName, CreateRussianTable());

        /// <summary>Available languages; the first one is the default.</summary>
        public static IReadOnlyList<EditorLanguage> Languages { get; } = new[] { English, Russian };

        /// <summary>
        /// Text of <paramref name="key"/> in <paramref name="language"/>, formatted with <paramref name="args"/>
        /// (invariant culture). A key missing from the table falls back to the English text.
        /// </summary>
        public static string Get(EditorLanguage language, TextKey key, params object[] args)
        {
            if (language?.Table == null || !language.Table.TryGetValue(key, out var text))
            {
                if (!English.Table.TryGetValue(key, out text))
                    text = key.ToString();
            }

            return args == null || args.Length == 0 ? text : string.Format(CultureInfo.InvariantCulture, text, args);
        }
    }
}
