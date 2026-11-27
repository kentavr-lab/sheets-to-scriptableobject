using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;

namespace Kentavr.SheetsToSO.Core.Tests
{
    public class GoogleSheetsClientTests
    {
        private const string ApiKey = "AIzaTestKey_123-secret";
        private const string SpreadsheetId = "1AbC-xyz_09";

        private FakeHandler _handler;
        private HttpClient _http;
        private List<TimeSpan> _delays;
        private GoogleSheetsClient _client;

        [SetUp]
        public void SetUp()
        {
            _handler = new FakeHandler();
            _http = new HttpClient(_handler);
            _delays = new List<TimeSpan>();
            _client = new GoogleSheetsClient(_http, ApiKey, new GoogleSheetsRetryOptions
            {
                Delay = (pause, ct) =>
                {
                    _delays.Add(pause);
                    return Task.CompletedTask;
                },
            });
        }

        /// <summary>Runs after every test: no request ever carries the key in its URL.</summary>
        [TearDown]
        public void KeyIsNeverInUrl()
        {
            _http.Dispose();
            foreach (var request in _handler.Requests)
            {
                Assert.That(request.Url, Does.Not.Contain(ApiKey));
                Assert.That(request.Url, Does.Not.Contain("key="));
                Assert.That(request.ApiKeyHeader, Is.EqualTo(ApiKey));
            }
        }

        // --- construction ---

        [TestCase(null)]
        [TestCase("")]
        [TestCase("  ")]
        public void RejectsEmptyKey(string key)
        {
            Assert.Throws<ArgumentException>(() => new GoogleSheetsClient(_http, key));
        }

        [Test]
        public void RejectsNullHttpClient()
        {
            Assert.Throws<ArgumentNullException>(() => new GoogleSheetsClient(null, ApiKey));
        }

        [TestCase(null)]
        [TestCase("")]
        [TestCase(" ")]
        public void RejectsEmptySpreadsheetId(string id)
        {
            Assert.ThrowsAsync<ArgumentException>(() => _client.GetSheetsAsync(id));
            Assert.ThrowsAsync<ArgumentException>(() => _client.GetValuesAsync(id, new[] { "A" }));
            Assert.That(_handler.Requests, Is.Empty);
        }

        [Test]
        public void DoesNotDisposeHttpClient()
        {
            _handler.Enqueue(HttpStatusCode.OK, "{\"sheets\":[]}");
            _handler.Enqueue(HttpStatusCode.OK, "{\"sheets\":[]}");

            Assert.DoesNotThrowAsync(() => _client.GetSheetsAsync(SpreadsheetId));
            Assert.DoesNotThrowAsync(() => _client.GetSheetsAsync(SpreadsheetId));
            Assert.That(_handler.Disposed, Is.False);
        }

        // --- spreadsheets.get ---

