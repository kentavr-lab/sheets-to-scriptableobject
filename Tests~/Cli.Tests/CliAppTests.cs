using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Kentavr.SheetsToSO.Core.Tests;
using NUnit.Framework;

namespace Kentavr.SheetsToSO.Cli.Tests
{
    public class CliAppTests
    {
        private const string ApiKey = "AIzaSecretTestKey123";
        private const string UnitsHeader = "id,name:string,hp:int,rarity:enum<Rarity>,weapon:ref<Weapons>";
        private const string WeaponsTsv = "id\tdamage:int\nsword\t10\nbow\t7\n";

        private string _dir;

        [SetUp]
        public void SetUp()
        {
            _dir = Path.Combine(Path.GetTempPath(), "sheets-to-so-cli-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_dir);
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(_dir))
                Directory.Delete(_dir, true);
        }

        // --- csv ---

        [Test]
        public async Task Csv_ValidFiles_ExitZero()
        {
            var units = Write("Units.csv", UnitsHeader + "\nknight,Knight,100,Common,sword\narcher,Archer,60,Rare,bow\n");
            var weapons = Write("Weapons.tsv", WeaponsTsv);

            var run = await RunAsync("--source", "csv", "--file", units, "--file", weapons);

            Assert.That(run.Code, Is.EqualTo(0), run.ToString());
            Assert.That(run.Out, Does.Contain("Sheets: 2, errors: 0, warnings: 0"));
            Assert.That(run.Err, Is.Empty);
        }

        [Test]
        public async Task Csv_BrokenRef_ExitOneWithAddress()
        {
            var units = Write("Units.csv", UnitsHeader + "\nknight,Knight,100,Common,axe\n");
            var weapons = Write("Weapons.tsv", WeaponsTsv);

            var run = await RunAsync("--source", "csv", "--file", units, "--file", weapons);

            Assert.That(run.Code, Is.EqualTo(1), run.ToString());
            Assert.That(run.Out, Does.Contain("error: Units!E2: "));
            Assert.That(run.Out, Does.Contain("errors: 1"));
        }

        [Test]
        public async Task Csv_WrongType_ExitOneWithAddress()
        {
            var units = Write("Units.csv", UnitsHeader + "\nknight,Knight,abc,Common,sword\n");
            var weapons = Write("Weapons.tsv", WeaponsTsv);

            var run = await RunAsync("--source", "csv", "--file", units, "--file", weapons);

            Assert.That(run.Code, Is.EqualTo(1), run.ToString());
            Assert.That(run.Out, Does.Contain("error: Units!C2: "));
        }

        [Test]
        public async Task Csv_UnclosedQuote_ExitOne()
        {
            var units = Write("Units.csv", "id,name:string\nknight,\"Knight\n");

            var run = await RunAsync("--source", "csv", "--file", units);

            Assert.That(run.Code, Is.EqualTo(1), run.ToString());
            Assert.That(run.Out, Does.Contain("error: Units line 2: "));
        }

        [Test]
        public async Task Csv_MissingFile_ExitTwo()
        {
            var run = await RunAsync("--source", "csv", "--file", Path.Combine(_dir, "Nope.csv"));

            Assert.That(run.Code, Is.EqualTo(2), run.ToString());
            Assert.That(run.Err, Does.Contain("error: file '").And.Contain("Nope.csv' not found"));
            Assert.That(run.Out, Is.Empty);
        }

        // --- api ---

        [Test]
        public async Task Api_AllSheetsExceptSkipped_ExitZero()
        {
            var handler = new QueueHttpMessageHandler()
                .Enqueue(HttpStatusCode.OK, @"{ ""sheets"": [
                    { ""properties"": { ""sheetId"": 0, ""title"": ""Units"", ""index"": 0 } },
                    { ""properties"": { ""sheetId"": 7, ""title"": ""#Notes"", ""index"": 1 } },
                    { ""properties"": { ""sheetId"": 9, ""title"": ""Weapons"", ""index"": 2 } } ] }")
                .Enqueue(HttpStatusCode.OK, @"{ ""valueRanges"": [
                    { ""values"": [ [""id"", ""hp:int"", ""weapon:ref<Weapons>""], [""knight"", 100, ""sword""] ] },
                    { ""values"": [ [""id"", ""damage:int""], [""sword"", 10] ] } ] }");

            var run = await RunAsync(handler, Env(ApiKey), "--source", "api", "--spreadsheet", "sheet1");

            Assert.That(run.Code, Is.EqualTo(0), run.ToString());
            Assert.That(run.Out, Does.Contain("Sheets: 2, errors: 0"));
            Assert.That(handler.RequestUris, Has.Count.EqualTo(2));
            var batchGet = Uri.UnescapeDataString(handler.RequestUris[1].Query);
            Assert.That(batchGet, Does.Contain("'Units'").And.Contain("'Weapons'"));
            Assert.That(batchGet, Does.Not.Contain("Notes"));
            AssertNoKey(run);
        }

