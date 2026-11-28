using System.Collections.Generic;
using System.Linq;
using Kentavr.SheetsToSO.Core;
using NUnit.Framework;

namespace Kentavr.SheetsToSO.Core.Tests
{
    public class SheetSetCheckerTests
    {
        private static readonly string[] UnitsHeader = { "id", "name:string", "hp:int", "rarity:enum<Rarity>", "weapon:ref<Weapons>" };
        private static readonly string[] WeaponsHeader = { "id", "damage:int" };

        private static (string Name, IReadOnlyList<IReadOnlyList<string>> Rows) Sheet(string name, params string[][] rows) =>
            (name, rows);

        private static IReadOnlyList<(string Name, IReadOnlyList<IReadOnlyList<string>> Rows)> Set(
            params (string Name, IReadOnlyList<IReadOnlyList<string>> Rows)[] sheets) => sheets;

        private static (string, IReadOnlyList<IReadOnlyList<string>>) Weapons() => Sheet("Weapons", WeaponsHeader,
            new[] { "w_01", "10" },
            new[] { "w_02", "20" });

        private static (string, IReadOnlyList<IReadOnlyList<string>>) Units(params string[][] rows) =>
            Sheet("Units", new[] { UnitsHeader }.Concat(rows).ToArray());

        private static string[] Knight => new[] { "knight", "Knight", "120", "Epic", "w_01" };
        private static string[] Peasant => new[] { "peasant", "Peasant", "1", "", "" };

        private static SheetSetCheckResult Valid(params (string Name, IReadOnlyList<IReadOnlyList<string>> Rows)[] sheets)
        {
            var result = SheetSetChecker.Check(sheets);
            Assert.That(result.IsValid, Is.True, string.Join("; ", result.Errors));
            return result;
        }

        [Test]
        public void ValidSetIsParsedWithEnums()
        {
            var result = Valid(Units(Knight, Peasant), Weapons());

            Assert.That(result.Sheets.Select(sheet => sheet.SheetName), Is.EqualTo(new[] { "Units", "Weapons" }));
            Assert.That(result.Sheets[0].Rows, Has.Count.EqualTo(2));
            Assert.That(result.Enums.EnumNames, Is.EqualTo(new[] { "Rarity" }));
            Assert.That(result.Enums.Members["Rarity"], Is.EqualTo(new[] { "Epic" }));
        }

        [Test]
        public void NullRowsCountAsEmpty()
        {
            var rows = new IReadOnlyList<string>[] { WeaponsHeader, null, new[] { "w_01", "10" } };

            var result = Valid(("Weapons", rows));

            Assert.That(result.Sheets[0].Rows.Single().RowNumber, Is.EqualTo(3));
        }

        [Test]
        public void EmptySetIsAnError()
        {
            var result = SheetSetChecker.Check(Set());

            Assert.That(result.Errors, Is.EqualTo(new[] { "No sheets to import" }));
        }

        [Test]
        public void ParseErrorsHaveAddresses()
        {
            var result = SheetSetChecker.Check(Set(Units(Knight, new[] { "peasant", "Peasant", "abc", "", "" }), Weapons()));

            Assert.That(result.IsValid, Is.False);
            Assert.That(result.Errors, Has.Count.EqualTo(1));
            Assert.That(result.Errors[0], Does.StartWith("Units!C3: "));
            Assert.That(result.Enums.EnumNames, Is.Empty);
        }

        [Test]
        public void ParseErrorAndBrokenRefAreReportedTogether()
        {
            var result = SheetSetChecker.Check(Set(
                Units(new[] { "knight", "Knight", "abc", "Epic", "w_01" }, new[] { "archer", "Archer", "60", "", "sword_02" }),
                Weapons()));

            Assert.That(result.Errors, Has.Count.EqualTo(2), string.Join("; ", result.Errors));
            Assert.That(result.Errors[0], Does.StartWith("Units!C2: "));
            Assert.That(result.Errors[1], Does.StartWith("Units!E3: ").And.Contain("sword_02"));
            Assert.That(result.Enums.EnumNames, Is.Empty);
        }

