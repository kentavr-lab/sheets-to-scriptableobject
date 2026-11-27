using System;
using System.Linq;
using Kentavr.SheetsToSO.Core;
using NUnit.Framework;

namespace Kentavr.SheetsToSO.Core.Tests
{
    public class HeaderParserTests
    {
        private static HeaderParseResult Parse(params string[] cells) => HeaderParser.Parse(cells);

        private static HeaderError SingleError(HeaderParseResult result)
        {
            Assert.That(result.Errors, Has.Count.EqualTo(1), string.Join("; ", result.Errors));
            return result.Errors[0];
        }

        [Test]
        public void ParsesDocumentationExample()
        {
            var result = Parse("id", "name:string!", "hp:int", "rarity:enum<Rarity>", "weapon:ref<Weapons>", "tags:string[]", "#notes");

            Assert.That(result.IsValid, Is.True, string.Join("; ", result.Errors));
            Assert.That(result.Columns.Select(c => c.Name), Is.EqualTo(new[] { "id", "name", "hp", "rarity", "weapon", "tags" }));

            var id = result.Columns[0];
            Assert.That(id.Type.Kind, Is.EqualTo(ColumnKind.String));
            Assert.That(id.IsTypeExplicit, Is.False);
            Assert.That(id.IsRequired, Is.False);
            Assert.That(id.Address, Is.EqualTo("A1"));
            Assert.That(id.ColumnIndex, Is.EqualTo(0));

            var name = result.Columns[1];
            Assert.That(name.Type.Kind, Is.EqualTo(ColumnKind.String));
            Assert.That(name.IsRequired, Is.True);
            Assert.That(name.IsTypeExplicit, Is.True);
            Assert.That(name.Address, Is.EqualTo("B1"));

            var hp = result.Columns[2];
            Assert.That(hp.Type.Kind, Is.EqualTo(ColumnKind.Int));
            Assert.That(hp.IsRequired, Is.False);
            Assert.That(hp.Type.IsArray, Is.False);

            var rarity = result.Columns[3];
            Assert.That(rarity.Type.Kind, Is.EqualTo(ColumnKind.Enum));
            Assert.That(rarity.Type.TypeName, Is.EqualTo("Rarity"));

            var weapon = result.Columns[4];
            Assert.That(weapon.Type.Kind, Is.EqualTo(ColumnKind.Ref));
            Assert.That(weapon.Type.TypeName, Is.EqualTo("Weapons"));

            var tags = result.Columns[5];
            Assert.That(tags.Type.Kind, Is.EqualTo(ColumnKind.String));
            Assert.That(tags.Type.IsArray, Is.True);
            Assert.That(tags.Type.TypeName, Is.Null);
            Assert.That(tags.Address, Is.EqualTo("F1"));
            Assert.That(tags.ColumnIndex, Is.EqualTo(5));
        }

        [TestCase("string", ColumnKind.String, null, false)]
        [TestCase("int", ColumnKind.Int, null, false)]
        [TestCase("float", ColumnKind.Float, null, false)]
        [TestCase("bool", ColumnKind.Bool, null, false)]
        [TestCase("enum<Rarity>", ColumnKind.Enum, "Rarity", false)]
        [TestCase("ref<Weapons>", ColumnKind.Ref, "Weapons", false)]
        [TestCase("string[]", ColumnKind.String, null, true)]
        [TestCase("int[]", ColumnKind.Int, null, true)]
        [TestCase("float[]", ColumnKind.Float, null, true)]
        [TestCase("bool[]", ColumnKind.Bool, null, true)]
        [TestCase("enum<Rarity>[]", ColumnKind.Enum, "Rarity", true)]
        [TestCase("ref<Weapons>[]", ColumnKind.Ref, "Weapons", true)]
        public void ParsesTypes(string typeText, ColumnKind kind, string typeName, bool isArray)
        {
            var result = Parse("id", "value:" + typeText);

            Assert.That(result.IsValid, Is.True, string.Join("; ", result.Errors));
            var column = result.Columns[1];
            Assert.That(column.Type.Kind, Is.EqualTo(kind));
            Assert.That(column.Type.TypeName, Is.EqualTo(typeName));
            Assert.That(column.Type.IsArray, Is.EqualTo(isArray));
            Assert.That(column.IsRequired, Is.False);
            Assert.That(column.IsTypeExplicit, Is.True);
            Assert.That(column.Type.ToString(), Is.EqualTo(typeText));
        }

