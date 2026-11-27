using System;
using System.IO;
using System.Linq;
using Kentavr.SheetsToSO.Core;
using NUnit.Framework;

namespace Kentavr.SheetsToSO.Core.Tests
{
    public class CsvReaderTests
    {
        private static readonly CsvDelimiter[] Delimiters = { CsvDelimiter.Comma, CsvDelimiter.Tab };

        private static string Sep(CsvDelimiter delimiter) => delimiter == CsvDelimiter.Tab ? "\t" : ",";

        /// <summary>Replaces '|' with the delimiter, so one text works for CSV and TSV.</summary>
        private static CsvReadResult Read(string text, CsvDelimiter delimiter) =>
            CsvReader.Read(text.Replace("|", Sep(delimiter)), delimiter);

        private static CsvReadResult ReadValid(string text, CsvDelimiter delimiter)
        {
            var result = Read(text, delimiter);
            Assert.That(result.IsValid, Is.True, string.Join("; ", result.Errors));
            return result;
        }

        private static CsvError SingleError(CsvReadResult result)
        {
            Assert.That(result.Errors, Has.Count.EqualTo(1), string.Join("; ", result.Errors));
            return result.Errors[0];
        }

        private static string[][] Cells(CsvReadResult result) =>
            result.Rows.Select(r => r.Cells.ToArray()).ToArray();

        [Test]
        public void ReadsSimpleValues([ValueSource(nameof(Delimiters))] CsvDelimiter delimiter)
        {
            var result = ReadValid("id|name\na|b\n", delimiter);

            Assert.That(Cells(result), Is.EqualTo(new[] { new[] { "id", "name" }, new[] { "a", "b" } }));
            Assert.That(result.Rows.Select(r => r.RowNumber), Is.EqualTo(new[] { 1, 2 }));
            Assert.That(result.Rows.Select(r => r.LineNumber), Is.EqualTo(new[] { 1, 2 }));
        }

        [Test]
        public void ReadsAcceptanceExample()
        {
            var result = CsvReader.Read("id,name\r\na,\"x, \"\"y\"\"\"\r\n", CsvDelimiter.Comma);

            Assert.That(result.IsValid, Is.True, string.Join("; ", result.Errors));
            Assert.That(Cells(result), Is.EqualTo(new[] { new[] { "id", "name" }, new[] { "a", "x, \"y\"" } }));
            Assert.That(result.Rows.Select(r => r.RowNumber), Is.EqualTo(new[] { 1, 2 }));
        }

        [Test]
        public void QuotedCellKeepsDelimiterAndEscapedQuotes([ValueSource(nameof(Delimiters))] CsvDelimiter delimiter)
        {
            var result = ReadValid("\"a|b\"|\"say \"\"hi\"\"\"|\"\"\"\"", delimiter);

            Assert.That(Cells(result), Is.EqualTo(new[] { new[] { "a" + Sep(delimiter) + "b", "say \"hi\"", "\"" } }));
        }

        [TestCase("\n")]
        [TestCase("\r\n")]
        public void LineBreakInsideQuotedCellIsNormalized(string newLine)
        {
            foreach (var delimiter in Delimiters)
            {
                var result = ReadValid($"x|\"a{newLine}b\"{newLine}next|c{newLine}", delimiter);

                Assert.That(Cells(result), Is.EqualTo(new[] { new[] { "x", "a\nb" }, new[] { "next", "c" } }));
                Assert.That(result.Rows[0].RowNumber, Is.EqualTo(1));
                Assert.That(result.Rows[0].LineNumber, Is.EqualTo(1));
                Assert.That(result.Rows[1].RowNumber, Is.EqualTo(2));
                Assert.That(result.Rows[1].LineNumber, Is.EqualTo(3));
            }
        }

        [Test]
        public void SkipsByteOrderMark([ValueSource(nameof(Delimiters))] CsvDelimiter delimiter)
        {
            var result = ReadValid("﻿id|name", delimiter);

            Assert.That(result.Rows[0].Cells[0], Is.EqualTo("id"));
        }

        [Test]
        public void ByteOrderMarkOnlyAtStartIsSkipped()
        {
            var result = ReadValid("a|﻿b", CsvDelimiter.Comma);

            Assert.That(result.Rows[0].Cells[1], Is.EqualTo("﻿b"));
        }

        [Test]
        public void ReadsMixedLineEndings([ValueSource(nameof(Delimiters))] CsvDelimiter delimiter)
        {
            var result = ReadValid("a|1\r\nb|2\nc|3\r\n", delimiter);

            Assert.That(Cells(result), Is.EqualTo(new[] { new[] { "a", "1" }, new[] { "b", "2" }, new[] { "c", "3" } }));
            Assert.That(result.Rows.Select(r => r.LineNumber), Is.EqualTo(new[] { 1, 2, 3 }));
        }