        [Test]
        public void RefToRowWithErrorInOtherCellIsValid()
        {
            var weapons = Sheet("Weapons", WeaponsHeader, new[] { "w_01", "abc" });

            var result = SheetSetChecker.Check(Set(Units(Knight), weapons));

            Assert.That(result.Errors, Has.Count.EqualTo(1), string.Join("; ", result.Errors));
            Assert.That(result.Errors[0], Does.StartWith("Weapons!B2: "));
        }

        [TestCase("bad id")]
        [TestCase("")]
        public void BrokenRefInRowWithBadIdIsReported(string id)
        {
            var result = SheetSetChecker.Check(Set(Units(new[] { id, "Orc", "5", "", "w_99" }), Weapons()));

            Assert.That(result.Errors, Has.Count.EqualTo(2), string.Join("; ", result.Errors));
            Assert.That(result.Errors[0], Does.StartWith("Units!A2: "));
            Assert.That(result.Errors[1], Does.StartWith("Units!E2: ").And.Contain("w_99"));
        }

        [Test]
        public void RefToInvalidIdIsNotBroken()
        {
            var weapons = Sheet("Weapons", WeaponsHeader, new[] { "sword 1", "10" });

            var result = SheetSetChecker.Check(Set(Units(new[] { "knight", "Knight", "1", "", "sword 1" }), weapons));

            Assert.That(result.Errors, Has.Count.EqualTo(1), string.Join("; ", result.Errors));
            Assert.That(result.Errors[0], Does.StartWith("Weapons!A2: ").And.Contain("Invalid id"));
        }

        [Test]
        public void RefToDuplicateIdIsNotBroken()
        {
            var weapons = Sheet("Weapons", WeaponsHeader, new[] { "w_01", "10" }, new[] { "w_01", "20" });

            var result = SheetSetChecker.Check(Set(Units(Knight), weapons));

            Assert.That(result.Errors, Has.Count.EqualTo(1), string.Join("; ", result.Errors));
            Assert.That(result.Errors[0], Does.StartWith("Weapons!A3: ").And.Contain("Duplicate id"));
        }

        [Test]
        public void RowWithEmptyIdIsNotARefTarget()
        {
            var weapons = Sheet("Weapons", WeaponsHeader, new[] { "w_01", "10" }, new[] { "", "20" });

            var result = SheetSetChecker.Check(Set(Units(new[] { "knight", "Knight", "1", "", "" }), weapons));

            Assert.That(result.Errors, Has.Count.EqualTo(1), string.Join("; ", result.Errors));
            Assert.That(result.Errors[0], Does.StartWith("Weapons!A3: "));
        }

        [Test]
        public void RefsToSheetWithBrokenIdHeaderAreNotChecked()
        {
            var weapons = Sheet("Weapons", new[] { "id:strng", "damage:int" }, new[] { "w_01", "10" });
            var items = Sheet("Items", new[] { "id", "rarity:enum<Rarity>", "weapon:ref<Weapons>" },
                new[] { "sword", "1st", "w_77" });

            var result = SheetSetChecker.Check(Set(items, weapons));

            var itemErrors = result.Errors.Where(error => error.StartsWith("Items!")).ToList();
            Assert.That(itemErrors, Has.Count.EqualTo(1), string.Join("; ", result.Errors));
            Assert.That(itemErrors[0], Does.StartWith("Items!B2: ").And.Contain("1st"));
            Assert.That(result.Errors, Has.Some.StartsWith("Weapons!A1: "));
            Assert.That(result.Errors, Has.None.Contain("w_77"));
        }

