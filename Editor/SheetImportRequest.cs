using System;
using System.Collections.Generic;

namespace Kentavr.SheetsToSO.Editor
{
    /// <summary>
    /// Input of <see cref="SheetAssetImporter.Import"/>: target folders, namespace and raw sheet cells.
    /// Serializable with <c>JsonUtility</c> so the import can continue after a script reload.
    /// </summary>
    [Serializable]
    public sealed class SheetImportRequest
    {
        /// <summary>Folder for the table assets, e.g. <c>Assets/Data</c>.</summary>
        public string assetFolder;

        /// <summary>Folder for the generated code, e.g. <c>Assets/Data/Generated</c>.</summary>
        public string codeFolder;

        /// <summary>Namespace of the generated code.</summary>
        public string @namespace;

        /// <summary>Sheets to import, in order.</summary>
        public List<SheetImportSheet> sheets = new List<SheetImportSheet>();
    }

    /// <summary>Raw cells of one sheet; <c>rows[0]</c> is the header row.</summary>
    [Serializable]
    public sealed class SheetImportSheet
    {
        /// <summary>Sheet name: the asset and, in code generation mode, the row and table classes are named after it.</summary>
        public string name;

        /// <summary>
        /// Full name of a <c>SheetTable&lt;TRow&gt;</c> subclass of the project (schema from code): rows are parsed
        /// by the fields of <c>TRow</c>, no code is generated for the sheet, the asset is of this type.
        /// Empty: the code of the sheet is generated.
        /// </summary>
        public string tableType;

        public List<SheetImportRow> rows = new List<SheetImportRow>();
    }

    /// <summary>Raw cells of one sheet row.</summary>
    [Serializable]
    public sealed class SheetImportRow
    {
        public List<string> cells = new List<string>();
    }
}