        [Test]
        public async Task Api_ExplicitSheets_SkipsListing()
        {
            var handler = new QueueHttpMessageHandler()
                .Enqueue(HttpStatusCode.OK, @"{ ""valueRanges"": [ { ""values"": [ [""id"", ""hp:int""], [""a"", ""x""] ] } ] }");

            var run = await RunAsync(handler, Env(ApiKey), "--source", "api", "--spreadsheet", "sheet1", "--sheet", "Units");

            Assert.That(run.Code, Is.EqualTo(1), run.ToString());
            Assert.That(run.Out, Does.Contain("error: Units!B2: "));
            Assert.That(handler.RequestUris, Has.Count.EqualTo(1));
            AssertNoKey(run);
        }

        [Test]
        public async Task Api_Forbidden_ExitTwo()
        {
            var handler = new QueueHttpMessageHandler().Enqueue(HttpStatusCode.Forbidden,
                @"{ ""error"": { ""code"": 403, ""message"": ""The caller does not have permission (key " + ApiKey + @")"", ""status"": ""PERMISSION_DENIED"" } }");

            var run = await RunAsync(handler, Env(ApiKey), "--source", "api", "--spreadsheet", "sheet1");

            Assert.That(run.Code, Is.EqualTo(2), run.ToString());
            Assert.That(run.Err, Does.StartWith("error: "));
            Assert.That(run.Err, Does.Contain("Нет доступа к таблице"));
            AssertNoKey(run);
        }

        [Test]
        public async Task Api_KeyVariableNotSet_ExitTwo()
        {
            var handler = new QueueHttpMessageHandler();

            var run = await RunAsync(handler, Env(null), "--source", "api", "--spreadsheet", "sheet1");

            Assert.That(run.Code, Is.EqualTo(2), run.ToString());
            Assert.That(run.Err, Does.Contain("SHEETS_TO_SO_API_KEY"));
            Assert.That(handler.RequestUris, Is.Empty);
        }

        [Test]
        public async Task Api_CustomKeyVariable_IsRead()
        {
            var handler = new QueueHttpMessageHandler()
                .Enqueue(HttpStatusCode.OK, @"{ ""valueRanges"": [ { ""values"": [ [""id""], [""a""] ] } ] }");
            var env = new Dictionary<string, string> { ["MY_KEY"] = ApiKey };

            var run = await RunAsync(handler, name => env.TryGetValue(name, out var v) ? v : null,
                "--source", "api", "--spreadsheet", "s", "--sheet", "Units", "--api-key-env", "MY_KEY");

            Assert.That(run.Code, Is.EqualTo(0), run.ToString());
            AssertNoKey(run);
        }

        // --- export ---

        [Test]
        public async Task Export_Csv_ExitZero()
        {
            var handler = FakeHttpMessageHandler.Respond(HttpStatusCode.OK, "id,hp:int\nknight,100\n", "text/csv");

            var run = await RunAsync(handler, Env(null), "--source", "export", "--spreadsheet", "abc", "--sheet", "Units=0");

            Assert.That(run.Code, Is.EqualTo(0), run.ToString());
            Assert.That(run.Out, Does.Contain("Sheets: 1, errors: 0, warnings: 0"));
            Assert.That(handler.LastRequestUri.Query, Does.Contain("gid=0"));
        }