        [Test]
        public void RefsToSheetWithBrokenOtherHeaderAreChecked()
        {
            var weapons = Sheet("Weapons", new[] { "id", "damage:integer" }, new[] { "w_01", "10" });

            var result = SheetSetChecker.Check(Set(Units(new[] { "knight", "Knight", "1", "", "w_99" }), weapons));

            Assert.That(result.Errors, Has.Count.EqualTo(2), string.Join("; ", result.Errors));
            Assert.That(result.Errors[0], Does.StartWith("Weapons!B1: "));
            Assert.That(result.Errors[1], Does.StartWith("Units!E2: ").And.Contain("w_99"));
        }

        [Test]
        public void DuplicateSheetNameSkipsCrossSheetChecks()
        {
            var result = SheetSetChecker.Check(Set(Units(new[] { "knight", "Knight", "1", "", "w_99" }), Weapons(), Weapons()));

            Assert.That(result.Errors, Is.EqualTo(new[] { "Duplicate sheet name 'Weapons'" }));
        }

        [Test]
        public void TypeNameConflictIsReportedWithParseErrors()
        {
            var result = SheetSetChecker.Check(Set(
                Sheet("Weapons", WeaponsHeader, new[] { "w_01", "abc" }),
                Sheet("WeaponsTable", WeaponsHeader, new[] { "w_01", "1" })));

            Assert.That(result.Errors, Has.Count.EqualTo(2), string.Join("; ", result.Errors));
            Assert.That(result.Errors[0], Does.StartWith("Weapons!B2: "));
            Assert.That(result.Errors[1], Does.StartWith("Type name 'WeaponsTable'"));
            Assert.That(result.Enums.EnumNames, Is.Empty);
        }

        [Test]
        public void IdMustBeStringColumn()
        {
            var result = SheetSetChecker.Check(Set(Sheet("Levels", new[] { "id:int", "name" }, new[] { "1", "One" })));

            Assert.That(result.Errors, Has.Some.EqualTo("Levels!A1: column 'id' must be of type 'string', found 'int'"));
        }

        [Test]
        public void BrokenRefIsAnError()
        {
            var result = SheetSetChecker.Check(Set(Units(Knight, new[] { "peasant", "Peasant", "1", "", "w_99" }), Weapons()));

            Assert.That(result.Errors, Has.Count.EqualTo(1));
            Assert.That(result.Errors[0], Does.StartWith("Units!E3: "));
            Assert.That(result.Errors[0], Does.Contain("w_99"));
        }

        [Test]
        public void RefToSheetNotLoadedIsAnError()
        {
            var result = SheetSetChecker.Check(Set(Units(Knight)));

            Assert.That(result.Errors, Has.Count.EqualTo(1));
            Assert.That(result.Errors[0], Does.StartWith("Units!E1: "));
            Assert.That(result.Errors[0], Does.Contain("'Weapons'"));
        }

        [Test]
        public void DuplicateSheetNameIsAnError()
        {
            var result = SheetSetChecker.Check(Set(Weapons(), Weapons()));

            Assert.That(result.Errors, Is.EqualTo(new[] { "Duplicate sheet name 'Weapons'" }));
            Assert.That(result.Sheets, Has.Count.EqualTo(1));
        }

        [Test]
        public void DuplicateSheetNameIgnoresCase()
        {
            var result = SheetSetChecker.Check(Set(Weapons(), Sheet("weapons", WeaponsHeader, new[] { "w_01", "1" })));

            Assert.That(result.Errors, Is.EqualTo(new[] { "Duplicate sheet name 'weapons': same as 'Weapons' ignoring case" }));
        }

        [Test]
        public void SheetWithoutNameIsAnError()
        {
            var result = SheetSetChecker.Check(Set(Weapons(), Sheet("", WeaponsHeader)));

            Assert.That(result.Errors, Is.EqualTo(new[] { "Sheet without a name" }));
        }

