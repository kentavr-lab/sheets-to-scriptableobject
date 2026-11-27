using System;

namespace Kentavr.SheetsToSO.Core
{
    /// <summary>
    /// Kind of a source warning.
    /// </summary>
    public enum SourceWarningCode
    {
        CommaInNumber,
    }

    /// <summary>
    /// Non-fatal problem found in loaded source data.
    /// </summary>
    public sealed class SourceWarning
    {
        public SourceWarning(SourceWarningCode code, string address, string message)
        {
            Code = code;
            Address = address ?? throw new ArgumentNullException(nameof(address));
            Message = message ?? throw new ArgumentNullException(nameof(message));
        }

        public SourceWarningCode Code { get; }

        /// <summary>Address of the first affected cell, e.g. <c>"C5"</c>.</summary>
        public string Address { get; }

        public string Message { get; }

        public override string ToString()
        {
            return Address + ": " + Message;
        }
    }
}
