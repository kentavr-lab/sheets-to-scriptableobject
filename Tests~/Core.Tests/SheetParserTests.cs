using System;
using System.Collections.Generic;
using System.Linq;
using Kentavr.SheetsToSO.Core;
using NUnit.Framework;

namespace Kentavr.SheetsToSO.Core.Tests
{
    public class SheetParserTests
    {
        private static readonly string[] UnitsHeader = { "id", "name:string!", "hp:int", "tags:string[]", "#notes" };

        private static IReadOnlyList<IReadOnlyList<string>> Sheet(params string[][] rows) => rows;

        private static SheetParseResult Parse(params string[][] rows) => SheetParser.Parse("Units", Sheet(rows));

        private static SheetParseResult ParseValid(params string[][] rows)
        {
            var result = Parse(rows);
            Assert.That(result.IsValid, Is.True, string.Join("; ", result.Errors));
            return result;
        }

        private static SheetError SingleError(SheetParseResult result)
        {
            Assert.That(result.Errors, Has.Count.EqualTo(1), string.Join("; ", result.Errors));
            return result.Errors[0];
        }

        private static SheetParseResult ParseIds(params string[] ids)
        {
            var rows = new List<string[]> { new[] { "id", "hp:int" } };
            rows.AddRange(ids.Select(id => new[] { id, "1" }));
            return Parse(rows.ToArray());
        }

        [Test]
        public void ParsesValidSheet()
        {
            var result = ParseValid(
                UnitsHeader,
                new[] { "sword_01", "Sword", "120", "melee|starter", "note" },
                new[] { "axe_02", "Axe", "", "", "" },
                new[] { "bow_03", "Bow", "-5", "ranged" });

            Assert.That(result.SheetName, Is.EqualTo("Units"));
            Assert.That(result.Columns.Select(c => c.Name), Is.EqualTo(new[] { "id", "name", "hp", "tags" }));
            Assert.That(result.Rows.Select(r => r.Id), Is.EqualTo(new[] { "sword_01", "axe_02", "bow_03" }));
            Assert.That(result.Rows.Select(r => r.RowNumber), Is.EqualTo(new[] { 2, 3, 4 }));

            var sword = result.Rows[0];
            Assert.That(sword.Values, Has.Count.EqualTo(4));
            Assert.That(sword.Values["id"], Is.EqualTo("sword_01"));
            Assert.That(sword.Values["name"], Is.EqualTo("Sword"));
            Assert.That(sword.Values["hp"], Is.EqualTo(120));
            Assert.That(sword.Values["tags"], Is.EqualTo(new[] { "melee", "starter" }));
            Assert.That(sword.Values.ContainsKey("#notes"), Is.False);

            Assert.That(result.Rows[1].Values["hp"], Is.EqualTo(0));
            Assert.That(result.Rows[1].Values["tags"], Is.EqualTo(Array.Empty<string>()));
            Assert.That(result.Rows[2].Values["hp"], Is.EqualTo(-5));
        }

        [Test]
        public void SkipsCommentRows()
        {
            var result = ParseValid(
                UnitsHeader,
                new[] { "#draft", "", "abc", "" },
                new[] { " #draft", "Draft", "1.5", "a||b" },
                new[] { "sword_01", "Sword", "1", "" });

            Assert.That(result.Rows.Select(r => r.Id), Is.EqualTo(new[] { "sword_01" }));
            Assert.That(result.Rows[0].RowNumber, Is.EqualTo(4));
        }

        [Test]
        public void SkipsEmptyRowsWithoutShiftingRowNumbers()
        {
            var result = ParseValid(
                UnitsHeader,
                new[] { "sword_01", "Sword", "1", "" },
                Array.Empty<string>(),
                new[] { "", "", "", "", "" },
                new[] { "  ", " ", "\t", "" },
                new[] { "", "", "", "", "only a note" },
                new[] { "", "", "", "", "", "right of the header" },
                new[] { "axe_02", "Axe", "2", "" });

            Assert.That(result.Rows.Select(r => r.Id), Is.EqualTo(new[] { "sword_01", "axe_02" }));
            Assert.That(result.Rows.Select(r => r.RowNumber), Is.EqualTo(new[] { 2, 8 }));
        }

        [Test]
        public void EmptyIdWithDataIsError()
        {
            var result = Parse(
                UnitsHeader,
                new[] { "sword_01", "Sword", "1", "" },
                new[] { "axe_02", "Axe", "2", "" },
                new[] { "bow_03", "Bow", "3", "" },
                new[] { " ", "Nameless", "4", "" });

            var error = SingleError(result);
            Assert.That(error.CellCode, Is.EqualTo(CellErrorCode.EmptyId));
            Assert.That(error.HeaderCode, Is.Null);
            Assert.That(error.Address, Is.EqualTo("Units!A5"));
            Assert.That(error.Message, Is.EqualTo("Row 5 has data but empty 'id'"));
            Assert.That(result.Rows, Has.Count.EqualTo(4));
        }