        [Test]
        public void LoneCarriageReturnIsOrdinaryCharacter([ValueSource(nameof(Delimiters))] CsvDelimiter delimiter)
        {
            var result = ReadValid("a\rb|c\n\"d\re\"", delimiter);

            Assert.That(Cells(result), Is.EqualTo(new[] { new[] { "a\rb", "c" }, new[] { "d\re" } }));
            Assert.That(result.Rows[1].LineNumber, Is.EqualTo(2));
        }

        [Test]
        public void TrimsTrailingEmptyCells([ValueSource(nameof(Delimiters))] CsvDelimiter delimiter)
        {
            var result = ReadValid("a|b||\nc|\"\"|\"\"\n|d", delimiter);

            Assert.That(Cells(result), Is.EqualTo(new[] { new[] { "a", "b" }, new[] { "c" }, new[] { "", "d" } }));
        }

        [Test]
        public void WhitespaceCellsAreNotEmpty([ValueSource(nameof(Delimiters))] CsvDelimiter delimiter)
        {
            var result = ReadValid(" a | ", delimiter);

            Assert.That(Cells(result), Is.EqualTo(new[] { new[] { " a ", " " } }));
        }

        [Test]
        public void QuoteInsideUnquotedCellIsOrdinaryCharacter([ValueSource(nameof(Delimiters))] CsvDelimiter delimiter)
        {
            var result = ReadValid("a\"b|5\"", delimiter);

            Assert.That(Cells(result), Is.EqualTo(new[] { new[] { "a\"b", "5\"" } }));
        }

        [Test]
        public void TrailingEmptyRowsAreRemoved()
        {
            var result = ReadValid("a,b,,\n\n\n", CsvDelimiter.Comma);

            Assert.That(Cells(result), Is.EqualTo(new[] { new[] { "a", "b" } }));
        }

        [Test]
        public void EmptyRowsInTheMiddleAreKept([ValueSource(nameof(Delimiters))] CsvDelimiter delimiter)
        {
            var result = ReadValid("a\n\nb\n||\nc\n|\n\n", delimiter);

            Assert.That(result.Rows.Select(r => r.Cells.Count), Is.EqualTo(new[] { 1, 0, 1, 0, 1 }));
            Assert.That(result.Rows[2].Cells[0], Is.EqualTo("b"));
            Assert.That(result.Rows[2].RowNumber, Is.EqualTo(3));
            Assert.That(result.Rows[4].RowNumber, Is.EqualTo(5));
        }

        [Test]
        public void ReadsAcceptanceEmptyRowExample()
        {
            var result = ReadValid("a\n\nb", CsvDelimiter.Comma);

            Assert.That(result.Rows, Has.Count.EqualTo(3));
            Assert.That(result.Rows[1].Cells, Is.Empty);
            Assert.That(result.Rows[2].Cells, Is.EqualTo(new[] { "b" }));
            Assert.That(result.Rows[2].RowNumber, Is.EqualTo(3));
        }

        [TestCase("")]
        [TestCase("﻿")]
        [TestCase("\n")]
        [TestCase("\r\n\n\r\n")]
        [TestCase(",,\n,")]
        public void EmptyTextGivesNoRows(string text)
        {
            var result = ReadValid(text, CsvDelimiter.Comma);

            Assert.That(result.Rows, Is.Empty);
        }

        [Test]
        public void TextReaderOverloadMatchesString([ValueSource(nameof(Delimiters))] CsvDelimiter delimiter)
        {
            var text = "﻿id|name\r\n\"a\r\nb\"|c\n\n\"x\"y|\"open".Replace("|", Sep(delimiter));

            var fromString = CsvReader.Read(text, delimiter);
            var fromReader = CsvReader.Read(new StringReader(text), delimiter);

            Assert.That(Cells(fromReader), Is.EqualTo(Cells(fromString)));
            Assert.That(fromReader.Rows.Select(r => (r.RowNumber, r.LineNumber)),
                Is.EqualTo(fromString.Rows.Select(r => (r.RowNumber, r.LineNumber))));
            Assert.That(fromReader.Errors.Select(e => e.ToString()), Is.EqualTo(fromString.Errors.Select(e => e.ToString())));
            Assert.That(fromReader.Errors, Has.Count.EqualTo(2));
        }

