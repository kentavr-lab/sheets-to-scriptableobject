using System;
using System.Collections.Generic;

namespace Kentavr.SheetsToSO.Core
{
    /// <summary>
    /// Result of loading a sheet through the CSV export link.
    /// </summary>
    public sealed class CsvExportResult
    {
        public CsvExportResult(SourceError error, CsvReadResult csv, IReadOnlyList<SourceWarning> warnings)
        {
            if (error == null && csv == null)
                throw new ArgumentNullException(nameof(csv), "CSV is required when there is no error");

            Error = error;
            Csv = error == null ? csv : null;
            Warnings = warnings ?? throw new ArgumentNullException(nameof(warnings));
        }

        /// <summary>Loading error; <c>null</c> when the sheet was downloaded.</summary>
        public SourceError Error { get; }

        /// <summary>Parsed CSV; <c>null</c> when <see cref="Error"/> is set.</summary>
        public CsvReadResult Csv { get; }

        /// <summary>Warnings about the data; never <c>null</c>.</summary>
        public IReadOnlyList<SourceWarning> Warnings { get; }

        public bool IsValid => Error == null && Csv.IsValid;
    }
}