        [Test]
        public void EmptyRequiredIdGivesSingleError()
        {
            var result = Parse(
                new[] { "id:string!", "hp:int" },
                new[] { "sword_01", "1" },
                new[] { "axe_02", "2" },
                new[] { "bow_03", "3" },
                new[] { "", "4" });

            var error = SingleError(result);
            Assert.That(error.CellCode, Is.EqualTo(CellErrorCode.EmptyId));
            Assert.That(error.Address, Is.EqualTo("Units!A5"));
        }

        [TestCase("sword 01")]
        [TestCase("меч")]
        [TestCase("a-b")]
        [TestCase(" a")]
        public void InvalidIdIsError(string id)
        {
            var error = SingleError(ParseIds(id));

            Assert.That(error.CellCode, Is.EqualTo(CellErrorCode.InvalidId));
            Assert.That(error.Address, Is.EqualTo("Units!A2"));
            Assert.That(error.Message, Is.EqualTo($"Invalid id '{id}': use Latin letters, digits and '_'"));
        }

        [Test]
        public void ValidIdFormats()
        {
            var result = ParseIds("01_sword", "Sword_01", "_", "ABC");

            Assert.That(result.IsValid, Is.True, string.Join("; ", result.Errors));
            Assert.That(result.Rows, Has.Count.EqualTo(4));
        }

        [Test]
        public void DuplicateIdIsError()
        {
            var result = ParseIds("axe_02", "sword_01", "bow_03", "sword_01");

            var error = SingleError(result);
            Assert.That(error.CellCode, Is.EqualTo(CellErrorCode.DuplicateId));
            Assert.That(error.Address, Is.EqualTo("Units!A5"));
            Assert.That(error.Message, Is.EqualTo("Duplicate id 'sword_01', already defined in Units!A3"));
            Assert.That(result.Rows, Has.Count.EqualTo(4));
        }

        [Test]
        public void IdsAreCaseSensitive()
        {
            var result = ParseIds("Sword", "sword");

            Assert.That(result.IsValid, Is.True, string.Join("; ", result.Errors));
        }

        [Test]
        public void InvalidAndEmptyIdsDoNotTakePartInDuplicateCheck()
        {
            var result = Parse(
                new[] { "id", "hp:int" },
                new[] { "a b", "1" },
                new[] { "a b", "2" },
                new[] { "", "3" },
                new[] { "", "4" });

            Assert.That(result.Errors.Select(e => e.CellCode), Is.EqualTo(new CellErrorCode?[]
            {
                CellErrorCode.InvalidId, CellErrorCode.InvalidId, CellErrorCode.EmptyId, CellErrorCode.EmptyId,
            }));
        }

        [Test]
        public void IdColumnAfterSkippedColumn()
        {
            var result = Parse(
                new[] { "#c", "id", "hp:int" },
                new[] { "note", "sword_01", "1" },
                new[] { "", "#draft", "x" },
                new[] { "note", "", "2" });

            var error = SingleError(result);
            Assert.That(error.CellCode, Is.EqualTo(CellErrorCode.EmptyId));
            Assert.That(error.Address, Is.EqualTo("Units!B4"));
            Assert.That(result.Rows.Select(r => r.Id), Is.EqualTo(new[] { "sword_01", "" }));
        }

        [Test]
        public void CollectsAllErrorsInOrder()
        {
            var result = Parse(
                new[] { "id", "name:string!", "hp:int", "speed:floaty" },
                new[] { "sword_01", "Sword", "1", "x" },
                new[] { "axe_02", "", "abc" },
                new[] { "bow_03", "Bow", "3" },
                new[] { "spear_04", "Spear", "4" },
                new[] { "mace_05", "Mace", "5" },
                new[] { "sword_01", "Sword", "6" });

            Assert.That(result.IsValid, Is.False);
            Assert.That(result.Errors.Select(e => e.Address), Is.EqualTo(new[]
            {
                "Units!D1", "Units!B3", "Units!C3", "Units!A7",
            }), string.Join("; ", result.Errors));
            Assert.That(result.Errors[0].HeaderCode, Is.EqualTo(HeaderErrorCode.UnknownType));
            Assert.That(result.Errors[0].CellCode, Is.Null);
            Assert.That(result.Errors[1].CellCode, Is.EqualTo(CellErrorCode.RequiredEmpty));
            Assert.That(result.Errors[2].CellCode, Is.EqualTo(CellErrorCode.InvalidInt));
            Assert.That(result.Errors[3].CellCode, Is.EqualTo(CellErrorCode.DuplicateId));
            Assert.That(result.Errors.All(e => e.SheetName == "Units"), Is.True);
            Assert.That(result.Rows, Has.Count.EqualTo(6));
        }

