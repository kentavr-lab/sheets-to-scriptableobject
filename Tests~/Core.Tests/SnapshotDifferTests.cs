using System;
using System.Collections.Generic;
using System.Linq;
using Kentavr.SheetsToSO.Core;
using NUnit.Framework;

namespace Kentavr.SheetsToSO.Core.Tests
{
    public class SnapshotDifferTests
    {
        private static readonly string[] Header =
            { "id", "name:string", "hp:int", "speed:float", "stackable:bool", "rarity:enum<Rarity>", "weapon:ref<Weapons>", "tags:string[]", "levels:int[]" };

        private static readonly string[] Sword = { "sword_01", "Sword", "120", "1.5", "true", "Epic", "w_01", "melee|starter", "1|2" };
        private static readonly string[] Axe = { "axe_02", "Axe", "90", "0.5", "false", "", "", "", "" };

        internal static SheetParseResult Sheet(string name, params string[][] rows)
        {
            var result = SheetParser.Parse(name, rows);
            Assert.That(result.IsValid, Is.True, string.Join("; ", result.Errors));
            return result;
        }

        internal static IReadOnlyList<SheetParseResult> Snapshot(params SheetParseResult[] sheets) => sheets;

        private static SnapshotDiff Compare(SheetParseResult before, SheetParseResult after) =>
            SnapshotDiffer.Compare(Snapshot(before), Snapshot(after));

        private static string[] With(string[] row, int index, string value)
        {
            var copy = (string[])row.Clone();
            copy[index] = value;
            return copy;
        }

        private static FieldChange SingleField(SnapshotDiff diff)
        {
            Assert.That(diff.Sheets, Has.Count.EqualTo(1));
            var sheet = diff.Sheets[0];
            Assert.That(sheet.Kind, Is.EqualTo(SheetDiffKind.Changed));
            Assert.That(sheet.AddedRows, Is.Empty);
            Assert.That(sheet.RemovedRows, Is.Empty);
            Assert.That(sheet.ChangedRows, Has.Count.EqualTo(1));
            Assert.That(sheet.ChangedRows[0].Fields, Has.Count.EqualTo(1));
            return sheet.ChangedRows[0].Fields[0];
        }

        private static FieldChange ChangeOne(int column, string value)
        {
            return SingleField(Compare(Sheet("Units", Header, Sword, Axe), Sheet("Units", Header, With(Sword, column, value), Axe)));
        }

        [Test]
        public void IdenticalSnapshotsHaveNoChanges()
        {
            var diff = Compare(Sheet("Units", Header, Sword, Axe), Sheet("Units", Header, Sword, Axe));

            Assert.That(diff.HasChanges, Is.False);
            Assert.That(diff.Sheets, Is.Empty);
        }

        [Test]
        public void EmptySnapshotsHaveNoChanges()
        {
            var diff = SnapshotDiffer.Compare(Snapshot(), Snapshot());

            Assert.That(diff.HasChanges, Is.False);
        }

        [Test]
        public void FirstImportAddsAllSheets()
        {
            var units = Sheet("Units", Header, Sword, Axe);
            var weapons = Sheet("Weapons", new[] { "id" }, new[] { "w_01" });

            var diff = SnapshotDiffer.Compare(Snapshot(), Snapshot(units, weapons));

            Assert.That(diff.Sheets.Select(s => s.SheetName), Is.EqualTo(new[] { "Units", "Weapons" }));
            Assert.That(diff.Sheets.Select(s => s.Kind), Is.All.EqualTo(SheetDiffKind.Added));
            var added = diff.Sheets[0];
            Assert.That(added.AddedRows.Select(r => r.Id), Is.EqualTo(new[] { "sword_01", "axe_02" }));
            Assert.That(added.AddedRows.Select(r => r.NewRowNumber), Is.EqualTo(new[] { 2, 3 }));
            Assert.That(added.AddedRows.Select(r => r.OldRowNumber), Is.All.EqualTo(0));
            Assert.That(added.AddedRows.SelectMany(r => r.Fields), Is.Empty);
            Assert.That(added.RemovedRows, Is.Empty);
            Assert.That(added.ChangedRows, Is.Empty);
        }

