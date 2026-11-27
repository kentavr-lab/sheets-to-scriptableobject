using System;
using System.Globalization;
using System.Linq;
using Kentavr.SheetsToSO.Core;
using NUnit.Framework;

namespace Kentavr.SheetsToSO.Core.Tests
{
    public class CellValueParserTests
    {
        private static ColumnDefinition[] Columns(params string[] header)
        {
            var result = HeaderParser.Parse(header);
            Assert.That(result.IsValid, Is.True, string.Join("; ", result.Errors));
            return result.Columns.ToArray();
        }

        // Header "id | <type>": the column under test is B.
        private static ColumnDefinition Column(string header) => Columns("id", header)[1];

        private static CellParseResult Parse(string header, string text, int rowNumber = 2) =>
            CellValueParser.Parse(Column(header), text, rowNumber);

        private static object ValidValue(string header, string text)
        {
            var result = Parse(header, text);
            Assert.That(result.IsValid, Is.True, string.Join("; ", result.Errors));
            return result.Value;
        }

        private static CellError SingleError(CellParseResult result)
        {
            Assert.That(result.Errors, Has.Count.EqualTo(1), string.Join("; ", result.Errors));
            return result.Errors[0];
        }

        [Test]
        public void ParsesAcceptanceRow()
        {
            var columns = Columns("id", "name:string!", "hp:int", "speed:float", "stackable:bool",
                "rarity:enum<Rarity>", "weapon:ref<Weapons>", "tags:string[]");
            var cells = new[] { "sword_01", "Sword", "120", "3.5", "TRUE", "Epic", "axe_02", "melee\\|starter|two handed" };

            var row = CellValueParser.ParseRow(columns, cells, 2);

            Assert.That(row.IsValid, Is.True, string.Join("; ", row.Errors));
            Assert.That(row.RowNumber, Is.EqualTo(2));
            Assert.That(row.Values, Has.Count.EqualTo(8));
            Assert.That(row.Values["id"], Is.EqualTo("sword_01"));
            Assert.That(row.Values["name"], Is.EqualTo("Sword"));
            Assert.That(row.Values["hp"], Is.EqualTo(120).And.TypeOf<int>());
            Assert.That(row.Values["speed"], Is.EqualTo(3.5f).And.TypeOf<float>());
            Assert.That(row.Values["stackable"], Is.EqualTo(true));
            Assert.That(row.Values["rarity"], Is.EqualTo("Epic"));
            Assert.That(row.Values["weapon"], Is.EqualTo("axe_02"));
            Assert.That(row.Values["tags"], Is.EqualTo(new[] { "melee|starter", "two handed" }).And.TypeOf<string[]>());
        }

        [TestCase("title", "")]
        [TestCase("title:string", "")]
        [TestCase("hp:int", 0)]
        [TestCase("speed:float", 0f)]
        [TestCase("stackable:bool", false)]
        [TestCase("rarity:enum<Rarity>", null)]
        [TestCase("weapon:ref<Weapons>", null)]
        public void EmptyCellGivesScalarDefault(string header, object expected)
        {
            foreach (var text in new[] { null, "", "   " })
            {
                var value = ValidValue(header, text);
                Assert.That(value, Is.EqualTo(expected), $"text '{text}'");
                if (expected != null)
                    Assert.That(value, Is.TypeOf(expected.GetType()));
            }
        }

        [TestCase("tags:string[]", typeof(string[]))]
        [TestCase("hp:int[]", typeof(int[]))]
        [TestCase("speed:float[]", typeof(float[]))]
        [TestCase("flags:bool[]", typeof(bool[]))]
        [TestCase("rarity:enum<Rarity>[]", typeof(string[]))]
        [TestCase("weapon:ref<Weapons>[]", typeof(string[]))]
        public void EmptyCellGivesEmptyArray(string header, Type expectedType)
        {
            foreach (var text in new[] { null, "", "  " })
            {
                var value = ValidValue(header, text);
                Assert.That(value, Is.TypeOf(expectedType));
                Assert.That((Array)value, Is.Empty);
            }
        }

        [TestCase("name:string!")]
        [TestCase("hp:int!")]
        [TestCase("tags:string[]!")]
        [TestCase("rarity:enum<Rarity>!")]
        public void EmptyRequiredCellIsError(string header)
        {
            foreach (var text in new[] { null, "", " " })
            {
                var result = Parse(header, text, 7);
                var error = SingleError(result);
                Assert.That(error.Code, Is.EqualTo(CellErrorCode.RequiredEmpty));
                Assert.That(error.Address, Is.EqualTo("B7"));
                Assert.That(error.Message, Does.Contain(Column(header).Name));
                Assert.That(error.ToString(), Does.StartWith("B7: "));
            }
        }