        [Test]
        public void IdErrorComesBeforeCellErrors()
        {
            var result = Parse(
                new[] { "id", "hp:int" },
                new[] { "a b", "x" });

            Assert.That(result.Errors.Select(e => e.CellCode), Is.EqualTo(new CellErrorCode?[]
            {
                CellErrorCode.InvalidId, CellErrorCode.InvalidInt,
            }));
        }

        [Test]
        public void MissingIdColumnStillParsesValues()
        {
            var result = Parse(
                new[] { "name", "hp:int" },
                new[] { "Sword", "abc" },
                new[] { "Axe", "2" });

            Assert.That(result.Errors.Select(e => e.Address), Is.EqualTo(new[] { "Units!A1", "Units!B2" }),
                string.Join("; ", result.Errors));
            Assert.That(result.Errors[0].HeaderCode, Is.EqualTo(HeaderErrorCode.MissingIdColumn));
            Assert.That(result.Rows, Has.Count.EqualTo(2));
            Assert.That(result.Rows[1].Values["hp"], Is.EqualTo(2));
        }

        [Test]
        public void InvalidIdColumnTypeSkipsIdChecks()
        {
            var result = Parse(
                new[] { "id:floaty", "hp:int" },
                new[] { "a b", "1" },
                new[] { "a b", "x" });

            Assert.That(result.IsValid, Is.False);
            Assert.That(result.Columns.Select(c => c.Name), Is.EqualTo(new[] { "hp" }));
            Assert.That(result.Errors.Select(e => e.Address), Is.EqualTo(new[] { "Units!A1", "Units!B3" }),
                string.Join("; ", result.Errors));
            Assert.That(result.Errors[0].HeaderCode, Is.EqualTo(HeaderErrorCode.UnknownType));
            Assert.That(result.Errors[1].CellCode, Is.EqualTo(CellErrorCode.InvalidInt));
            Assert.That(result.Rows, Has.Count.EqualTo(2));
            Assert.That(result.Rows.All(r => r.Id == string.Empty), Is.True);
        }

        [Test]
        public void EmptySheetReportsMissingIdColumn()
        {
            var error = SingleError(Parse());

            Assert.That(error.HeaderCode, Is.EqualTo(HeaderErrorCode.MissingIdColumn));
            Assert.That(error.Address, Is.EqualTo("Units!A1"));
        }

        [Test]
        public void SheetErrorToString()
        {
            var result = Parse(
                UnitsHeader,
                new[] { "sword_01", "Sword", "1" },
                new[] { "axe_02", "Axe", "abc" });

            var error = SingleError(result);
            Assert.That(error.CellAddress, Is.EqualTo("C3"));
            Assert.That(error.ToString(), Is.EqualTo("Units!C3: Column 'hp': 'abc' is not a valid int"));
        }

        [Test]
        public void ParsesCsvRows()
        {
            var csv = "id,name:string!,hp:int\n" +
                      "sword_01,Sword,1\n" +
                      "\"multi\nline\",Axe,2\n" +
                      "sword_01,Sword,abc\n";
            var csvResult = CsvReader.Read(csv, CsvDelimiter.Comma);
            Assert.That(csvResult.Errors, Is.Empty);

            var result = SheetParser.Parse("Units", csvResult.Rows.Select(r => r.Cells).ToList());

            Assert.That(result.Rows.Select(r => r.RowNumber), Is.EqualTo(csvResult.Rows.Skip(1).Select(r => r.RowNumber)));
            Assert.That(result.Errors.Select(e => e.Address), Is.EqualTo(new[] { "Units!A3", "Units!A4", "Units!C4" }),
                string.Join("; ", result.Errors));
        }

        [Test]
        public void NullArgumentsThrow()
        {
            var rows = Sheet(new[] { "id" });

            Assert.Throws<ArgumentException>(() => SheetParser.Parse(null, rows));
            Assert.Throws<ArgumentException>(() => SheetParser.Parse("", rows));
            Assert.Throws<ArgumentNullException>(() => SheetParser.Parse("Units", null));
            Assert.Throws<ArgumentException>(() => SheetParser.Parse("Units", new IReadOnlyList<string>[] { new[] { "id" }, null }));
        }
    }
}