        [Test]
        public async Task GetSheetsReturnsPropertiesInResponseOrder()
        {
            _handler.Enqueue(HttpStatusCode.OK, @"{
              ""sheets"": [
                { ""properties"": { ""sheetId"": 0, ""title"": ""Units"", ""index"": 0 } },
                { ""properties"": { ""sheetId"": 123456789, ""title"": ""#Draft"", ""index"": 1, ""hidden"": true } },
                { ""properties"": { ""sheetId"": 42, ""title"": ""Оружие"", ""index"": 2, ""hidden"": false } }
              ]
            }");

            var sheets = await _client.GetSheetsAsync(SpreadsheetId);

            Assert.That(sheets.Select(s => (s.Title, s.SheetId, s.Index, s.Hidden)), Is.EqualTo(new[]
            {
                ("Units", 0, 0, false),
                ("#Draft", 123456789, 1, true),
                ("Оружие", 42, 2, false),
            }));
        }

        [Test]
        public async Task GetSheetsTreatsOmittedDefaultsAsZeroAndFalse()
        {
            _handler.Enqueue(HttpStatusCode.OK, "{\"sheets\":[{\"properties\":{\"title\":\"Only\"}}]}");

            var sheet = (await _client.GetSheetsAsync(SpreadsheetId)).Single();

            Assert.That((sheet.Title, sheet.SheetId, sheet.Index, sheet.Hidden), Is.EqualTo(("Only", 0, 0, false)));
        }

        [Test]
        public async Task GetSheetsRequestsOnlyNeededFields()
        {
            _handler.Enqueue(HttpStatusCode.OK, "{\"sheets\":[]}");

            await _client.GetSheetsAsync(SpreadsheetId);

            var request = _handler.Requests.Single();
            Assert.That(request.Method, Is.EqualTo(HttpMethod.Get));
            Assert.That(request.Url, Is.EqualTo(
                "https://sheets.googleapis.com/v4/spreadsheets/1AbC-xyz_09" +
                "?fields=sheets.properties%28sheetId%2Ctitle%2Cindex%2Chidden%29"));
            Assert.That(Uri.UnescapeDataString(request.Url), Does.EndWith(
                "?fields=sheets.properties(sheetId,title,index,hidden)"));
        }

        [Test]
        public async Task EscapesSpreadsheetId()
        {
            _handler.Enqueue(HttpStatusCode.OK, "{\"sheets\":[]}");

            await _client.GetSheetsAsync("a/b?c");

            Assert.That(_handler.Requests.Single().Url, Does.StartWith(
                "https://sheets.googleapis.com/v4/spreadsheets/a%2Fb%3Fc?"));
        }

        [TestCase("[]")]
        [TestCase("{\"sheets\":{}}")]
        [TestCase("{\"sheets\":[{}]}")]
        [TestCase("{\"sheets\":[{\"properties\":{\"sheetId\":1}}]}")]
        [TestCase("{\"sheets\":[{\"properties\":{\"title\":\"A\",\"sheetId\":\"1\"}}]}")]
        [TestCase("{\"sheets\":[{\"properties\":{\"title\":\"A\",\"index\":1.5}}]}")]
        [TestCase("{\"sheets\":[{\"properties\":{\"title\":\"A\",\"hidden\":\"yes\"}}]}")]
        public void GetSheetsRejectsUnexpectedStructure(string body)
        {
            _handler.Enqueue(HttpStatusCode.OK, body);

            var e = Assert.ThrowsAsync<GoogleSheetsException>(() => _client.GetSheetsAsync(SpreadsheetId));

            Assert.That(e.Code, Is.EqualTo(GoogleSheetsErrorCode.InvalidResponse));
        }

        [Test]
        public void BrokenJsonIsInvalidResponse()
        {
            _handler.Enqueue(HttpStatusCode.OK, "<html>not json</html>");

            var e = Assert.ThrowsAsync<GoogleSheetsException>(() => _client.GetSheetsAsync(SpreadsheetId));

            Assert.That(e.Code, Is.EqualTo(GoogleSheetsErrorCode.InvalidResponse));
            Assert.That(e.StatusCode, Is.Null);
            Assert.That(_handler.Requests, Has.Count.EqualTo(1));
        }

        // --- values:batchGet ---

        [Test]
        public async Task GetValuesSendsOneBatchRequestWithAllParameters()
        {
            _handler.Enqueue(HttpStatusCode.OK, ValueRanges(3));

            await _client.GetValuesAsync(SpreadsheetId, new[] { "Units", "Weapons", "Items" });

            var request = _handler.Requests.Single();
            Assert.That(request.Method, Is.EqualTo(HttpMethod.Get));
            Assert.That(request.Url, Is.EqualTo(
                "https://sheets.googleapis.com/v4/spreadsheets/1AbC-xyz_09/values:batchGet" +
                "?ranges=%27Units%27&ranges=%27Weapons%27&ranges=%27Items%27" +
                "&majorDimension=ROWS&valueRenderOption=UNFORMATTED_VALUE&dateTimeRenderOption=SERIAL_NUMBER"));
        }

        [TestCase("Units", "'Units'")]
        [TestCase("It's", "'It''s'")]
        [TestCase("''", "''''''")]
        [TestCase("My Units", "'My Units'")]
        [TestCase("Оружие и броня", "'Оружие и броня'")]
        [TestCase("A&B=C#1!", "'A&B=C#1!'")]
        public async Task QuotesAndEscapesSheetTitleInRange(string title, string range)
        {
            _handler.Enqueue(HttpStatusCode.OK, ValueRanges(1));

            await _client.GetValuesAsync(SpreadsheetId, new[] { title });

            var query = new Uri(_handler.Requests.Single().Url).Query;
            var rangeParam = query.TrimStart('?').Split('&').Single(p => p.StartsWith("ranges="));
            Assert.That(rangeParam, Is.EqualTo("ranges=" + Uri.EscapeDataString(range)));
            Assert.That(rangeParam, Does.Not.Contain(" ").And.Not.Contain("'").And.Not.Contain("&").And.Not.Contain("#"));
            Assert.That(Uri.UnescapeDataString(rangeParam.Substring("ranges=".Length)), Is.EqualTo(range));
        }

        [Test]
        public async Task EncodesSpaceAndCyrillicAsUtf8()
        {
            _handler.Enqueue(HttpStatusCode.OK, ValueRanges(1));

            await _client.GetValuesAsync(SpreadsheetId, new[] { "Я b" });

            Assert.That(_handler.Requests.Single().Url, Does.Contain("ranges=%27%D0%AF%20b%27&"));
        }

        [Test]
        public async Task GetValuesWithNoTitlesMakesNoRequest()
        {
            var result = await _client.GetValuesAsync(SpreadsheetId, Array.Empty<string>());

            Assert.That(result, Is.Empty);
            Assert.That(_handler.Requests, Is.Empty);
        }

        [Test]
        public void GetValuesRejectsNullOrEmptyTitles()
        {
            Assert.ThrowsAsync<ArgumentNullException>(() => _client.GetValuesAsync(SpreadsheetId, null));
            Assert.ThrowsAsync<ArgumentException>(() => _client.GetValuesAsync(SpreadsheetId, new[] { "A", "" }));
            Assert.ThrowsAsync<ArgumentException>(() => _client.GetValuesAsync(SpreadsheetId, new string[] { null }));
            Assert.That(_handler.Requests, Is.Empty);
        }

        [Test]
        public async Task GetValuesMatchesRangesByPosition()
        {
            _handler.Enqueue(HttpStatusCode.OK, @"{
              ""spreadsheetId"": ""1AbC-xyz_09"",
              ""valueRanges"": [
                { ""range"": ""'B'!A1:Z1000"", ""majorDimension"": ""ROWS"", ""values"": [[""b""]] },
                { ""range"": ""'A'!A1:Z1000"", ""majorDimension"": ""ROWS"", ""values"": [[""a""]] }
              ]
            }");

            var result = await _client.GetValuesAsync(SpreadsheetId, new[] { "B", "A" });

            Assert.That(result.Select(v => v.Title), Is.EqualTo(new[] { "B", "A" }));
            Assert.That(result.Select(v => v.Rows.Single().Cells.Single()), Is.EqualTo(new[] { "b", "a" }));
        }

        [Test]
        public async Task ConvertsScalarValuesToCsvStrings()
        {
            _handler.Enqueue(HttpStatusCode.OK, @"{ ""valueRanges"": [ { ""values"": [
                [""id"", ""hp:int"", ""speed:float"", ""stackable:bool"", ""born"", ""note""],
                [""sword_01"", 120, 3.5, true, 46349.5, null],
                [""shield"", -7, 1e-3, false, 0, "" spaced ""]
            ] } ] }");

