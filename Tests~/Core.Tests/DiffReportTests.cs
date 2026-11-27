using System;
using Kentavr.SheetsToSO.Core;
using NUnit.Framework;
using static Kentavr.SheetsToSO.Core.Tests.SnapshotDifferTests;

namespace Kentavr.SheetsToSO.Core.Tests
{
    public class DiffReportTests
    {
        [Test]
        public void FormatsPlanExample()
        {
            var before = Snapshot(
                Sheet("Units", new[] { "id", "hp:int", "name:string" },
                    new[] { "sword_01", "120", "Sword" },
                    new[] { "axe_02", "90", "Axe" }),
                Sheet("Old", new[] { "id" }, new[] { "o_01" }, new[] { "o_02" }));
            var after = Snapshot(
                Sheet("Units", new[] { "id", "hp:int", "name:string" },
                    new[] { "sword_01", "150", "Long Sword" },
                    new[] { "bow_03", "40", "Bow" }),
                Sheet("Weapons", new[] { "id" }, new[] { "w_01" }, new[] { "w_02" }, new[] { "w_03" }));

            var report = DiffReport.Format(SnapshotDiffer.Compare(before, after));

            Assert.That(report, Is.EqualTo(
                "Units: +1 -1 ~1\n" +
                "  + bow_03\n" +
                "  - axe_02\n" +
                "  ~ sword_01: hp: 120 → 150; name: \"Sword\" → \"Long Sword\"\n" +
                "+ Weapons (new sheet, 3 rows)\n" +
                "- Old (removed sheet, 2 rows)"));
        }

        [Test]
        public void IdenticalSnapshotsGiveNoChanges()
        {
            var units = Sheet("Units", new[] { "id", "hp:int" }, new[] { "a", "1" });

            var report = DiffReport.Format(SnapshotDiffer.Compare(Snapshot(units), Snapshot(units)));

            Assert.That(report, Is.EqualTo("No changes"));
        }

        [Test]
        public void FormatsZeroCountsMissingColumnsAndSingleRow()
        {
            var before = Snapshot(Sheet("T", new[] { "id", "old:float" }, new[] { "a", "0.5" }));
            var after = Snapshot(
                Sheet("T", new[] { "id", "tags:string[]", "e:enum<E>" }, new[] { "a", "x|y", "" }),
                Sheet("One", new[] { "id" }, new[] { "r" }));

            var report = DiffReport.Format(SnapshotDiffer.Compare(before, after));

            Assert.That(report, Is.EqualTo(
                "T: +0 -0 ~1\n" +
                "  ~ a: tags: (missing) → [\"x\", \"y\"]; e: (missing) → null; old: 0.5 → (missing)\n" +
                "+ One (new sheet, 1 row)"));
        }

        [Test]
        public void NullDiffThrows()
        {
            Assert.Throws<ArgumentNullException>(() => DiffReport.Format(null));
        }
    }
}
