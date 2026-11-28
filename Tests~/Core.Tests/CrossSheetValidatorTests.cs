using System;
using System.Collections.Generic;
using System.Linq;
using Kentavr.SheetsToSO.Core;
using NUnit.Framework;

namespace Kentavr.SheetsToSO.Core.Tests
{
    public class CrossSheetValidatorTests
    {
        private static readonly string[] UnitsHeader =
            { "id", "name:string", "hp:int", "rarity:enum<Rarity>", "weapon:ref<Weapons>", "spares:ref<Weapons>[]" };

        private static SheetParseResult Sheet(string name, params string[][] rows)
        {
            var result = SheetParser.Parse(name, rows);
            Assert.That(result.IsValid, Is.True, string.Join("; ", result.Errors));
            return result;
        }

        private static SheetParseResult Weapons() => Sheet("Weapons",
            new[] { "id", "name:string", "rarity:enum<Rarity>" },
            new[] { "sword_01", "Sword", "Epic" },
            new[] { "axe_02", "Axe", "Rare" });

        private static CrossSheetResult Validate(IReadOnlyDictionary<string, IReadOnlyCollection<string>> knownEnums,
            params SheetParseResult[] sheets) => CrossSheetValidator.Validate(sheets, knownEnums);

        private static CrossSheetResult Validate(params SheetParseResult[] sheets) => CrossSheetValidator.Validate(sheets);

        private static SheetError SingleError(CrossSheetResult result)
        {
            Assert.That(result.Errors, Has.Count.EqualTo(1), string.Join("; ", result.Errors));
            return result.Errors[0];
        }

        private static IReadOnlyDictionary<string, IReadOnlyCollection<string>> Enums(string name, params string[] values) =>
            new Dictionary<string, IReadOnlyCollection<string>> { [name] = values };

        [Test]
        public void ValidSetHasNoErrors()
        {
            var units = Sheet("Units", UnitsHeader,
                new[] { "knight", "Knight", "10", "Epic", "sword_01", "axe_02|sword_01" },
                new[] { "peasant", "Peasant", "1", "", "", "" });
            var nodes = Sheet("Nodes",
                new[] { "id", "next:ref<Nodes>", "prev:ref<Nodes>[]" },
                new[] { "a", "b", "" },
                new[] { "b", "", "a|b" });

            var result = Validate(Weapons(), units, nodes);

            Assert.That(result.IsValid, Is.True, string.Join("; ", result.Errors));
            Assert.That(result.Errors, Is.Empty);
        }

        [Test]
        public void BrokenRefReportsCellAddressValueAndTargetSheet()
        {
            var units = Sheet("Units", UnitsHeader,
                new[] { "knight", "Knight", "10", "Epic", "sword_01", "" },
                new[] { "orc", "Orc", "5", "Rare", "axe_99", "" });

            var error = SingleError(Validate(Weapons(), units));

            Assert.That(error.CellCode, Is.EqualTo(CellErrorCode.BrokenRef));
            Assert.That(error.Address, Is.EqualTo("Units!E3"));
            Assert.That(error.Message, Does.Contain("weapon"));
            Assert.That(error.Message, Does.Contain("axe_99"));
            Assert.That(error.Message, Does.Contain("Weapons"));
        }

        [Test]
        public void BrokenRefInArrayReportsElementNumber()
        {
            var units = Sheet("Units", UnitsHeader,
                new[] { "knight", "Knight", "10", "", "", "sword_01|axe_99" });

            var error = SingleError(Validate(Weapons(), units));

            Assert.That(error.CellCode, Is.EqualTo(CellErrorCode.BrokenRef));
            Assert.That(error.Address, Is.EqualTo("Units!F2"));
            Assert.That(error.Message, Does.Contain("spares"));
            Assert.That(error.Message, Does.Contain("element 2 'axe_99'"));
            Assert.That(error.Message, Does.Contain("Weapons"));
        }

        [Test]
        public void UnknownRefSheetGivesOneErrorAtHeader()
        {
            var units = Sheet("Units", UnitsHeader,
                new[] { "knight", "Knight", "10", "", "sword_01", "" },
                new[] { "orc", "Orc", "5", "", "axe_99", "" },
                new[] { "elf", "Elf", "5", "", "bow_01", "" });

            var result = Validate(units);

            Assert.That(result.Errors, Has.Count.EqualTo(2), string.Join("; ", result.Errors));
            Assert.That(result.Errors.Select(e => e.CellCode), Is.All.EqualTo(CellErrorCode.UnknownRefSheet));
            Assert.That(result.Errors.Select(e => e.Address), Is.EqualTo(new[] { "Units!E1", "Units!F1" }));
            Assert.That(result.Errors[0].Message, Does.Contain("weapon"));
            Assert.That(result.Errors[0].Message, Does.Contain("Weapons"));
        }