            var rows = (await _client.GetValuesAsync(SpreadsheetId, new[] { "Units" })).Single().Rows;

            Assert.That(Cells(rows), Is.EqualTo(new[]
            {
                new[] { "id", "hp:int", "speed:float", "stackable:bool", "born", "note" },
                new[] { "sword_01", "120", "3.5", "TRUE", "46349.5" },
                new[] { "shield", "-7", "1e-3", "FALSE", "0", " spaced " },
            }));
        }

        [Test]
        public async Task TrimsLikeCsvReader()
        {
            _handler.Enqueue(HttpStatusCode.OK, @"{ ""valueRanges"": [ { ""values"": [
                [""id"", ""name"", """", null],
                [],
                [""a"", """", ""x""],
                [""""],
                [],
                [null, """"]
            ] } ] }");

            var rows = (await _client.GetValuesAsync(SpreadsheetId, new[] { "Units" })).Single().Rows;

            Assert.That(Cells(rows), Is.EqualTo(new[]
            {
                new[] { "id", "name" },
                new string[0],
                new[] { "a", "", "x" },
            }));
            Assert.That(rows.Select(r => r.RowNumber), Is.EqualTo(new[] { 1, 2, 3 }));
            Assert.That(rows.Select(r => r.LineNumber), Is.EqualTo(new[] { 1, 2, 3 }));
        }

        [Test]
        public async Task GivesSameRowsAsCsvReader()
        {
            _handler.Enqueue(HttpStatusCode.OK, @"{ ""valueRanges"": [ { ""values"": [
                [""id"", ""hp:int"", ""ok:bool""],
                [],
                [""a"", 1, true, """"],
                [""""]
            ] } ] }");
            var csv = CsvReader.Read("id,hp:int,ok:bool\n\na,1,TRUE,\n,\n", CsvDelimiter.Comma);

            var rows = (await _client.GetValuesAsync(SpreadsheetId, new[] { "Units" })).Single().Rows;

            Assert.That(Cells(rows), Is.EqualTo(Cells(csv.Rows)));
            Assert.That(rows.Select(r => r.RowNumber), Is.EqualTo(csv.Rows.Select(r => r.RowNumber)));
        }

        [Test]
        public async Task EmptySheetHasNoRows()
        {
            _handler.Enqueue(HttpStatusCode.OK,
                "{\"valueRanges\":[{\"range\":\"'Empty'!A1:Z1000\",\"majorDimension\":\"ROWS\"},{\"values\":[[\"x\"]]}]}");

            var result = await _client.GetValuesAsync(SpreadsheetId, new[] { "Empty", "Full" });

            Assert.That(result[0].Title, Is.EqualTo("Empty"));
            Assert.That(result[0].Rows, Is.Empty);
            Assert.That(result[1].Rows, Has.Count.EqualTo(1));
        }

        [TestCase("{}")]
        [TestCase("{\"valueRanges\":[]}")]
        [TestCase("{\"valueRanges\":[{}, {}]}")]
        [TestCase("{\"valueRanges\":[1]}")]
        [TestCase("{\"valueRanges\":[{\"values\":{}}]}")]
        [TestCase("{\"valueRanges\":[{\"values\":[\"row\"]}]}")]
        [TestCase("{\"valueRanges\":[{\"values\":[[[1]]]}]}")]
        [TestCase("{\"valueRanges\":[{\"values\":[[{}]]}]}")]
        public void GetValuesRejectsUnexpectedStructure(string body)
        {
            _handler.Enqueue(HttpStatusCode.OK, body);

            var e = Assert.ThrowsAsync<GoogleSheetsException>(() => _client.GetValuesAsync(SpreadsheetId, new[] { "A" }));

            Assert.That(e.Code, Is.EqualTo(GoogleSheetsErrorCode.InvalidResponse));
        }

        // --- errors without retries ---

        [Test]
        public void ForbiddenIsAccessDenied()
        {
            _handler.Enqueue(HttpStatusCode.Forbidden, GoogleError(403,
                "The caller does not have permission", "PERMISSION_DENIED"));

            var e = Assert.ThrowsAsync<GoogleSheetsException>(() => _client.GetSheetsAsync(SpreadsheetId));

            Assert.That(e.Code, Is.EqualTo(GoogleSheetsErrorCode.AccessDenied));
            Assert.That(e.StatusCode, Is.EqualTo(403));
            Assert.That(e.Message, Does.StartWith("Нет доступа к таблице."));
            Assert.That(e.Message, Does.Contain("просмотр").And.Contain("Google Sheets API"));
            Assert.That(e.Message, Does.EndWith("The caller does not have permission"));
            AssertNoRetry();
        }

        [Test]
        public void InvalidKeyIsReportedSeparately()
        {
            _handler.Enqueue(HttpStatusCode.BadRequest, @"{
              ""error"": {
                ""code"": 400,
                ""message"": ""API key not valid. Please pass a valid API key."",
                ""status"": ""INVALID_ARGUMENT"",
                ""details"": [
                  { ""@type"": ""type.googleapis.com/google.rpc.ErrorInfo"", ""reason"": ""API_KEY_INVALID"",
                    ""domain"": ""googleapis.com"", ""metadata"": { ""service"": ""sheets.googleapis.com"" } }
                ]
              }
            }");

            var e = Assert.ThrowsAsync<GoogleSheetsException>(() => _client.GetSheetsAsync(SpreadsheetId));

            Assert.That(e.Code, Is.EqualTo(GoogleSheetsErrorCode.InvalidApiKey));
            Assert.That(e.StatusCode, Is.EqualTo(400));
            Assert.That(e.Message, Does.StartWith("API-ключ не принят Google. Проверьте ключ"));
            AssertNoRetry();
        }

        [Test]
        public void NotFoundNamesSpreadsheet()
        {
            _handler.Enqueue(HttpStatusCode.NotFound, GoogleError(404, "Requested entity was not found.", "NOT_FOUND"));

            var e = Assert.ThrowsAsync<GoogleSheetsException>(() => _client.GetSheetsAsync(SpreadsheetId));

            Assert.That(e.Code, Is.EqualTo(GoogleSheetsErrorCode.NotFound));
            Assert.That(e.StatusCode, Is.EqualTo(404));
            Assert.That(e.Message, Does.StartWith($"Таблица {SpreadsheetId} не найдена. Проверьте ID"));
            AssertNoRetry();
        }

        [Test]
        public void OtherBadRequestIsBadRequest()
        {
            _handler.Enqueue(HttpStatusCode.BadRequest,
                GoogleError(400, "Unable to parse range: 'Missing'", "INVALID_ARGUMENT"));

            var e = Assert.ThrowsAsync<GoogleSheetsException>(() => _client.GetValuesAsync(SpreadsheetId, new[] { "Missing" }));

            Assert.That(e.Code, Is.EqualTo(GoogleSheetsErrorCode.BadRequest));
            Assert.That(e.StatusCode, Is.EqualTo(400));
            Assert.That(e.Message, Does.EndWith("Unable to parse range: 'Missing'"));
            AssertNoRetry();
        }

        [Test]
        public void ErrorWithNonJsonBodyStillHasMessage()
        {
            _handler.Enqueue(HttpStatusCode.Forbidden, "<html>Forbidden</html>");

            var e = Assert.ThrowsAsync<GoogleSheetsException>(() => _client.GetSheetsAsync(SpreadsheetId));

            Assert.That(e.Code, Is.EqualTo(GoogleSheetsErrorCode.AccessDenied));
            Assert.That(e.Message, Does.StartWith("Нет доступа к таблице."));
            Assert.That(e.Message, Does.Not.Contain("html"));
        }

        [Test]
        public void NetworkErrorsAreNotRetried()
        {
            _handler.Enqueue(_ => throw new HttpRequestException("connection refused"));

            Assert.ThrowsAsync<HttpRequestException>(() => _client.GetSheetsAsync(SpreadsheetId));
            AssertNoRetry();
        }

        // --- API key secrecy ---

        [TestCase(HttpStatusCode.BadRequest)]
        [TestCase(HttpStatusCode.Forbidden)]
        [TestCase(HttpStatusCode.NotFound)]
        [TestCase(HttpStatusCode.TooManyRequests)]
        [TestCase(HttpStatusCode.ServiceUnavailable)]
        public void KeyEchoedByGoogleIsRemovedFromException(HttpStatusCode status)
        {
            for (var i = 0; i < 10; i++)
                _handler.Enqueue(status, GoogleError((int)status, $"API key {ApiKey} rejected, key={ApiKey}", "X"));

            var e = Assert.ThrowsAsync<GoogleSheetsException>(() => _client.GetSheetsAsync(SpreadsheetId));

            Assert.That(e.Message, Does.Not.Contain(ApiKey));
            Assert.That(e.ToString(), Does.Not.Contain(ApiKey));
            Assert.That(e.Message, Does.Contain("API key *** rejected"));
        }

        [Test]
        public void KeyInSuccessfulButBrokenResponseIsRemovedFromException()
        {
            _handler.Enqueue(HttpStatusCode.OK, "{\"sheets\":[{\"properties\":{\"title\":\"A\",\"" + ApiKey + "\":1,\"sheetId\":\"" + ApiKey + "\"}}]}");

            var e = Assert.ThrowsAsync<GoogleSheetsException>(() => _client.GetSheetsAsync(SpreadsheetId));

            Assert.That(e.ToString(), Does.Not.Contain(ApiKey));
        }

        [Test]
        public async Task KeyIsSentAsHeaderOnEveryRequest()
        {
            _handler.Enqueue(HttpStatusCode.ServiceUnavailable, "");
            _handler.Enqueue(HttpStatusCode.TooManyRequests, "");
            _handler.Enqueue(HttpStatusCode.OK, "{\"sheets\":[]}");
            _handler.Enqueue(HttpStatusCode.OK, ValueRanges(1));

            await _client.GetSheetsAsync(SpreadsheetId);
            await _client.GetValuesAsync(SpreadsheetId, new[] { "A" });

            Assert.That(_handler.Requests, Has.Count.EqualTo(4));
            Assert.That(_handler.Requests.Select(r => r.ApiKeyHeader), Is.All.EqualTo(ApiKey));
            // KeyIsNeverInUrl (TearDown) checks the URLs.
        }

        // --- retries ---

        [Test]
        public async Task QuotaRetryWaitsRetryAfterSeconds()
        {
            _handler.Enqueue(TooManyRequests(r => r.Headers.RetryAfter = new System.Net.Http.Headers.RetryConditionHeaderValue(TimeSpan.FromSeconds(17))));
            _handler.Enqueue(HttpStatusCode.OK, "{\"sheets\":[]}");

            await _client.GetSheetsAsync(SpreadsheetId);

            Assert.That(_delays, Is.EqualTo(new[] { TimeSpan.FromSeconds(17) }));
            Assert.That(_handler.Requests, Has.Count.EqualTo(2));
        }

        [Test]
        public async Task QuotaRetryWaitsUntilRetryAfterDate()
        {
            var until = DateTimeOffset.UtcNow.AddSeconds(30);
            _handler.Enqueue(TooManyRequests(r => r.Headers.RetryAfter = new System.Net.Http.Headers.RetryConditionHeaderValue(until)));
            _handler.Enqueue(HttpStatusCode.OK, "{\"sheets\":[]}");

            await _client.GetSheetsAsync(SpreadsheetId);

            Assert.That(_delays.Single(), Is.InRange(TimeSpan.FromSeconds(25), TimeSpan.FromSeconds(30)));
        }

        [Test]
        public async Task QuotaRetryWithPastRetryAfterDateDoesNotWait()
        {
            var past = DateTimeOffset.UtcNow.AddMinutes(-5);
            _handler.Enqueue(TooManyRequests(r => r.Headers.RetryAfter = new System.Net.Http.Headers.RetryConditionHeaderValue(past)));
            _handler.Enqueue(HttpStatusCode.OK, "{\"sheets\":[]}");

            await _client.GetSheetsAsync(SpreadsheetId);

            Assert.That(_delays, Is.EqualTo(new[] { TimeSpan.Zero }));
        }

        [Test]
        public async Task QuotaRetryWithoutRetryAfterWaitsQuotaWindow()
        {
            _handler.Enqueue(HttpStatusCode.TooManyRequests, GoogleError(429, "Quota exceeded", "RESOURCE_EXHAUSTED"));
            _handler.Enqueue(HttpStatusCode.TooManyRequests, "");
            _handler.Enqueue(HttpStatusCode.OK, ValueRanges(1));

            await _client.GetValuesAsync(SpreadsheetId, new[] { "A" });

            Assert.That(_delays, Is.EqualTo(new[] { TimeSpan.FromSeconds(60), TimeSpan.FromSeconds(60) }));
        }

        [Test]
        public void QuotaRetriesExhaustedIsQuotaExceeded()
        {
            for (var i = 0; i < 4; i++)
                _handler.Enqueue(HttpStatusCode.TooManyRequests, GoogleError(429, "Quota exceeded for quota metric", "RESOURCE_EXHAUSTED"));

            var e = Assert.ThrowsAsync<GoogleSheetsException>(() => _client.GetSheetsAsync(SpreadsheetId));

            Assert.That(e.Code, Is.EqualTo(GoogleSheetsErrorCode.QuotaExceeded));
            Assert.That(e.StatusCode, Is.EqualTo(429));
            Assert.That(e.Message, Does.StartWith("Превышена квота"));
            Assert.That(_handler.Requests, Has.Count.EqualTo(4));
            Assert.That(_delays, Has.Count.EqualTo(3));
            Assert.That(_handler.Pending, Is.Zero);
        }

        [Test]
        public async Task ServerErrorRetrySucceeds()
        {
            _handler.Enqueue(HttpStatusCode.InternalServerError, "");
            _handler.Enqueue(HttpStatusCode.BadGateway, "");
            _handler.Enqueue(HttpStatusCode.OK, "{\"sheets\":[{\"properties\":{\"title\":\"A\"}}]}");

            var sheets = await _client.GetSheetsAsync(SpreadsheetId);

            Assert.That(sheets.Single().Title, Is.EqualTo("A"));
            Assert.That(_delays, Is.EqualTo(new[] { TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(2) }));
        }

        [Test]
        public void ServerErrorBackoffDoublesUpToCapThenFails()
        {
            for (var i = 0; i < 6; i++)
                _handler.Enqueue(HttpStatusCode.ServiceUnavailable, GoogleError(503, "The service is currently unavailable.", "UNAVAILABLE"));

            var e = Assert.ThrowsAsync<GoogleSheetsException>(() => _client.GetSheetsAsync(SpreadsheetId));

            Assert.That(e.Code, Is.EqualTo(GoogleSheetsErrorCode.ServerError));
            Assert.That(e.StatusCode, Is.EqualTo(503));
            Assert.That(e.Message, Does.Contain("503").And.EndWith("The service is currently unavailable."));
            Assert.That(_handler.Requests, Has.Count.EqualTo(6));
            Assert.That(_delays.Select(d => d.TotalSeconds), Is.EqualTo(new double[] { 1, 2, 4, 8, 16 }));
        }

        [Test]
        public void BackoffIsCappedByMaxBackoff()
        {
            var client = new GoogleSheetsClient(_http, ApiKey, new GoogleSheetsRetryOptions
            {
                InitialBackoff = TimeSpan.FromSeconds(3),
                MaxBackoff = TimeSpan.FromSeconds(10),
                MaxServerRetries = 4,
                Delay = (pause, ct) =>
                {
                    _delays.Add(pause);
                    return Task.CompletedTask;
                },
            });
            for (var i = 0; i < 5; i++)
                _handler.Enqueue(HttpStatusCode.InternalServerError, "");

            var e = Assert.ThrowsAsync<GoogleSheetsException>(() => client.GetSheetsAsync(SpreadsheetId));

            Assert.That(e.Code, Is.EqualTo(GoogleSheetsErrorCode.ServerError));
            Assert.That(_delays.Select(d => d.TotalSeconds), Is.EqualTo(new double[] { 3, 6, 10, 10 }));
        }

        [Test]
        public void DefaultBackoffReachesThirtySecondCap()
        {
            var options = new GoogleSheetsRetryOptions();
            Assert.That(options.InitialBackoff, Is.EqualTo(TimeSpan.FromSeconds(1)));
            Assert.That(options.MaxBackoff, Is.EqualTo(TimeSpan.FromSeconds(30)));
            Assert.That(options.MaxServerRetries, Is.EqualTo(5));
            Assert.That(options.QuotaWindow, Is.EqualTo(TimeSpan.FromSeconds(60)));
            Assert.That(options.MaxQuotaRetries, Is.EqualTo(3));
            Assert.That(options.Delay, Is.Not.Null);
        }

        [Test]
        public void QuotaAndServerRetriesAreCountedSeparately()
        {
            _handler.Enqueue(HttpStatusCode.TooManyRequests, "");
            _handler.Enqueue(HttpStatusCode.InternalServerError, "");
            _handler.Enqueue(HttpStatusCode.TooManyRequests, "");
            _handler.Enqueue(HttpStatusCode.InternalServerError, "");
            _handler.Enqueue(HttpStatusCode.OK, "{\"sheets\":[]}");

            Assert.DoesNotThrowAsync(() => _client.GetSheetsAsync(SpreadsheetId));
            Assert.That(_delays.Select(d => d.TotalSeconds), Is.EqualTo(new double[] { 60, 1, 60, 2 }));
        }

        // --- cancellation ---

        [Test]
        public void CancellationInterruptsPause()
        {
            using var cts = new CancellationTokenSource();
            var client = new GoogleSheetsClient(_http, ApiKey, new GoogleSheetsRetryOptions
            {
                Delay = (pause, ct) =>
                {
                    cts.Cancel();
                    return Task.Delay(pause, ct);
                },
            });
            _handler.Enqueue(HttpStatusCode.ServiceUnavailable, "");

            Assert.CatchAsync<OperationCanceledException>(() => client.GetSheetsAsync(SpreadsheetId, cts.Token));
            Assert.That(_handler.Requests, Has.Count.EqualTo(1));
        }

        [Test]
        public void CancellationIsPassedToRequest()
        {
            using var cts = new CancellationTokenSource();
            _handler.Enqueue(_ =>
            {
                cts.Cancel();
                return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("{\"sheets\":[]}") };
            });
            _handler.Enqueue(HttpStatusCode.OK, "{\"sheets\":[]}");

            Assert.CatchAsync<OperationCanceledException>(() => _client.GetSheetsAsync(SpreadsheetId, cts.Token));
        }

        [Test]
        public void AlreadyCancelledTokenSendsNothing()
        {
            using var cts = new CancellationTokenSource();
            cts.Cancel();

            Assert.CatchAsync<OperationCanceledException>(() => _client.GetSheetsAsync(SpreadsheetId, cts.Token));
            Assert.That(_handler.Requests, Is.Empty);
        }

        // --- helpers ---

        private void AssertNoRetry()
        {
            Assert.That(_handler.Requests, Has.Count.EqualTo(1));
            Assert.That(_delays, Is.Empty);
        }

        private static string[][] Cells(IEnumerable<CsvRow> rows) => rows.Select(r => r.Cells.ToArray()).ToArray();

        private static string ValueRanges(int count) =>
            "{\"valueRanges\":[" + string.Join(",", Enumerable.Repeat("{\"values\":[[\"id\"]]}", count)) + "]}";

        private static string GoogleError(int code, string message, string status) =>
            "{\"error\":{\"code\":" + code + ",\"message\":\"" + message.Replace("\"", "\\\"") +
            "\",\"status\":\"" + status + "\"}}";

        private static Func<HttpRequestMessage, HttpResponseMessage> TooManyRequests(Action<HttpResponseMessage> setup) =>
            _ =>
            {
                var response = new HttpResponseMessage(HttpStatusCode.TooManyRequests) { Content = new StringContent("") };
                setup(response);
                return response;
            };

        private sealed class RecordedRequest
        {
            public HttpMethod Method;
            public string Url;
            public string ApiKeyHeader;
        }

        /// <summary>Answers from a queue and records requests. Never touches the network.</summary>
        private sealed class FakeHandler : HttpMessageHandler
        {
            private readonly Queue<Func<HttpRequestMessage, HttpResponseMessage>> _responses =
                new Queue<Func<HttpRequestMessage, HttpResponseMessage>>();

            public List<RecordedRequest> Requests { get; } = new List<RecordedRequest>();

            public bool Disposed { get; private set; }

            public int Pending => _responses.Count;

            public void Enqueue(HttpStatusCode status, string body) =>
                _responses.Enqueue(_ => new HttpResponseMessage(status)
                {
                    Content = new StringContent(body, Encoding.UTF8, "application/json"),
                });

            public void Enqueue(Func<HttpRequestMessage, HttpResponseMessage> respond) => _responses.Enqueue(respond);

            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
            {
                ct.ThrowIfCancellationRequested();
                Requests.Add(new RecordedRequest
                {
                    Method = request.Method,
                    Url = request.RequestUri.AbsoluteUri,
                    ApiKeyHeader = request.Headers.TryGetValues("X-goog-api-key", out var values)
                        ? string.Join(",", values)
                        : null,
                });

                if (_responses.Count == 0)
                    throw new InvalidOperationException("Unexpected request: " + request.RequestUri);

                var response = _responses.Dequeue()(request);
                ct.ThrowIfCancellationRequested();
                return Task.FromResult(response);
            }

            protected override void Dispose(bool disposing)
            {
                Disposed = true;
                base.Dispose(disposing);
            }
        }
    }
}