        [Test]
        public void DetectsAddedAndRemovedSheets()
        {
            var units = Sheet("Units", Header, Sword);
            var weapons = Sheet("Weapons", new[] { "id" }, new[] { "w_01" }, new[] { "w_02" });
            var old = Sheet("Old", new[] { "id" }, new[] { "o_01" });

            var diff = SnapshotDiffer.Compare(Snapshot(units, old), Snapshot(units, weapons));

            Assert.That(diff.Sheets.Select(s => s.SheetName), Is.EqualTo(new[] { "Weapons", "Old" }));
            Assert.That(diff.Sheets[0].Kind, Is.EqualTo(SheetDiffKind.Added));
            Assert.That(diff.Sheets[0].AddedRows, Has.Count.EqualTo(2));
            Assert.That(diff.Sheets[1].Kind, Is.EqualTo(SheetDiffKind.Removed));
            Assert.That(diff.Sheets[1].RemovedRows.Select(r => r.Id), Is.EqualTo(new[] { "o_01" }));
            Assert.That(diff.Sheets[1].RemovedRows[0].OldRowNumber, Is.EqualTo(2));
            Assert.That(diff.Sheets[1].RemovedRows[0].NewRowNumber, Is.EqualTo(0));
            Assert.That(diff.Sheets[1].AddedRows, Is.Empty);
        }

        [Test]
        public void DetectsAddedAndRemovedRows()
        {
            var bow = new[] { "bow_03", "Bow", "50", "2", "false", "", "", "", "" };

            var diff = Compare(Sheet("Units", Header, Sword, Axe), Sheet("Units", Header, Sword, bow));

            var sheet = diff.Sheets.Single();
            Assert.That(sheet.Kind, Is.EqualTo(SheetDiffKind.Changed));
            Assert.That(sheet.AddedRows.Select(r => r.Id), Is.EqualTo(new[] { "bow_03" }));
            Assert.That(sheet.AddedRows[0].NewRowNumber, Is.EqualTo(3));
            Assert.That(sheet.AddedRows[0].OldRowNumber, Is.EqualTo(0));
            Assert.That(sheet.RemovedRows.Select(r => r.Id), Is.EqualTo(new[] { "axe_02" }));
            Assert.That(sheet.RemovedRows[0].OldRowNumber, Is.EqualTo(3));
            Assert.That(sheet.ChangedRows, Is.Empty);
        }

        [Test]
        public void ChangedIdIsRemoveAndAdd()
        {
            var diff = Compare(Sheet("Units", Header, Sword), Sheet("Units", Header, With(Sword, 0, "sword_02")));

            var sheet = diff.Sheets.Single();
            Assert.That(sheet.RemovedRows.Select(r => r.Id), Is.EqualTo(new[] { "sword_01" }));
            Assert.That(sheet.AddedRows.Select(r => r.Id), Is.EqualTo(new[] { "sword_02" }));
            Assert.That(sheet.ChangedRows, Is.Empty);
        }

        [Test]
        public void IdIsCaseSensitive()
        {
            var diff = Compare(Sheet("Units", Header, Sword), Sheet("Units", Header, With(Sword, 0, "Sword_01")));

            var sheet = diff.Sheets.Single();
            Assert.That(sheet.RemovedRows.Select(r => r.Id), Is.EqualTo(new[] { "sword_01" }));
            Assert.That(sheet.AddedRows.Select(r => r.Id), Is.EqualTo(new[] { "Sword_01" }));
            Assert.That(sheet.ChangedRows, Is.Empty);
        }

        [Test]
        public void SheetNameIsCaseSensitive()
        {
            var diff = SnapshotDiffer.Compare(Snapshot(Sheet("Units", Header, Sword)), Snapshot(Sheet("units", Header, Sword)));

            Assert.That(diff.Sheets.Select(s => s.SheetName), Is.EqualTo(new[] { "units", "Units" }));
            Assert.That(diff.Sheets.Select(s => s.Kind), Is.EqualTo(new[] { SheetDiffKind.Added, SheetDiffKind.Removed }));
        }

        [Test]
        public void DetectsIntChange()
        {
            var field = ChangeOne(2, "150");

            Assert.That(field.Column, Is.EqualTo("hp"));
            Assert.That(field.OldValue, Is.EqualTo(120));
            Assert.That(field.NewValue, Is.EqualTo(150));
            Assert.That(field.OldText, Is.EqualTo("120"));
            Assert.That(field.NewText, Is.EqualTo("150"));
            Assert.That(field.OldMissing, Is.False);
            Assert.That(field.NewMissing, Is.False);
        }

        [Test]
        public void DetectsFloatChange()
        {
            var field = ChangeOne(3, "0.1");

            Assert.That(field.Column, Is.EqualTo("speed"));
            Assert.That(field.OldValue, Is.EqualTo(1.5f));
            Assert.That(field.NewValue, Is.EqualTo(0.1f));
            Assert.That(field.OldText, Is.EqualTo("1.5"));
            Assert.That(field.NewText, Is.EqualTo("0.1"));
        }

        [Test]
        public void FloatWrittenDifferentlyButEqualIsNotChange()
        {
            var diff = Compare(Sheet("Units", Header, Sword), Sheet("Units", Header, With(Sword, 3, "1.50")));

            Assert.That(diff.HasChanges, Is.False);
        }