        [Test]
        public void RefIsCaseSensitive()
        {
            var units = Sheet("Units", UnitsHeader,
                new[] { "knight", "Knight", "10", "", "Sword_01", "" });

            var error = SingleError(Validate(Weapons(), units));

            Assert.That(error.CellCode, Is.EqualTo(CellErrorCode.BrokenRef));
            Assert.That(error.Message, Does.Contain("Sword_01"));
        }

        [Test]
        public void KnownEnumAcceptsListedValueAndRejectsTypo()
        {
            var units = Sheet("Units", UnitsHeader,
                new[] { "knight", "Knight", "10", "Epic", "", "" },
                new[] { "orc", "Orc", "5", "Epci", "", "" });

            var result = Validate(Enums("Rarity", "Common", "Rare", "Epic"), Weapons(), units);

            var error = SingleError(result);
            Assert.That(error.CellCode, Is.EqualTo(CellErrorCode.UnknownEnumValue));
            Assert.That(error.Address, Is.EqualTo("Units!D3"));
            Assert.That(error.Message, Does.Contain("rarity"));
            Assert.That(error.Message, Does.Contain("Epci"));
            Assert.That(error.Message, Does.Contain("Rarity"));
            Assert.That(error.Message, Does.Contain("Common, Rare, Epic"));
        }

        [Test]
        public void KnownEnumIsCaseSensitive()
        {
            var units = Sheet("Units", UnitsHeader, new[] { "knight", "Knight", "10", "epic", "", "" });

            var error = SingleError(Validate(Enums("Rarity", "Epic", "Rare"), Weapons(), units));

            Assert.That(error.CellCode, Is.EqualTo(CellErrorCode.UnknownEnumValue));
        }

        [Test]
        public void KnownEnumArrayReportsElementNumber()
        {
            var items = Sheet("Items",
                new[] { "id", "flags:enum<Flag>[]" },
                new[] { "a", "Fire|Ice|Wnd" });

            var error = SingleError(Validate(Enums("Flag", "Fire", "Ice", "Wind"), items));

            Assert.That(error.CellCode, Is.EqualTo(CellErrorCode.UnknownEnumValue));
            Assert.That(error.Address, Is.EqualTo("Items!B2"));
            Assert.That(error.Message, Does.Contain("element 3 'Wnd'"));
        }

        [Test]
        public void KnownEnumIsNotCollected()
        {
            var result = Validate(Enums("Rarity", "Epic", "Rare"), Weapons());

            Assert.That(result.IsValid, Is.True, string.Join("; ", result.Errors));
            Assert.That(result.CollectedEnums.ContainsKey("Rarity"), Is.False);
        }

        [Test]
        public void UnknownEnumValuesAreCollectedWithoutDuplicatesInOrder()
        {
            var units = Sheet("Units", UnitsHeader,
                new[] { "knight", "Knight", "10", "Rare", "", "" },
                new[] { "orc", "Orc", "5", "Common", "", "" },
                new[] { "elf", "Elf", "5", "", "", "" });
            var items = Sheet("Items",
                new[] { "id", "rarity:enum<Rarity>", "flags:enum<Flag>[]" },
                new[] { "a", "Epic", "Ice|Fire|Ice" });

            var result = Validate(Weapons(), units, items);

            Assert.That(result.IsValid, Is.True, string.Join("; ", result.Errors));
            Assert.That(result.CollectedEnums.Keys, Is.EquivalentTo(new[] { "Rarity", "Flag" }));
            Assert.That(result.CollectedEnums["Rarity"], Is.EqualTo(new[] { "Epic", "Rare", "Common" }));
            Assert.That(result.CollectedEnums["Flag"], Is.EqualTo(new[] { "Ice", "Fire" }));
        }

        [Test]
        public void UnknownEnumRejectsInvalidNames()
        {
            var items = Sheet("Items",
                new[] { "id", "rarity:enum<Rarity>", "flags:enum<Flag>[]" },
                new[] { "a", "1st", "Ice|class" },
                new[] { "b", "Epic", "" });

            var result = Validate(items);

            Assert.That(result.Errors.Select(e => e.CellCode),
                Is.All.EqualTo(CellErrorCode.InvalidEnumValue));
            Assert.That(result.Errors.Select(e => e.Address), Is.EqualTo(new[] { "Items!B2", "Items!C2" }));
            Assert.That(result.Errors[0].Message, Does.Contain("'1st'"));
            Assert.That(result.Errors[1].Message, Does.Contain("element 2 'class'"));
            Assert.That(result.CollectedEnums["Rarity"], Is.EqualTo(new[] { "Epic" }));
            Assert.That(result.CollectedEnums["Flag"], Is.EqualTo(new[] { "Ice" }));
        }