        [Test]
        public void RequiredEmptyValueIsDefault()
        {
            Assert.That(Parse("name:string!", "").Value, Is.EqualTo(string.Empty));
            Assert.That(Parse("hp:int!", "").Value, Is.EqualTo(0));
        }

        [Test]
        public void StringIsKeptAsIs()
        {
            Assert.That(ValidValue("title", "  Long Sword  "), Is.EqualTo("  Long Sword  "));
            Assert.That(ValidValue("title", "a\\|b"), Is.EqualTo("a\\|b"));
            Assert.That(ValidValue("title", "a|b"), Is.EqualTo("a|b"));
        }

        [TestCase("120", 120)]
        [TestCase(" 42 ", 42)]
        [TestCase("-5", -5)]
        [TestCase("+7", 7)]
        [TestCase("0", 0)]
        [TestCase("2147483647", int.MaxValue)]
        [TestCase("-2147483648", int.MinValue)]
        public void ParsesInt(string text, int expected)
        {
            Assert.That(ValidValue("hp:int", text), Is.EqualTo(expected).And.TypeOf<int>());
        }

        [TestCase("abc")]
        [TestCase("1.0")]
        [TestCase("1 000")]
        [TestCase("1,000")]
        [TestCase("2147483648")]
        [TestCase("1e3")]
        [TestCase("0x10")]
        public void InvalidIntIsError(string text)
        {
            var result = Parse("hp:int", text);
            var error = SingleError(result);
            Assert.That(error.Code, Is.EqualTo(CellErrorCode.InvalidInt));
            Assert.That(error.Message, Does.Contain("hp").And.Contain(text));
            Assert.That(result.Value, Is.EqualTo(0));
        }

        [TestCase("3.5", 3.5f)]
        [TestCase(" 3.5 ", 3.5f)]
        [TestCase("-0.25", -0.25f)]
        [TestCase("10", 10f)]
        [TestCase("1e3", 1000f)]
        [TestCase("1.5E-2", 0.015f)]
        [TestCase(".5", 0.5f)]
        public void ParsesFloat(string text, float expected)
        {
            Assert.That(ValidValue("speed:float", text), Is.EqualTo(expected).And.TypeOf<float>());
        }

        [Test]
        public void FloatWithCommaHasHint()
        {
            var result = Parse("speed:float", "1,5");
            var error = SingleError(result);
            Assert.That(error.Code, Is.EqualTo(CellErrorCode.InvalidFloat));
            Assert.That(error.Message, Does.Contain("speed").And.Contain("'1,5'"));
            Assert.That(error.Message, Does.Contain("use '.' as decimal separator: '1.5'"));
            Assert.That(result.Value, Is.EqualTo(0f));
        }

        [TestCase("abc")]
        [TestCase("1,5,5")]
        [TestCase("a,b")]
        public void InvalidFloatWithoutHint(string text)
        {
            var error = SingleError(Parse("speed:float", text));
            Assert.That(error.Code, Is.EqualTo(CellErrorCode.InvalidFloat));
            Assert.That(error.Message, Does.Contain(text));
            Assert.That(error.Message, Does.Not.Contain("decimal separator"));
        }

        [TestCase("NaN")]
        [TestCase("Infinity")]
        [TestCase("-Infinity")]
        [TestCase("∞")]
        [TestCase("1e39")]
        [TestCase("-1e39")]
        public void NonFiniteFloatIsError(string text)
        {
            var error = SingleError(Parse("speed:float", text));
            Assert.That(error.Code, Is.EqualTo(CellErrorCode.InvalidFloat));
        }

        [TestCase("true", true)]
        [TestCase("TRUE", true)]
        [TestCase("True", true)]
        [TestCase(" false ", false)]
        [TestCase("FALSE", false)]
        public void ParsesBool(string text, bool expected)
        {
            Assert.That(ValidValue("stackable:bool", text), Is.EqualTo(expected));
        }

        [TestCase("yes")]
        [TestCase("1")]
        [TestCase("0")]
        [TestCase("t")]
        public void InvalidBoolIsError(string text)
        {
            var result = Parse("stackable:bool", text);
            var error = SingleError(result);
            Assert.That(error.Code, Is.EqualTo(CellErrorCode.InvalidBool));
            Assert.That(error.Message, Does.Contain("stackable").And.Contain(text));
            Assert.That(result.Value, Is.EqualTo(false));
        }