        [Test]
        public void RequiredArray()
        {
            var column = Parse("id", "tags:string[]!").Columns[1];

            Assert.That(column.IsRequired, Is.True);
            Assert.That(column.Type.IsArray, Is.True);
            Assert.That(column.Type.Kind, Is.EqualTo(ColumnKind.String));
            Assert.That(column.Type.ToString(), Is.EqualTo("string[]"));
        }

        [TestCase("hp : int")]
        [TestCase("  hp:int  ")]
        [TestCase("hp:  int")]
        [TestCase("hp  :int")]
        public void TrimsWhitespaceAroundSeparator(string cell)
        {
            var result = Parse(" id ", cell);

            Assert.That(result.IsValid, Is.True, string.Join("; ", result.Errors));
            Assert.That(result.Columns[0].Name, Is.EqualTo("id"));
            Assert.That(result.Columns[1].Name, Is.EqualTo("hp"));
            Assert.That(result.Columns[1].Type.Kind, Is.EqualTo(ColumnKind.Int));
        }

        [TestCase("var")]
        [TestCase("value")]
        [TestCase("record")]
        [TestCase("_private")]
        [TestCase("Hp2")]
        public void AcceptsValidNames(string name)
        {
            Assert.That(Parse("id", name).IsValid, Is.True);
        }

        [Test]
        public void SkippedColumnKeepsPositions()
        {
            var result = Parse("id", "#x", "hp:int");

            Assert.That(result.IsValid, Is.True);
            Assert.That(result.Columns.Select(c => c.Name), Is.EqualTo(new[] { "id", "hp" }));
            Assert.That(result.Columns[1].Address, Is.EqualTo("C1"));
            Assert.That(result.Columns[1].ColumnIndex, Is.EqualTo(2));
        }

        [Test]
        public void SkippedColumnIsNotValidated()
        {
            var result = Parse("id", "# any text : integer", "#class", "#id");

            Assert.That(result.IsValid, Is.True);
            Assert.That(result.Columns, Has.Count.EqualTo(1));
        }

        [Test]
        public void SkippedColumnBeforeIdIsAllowed()
        {
            var result = Parse("#comment", "id", "hp:int");

            Assert.That(result.IsValid, Is.True);
            Assert.That(result.Columns[0].Address, Is.EqualTo("B1"));
        }

        [Test]
        public void TrailingEmptyCellsAreIgnored()
        {
            var result = Parse("id", "hp:int", "", "  ", null);

            Assert.That(result.IsValid, Is.True);
            Assert.That(result.Columns, Has.Count.EqualTo(2));
        }

        [TestCase("")]
        [TestCase("   ")]
        [TestCase(null)]
        public void EmptyCellInMiddleIsError(string empty)
        {
            var result = Parse("id", empty, "hp:int");

            var error = SingleError(result);
            Assert.That(error.Code, Is.EqualTo(HeaderErrorCode.EmptyName));
            Assert.That(error.Address, Is.EqualTo("B1"));
            Assert.That(result.Columns.Select(c => c.Name), Is.EqualTo(new[] { "id", "hp" }));
        }

        [TestCase(":int")]
        [TestCase("  : int")]
        public void EmptyNameBeforeTypeIsError(string cell)
        {
            var error = SingleError(Parse("id", cell));

            Assert.That(error.Code, Is.EqualTo(HeaderErrorCode.EmptyName));
            Assert.That(error.Address, Is.EqualTo("B1"));
        }

        [TestCase("max hp")]
        [TestCase("1st")]
        [TestCase("hp-max")]
        [TestCase("здоровье")]
        [TestCase("hp.max:int")]
        public void InvalidNameIsError(string cell)
        {
            var error = SingleError(Parse("id", cell));

            Assert.That(error.Code, Is.EqualTo(HeaderErrorCode.InvalidName));
            Assert.That(error.Address, Is.EqualTo("B1"));
        }

        [TestCase("class")]
        [TestCase("int:int")]
        [TestCase("string")]
        [TestCase("event")]
        [TestCase("default:bool")]
        public void CSharpKeywordIsError(string cell)
        {
            var error = SingleError(Parse("id", cell));

            Assert.That(error.Code, Is.EqualTo(HeaderErrorCode.CSharpKeyword));
            Assert.That(error.Address, Is.EqualTo("B1"));
        }

        [Test]
        public void KeywordCheckIsCaseSensitive()
        {
            Assert.That(Parse("id", "Class").IsValid, Is.True);
        }