        [Test]
        public void TypeNameConflictIsAnError()
        {
            var result = SheetSetChecker.Check(Set(Weapons(), Sheet("WeaponsTable", WeaponsHeader, new[] { "w_01", "1" })));

            Assert.That(result.Errors, Is.EqualTo(new[]
            {
                "Type name 'WeaponsTable' is used by both the table class of sheet 'Weapons' and the row class of sheet 'WeaponsTable'",
            }));
        }

        [Test]
        public void RefPropertyConflictIsAnErrorAtHeader()
        {
            var result = SheetSetChecker.Check(Set(
                Sheet("Units", new[] { "id", "name", "hp:int", "rarity:enum<Rarity>", "Weapon:ref<Weapons>" },
                    new[] { "knight", "Knight", "1", "Epic", "w_01" }),
                Weapons()));

            Assert.That(result.Errors, Is.EqualTo(new[]
            {
                "Units!E1: Column 'Weapon': property 'Weapon' generated for ref<Weapons> has the same name as column 'Weapon'; " +
                "rename one of the columns",
            }));
            Assert.That(result.Enums.EnumNames, Is.Empty);
        }

        [Test]
        public void RefPropertyEqualToRowClassIsAnError()
        {
            var result = SheetSetChecker.Check(Set(
                Sheet("Units", new[] { "id", "units:ref<Units>[]" }, new[] { "knight", "" }),
                Weapons()));

            Assert.That(result.Errors, Is.EqualTo(new[]
            {
                "Units!B1: Column 'units': property 'Units' generated for ref<Units>[] has the same name as the row class 'Units'; " +
                "rename the column",
            }));
        }

        [Test]
        public void RefColumnsWithoutConflictsAreValid()
        {
            Valid(
                Sheet("Units", new[] { "id", "weapon:ref<Weapons>", "weapons:ref<Weapons>[]", "leader:ref<Units>" },
                    new[] { "knight", "w_01", "w_01|w_02", "" },
                    new[] { "squire", "", "", "knight" }),
                Weapons());
        }

        [Test]
        public void EnumNameConflictsWithSheet()
        {
            var result = SheetSetChecker.Check(Set(
                Sheet("Rarity", new[] { "id" }, new[] { "common" }),
                Sheet("Items", new[] { "id", "rarity:enum<Rarity>" }, new[] { "sword", "Epic" })));

            Assert.That(result.Errors, Is.EqualTo(new[]
            {
                "Type name 'Rarity' is used by both the row class of sheet 'Rarity' and the enum 'Rarity'",
            }));
        }

        [Test]
        public void InvalidEnumValueIsReportedOnce()
        {
            var result = SheetSetChecker.Check(Set(
                Sheet("Items", new[] { "id", "rarity:enum<Rarity>", "tags:enum<Tag>[]" },
                    new[] { "sword", "1st", "class|ok" })));

            Assert.That(result.Errors, Has.Count.EqualTo(2), string.Join("; ", result.Errors));
            Assert.That(result.Errors[0], Does.StartWith("Items!B2: "));
            Assert.That(result.Errors[1], Does.StartWith("Items!C2: "));
            Assert.That(result.Errors.Distinct().Count(), Is.EqualTo(2));
        }

        [Test]
        public void DiffWithEmptySnapshotAddsAllSheets()
        {
            var current = Valid(Units(Knight, Peasant), Weapons());

            var diff = SheetSetChecker.Diff(Set(), current);

            Assert.That(diff.IsAvailable, Is.True);
            Assert.That(diff.HasRemovals, Is.False);
            Assert.That(diff.Text, Is.EqualTo("+ Units (new sheet, 2 rows)\n+ Weapons (new sheet, 2 rows)"));
            Assert.That(SheetSetChecker.Diff(null, current).Text, Is.EqualTo(diff.Text));
        }

