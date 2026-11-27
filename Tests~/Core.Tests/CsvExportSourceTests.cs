using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Kentavr.SheetsToSO.Core;
using NUnit.Framework;

namespace Kentavr.SheetsToSO.Core.Tests
{
    public class CsvExportSourceTests
    {
        private const string Id = "1AbC-d_E2";
        private const string Gid = "123";
        private const string ExpectedUri = "https://docs.google.com/spreadsheets/d/1AbC-d_E2/export?format=csv&gid=123";

        private static async Task<CsvExportResult> LoadAsync(FakeHttpMessageHandler handler, CancellationToken token = default)
        {
            using (var client = new HttpClient(handler))
                return await new CsvExportSource(client).LoadAsync(Id, Gid, token);
        }

        private static Task<CsvExportResult> LoadCsvAsync(string body) =>
            LoadAsync(FakeHttpMessageHandler.Respond(HttpStatusCode.OK, body, "text/csv; charset=utf-8"));

        private static SourceError ErrorOf(CsvExportResult result)
        {
            Assert.That(result.Error, Is.Not.Null);
            Assert.That(result.Csv, Is.Null);
            Assert.That(result.Warnings, Is.Empty);
            Assert.That(result.IsValid, Is.False);
            return result.Error;
        }

        // BuildExportUri

        [Test]
        public void BuildExportUri_ReturnsExportLink()
        {
            Assert.That(CsvExportSource.BuildExportUri(Id, Gid).AbsoluteUri, Is.EqualTo(ExpectedUri));
        }

        [TestCase(null)]
        [TestCase("")]
        [TestCase("abc/def")]
        [TestCase("abc def")]
        [TestCase("abc?x")]
        public void BuildExportUri_InvalidId_Throws(string id)
        {
            Assert.Throws<ArgumentException>(() => CsvExportSource.BuildExportUri(id, Gid));
        }

        [TestCase(null)]
        [TestCase("")]
        [TestCase("12a")]
        [TestCase("1/2")]
        [TestCase("-1")]
        public void BuildExportUri_InvalidGid_Throws(string gid)
        {
            Assert.Throws<ArgumentException>(() => CsvExportSource.BuildExportUri(Id, gid));
        }

        [Test]
        public void Constructor_NullClient_Throws()
        {
            Assert.Throws<ArgumentNullException>(() => new CsvExportSource(null));
        }

        [Test]
        public void LoadAsync_InvalidGid_ThrowsArgumentException()
        {
            using (var client = new HttpClient(FakeHttpMessageHandler.Respond(HttpStatusCode.OK, "", "text/csv")))
            {
                var source = new CsvExportSource(client);
                Assert.ThrowsAsync<ArgumentException>(() => source.LoadAsync(Id, "x"));
            }
        }

        // Request

        [Test]
        public async Task LoadAsync_SendsGetToExportUri()
        {
            var handler = FakeHttpMessageHandler.Respond(HttpStatusCode.OK, "id\na\n", "text/csv");
            await LoadAsync(handler);

            Assert.That(handler.RequestCount, Is.EqualTo(1));
            Assert.That(handler.LastRequestMethod, Is.EqualTo(HttpMethod.Get));
            Assert.That(handler.LastRequestUri.AbsoluteUri, Is.EqualTo(ExpectedUri));
        }

        // CSV

        [Test]
        public async Task LoadAsync_Csv_ReturnsRows()
        {
            var result = await LoadCsvAsync("id,name\r\nsword,\"Меч \"\"Рассвет\"\", ур. 1\"\r\nbow,Лук\r\n");

            Assert.That(result.IsValid, Is.True);
            Assert.That(result.Error, Is.Null);
            Assert.That(result.Warnings, Is.Empty);
            Assert.That(result.Csv.Rows.Select(r => r.Cells.ToArray()), Is.EqualTo(new[]
            {
                new[] { "id", "name" },
                new[] { "sword", "Меч \"Рассвет\", ур. 1" },
                new[] { "bow", "Лук" },
            }));
        }

        [Test]
        public async Task LoadAsync_CsvWithBom_StripsBom()
        {
            var bytes = Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes("имя,hp:int\nМеч,5\n")).ToArray();
            var result = await LoadAsync(FakeHttpMessageHandler.RespondBytes(HttpStatusCode.OK, bytes, "text/csv"));