        [Test]
        public void DetectsBoolChange()
        {
            var field = ChangeOne(4, "false");

            Assert.That(field.Column, Is.EqualTo("stackable"));
            Assert.That(field.OldText, Is.EqualTo("true"));
            Assert.That(field.NewText, Is.EqualTo("false"));
        }

        [Test]
        public void DetectsStringChange()
        {
            var field = ChangeOne(1, "Long Sword");

            Assert.That(field.Column, Is.EqualTo("name"));
            Assert.That(field.OldValue, Is.EqualTo("Sword"));
            Assert.That(field.OldText, Is.EqualTo("\"Sword\""));
            Assert.That(field.NewText, Is.EqualTo("\"Long Sword\""));
        }

        [Test]
        public void DetectsEnumChangeToNull()
        {
            var field = ChangeOne(5, "");

            Assert.That(field.Column, Is.EqualTo("rarity"));
            Assert.That(field.OldValue, Is.EqualTo("Epic"));
            Assert.That(field.NewValue, Is.Null);
            Assert.That(field.OldText, Is.EqualTo("\"Epic\""));
            Assert.That(field.NewText, Is.EqualTo("null"));
            Assert.That(field.NewMissing, Is.False);
        }

        [Test]
        public void DetectsRefChangeFromNull()
        {
            var diff = Compare(Sheet("Units", Header, Axe), Sheet("Units", Header, With(Axe, 6, "w_02")));

            var field = SingleField(diff);
            Assert.That(field.Column, Is.EqualTo("weapon"));
            Assert.That(field.OldValue, Is.Null);
            Assert.That(field.NewValue, Is.EqualTo("w_02"));
            Assert.That(field.OldText, Is.EqualTo("null"));
            Assert.That(field.NewText, Is.EqualTo("\"w_02\""));
        }

        [Test]
        public void DetectsStringArrayChange()
        {
            var field = ChangeOne(7, "melee");

            Assert.That(field.Column, Is.EqualTo("tags"));
            Assert.That(field.OldText, Is.EqualTo("[\"melee\", \"starter\"]"));
            Assert.That(field.NewText, Is.EqualTo("[\"melee\"]"));
        }

        [Test]
        public void DetectsIntArrayChangeToEmpty()
        {
            var field = ChangeOne(8, "");

            Assert.That(field.Column, Is.EqualTo("levels"));
            Assert.That(field.OldText, Is.EqualTo("[1, 2]"));
            Assert.That(field.NewText, Is.EqualTo("[]"));
        }

        [Test]
        public void ArrayOrderMatters()
        {
            var field = ChangeOne(8, "2|1");

            Assert.That(field.NewText, Is.EqualTo("[2, 1]"));
        }

        [Test]
        public void EqualArraysAreNotChange()
        {
            var header = new[] { "id", "s:string[]", "i:int[]", "f:float[]", "b:bool[]" };
            var row = new[] { "a", "x|y", "1|2", "0.5|1", "true|false" };

            var diff = Compare(Sheet("T", header, row), Sheet("T", header, (string[])row.Clone()));

            Assert.That(diff.HasChanges, Is.False);
        }

        [Test]
        public void ReorderedRowsAndColumnsAreNotChange()
        {
            // 'id' must stay the first column; the rest are reversed.
            string[] Reverse(string[] row) => new[] { row[0] }.Concat(row.Skip(1).Reverse()).ToArray();
            var reorderedHeader = Reverse(Header);

            var diff = Compare(
                Sheet("Units", Header, Sword, Axe),
                Sheet("Units", reorderedHeader, Reverse(Axe), Reverse(Sword)));

            Assert.That(diff.HasChanges, Is.False);
        }

        [Test]
        public void AddedAndRemovedColumnsAreFieldChanges()
        {
            var oldSheet = Sheet("T", new[] { "id", "hp:int", "old:string" }, new[] { "a", "1", "x" });
            var newSheet = Sheet("T", new[] { "id", "fresh:bool", "hp:int" }, new[] { "a", "true", "1" });

            var diff = Compare(oldSheet, newSheet);

            var row = diff.Sheets.Single().ChangedRows.Single();
            Assert.That(row.Fields.Select(f => f.Column), Is.EqualTo(new[] { "fresh", "old" }));

            var fresh = row.Fields[0];
            Assert.That(fresh.OldMissing, Is.True);
            Assert.That(fresh.NewMissing, Is.False);
            Assert.That(fresh.OldValue, Is.Null);
            Assert.That(fresh.NewValue, Is.EqualTo(true));
            Assert.That(fresh.OldText, Is.EqualTo("(missing)"));
            Assert.That(fresh.NewText, Is.EqualTo("true"));

            var old = row.Fields[1];
            Assert.That(old.OldMissing, Is.False);
            Assert.That(old.NewMissing, Is.True);
            Assert.That(old.OldText, Is.EqualTo("\"x\""));
            Assert.That(old.NewText, Is.EqualTo("(missing)"));
        }