        [Test]
        public void EnumAndRefAreTrimmedNames()
        {
            Assert.That(ValidValue("rarity:enum<Rarity>", " Epic "), Is.EqualTo("Epic"));
            Assert.That(ValidValue("rarity:enum<Rarity>", "NotInEnum"), Is.EqualTo("NotInEnum"));
            Assert.That(ValidValue("weapon:ref<Weapons>", " axe_02"), Is.EqualTo("axe_02"));
        }

        [Test]
        public void StringArrayKeepsSpacesAndEmptyElements()
        {
            Assert.That(ValidValue("tags:string[]", "melee"), Is.EqualTo(new[] { "melee" }));
            Assert.That(ValidValue("tags:string[]", " a | b "), Is.EqualTo(new[] { " a ", " b " }));
            Assert.That(ValidValue("tags:string[]", "a||b"), Is.EqualTo(new[] { "a", "", "b" }));
            Assert.That(ValidValue("tags:string[]", "a|"), Is.EqualTo(new[] { "a", "" }));
            Assert.That(ValidValue("tags:string[]", "two handed|one"), Is.EqualTo(new[] { "two handed", "one" }));
        }

        [Test]
        public void ArrayEscaping()
        {
            Assert.That(ValidValue("tags:string[]", "a\\|b|c"), Is.EqualTo(new[] { "a|b", "c" }));
            Assert.That(ValidValue("tags:string[]", "\\|"), Is.EqualTo(new[] { "|" }));
            Assert.That(ValidValue("tags:string[]", "a\\b|c\\"), Is.EqualTo(new[] { "a\\b", "c\\" }));
            Assert.That(ValidValue("tags:string[]", "a\\\\|b"), Is.EqualTo(new[] { "a\\|b" }));
            Assert.That(ValidValue("rarity:enum<Rarity>[]", "A\\|B|C"), Is.EqualTo(new[] { "A|B", "C" }));
        }

        [Test]
        public void ParsesTypedArrays()
        {
            Assert.That(ValidValue("hp:int[]", "1 | 2|-3"), Is.EqualTo(new[] { 1, 2, -3 }).And.TypeOf<int[]>());
            Assert.That(ValidValue("speed:float[]", "1.5| 2"), Is.EqualTo(new[] { 1.5f, 2f }).And.TypeOf<float[]>());
            Assert.That(ValidValue("flags:bool[]", "TRUE|false"), Is.EqualTo(new[] { true, false }).And.TypeOf<bool[]>());
            Assert.That(ValidValue("rarity:enum<Rarity>[]", "Epic | Rare"), Is.EqualTo(new[] { "Epic", "Rare" }).And.TypeOf<string[]>());
            Assert.That(ValidValue("weapon:ref<Weapons>[]", "a|b"), Is.EqualTo(new[] { "a", "b" }).And.TypeOf<string[]>());
        }

        [TestCase("hp:int[]", "1||2")]
        [TestCase("hp:int[]", "1| |2")]
        [TestCase("rarity:enum<Rarity>[]", "Epic|")]
        public void EmptyElementIsError(string header, string text)
        {
            var result = Parse(header, text, 4);
            var error = SingleError(result);
            Assert.That(error.Code, Is.EqualTo(CellErrorCode.EmptyArrayElement));
            Assert.That(error.Address, Is.EqualTo("B4"));
            Assert.That(((Array)result.Value).Length, Is.EqualTo(0));
        }

        [Test]
        public void CollectsAllElementErrorsOfCell()
        {
            var result = Parse("hp:int[]", "1|abc|3|x|", 5);

            Assert.That(result.IsValid, Is.False);
            Assert.That(result.Errors.Select(e => e.Code), Is.EqualTo(new[]
            {
                CellErrorCode.InvalidInt, CellErrorCode.InvalidInt, CellErrorCode.EmptyArrayElement,
            }));
            Assert.That(result.Errors.Select(e => e.Address), Is.All.EqualTo("B5"));
            Assert.That(result.Errors[0].Message, Does.Contain("element 2").And.Contain("'abc'").And.Contain("hp"));
            Assert.That(result.Errors[1].Message, Does.Contain("element 4").And.Contain("'x'"));
            Assert.That(result.Errors[2].Message, Does.Contain("element 5"));
            Assert.That(result.Value, Is.EqualTo(Array.Empty<int>()));
        }

        [Test]
        public void FloatArrayElementWithCommaHasHint()
        {
            var error = SingleError(Parse("speed:float[]", "1.5|2,5"));
            Assert.That(error.Code, Is.EqualTo(CellErrorCode.InvalidFloat));
            Assert.That(error.Message, Does.Contain("element 2").And.Contain("'2.5'"));
        }

