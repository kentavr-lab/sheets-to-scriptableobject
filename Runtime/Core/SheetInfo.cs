using System;

namespace Kentavr.SheetsToSO.Core
{
    /// <summary>Spreadsheet tab properties from Google Sheets API <c>spreadsheets.get</c>.</summary>
    public sealed class SheetInfo
    {
        public SheetInfo(string title, int sheetId, int index, bool hidden)
        {
            Title = title ?? throw new ArgumentNullException(nameof(title));
            SheetId = sheetId;
            Index = index;
            Hidden = hidden;
        }

        /// <summary>Tab name.</summary>
        public string Title { get; }

        /// <summary>Tab id (<c>gid</c> in the sheet URL).</summary>
        public int SheetId { get; }

        /// <summary>0-based tab position.</summary>
        public int Index { get; }

        /// <summary>Whether the tab is hidden in the UI.</summary>
        public bool Hidden { get; }

        public override string ToString() => $"{Title} (id {SheetId}, index {Index}{(Hidden ? ", hidden" : "")})";
    }
}