        [Test]
        public void DiffShowsChangedRow()
        {
            var previous = Set(Units(Knight, Peasant), Weapons());
            var changed = new[] { "knight", "Knight", "150", "Epic", "w_01" };
            var current = Valid(Units(changed, Peasant), Weapons());

            var diff = SheetSetChecker.Diff(previous, current);

            Assert.That(diff.Text, Is.EqualTo("Units: +0 -0 ~1\n  ~ knight: hp: 120 → 150"));
            Assert.That(diff.HasRemovals, Is.False);
        }

        [Test]
        public void DiffWithoutChanges()
        {
            var previous = Set(Units(Knight, Peasant), Weapons());

            var diff = SheetSetChecker.Diff(previous, Valid(Units(Knight, Peasant), Weapons()));

            Assert.That(diff.Text, Is.EqualTo("No changes"));
        }

        [Test]
        public void DiffReportsRemovedRows()
        {
            var previous = Set(Units(Knight, Peasant), Weapons());

            var diff = SheetSetChecker.Diff(previous, Valid(Units(Knight), Weapons()));

            Assert.That(diff.HasRemovals, Is.True);
            Assert.That(diff.Text, Is.EqualTo("Units: +0 -1 ~0\n  - peasant"));
        }

        [Test]
        public void DiffIgnoresSnapshotSheetsNotInCurrentSet()
        {
            var previous = Set(Weapons(), Sheet("Old", new[] { "id" }, new[] { "a" }));

            var diff = SheetSetChecker.Diff(previous, Valid(Weapons()));

            Assert.That(diff.HasRemovals, Is.False);
            Assert.That(diff.Text, Is.EqualTo("No changes"));
        }

        [Test]
        public void DiffIgnoresErrorsOfSnapshotSheetsNotInCurrentSet()
        {
            var previous = Set(Weapons(), Sheet("Old", new[] { "id", "hp:int" }, new[] { "a", "abc" }));

            var diff = SheetSetChecker.Diff(previous, Valid(Weapons()));

            Assert.That(diff.IsAvailable, Is.True);
            Assert.That(diff.Text, Is.EqualTo("No changes"));
        }

        [Test]
        public void DiffDoesNotRecheckRefsOfSnapshot()
        {
            // Snapshot sheets may come from different imports: Units refers to a weapon no longer in Weapons.
            var previous = Set(Units(Knight), Sheet("Weapons", WeaponsHeader, new[] { "w_02", "20" }));

            var diff = SheetSetChecker.Diff(previous, Valid(Units(Knight), Weapons()));

            Assert.That(diff.IsAvailable, Is.True);
            Assert.That(diff.Text, Is.EqualTo("Weapons: +1 -0 ~0\n  + w_01"));
        }

        [Test]
        public void DiffWithUnreadableSnapshotIsNotAvailable()
        {
            var previous = Set(Units(new[] { "knight", "Knight", "abc", "", "" }), Weapons());

            var diff = SheetSetChecker.Diff(previous, Valid(Units(Knight), Weapons()));

            Assert.That(diff.IsAvailable, Is.False);
            Assert.That(diff.Diff, Is.Null);
            Assert.That(diff.HasRemovals, Is.False);
            Assert.That(diff.Text, Does.StartWith(SheetSetChecker.PreviousUnreadableText + ": Units!C2: "));
        }

        [Test]
        public void DiffNeedsCheckedCurrentSheets()
        {
            var current = SheetSetChecker.Check(Set(Weapons(), Weapons()));

            Assert.Throws<System.ArgumentException>(() => SheetSetChecker.Diff(Set(), current));
        }

        // Schema from code.

#pragma warning disable CS0649 // Fields are set through reflection only.
        [System.Serializable]
        private sealed class CodeUnit
        {
            public string id;
            public string name;
            public int hp;
            public Rarity rarity;
            public string weapon;
            public List<string> tags;
        }

        [System.Serializable]
        private sealed class OtherRarityRow
        {
            public string id;
            public RowMapperTests.Rarity rarity;
        }

        [System.Serializable]
        private sealed class IntIdRow
        {
            public int id;
        }

        private sealed class NotSerializableRow
        {
            public string id;
        }