        [Test]
        public void UnclosedQuoteIsReported([ValueSource(nameof(Delimiters))] CsvDelimiter delimiter)
        {
            var result = Read("id\nx\na|\"b\nc", delimiter);

            Assert.That(result.IsValid, Is.False);
            var error = SingleError(result);
            Assert.That(error.Code, Is.EqualTo(CsvErrorCode.UnclosedQuote));
            Assert.That(error.RowNumber, Is.EqualTo(3));
            Assert.That(error.LineNumber, Is.EqualTo(3));
            Assert.That(error.ToString(), Does.StartWith("line 3: "));
            Assert.That(result.Rows, Has.Count.EqualTo(3));
            Assert.That(result.Rows[2].Cells, Is.EqualTo(new[] { "a", "b\nc" }));
        }

        [Test]
        public void UnclosedQuoteAcceptanceExample()
        {
            var result = CsvReader.Read("a,\"b", CsvDelimiter.Comma);

            Assert.That(result.IsValid, Is.False);
            Assert.That(SingleError(result).Code, Is.EqualTo(CsvErrorCode.UnclosedQuote));
            Assert.That(Cells(result), Is.EqualTo(new[] { new[] { "a", "b" } }));
        }

        [Test]
        public void TextAfterClosingQuoteIsReported([ValueSource(nameof(Delimiters))] CsvDelimiter delimiter)
        {
            var result = Read("id\n\"a\nb\"x\"y|c\nd", delimiter);

            Assert.That(result.IsValid, Is.False);
            var error = SingleError(result);
            Assert.That(error.Code, Is.EqualTo(CsvErrorCode.TextAfterClosingQuote));
            Assert.That(error.RowNumber, Is.EqualTo(2));
            Assert.That(error.LineNumber, Is.EqualTo(3));
            Assert.That(Cells(result), Is.EqualTo(new[] { new[] { "id" }, new[] { "a\nbx\"y", "c" }, new[] { "d" } }));
            Assert.That(result.Rows[2].RowNumber, Is.EqualTo(3));
        }

        [Test]
        public void TextAfterClosingQuoteAcceptanceExample()
        {
            var result = CsvReader.Read("\"a\"x,b", CsvDelimiter.Comma);

            Assert.That(SingleError(result).Code, Is.EqualTo(CsvErrorCode.TextAfterClosingQuote));
            Assert.That(Cells(result), Is.EqualTo(new[] { new[] { "ax", "b" } }));
        }

        [Test]
        public void LoneCarriageReturnAfterClosingQuoteIsText()
        {
            var result = CsvReader.Read("\"a\"\rb,c", CsvDelimiter.Comma);

            Assert.That(SingleError(result).Code, Is.EqualTo(CsvErrorCode.TextAfterClosingQuote));
            Assert.That(Cells(result), Is.EqualTo(new[] { new[] { "a\rb", "c" } }));
        }

        [Test]
        public void CrLfAfterClosingQuoteEndsRecord()
        {
            var result = ReadValid("\"a\"\r\n\"b\"", CsvDelimiter.Comma);

            Assert.That(Cells(result), Is.EqualTo(new[] { new[] { "a" }, new[] { "b" } }));
            Assert.That(result.Rows[1].LineNumber, Is.EqualTo(2));
        }

        [Test]
        public void NullArgumentsThrow()
        {
            Assert.Throws<ArgumentNullException>(() => CsvReader.Read((string)null, CsvDelimiter.Comma));
            Assert.Throws<ArgumentNullException>(() => CsvReader.Read((TextReader)null, CsvDelimiter.Comma));
        }

        [Test]
        public void InvalidDelimiterThrows()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => CsvReader.Read("a", (CsvDelimiter)42));
            Assert.Throws<ArgumentOutOfRangeException>(() => CsvReader.Read(new StringReader("a"), (CsvDelimiter)42));
        }

        [Test]
        public void TabDelimiterTreatsCommaAsText()
        {
            var result = ReadValid("a,b\tc", CsvDelimiter.Tab);

            Assert.That(Cells(result), Is.EqualTo(new[] { new[] { "a,b", "c" } }));
        }

        [Test]
        public void TabInsideQuotesIsPartOfValue()
        {
            var result = ReadValid("\"a\tb\"\tc", CsvDelimiter.Tab);

            Assert.That(Cells(result), Is.EqualTo(new[] { new[] { "a\tb", "c" } }));
        }

        [Test]
        public void CommaDelimiterTreatsTabAsText()
        {
            var result = ReadValid("a\tb,c", CsvDelimiter.Comma);

            Assert.That(Cells(result), Is.EqualTo(new[] { new[] { "a\tb", "c" } }));
        }
    }
}
