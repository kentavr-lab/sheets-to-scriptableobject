using System;
using System.Collections.Generic;
using Kentavr.SheetsToSO.Core;
using NUnit.Framework;

namespace Kentavr.SheetsToSO.Core.Tests
{
    public class RowMapperTests
    {
        public enum Rarity
        {
            Common,
            Rare,
            Epic,
        }

        public sealed class TestRow
        {
            public string id;
            public string name;
            public int hp;
            public float speed;
            public bool flying;
            public Rarity rarity = Rarity.Rare;
            public string weapon;
            public string[] tags;
            public int[] levels;
            public float[] weights;
            public bool[] flags;
            public Rarity[] rarities;
            public string[] drops;
        }

        private static SheetParseResult Parse(params string[][] rows)
        {
            var result = SheetParser.Parse("Units", rows);
            Assert.That(result.IsValid, Is.True, string.Join("; ", result.Errors));
            return result;
        }

        private static readonly string[] Header =
        {
            "id", "name:string!", "hp:int", "speed:float", "flying:bool", "rarity:enum<Rarity>", "weapon:ref<Weapons>",
            "tags:string[]", "levels:int[]", "weights:float[]", "flags:bool[]", "rarities:enum<Rarity>[]",
            "drops:ref<Items>[]", "#notes",
        };

        [Test]
        public void MapsAllTypes()
        {
            var sheet = Parse(Header, new[]
            {
                "knight", "Knight", "120", "1.5", "true", "Epic", "sword",
                "a|b", "1|2|3", "0.5|2", "true|false", "Common|Epic", "gem|coin", "ignored",
            });

            var row = (TestRow)RowMapper.Map(typeof(TestRow), sheet.Rows[0], "Units");

            Assert.That(row.id, Is.EqualTo("knight"));
            Assert.That(row.name, Is.EqualTo("Knight"));
            Assert.That(row.hp, Is.EqualTo(120));
            Assert.That(row.speed, Is.EqualTo(1.5f));
            Assert.That(row.flying, Is.True);
            Assert.That(row.rarity, Is.EqualTo(Rarity.Epic));
            Assert.That(row.weapon, Is.EqualTo("sword"));
            Assert.That(row.tags, Is.EqualTo(new[] { "a", "b" }));
            Assert.That(row.levels, Is.EqualTo(new[] { 1, 2, 3 }));
            Assert.That(row.weights, Is.EqualTo(new[] { 0.5f, 2f }));
            Assert.That(row.flags, Is.EqualTo(new[] { true, false }));
            Assert.That(row.rarities, Is.EqualTo(new[] { Rarity.Common, Rarity.Epic }));
            Assert.That(row.drops, Is.EqualTo(new[] { "gem", "coin" }));
        }

        [Test]
        public void EmptyCellsGiveDefaults()
        {
            var sheet = Parse(Header, new[] { "knight", "Knight" });

            var row = (TestRow)RowMapper.Map(typeof(TestRow), sheet.Rows[0]);

            Assert.That(row.hp, Is.EqualTo(0));
            Assert.That(row.speed, Is.EqualTo(0f));
            Assert.That(row.flying, Is.False);
            Assert.That(row.rarity, Is.EqualTo(Rarity.Rare), "empty enum keeps the field default");
            Assert.That(row.weapon, Is.Null);
            Assert.That(row.tags, Is.Empty);
            Assert.That(row.levels, Is.Empty);
            Assert.That(row.rarities, Is.Empty);
            Assert.That(row.rarities, Is.TypeOf<Rarity[]>());
            Assert.That(row.drops, Is.Empty);
        }

        [Test]
        public void MissingFieldThrowsWithSheetAndColumn()
        {
            var sheet = Parse(new[] { "id", "mana:int" }, new[] { "knight", "5" });

            var ex = Assert.Throws<ArgumentException>(() => RowMapper.Map(typeof(TestRow), sheet.Rows[0], "Units"));

            Assert.That(ex.Message, Does.Contain("Units"));
            Assert.That(ex.Message, Does.Contain("mana"));
        }

        [Test]
        public void IncompatibleFieldTypeThrows()
        {
            var sheet = Parse(new[] { "id", "name:int" }, new[] { "knight", "5" });

            var ex = Assert.Throws<ArgumentException>(() => RowMapper.Map(typeof(TestRow), sheet.Rows[0], "Units"));

            Assert.That(ex.Message, Does.Contain("'name'"));
        }

        [Test]
        public void UnknownEnumValueThrows()
        {
            var sheet = Parse(new[] { "id", "rarity:enum<Rarity>" }, new[] { "knight", "Legendary" });

            Assert.Throws<ArgumentException>(() => RowMapper.Map(typeof(TestRow), sheet.Rows[0], "Units"));
        }

        [Test]
        public void EnumIsCaseSensitive()
        {
            var sheet = Parse(new[] { "id", "rarities:enum<Rarity>[]" }, new[] { "knight", "Common|epic" });

            Assert.Throws<ArgumentException>(() => RowMapper.Map(typeof(TestRow), sheet.Rows[0], "Units"));
        }

        [Test]
        public void RejectsNullArguments()
        {
            var row = new SheetRow(2, "x", new Dictionary<string, object>());

            Assert.Throws<ArgumentNullException>(() => RowMapper.Map(null, row));
            Assert.Throws<ArgumentNullException>(() => RowMapper.Map(typeof(TestRow), null));
        }
    }
}
