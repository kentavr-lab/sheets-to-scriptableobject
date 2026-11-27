using System;

namespace Kentavr.SheetsToSO.Core
{
    /// <summary>
    /// Kind of a source loading error.
    /// </summary>
    public enum SourceErrorCode
    {
        InvalidGid,
        NotCsv,
        HttpError,
        NetworkError,
    }

    /// <summary>
    /// Error that prevented a source from returning data.
    /// </summary>
    public sealed class SourceError
    {
        public SourceError(SourceErrorCode code, string message)
        {
            Code = code;
            Message = message ?? throw new ArgumentNullException(nameof(message));
        }

        public SourceErrorCode Code { get; }

        public string Message { get; }

        public override string ToString()
        {
            return Code + ": " + Message;
        }
    }
}