            Assert.That(result.IsValid, Is.True);
            Assert.That(result.Csv.Rows[0].Cells, Is.EqualTo(new[] { "имя", "hp:int" }));
            Assert.That(result.Csv.Rows[1].Cells, Is.EqualTo(new[] { "Меч", "5" }));
        }

        [Test]
        public async Task LoadAsync_CsvWithoutCharset_DecodesUtf8()
        {
            var result = await LoadAsync(FakeHttpMessageHandler.Respond(HttpStatusCode.OK, "id\nЁжик\n", null));

            Assert.That(result.IsValid, Is.True);
            Assert.That(result.Csv.Rows[1].Cells, Is.EqualTo(new[] { "Ёжик" }));
        }

        [Test]
        public async Task LoadAsync_CsvReadError_IsNotValidButHasCsv()
        {
            var result = await LoadCsvAsync("id\n\"open\n");

            Assert.That(result.Error, Is.Null);
            Assert.That(result.Csv, Is.Not.Null);
            Assert.That(result.Csv.IsValid, Is.False);
            Assert.That(result.IsValid, Is.False);
        }

        [Test]
        public async Task LoadAsync_EmptyBody_ReturnsNoRowsAndNoWarnings()
        {
            var result = await LoadCsvAsync("");

            Assert.That(result.IsValid, Is.True);
            Assert.That(result.Csv.Rows, Is.Empty);
            Assert.That(result.Warnings, Is.Empty);
        }

        // HTML

        [Test]
        public async Task LoadAsync_HtmlContentType_IsNotCsv()
        {
            var result = await LoadAsync(FakeHttpMessageHandler.Respond(HttpStatusCode.OK, "id\na\n", "text/html; charset=utf-8"));

            var error = ErrorOf(result);
            Assert.That(error.Code, Is.EqualTo(SourceErrorCode.NotCsv));
            Assert.That(error.Message, Does.Contain("Anyone with the link"));
            Assert.That(error.Message, Does.Contain(ExpectedUri));
        }

        [TestCase(null)]
        [TestCase("text/plain")]
        [TestCase("text/csv")]
        public async Task LoadAsync_HtmlBody_IsNotCsv(string contentType)
        {
            var body = "﻿ \r\n\t<!DOCTYPE html><html><head><title>Sign in</title></head></html>";
            var result = await LoadAsync(FakeHttpMessageHandler.Respond(HttpStatusCode.OK, body, contentType));

            var error = ErrorOf(result);
            Assert.That(error.Code, Is.EqualTo(SourceErrorCode.NotCsv));
            Assert.That(error.Message, Does.Contain("Anyone with the link"));
        }

        [Test]
        public async Task LoadAsync_LessThanInsideCsv_IsCsv()
        {
            var result = await LoadCsvAsync("id,expr\na,<b>\n");

            Assert.That(result.IsValid, Is.True);
            Assert.That(result.Csv.Rows[1].Cells, Is.EqualTo(new[] { "a", "<b>" }));
        }

        // HTTP status

        [Test]
        public async Task LoadAsync_Status400_IsInvalidGid()
        {
            var result = await LoadAsync(FakeHttpMessageHandler.Respond(HttpStatusCode.BadRequest, "<html></html>", "text/html"));

            var error = ErrorOf(result);
            Assert.That(error.Code, Is.EqualTo(SourceErrorCode.InvalidGid));
            Assert.That(error.Message, Does.Contain("'" + Gid + "'"));
            Assert.That(error.Message, Does.Contain(Id));
            Assert.That(error.Message, Does.Contain("400"));
        }

        [TestCase(HttpStatusCode.Unauthorized, true)]
        [TestCase(HttpStatusCode.Forbidden, true)]
        [TestCase(HttpStatusCode.NotFound, true)]
        [TestCase(HttpStatusCode.InternalServerError, false)]
        public async Task LoadAsync_ErrorStatus_IsHttpError(HttpStatusCode status, bool hasSharingHint)
        {
            var result = await LoadAsync(FakeHttpMessageHandler.Respond(status, "oops", "text/html"));

            var error = ErrorOf(result);
            Assert.That(error.Code, Is.EqualTo(SourceErrorCode.HttpError));
            Assert.That(error.Message, Does.StartWith("HTTP " + (int)status + " "));
            Assert.That(error.Message, Does.Contain(ExpectedUri));
            Assert.That(error.Message.Contains("Anyone with the link"), Is.EqualTo(hasSharingHint));
        }

        // Network

