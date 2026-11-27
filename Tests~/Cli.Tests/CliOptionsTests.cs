using Kentavr.SheetsToSO.Core;
using NUnit.Framework;

namespace Kentavr.SheetsToSO.Cli.Tests
{
    public class CliOptionsTests
    {
        [Test]
        public void Csv_FilesBecomeSheetsNamedAfterFile()
        {
            var options = CliOptions.Parse(new[] { "--source", "csv", "--file", "data/Units.csv", "--file", "Weapons.TSV" },
                out var error);

            Assert.That(error, Is.Null);
            Assert.That(options.Source, Is.EqualTo(SourceKind.Csv));
            Assert.That(options.Sheets, Has.Count.EqualTo(2));
            Assert.That(options.Sheets[0].Name, Is.EqualTo("Units"));
            Assert.That(options.Sheets[0].Delimiter, Is.EqualTo(CsvDelimiter.Comma));
            Assert.That(options.Sheets[1].Name, Is.EqualTo("Weapons"));
            Assert.That(options.Sheets[1].Delimiter, Is.EqualTo(CsvDelimiter.Tab));
        }

        [Test]
        public void Api_DefaultsKeyVariable()
        {
            var options = CliOptions.Parse(new[] { "--source", "api", "--spreadsheet", "abc" }, out var error);

            Assert.That(error, Is.Null);
            Assert.That(options.ApiKeyVariable, Is.EqualTo("SHEETS_TO_SO_API_KEY"));
            Assert.That(options.Sheets, Is.Empty);
        }

        [Test]
        public void Api_CustomKeyVariable()
        {
            var options = CliOptions.Parse(new[] { "--source", "api", "--spreadsheet", "abc", "--api-key-env", "MY_KEY" },
                out _);

            Assert.That(options.ApiKeyVariable, Is.EqualTo("MY_KEY"));
        }

        [Test]
        public void Export_SheetIsNameEqualsGid()
        {
            var options = CliOptions.Parse(new[] { "--source", "export", "--spreadsheet", "abc", "--sheet", "Units=123" },
                out var error);

            Assert.That(error, Is.Null);
            Assert.That(options.Sheets[0].Name, Is.EqualTo("Units"));
            Assert.That(options.Sheets[0].Gid, Is.EqualTo("123"));
        }

        [TestCase("--source", "csv", "--file", "a.csv", "--verbose")]
        [TestCase("--file", "a.csv")]
        [TestCase("--source", "xml", "--file", "a.csv")]
        [TestCase("--source", "csv")]
        [TestCase("--source", "csv", "--file")]
        [TestCase("--source", "csv", "--file", "a.csv", "--file", "dir/a.tsv")]
        [TestCase("--source", "api", "--spreadsheet", "abc", "--file", "a.csv")]
        [TestCase("--source", "api")]
        [TestCase("--source", "export", "--spreadsheet", "abc", "--sheet", "Units")]
        [TestCase("--source", "export", "--spreadsheet", "abc", "--sheet", "Units=x1")]
        [TestCase("--source", "export", "--spreadsheet", "abc", "--sheet", "=1")]
        [TestCase("--source", "export", "--spreadsheet", "abc")]
        [TestCase("--source", "export", "--spreadsheet", "abc", "--sheet", "A=1", "--api-key-env", "K")]
        [TestCase("--source", "csv", "--source", "csv", "--file", "a.csv")]
        public void InvalidArguments_ReturnError(params string[] args)
        {
            var options = CliOptions.Parse(args, out var error);

            Assert.That(options, Is.Null);
            Assert.That(error, Is.Not.Empty);
        }

        [Test]
        public void Help_WinsOverOtherArguments()
        {
            var options = CliOptions.Parse(new[] { "--source", "xml", "--help" }, out var error);

            Assert.That(error, Is.Null);
            Assert.That(options.Help, Is.True);
        }
    }
}
