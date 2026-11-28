using System;
using System.Collections.Generic;

namespace Kentavr.SheetsToSO.Core
{
    /// <summary>
    /// Result of <see cref="SheetSetChecker.Check"/>: parsed sheets, enums and all errors of a set of sheets.
    /// </summary>
    public sealed class SheetSetCheckResult
    {
        public SheetSetCheckResult(IReadOnlyList<SheetParseResult> sheets, EnumMembersResult enums,
            IReadOnlyList<string> errors)
        {
            Sheets = sheets ?? throw new ArgumentNullException(nameof(sheets));
            Enums = enums ?? throw new ArgumentNullException(nameof(enums));
            Errors = errors ?? throw new ArgumentNullException(nameof(errors));
        }

        /// <summary>Parsed sheets in input order; sheets without a name and repeated names are left out.</summary>
        public IReadOnlyList<SheetParseResult> Sheets { get; }

        /// <summary>
        /// Enums collected from the data. Empty when there are any errors;
        /// the errors of <see cref="EnumMembersResult.Errors"/> are already in <see cref="Errors"/>.
        /// </summary>
        public EnumMembersResult Enums { get; }

        /// <summary>Errors, with addresses like <c>Units!B7: …</c> where a cell is known.</summary>
        public IReadOnlyList<string> Errors { get; }

        public bool IsValid => Errors.Count == 0;
    }
}