        [Test]
        public async Task LoadAsync_HttpRequestException_IsNetworkError()
        {
            var result = await LoadAsync(FakeHttpMessageHandler.Throw(new HttpRequestException("No such host is known")));

            var error = ErrorOf(result);
            Assert.That(error.Code, Is.EqualTo(SourceErrorCode.NetworkError));
            Assert.That(error.Message, Does.Contain("No such host is known"));
        }

        [Test]
        public async Task LoadAsync_Timeout_IsNetworkError()
        {
            var result = await LoadAsync(FakeHttpMessageHandler.Throw(new TaskCanceledException("The request was canceled due to the configured timeout")));

            var error = ErrorOf(result);
            Assert.That(error.Code, Is.EqualTo(SourceErrorCode.NetworkError));
            Assert.That(error.Message, Does.Contain("timed out"));
        }

        [Test]
        public void LoadAsync_CancelledToken_ThrowsOperationCanceled()
        {
            using (var cts = new CancellationTokenSource())
            {
                cts.Cancel();
                var handler = FakeHttpMessageHandler.Respond(HttpStatusCode.OK, "id\na\n", "text/csv");
                Assert.That(async () => await LoadAsync(handler, cts.Token), Throws.InstanceOf<OperationCanceledException>());
            }
        }

        // Locale warning

        [Test]
        public async Task LoadAsync_CommaInNumberColumn_WarnsOncePerColumn()
        {
            var result = await LoadCsvAsync(
                "id,hp:int,speed:float,name\n" +
                "a,1,1.5,x\n" +
                "b,2,\"1,5\",\"1,5\"\n" +
                "c,3,\"2,5\",y\n");

            Assert.That(result.IsValid, Is.True);
            Assert.That(result.Warnings, Has.Count.EqualTo(1), string.Join("; ", result.Warnings));
            var warning = result.Warnings[0];
            Assert.That(warning.Code, Is.EqualTo(SourceWarningCode.CommaInNumber));
            Assert.That(warning.Address, Is.EqualTo("C3"));
            Assert.That(warning.Message, Does.Contain("'speed'"));
            Assert.That(warning.Message, Does.Contain(" 2 value(s)"));
            Assert.That(warning.Message, Does.Contain("'1,5'"));
            Assert.That(warning.ToString(), Does.StartWith("C3: "));
        }

        [Test]
        public async Task LoadAsync_CommaInSeveralNumberColumns_WarnsForEach()
        {
            var result = await LoadCsvAsync(
                "id,hp:int,speed:float\n" +
                "a,\"1,000\",\"1,5\"\n");

            Assert.That(result.Warnings.Select(w => w.Address), Is.EqualTo(new[] { "B2", "C2" }));
        }

        [Test]
        public async Task LoadAsync_DotInNumbers_NoWarnings()
        {
            var result = await LoadCsvAsync(
                "id,hp:int,speed:float,name\n" +
                "a,1,1.5,\"x,y\"\n" +
                "b,2,0.25,z\n");

            Assert.That(result.Warnings, Is.Empty);
        }

        [Test]
        public async Task LoadAsync_CommaInFloatArray_Warns()
        {
            var result = await LoadCsvAsync(
                "id,weights:float[]\n" +
                "a,1.5|2.5\n" +
                "b,\"1,5|2,5\"\n");

            Assert.That(result.Warnings, Has.Count.EqualTo(1));
            Assert.That(result.Warnings[0].Address, Is.EqualTo("B3"));
            Assert.That(result.Warnings[0].Message, Does.Contain("'weights'"));
        }

        [Test]
        public async Task LoadAsync_RowsShorterThanColumn_AreSkipped()
        {
            var result = await LoadCsvAsync(
                "id,name,speed:float\n" +
                "a\n" +
                "\n" +
                "b,x,\"3,5\"\n");

            Assert.That(result.Warnings, Has.Count.EqualTo(1));
            Assert.That(result.Warnings[0].Address, Is.EqualTo("C4"));
        }

        [Test]
        public async Task LoadAsync_InvalidHeader_SkipsColumnsWithoutError()
        {
            var result = await LoadCsvAsync(
                "id,hp:wat,speed:float\n" +
                "a,\"1,5\",\"1,5\"\n");

            Assert.That(result.Error, Is.Null);
            Assert.That(result.Warnings.Select(w => w.Address), Is.EqualTo(new[] { "C2" }));
        }
    }
}
