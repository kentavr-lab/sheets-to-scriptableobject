using System;
using System.Threading;
using System.Threading.Tasks;

namespace Kentavr.SheetsToSO.Core
{
    /// <summary>Retry policy of <see cref="GoogleSheetsClient"/>.</summary>
    public sealed class GoogleSheetsRetryOptions
    {
        /// <summary>Pause after 429 when the response has no <c>Retry-After</c>. Sheets API quota is per minute.</summary>
        public TimeSpan QuotaWindow { get; set; } = TimeSpan.FromSeconds(60);

        /// <summary>How many times a 429 response is retried.</summary>
        public int MaxQuotaRetries { get; set; } = 3;

        /// <summary>First pause after 5xx; each next pause doubles.</summary>
        public TimeSpan InitialBackoff { get; set; } = TimeSpan.FromSeconds(1);

        /// <summary>Upper bound of a 5xx pause.</summary>
        public TimeSpan MaxBackoff { get; set; } = TimeSpan.FromSeconds(30);

        /// <summary>How many times a 5xx response is retried.</summary>
        public int MaxServerRetries { get; set; } = 5;

        /// <summary>Waits between retries; replaceable in tests.</summary>
        public Func<TimeSpan, CancellationToken, Task> Delay { get; set; } = Task.Delay;
    }
}
