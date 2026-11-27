using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Kentavr.SheetsToSO.Cli.Tests
{
    /// <summary>
    /// Returns queued JSON responses in order, for flows with several requests (list tabs, then batchGet).
    /// </summary>
    internal sealed class QueueHttpMessageHandler : HttpMessageHandler
    {
        private readonly Queue<(HttpStatusCode Status, string Body)> _responses = new Queue<(HttpStatusCode, string)>();

        public List<Uri> RequestUris { get; } = new List<Uri>();

        public QueueHttpMessageHandler Enqueue(HttpStatusCode status, string body)
        {
            _responses.Enqueue((status, body));
            return this;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            RequestUris.Add(request.RequestUri);
            if (_responses.Count == 0)
                throw new InvalidOperationException("Unexpected request " + request.RequestUri);

            var (status, body) = _responses.Dequeue();
            return Task.FromResult(new HttpResponseMessage(status)
            {
                Content = new StringContent(body, Encoding.UTF8, "application/json"),
                RequestMessage = request,
            });
        }
    }
}