        [System.Serializable]
        private sealed class NoConstructorRow
        {
            public string id;

            public NoConstructorRow(string id) => this.id = id;
        }
#pragma warning restore CS0649

        private static readonly string[] CodeUnitsHeader = { "id", "name", "hp", "rarity", "weapon:ref<Weapons>", "tags" };

        private static (string Name, IReadOnlyList<IReadOnlyList<string>> Rows, System.Type RowType) Code(
            (string Name, IReadOnlyList<IReadOnlyList<string>> Rows) sheet, System.Type rowType) =>
            (sheet.Name, sheet.Rows, rowType);

        private static (string Name, IReadOnlyList<IReadOnlyList<string>> Rows, System.Type RowType) Generated(
            (string Name, IReadOnlyList<IReadOnlyList<string>> Rows) sheet) =>
            (sheet.Name, sheet.Rows, null);

        private static (string, IReadOnlyList<IReadOnlyList<string>>) CodeUnits(params string[][] rows) =>
            Sheet("Units", new[] { CodeUnitsHeader }.Concat(rows).ToArray());

        private static string[] CodeKnight => new[] { "knight", "Knight", "120", "Epic", "w_01", "a|b" };

        private static SheetSetCheckResult CheckMixed(
            params (string Name, IReadOnlyList<IReadOnlyList<string>> Rows, System.Type RowType)[] sheets) =>
            SheetSetChecker.Check(sheets);

        [Test]
        public void MixedSetParsesCodeSheetByClass()
        {
            var result = CheckMixed(Code(CodeUnits(CodeKnight), typeof(CodeUnit)), Generated(Weapons()));

            Assert.That(result.IsValid, Is.True, string.Join("; ", result.Errors));
            Assert.That(result.RowTypes["Units"], Is.EqualTo(typeof(CodeUnit)));
            Assert.That(result.RowTypes.ContainsKey("Weapons"), Is.False);
            Assert.That(result.Enums.EnumNames, Is.Empty, "C# enums are not generated");
            var units = result.Sheets[0];
            Assert.That(units.Rows[0].Values["hp"], Is.EqualTo(120));
            Assert.That(units.Rows[0].Values["tags"], Is.EqualTo(new[] { "a", "b" }));
        }

        [Test]
        public void MixedSetChecksRefsBetweenModes()
        {
            var result = CheckMixed(
                Code(CodeUnits(new[] { "knight", "Knight", "120", "Epic", "w_09", "" }), typeof(CodeUnit)),
                Generated(Weapons()));

            Assert.That(result.Errors, Has.Count.EqualTo(1));
            Assert.That(result.Errors[0], Does.StartWith("Units!E2: "));
            Assert.That(result.Errors[0], Does.Contain("no row with this id in sheet 'Weapons'"));
        }

        [Test]
        public void GeneratedSheetKeepsItsEnumsNextToCodeSheet()
        {
            var items = Sheet("Items", new[] { "id", "grade:enum<Quality>" }, new[] { "gem", "High" });

            var result = CheckMixed(Code(CodeUnits(CodeKnight), typeof(CodeUnit)), Generated(Weapons()), Generated(items));

            Assert.That(result.IsValid, Is.True, string.Join("; ", result.Errors));
            Assert.That(result.Enums.EnumNames, Is.EqualTo(new[] { "Quality" }));
        }

        [Test]
        public void UnknownCSharpEnumValueListsAllowedValues()
        {
            var result = CheckMixed(
                Code(CodeUnits(new[] { "knight", "Knight", "120", "Legendary", "w_01", "" }), typeof(CodeUnit)),
                Generated(Weapons()));

            Assert.That(result.Errors, Has.Count.EqualTo(1));
            Assert.That(result.Errors[0], Does.StartWith("Units!D2: "));
            Assert.That(result.Errors[0], Does.Contain("allowed: Common, Epic"));
        }

