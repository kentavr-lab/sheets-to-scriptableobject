using System;
using System.Collections.Generic;

namespace Kentavr.SheetsToSO.Core
{
    /// <summary>
    /// Result of checking <c>ref&lt;&gt;</c> and <c>enum&lt;&gt;</c> values across a set of sheets.
    /// </summary>
    public sealed class CrossSheetResult
    {
        public CrossSheetResult(IReadOnlyList<SheetError> errors,
            IReadOnlyDictionary<string, IReadOnlyList<string>> collectedEnums)
        {
            Errors = errors ?? throw new ArgumentNullException(nameof(errors));
            CollectedEnums = collectedEnums ?? throw new ArgumentNullException(nameof(collectedEnums));
        }

        /// <summary>All errors: sheets in input order; within a sheet, unknown ref sheets first, then cells.</summary>
        public IReadOnlyList<SheetError> Errors { get; }

        /// <summary>
        /// Values of enums that were not given as known enums: unique valid values in order of first appearance.
        /// Ready to pass to <see cref="CodeGenerator.GenerateEnum"/>.
        /// </summary>
        public IReadOnlyDictionary<string, IReadOnlyList<string>> CollectedEnums { get; }

        public bool IsValid => Errors.Count == 0;
    }
}