        [Test]
        public void AddressAccountsForSkippedColumns()
        {
            var columns = Columns("id", "#notes", "hp:int");
            Assert.That(CellValueParser.Parse(columns[1], "abc", 7).Errors[0].Address, Is.EqualTo("C7"));

            var header = new string[28];
            header[0] = "id";
            for (var i = 1; i < 26; i++)
                header[i] = "#c" + i;
            header[26] = "hp:int";
            header[27] = "speed:float";
            var wide = Columns(header);
            Assert.That(wide[1].ColumnIndex, Is.EqualTo(26));

            var row = CellValueParser.ParseRow(wide, Enumerable.Repeat("x", 28).ToArray(), 3);
            Assert.That(row.Errors.Select(e => e.Address), Is.EqualTo(new[] { "AA3", "AB3" }));
        }

        [Test]
        public void ErrorInColumnIndexOneRowSevenIsB7()
        {
            var column = Column("hp:int");
            Assert.That(column.ColumnIndex, Is.EqualTo(1));
            var error = SingleError(CellValueParser.Parse(column, "abc", 7));
            Assert.That(error.Address, Is.EqualTo("B7"));
            Assert.That(error.ToString(), Does.StartWith("B7: "));
        }

        [Test]
        public void ShortRowGivesEmptyCells()
        {
            var columns = Columns("id", "hp:int", "tags:string[]", "name:string!");
            var row = CellValueParser.ParseRow(columns, new[] { "a" }, 9);

            Assert.That(row.Values["id"], Is.EqualTo("a"));
            Assert.That(row.Values["hp"], Is.EqualTo(0));
            Assert.That(row.Values["tags"], Is.EqualTo(Array.Empty<string>()));
            Assert.That(row.Values["name"], Is.EqualTo(string.Empty));
            var error = row.Errors.Single();
            Assert.That(error.Code, Is.EqualTo(CellErrorCode.RequiredEmpty));
            Assert.That(error.Address, Is.EqualTo("D9"));
        }

        [Test]
        public void CollectsAllErrorsOfRowInColumnOrder()
        {
            var columns = Columns("id", "name:string!", "hp:int", "speed:float", "stackable:bool", "levels:int[]");
            var row = CellValueParser.ParseRow(columns, new[] { "a", "", "abc", "1,5", "yes", "1|x|y" }, 7);

            Assert.That(row.IsValid, Is.False);
            Assert.That(row.Errors.Select(e => e.Code), Is.EqualTo(new[]
            {
                CellErrorCode.RequiredEmpty, CellErrorCode.InvalidInt, CellErrorCode.InvalidFloat,
                CellErrorCode.InvalidBool, CellErrorCode.InvalidInt, CellErrorCode.InvalidInt,
            }));
            Assert.That(row.Errors.Select(e => e.Address), Is.EqualTo(new[] { "B7", "C7", "D7", "E7", "F7", "F7" }));
            Assert.That(row.Values["hp"], Is.EqualTo(0));
            Assert.That(row.Values["speed"], Is.EqualTo(0f));
            Assert.That(row.Values["stackable"], Is.EqualTo(false));
            Assert.That(row.Values["levels"], Is.EqualTo(Array.Empty<int>()));
        }

        [Test]
        public void ParsesIndependentlyOfCurrentCulture()
        {
            var original = CultureInfo.CurrentCulture;
            try
            {
                CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("ru-RU");

                Assert.That(ValidValue("speed:float", "3.5"), Is.EqualTo(3.5f));
                Assert.That(ValidValue("speed:float[]", "1.25|-2.5"), Is.EqualTo(new[] { 1.25f, -2.5f }));
                Assert.That(ValidValue("hp:int", "-12"), Is.EqualTo(-12));

                var error = SingleError(Parse("speed:float", "3,5"));
                Assert.That(error.Code, Is.EqualTo(CellErrorCode.InvalidFloat));
                Assert.That(error.Message, Does.Contain("'3.5'"));
            }
            finally
            {
                CultureInfo.CurrentCulture = original;
            }
        }

        [Test]
        public void NullArgumentsThrow()
        {
            var columns = Columns("id");
            Assert.Throws<ArgumentNullException>(() => CellValueParser.Parse(null, "x", 2));
            Assert.Throws<ArgumentNullException>(() => CellValueParser.ParseRow(null, new[] { "x" }, 2));
            Assert.Throws<ArgumentNullException>(() => CellValueParser.ParseRow(columns, null, 2));
        }
    }
}
