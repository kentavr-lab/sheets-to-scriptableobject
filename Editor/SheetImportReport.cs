using System.Collections.Generic;
using System.Text;

namespace Kentavr.SheetsToSO.Editor
{
    /// <summary>
    /// Outcome of an import. Paths are project-relative, e.g. <c>Assets/Data/Units.asset</c>.
    /// </summary>
    public sealed class SheetImportReport
    {
        /// <summary>Assets created by this import.</summary>
        public List<string> CreatedAssets { get; } = new List<string>();

        /// <summary>Existing assets whose rows were replaced.</summary>
        public List<string> UpdatedAssets { get; } = new List<string>();

        /// <summary>Generated code files written because their text changed.</summary>
        public List<string> WrittenFiles { get; } = new List<string>();

        /// <summary>Generated code files left as is: same text.</summary>
        public List<string> UnchangedFiles { get; } = new List<string>();

        /// <summary>Table assets in the asset folder with no sheet in the request. Not deleted.</summary>
        public List<string> AssetsWithoutSheet { get; } = new List<string>();

        /// <summary>Warnings, e.g. enum values kept although no longer in the sheets. Do not fail the import.</summary>
        public List<string> Warnings { get; } = new List<string>();

        /// <summary>Asset each warning is about, for the Console link; warnings without an asset are not here.</summary>
        internal Dictionary<string, string> WarningAssets { get; } = new Dictionary<string, string>();

        /// <summary>Adds a warning about the asset at <paramref name="assetPath"/>.</summary>
        internal void AddWarning(string message, string assetPath)
        {
            Warnings.Add(message);
            WarningAssets[message] = assetPath;
        }

        /// <summary>Errors; when any, nothing or only part of the import was applied (see the messages).</summary>
        public List<string> Errors { get; } = new List<string>();

        /// <summary>Code changed: assets are updated after the scripts recompile, in a separate report.</summary>
        public bool WaitingForReload { get; set; }

        public bool HasErrors => Errors.Count > 0;

        public override string ToString()
        {
            var text = new StringBuilder();
            text.Append("Sheets to SO import: ");
            if (HasErrors)
                text.Append($"{Errors.Count} error(s)");
            else if (WaitingForReload)
                text.Append("code updated, assets will be updated after the scripts recompile");
            else
                text.Append("done");

            Append(text, "Created assets", CreatedAssets);
            Append(text, "Updated assets", UpdatedAssets);
            Append(text, "Written code files", WrittenFiles);
            Append(text, "Unchanged code files", UnchangedFiles);
            Append(text, "Assets without a sheet (not deleted)", AssetsWithoutSheet);
            Append(text, "Warnings", Warnings);
            Append(text, "Errors", Errors);
            return text.ToString();
        }

        private static void Append(StringBuilder text, string title, List<string> items)
        {
            if (items.Count == 0)
                return;
            text.Append('\n').Append(title).Append(" (").Append(items.Count).Append("):");
            foreach (var item in items)
                text.Append("\n  ").Append(item);
        }
    }
}