        [Test]
        public void TwoCSharpEnumsWithSameNameAreAnError()
        {
            var other = Sheet("Heroes", new[] { "id", "rarity" }, new[] { "hero", "Epic" });

            var result = CheckMixed(Code(CodeUnits(CodeKnight), typeof(CodeUnit)), Generated(Weapons()),
                Code(other, typeof(OtherRarityRow)));

            Assert.That(result.Errors, Has.Count.EqualTo(1));
            Assert.That(result.Errors[0], Does.StartWith("Heroes!B1: enum name 'Rarity' is used by two C# enums"));
        }

        [Test]
        public void GeneratedEnumWithNameOfCSharpEnumIsAnError()
        {
            var items = Sheet("Items", new[] { "id", "rarity:enum<Rarity>" }, new[] { "gem", "Epic" });

            var result = CheckMixed(Code(CodeUnits(CodeKnight), typeof(CodeUnit)), Generated(Weapons()), Generated(items));

            Assert.That(result.Errors, Has.Count.EqualTo(1));
            Assert.That(result.Errors[0], Does.StartWith("Items!B1: column 'rarity' uses enum<Rarity>"));
            Assert.That(result.Enums.EnumNames, Is.Empty);
        }

        [Test]
        public void CodeSheetTakesNoPartInTypeNameCheck()
        {
            // In generation mode the table class of 'Units' would be 'UnitsTable', the row class of the other sheet.
            var unitsTable = Sheet("UnitsTable", new[] { "id" }, new[] { "a" });

            Assert.That(SheetSetChecker.Check(Set(Units(Knight), Weapons(), unitsTable)).Errors,
                Has.Some.Contains("Type name 'UnitsTable'"));

            var result = CheckMixed(Code(CodeUnits(CodeKnight), typeof(CodeUnit)), Generated(Weapons()),
                Generated(unitsTable));

            Assert.That(result.IsValid, Is.True, string.Join("; ", result.Errors));
        }

        [Test]
        public void RowClassMustBeSerializable()
        {
            var result = CheckMixed(Code(Sheet("Units", new[] { "id" }, new[] { "a" }), typeof(NotSerializableRow)));

            Assert.That(result.Errors, Has.Count.EqualTo(1));
            Assert.That(result.Errors[0], Does.StartWith("Sheet 'Units': row class "));
            Assert.That(result.Errors[0], Does.Contain("[Serializable]"));
        }

        [Test]
        public void RowClassNeedsParameterlessConstructor()
        {
            var result = CheckMixed(Code(Sheet("Units", new[] { "id" }, new[] { "a" }), typeof(NoConstructorRow)));

            Assert.That(result.Errors, Has.Count.EqualTo(1));
            Assert.That(result.Errors[0], Does.Contain("public parameterless constructor"));
        }

        [Test]
        public void CodeSheetKeepsIdChecks()
        {
            var result = CheckMixed(Code(Sheet("Units", new[] { "id" }, new[] { "1" }), typeof(IntIdRow)));

            Assert.That(result.Errors, Has.Some.Contains("column 'id' must be of type 'string'"));
        }

        [Test]
        public void DiffParsesSnapshotOfCodeSheetByClass()
        {
            var current = CheckMixed(Code(CodeUnits(CodeKnight), typeof(CodeUnit)), Generated(Weapons()));
            Assert.That(current.IsValid, Is.True, string.Join("; ", current.Errors));

            var same = SheetSetChecker.Diff(Set(CodeUnits(CodeKnight), Weapons()), current);
            Assert.That(same.Text, Is.EqualTo("No changes"));

            var changed = SheetSetChecker.Diff(
                Set(CodeUnits(new[] { "knight", "Knight", "100", "Epic", "w_01", "a|b" }), Weapons()), current);
            Assert.That(changed.Text, Is.EqualTo("Units: +0 -0 ~1\n  ~ knight: hp: 100 → 120"));
        }
    }
}