        [Test]
        public void ChangedColumnTypeIsFieldChange()
        {
            var diff = Compare(
                Sheet("T", new[] { "id", "hp:int" }, new[] { "a", "120" }),
                Sheet("T", new[] { "id", "hp:float" }, new[] { "a", "120" }));

            var field = SingleField(diff);
            Assert.That(field.OldText, Is.EqualTo("120"));
            Assert.That(field.NewText, Is.EqualTo("120"));
        }

        [Test]
        public void FieldsFollowColumnUnionOrder()
        {
            var oldSheet = Sheet("T", new[] { "id", "a:int", "gone1:int", "b:int", "gone2:int" }, new[] { "r", "1", "1", "1", "1" });
            var newSheet = Sheet("T", new[] { "id", "b:int", "a:int" }, new[] { "r", "2", "2" });

            var row = Compare(oldSheet, newSheet).Sheets.Single().ChangedRows.Single();

            Assert.That(row.Fields.Select(f => f.Column), Is.EqualTo(new[] { "b", "a", "gone1", "gone2" }));
            Assert.That(row.OldRowNumber, Is.EqualTo(2));
            Assert.That(row.NewRowNumber, Is.EqualTo(2));
        }

        [Test]
        public void OutputOrderFollowsSnapshots()
        {
            var h = new[] { "id", "v:int" };
            var before = Snapshot(
                Sheet("Gone1", h, new[] { "x", "1" }),
                Sheet("B", h, new[] { "r1", "1" }, new[] { "r2", "1" }, new[] { "r3", "1" }, new[] { "r4", "1" }),
                Sheet("Gone2", h, new[] { "x", "1" }),
                Sheet("A", h, new[] { "x", "1" }));
            var after = Snapshot(
                Sheet("A", h, new[] { "x", "2" }),
                Sheet("New", h, new[] { "x", "1" }),
                Sheet("B", h, new[] { "n2", "1" }, new[] { "r3", "2" }, new[] { "n1", "1" }, new[] { "r1", "2" }));

            var diff = SnapshotDiffer.Compare(before, after);

            Assert.That(diff.Sheets.Select(s => s.SheetName), Is.EqualTo(new[] { "A", "New", "B", "Gone1", "Gone2" }));
            var b = diff.Sheets[2];
            Assert.That(b.AddedRows.Select(r => r.Id), Is.EqualTo(new[] { "n2", "n1" }));
            Assert.That(b.RemovedRows.Select(r => r.Id), Is.EqualTo(new[] { "r2", "r4" }));
            Assert.That(b.ChangedRows.Select(r => r.Id), Is.EqualTo(new[] { "r3", "r1" }));
        }

        [Test]
        public void UnchangedSheetIsOmitted()
        {
            var units = Sheet("Units", Header, Sword);
            var diff = SnapshotDiffer.Compare(
                Snapshot(units, Sheet("T", new[] { "id", "v:int" }, new[] { "a", "1" })),
                Snapshot(units, Sheet("T", new[] { "id", "v:int" }, new[] { "a", "2" })));

            Assert.That(diff.Sheets.Select(s => s.SheetName), Is.EqualTo(new[] { "T" }));
        }

        [Test]
        public void NullArgumentsThrow()
        {
            var empty = Snapshot();
            Assert.Throws<ArgumentNullException>(() => SnapshotDiffer.Compare(null, empty));
            Assert.Throws<ArgumentNullException>(() => SnapshotDiffer.Compare(empty, null));
            Assert.Throws<ArgumentException>(() => SnapshotDiffer.Compare(Snapshot(new SheetParseResult[] { null }), empty));
        }

        [Test]
        public void InvalidSheetThrows()
        {
            var invalid = SheetParser.Parse("Units", new[] { new[] { "id", "hp:int" }, new[] { "a", "abc" } });
            Assert.That(invalid.IsValid, Is.False);

            Assert.Throws<ArgumentException>(() => SnapshotDiffer.Compare(Snapshot(), Snapshot(invalid)));
            Assert.Throws<ArgumentException>(() => SnapshotDiffer.Compare(Snapshot(invalid), Snapshot()));
        }

        [Test]
        public void DuplicateSheetNameThrows()
        {
            var units = Sheet("Units", Header, Sword);

            Assert.Throws<ArgumentException>(() => SnapshotDiffer.Compare(Snapshot(), Snapshot(units, units)));
            Assert.Throws<ArgumentException>(() => SnapshotDiffer.Compare(Snapshot(units, units), Snapshot()));
        }
    }
}