        [Test]
        public async Task Export_Html_ExitTwo()
        {
            var handler = FakeHttpMessageHandler.Respond(HttpStatusCode.OK, "<html><body>Sign in</body></html>", "text/html");

            var run = await RunAsync(handler, Env(null), "--source", "export", "--spreadsheet", "abc", "--sheet", "Units=0");

            Assert.That(run.Code, Is.EqualTo(2), run.ToString());
            Assert.That(run.Err, Does.StartWith("error: Units: ").And.Contain("HTML"));
        }

        [Test]
        public async Task Export_CommaInNumber_WarnsWithoutChangingCode()
        {
            var handler = FakeHttpMessageHandler.Respond(HttpStatusCode.OK,
                "id,speed:float,hp:int\na,1.5,1\nb,1.5,2\nc,1.5,\"3,5\"\n", "text/csv");

            var run = await RunAsync(handler, Env(null), "--source", "export", "--spreadsheet", "abc", "--sheet", "Units=0");

            Assert.That(run.Out, Does.Contain("warning: Units!C4: "));
            Assert.That(run.Out, Does.Contain("warnings: 1"));
            // "3,5" is also not an int, so this is a data error; the warning itself must not raise the code to 2.
            Assert.That(run.Code, Is.EqualTo(1), run.ToString());
        }

        [Test]
        public async Task Export_WarningOnly_ExitZero()
        {
            var handler = FakeHttpMessageHandler.Respond(HttpStatusCode.OK,
                "id,hp:int\na,1\n#draft,\"1,5\"\n", "text/csv");

            var run = await RunAsync(handler, Env(null), "--source", "export", "--spreadsheet", "abc", "--sheet", "Units=0");

            Assert.That(run.Code, Is.EqualTo(0), run.ToString());
            Assert.That(run.Out, Does.Contain("warnings: 1"));
        }

        // --- arguments ---

        [TestCase("--source", "csv", "--file", "a.csv", "--verbose")]
        [TestCase("--file", "a.csv")]
        [TestCase("--source", "export", "--spreadsheet", "abc", "--sheet", "Units")]
        [TestCase("--source", "api", "--spreadsheet", "abc", "--file", "a.csv")]
        [TestCase("--source", "export", "--spreadsheet", "a/b", "--sheet", "A=1", "--sheet", "B=2")]
        public async Task InvalidArguments_ExitTwoWithUsage(params string[] args)
        {
            var run = await RunAsync(args);

            Assert.That(run.Code, Is.EqualTo(2), run.ToString());
            Assert.That(run.Err, Does.StartWith("error: ").And.Contain("Usage:"));
            Assert.That(run.Out, Is.Empty);
        }

        [Test]
        public async Task Help_ExitZeroToStdout()
        {
            var run = await RunAsync("--help");

            Assert.That(run.Code, Is.EqualTo(0));
            Assert.That(run.Out, Does.Contain("Usage:"));
            Assert.That(run.Err, Is.Empty);
        }

        // --- helpers ---

        private string Write(string name, string text)
        {
            var path = Path.Combine(_dir, name);
            File.WriteAllText(path, text);
            return path;
        }

        private static Func<string, string> Env(string apiKey) =>
            name => name == "SHEETS_TO_SO_API_KEY" ? apiKey : null;

        private static Task<RunResult> RunAsync(params string[] args) =>
            RunAsync(new QueueHttpMessageHandler(), Env(null), args);

        private static async Task<RunResult> RunAsync(HttpMessageHandler handler, Func<string, string> env, params string[] args)
        {
            var stdout = new StringWriter();
            var stderr = new StringWriter();
            var code = await CliApp.RunAsync(args, stdout, stderr, env, handler);
            return new RunResult(code, stdout.ToString(), stderr.ToString());
        }

        private static void AssertNoKey(RunResult run)
        {
            Assert.That(run.Out, Does.Not.Contain(ApiKey));
            Assert.That(run.Err, Does.Not.Contain(ApiKey));
        }

        private sealed class RunResult
        {
            public RunResult(int code, string @out, string err)
            {
                Code = code;
                Out = @out;
                Err = err;
            }

            public int Code { get; }

            public string Out { get; }

            public string Err { get; }

            public override string ToString() => $"code {Code}\nstdout:\n{Out}\nstderr:\n{Err}";
        }
    }
}
