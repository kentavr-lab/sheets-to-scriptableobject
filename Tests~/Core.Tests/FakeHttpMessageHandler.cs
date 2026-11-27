using System;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Kentavr.SheetsToSO.Core.Tests
{
    /// <summary>
    /// Returns a preset response or throws a preset exception; never touches the network.
    /// </summary>
    internal sealed class FakeHttpMessageHandler : HttpMessageHandler
    {
        private readonly HttpStatusCode _status;
        private readonly byte[] _body;
        private readonly string _contentType;
        private readonly Exception _exception;

        private FakeHttpMessageHandler(HttpStatusCode status, byte[] body, string contentType, Exception exception)
        {
            _status = status;
            _body = body;
            _contentType = contentType;
            _exception = exception;
        }

        public Uri LastRequestUri { get; private set; }

        public HttpMethod LastRequestMethod { get; private set; }

        public int RequestCount { get; private set; }

        public static FakeHttpMessageHandler Respond(HttpStatusCode status, string body, string contentType) =>
            new FakeHttpMessageHandler(status, Encoding.UTF8.GetBytes(body), contentType, null);

        public static FakeHttpMessageHandler RespondBytes(HttpStatusCode status, byte[] body, string contentType) =>
            new FakeHttpMessageHandler(status, body, contentType, null);

        public static FakeHttpMessageHandler Throw(Exception exception) =>
            new FakeHttpMessageHandler(HttpStatusCode.OK, null, null, exception);

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            RequestCount++;
            LastRequestUri = request.RequestUri;
            LastRequestMethod = request.Method;

            cancellationToken.ThrowIfCancellationRequested();
            if (_exception != null)
                throw _exception;

            var content = new ByteArrayContent(_body ?? Array.Empty<byte>());
            content.Headers.ContentType = _contentType == null ? null : MediaTypeHeaderValue.Parse(_contentType);
            return Task.FromResult(new HttpResponseMessage(_status) { Content = content, RequestMessage = request });
        }
    }
}