        [Test]
        public void CollectsAllErrorsAcrossSheetsInOrder()
        {
            var units = Sheet("Units", UnitsHeader,
                new[] { "knight", "Knight", "10", "Epci", "axe_99", "" },
                new[] { "orc", "Orc", "5", "", "", "sword_01|bow_7" });
            var items = Sheet("Items",
                new[] { "id", "loot:ref<Loot>", "rarity:enum<Rarity>", "weapon:ref<Weapons>" },
                new[] { "a", "x", "Bad", "nope" });

            var result = Validate(Enums("Rarity", "Epic", "Rare"), Weapons(), units, items);

            Assert.That(result.Errors.Select(e => e.Address), Is.EqualTo(new[]
            {
                "Units!D2", "Units!E2", "Units!F3",
                "Items!B1", "Items!C2", "Items!D2",
            }), string.Join("; ", result.Errors));
            Assert.That(result.Errors.Select(e => e.CellCode), Is.EqualTo(new CellErrorCode?[]
            {
                CellErrorCode.UnknownEnumValue, CellErrorCode.BrokenRef, CellErrorCode.BrokenRef,
                CellErrorCode.UnknownRefSheet, CellErrorCode.UnknownEnumValue, CellErrorCode.BrokenRef,
            }));
        }

        [Test]
        public void ChecksRowsOfSheetWithParseErrorsWithoutCopyingThem()
        {
            var units = SheetParser.Parse("Units", new[]
            {
                UnitsHeader,
                new[] { "knight", "Knight", "abc", "", "axe_99", "" },
            });
            Assert.That(units.IsValid, Is.False);

            var error = SingleError(Validate(Weapons(), units));

            Assert.That(error.CellCode, Is.EqualTo(CellErrorCode.BrokenRef));
            Assert.That(error.Address, Is.EqualTo("Units!E2"));
        }

        [TestCase("id:strng")]
        [TestCase("name")]
        public void RefsToSheetWithoutIdColumnAreNotChecked(string firstHeader)
        {
            var weapons = SheetParser.Parse("Weapons", new[]
            {
                new[] { firstHeader, "damage:int" },
                new[] { "sword_01", "10" },
            });
            Assert.That(weapons.Columns.Select(c => c.Name), Has.None.EqualTo("id"));
            var units = Sheet("Units", UnitsHeader,
                new[] { "knight", "Knight", "10", "", "axe_99", "bow_01|sword_01" });

            var result = Validate(weapons, units);

            Assert.That(result.Errors, Is.Empty, string.Join("; ", result.Errors));
        }

        [Test]
        public void SheetWithoutIdColumnIsStillChecked()
        {
            var items = SheetParser.Parse("Items", new[]
            {
                new[] { "id:strng", "rarity:enum<Rarity>", "weapon:ref<Weapons>" },
                new[] { "sword", "1st", "axe_99" },
            });

            var result = Validate(Weapons(), items);

            Assert.That(result.Errors.Select(e => e.Address), Is.EqualTo(new[] { "Items!B2", "Items!C2" }),
                string.Join("; ", result.Errors));
        }

        [Test]
        public void RefToSheetNotInSetIsStillUnknown()
        {
            var units = Sheet("Units", new[] { "id", "weapon:ref<Weapons>" }, new[] { "knight", "sword_01" });
            var other = SheetParser.Parse("Other", new[] { new[] { "name" }, new[] { "a" } });

            var error = SingleError(Validate(units, other));

            Assert.That(error.CellCode, Is.EqualTo(CellErrorCode.UnknownRefSheet));
            Assert.That(error.Address, Is.EqualTo("Units!B1"));
        }

        [Test]
        public void ThrowsOnInvalidArguments()
        {
            Assert.Throws<ArgumentNullException>(() => CrossSheetValidator.Validate(null));
            Assert.Throws<ArgumentException>(() => CrossSheetValidator.Validate(new SheetParseResult[] { null }));
            Assert.Throws<ArgumentException>(() => CrossSheetValidator.Validate(new[] { Weapons(), Weapons() }));
            Assert.Throws<ArgumentException>(() => CrossSheetValidator.Validate(new[] { Weapons() },
                new Dictionary<string, IReadOnlyCollection<string>> { ["Rarity"] = null }));
        }
    }
}
