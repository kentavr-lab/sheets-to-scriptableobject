using System;
using System.Collections.Generic;

namespace Kentavr.SheetsToSO.Core
{
    /// <summary>
    /// Result of <see cref="SheetSetChecker.Check"/>: parsed sheets, enums and all errors of a set of sheets.
    /// </summary>
    public sealed class SheetSetCheckResult
    {
        private static readonly IReadOnlyDictionary<string, Type> NoRowTypes = new Dictionary<string, Type>();

        public SheetSetCheckResult(IReadOnlyList<SheetParseResult> sheets, EnumMembersResult enums,
            IReadOnlyList<string> errors, IReadOnlyDictionary<string, Type> rowTypes = null)
        {
            Sheets = sheets ?? throw new ArgumentNullException(nameof(sheets));
            Enums = enums ?? throw new ArgumentNullException(nameof(enums));
            Errors = errors ?? throw new ArgumentNullException(nameof(errors));
            RowTypes = rowTypes ?? NoRowTypes;
        }

        /// <summary>
        /// Row classes of the sheets in schema-from-code mode, by sheet name (ordinal).
        /// Sheets in code generation mode are not listed.
        /// </summary>
        public IReadOnlyDictionary<string, Type> RowTypes { get; }

        /// <summary>Parsed sheets in input order; sheets without a name and repeated names are left out.</summary>
        public IReadOnlyList<SheetParseResult> Sheets { get; }

        /// <summary>
        /// Enums collected from the data of sheets in code generation mode: the enums to generate.
        /// C# enums of sheets in schema-from-code mode are not here. Empty when there are any errors;
        /// the errors of <see cref="EnumMembersResult.Errors"/> are already in <see cref="Errors"/>.
        /// </summary>
        public EnumMembersResult Enums { get; }

        /// <summary>Errors, with addresses like <c>Units!B7: …</c> where a cell is known.</summary>
        public IReadOnlyList<string> Errors { get; }

        public bool IsValid => Errors.Count == 0;
    }
}