        [TestCase("hp:integer")]
        [TestCase("hp:")]
        [TestCase("hp:Int")]
        [TestCase("hp:int[][]")]
        [TestCase("hp:enum<>")]
        [TestCase("hp:ref<Weapons")]
        [TestCase("hp:!int")]
        [TestCase("hp:int!!")]
        [TestCase("hp:int![]")]
        [TestCase("hp:enum<1x>")]
        [TestCase("hp:ref<Bad Name>")]
        [TestCase("hp:int:int")]
        public void UnknownTypeIsError(string cell)
        {
            var error = SingleError(Parse("id", cell));

            Assert.That(error.Code, Is.EqualTo(HeaderErrorCode.UnknownType));
            Assert.That(error.Address, Is.EqualTo("B1"));
        }

        [Test]
        public void UnknownTypeMessageContainsCellText()
        {
            var error = SingleError(Parse("id", "hp:integer"));

            Assert.That(error.Message, Is.EqualTo("Unknown type 'integer' in column 'hp'"));
            Assert.That(error.ToString(), Is.EqualTo("B1: Unknown type 'integer' in column 'hp'"));
        }

        [Test]
        public void DuplicateNameIsErrorOnLaterOccurrences()
        {
            var result = Parse("id", "hp:int", "name", "HP:float", "hp");

            Assert.That(result.Errors.Select(e => e.Code), Is.EqualTo(new[] { HeaderErrorCode.DuplicateName, HeaderErrorCode.DuplicateName }));
            Assert.That(result.Errors.Select(e => e.Address), Is.EqualTo(new[] { "D1", "E1" }));
            Assert.That(result.Columns.Select(c => c.Name), Is.EqualTo(new[] { "id", "hp", "name" }));
        }

        [Test]
        public void SkippedColumnDoesNotCountAsDuplicate()
        {
            Assert.That(Parse("id", "#hp", "hp:int").IsValid, Is.True);
        }

        [Test]
        public void FirstColumnNotIdIsError()
        {
            var result = Parse("name", "id");

            var error = SingleError(result);
            Assert.That(error.Code, Is.EqualTo(HeaderErrorCode.MissingIdColumn));
            Assert.That(error.Address, Is.EqualTo("A1"));
        }

        [Test]
        public void FirstColumnNotIdAfterSkippedColumnIsError()
        {
            var error = SingleError(Parse("#comment", "ID"));

            Assert.That(error.Code, Is.EqualTo(HeaderErrorCode.MissingIdColumn));
            Assert.That(error.Address, Is.EqualTo("B1"));
        }

        [Test]
        public void IdWithExplicitTypeIsAccepted()
        {
            var result = Parse("id:int", "name");

            Assert.That(result.IsValid, Is.True);
            Assert.That(result.Columns[0].Type.Kind, Is.EqualTo(ColumnKind.Int));
        }

        [Test]
        public void NoColumnsIsMissingIdError()
        {
            var headers = new[] { new string[0], new[] { "", " " }, new[] { "#notes" } };
            foreach (var cells in headers)
            {
                var result = HeaderParser.Parse(cells);

                var error = SingleError(result);
                Assert.That(error.Code, Is.EqualTo(HeaderErrorCode.MissingIdColumn));
                Assert.That(error.Address, Is.EqualTo("A1"));
                Assert.That(result.Columns, Is.Empty);
            }
        }

        [Test]
        public void CollectsAllErrorsInRow()
        {
            var result = Parse("name", ":int", "max hp", "class", "hp:integer", "ok", "OK", "", "tail");

            Assert.That(result.Errors.Select(e => e.Code), Is.EqualTo(new[]
            {
                HeaderErrorCode.MissingIdColumn,
                HeaderErrorCode.EmptyName,
                HeaderErrorCode.InvalidName,
                HeaderErrorCode.CSharpKeyword,
                HeaderErrorCode.UnknownType,
                HeaderErrorCode.DuplicateName,
                HeaderErrorCode.EmptyName,
            }));
            Assert.That(result.Errors.Select(e => e.Address), Is.EqualTo(new[] { "A1", "B1", "C1", "D1", "E1", "G1", "H1" }));
            Assert.That(result.Columns.Select(c => c.Name), Is.EqualTo(new[] { "ok", "tail" }));
            Assert.That(result.IsValid, Is.False);
        }

        [Test]
        public void NullCellsThrow()
        {
            Assert.Throws<ArgumentNullException>(() => HeaderParser.Parse(null));
        }
    }
}
