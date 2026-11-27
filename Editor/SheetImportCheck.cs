using System.Collections.Generic;

namespace Kentavr.SheetsToSO.Editor
{
    /// <summary>
    /// Outcome of <see cref="SheetAssetImporter.Check"/>: errors that block the import and the diff with the
    /// last import applied to the same asset folder on this machine.
    /// </summary>
    public sealed class SheetImportCheck
    {
        /// <summary>Errors: folders, namespace, sheets. Any error blocks the import.</summary>
        public List<string> Errors { get; } = new List<string>();

        /// <summary>Diff text, or why there is no diff; <c>null</c> when there are errors.</summary>
        public string DiffText { get; set; }

        /// <summary>Whether the diff removes sheets or rows.</summary>
        public bool HasRemovals { get; set; }

        public bool HasErrors => Errors.Count > 0;
    }
}
