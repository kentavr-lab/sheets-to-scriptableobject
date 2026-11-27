using Kentavr.SheetsToSO.Core;
using NUnit.Framework;

namespace Kentavr.SheetsToSO.Core.Tests
{
    public class RowIndexTests
    {
        private sealed class TestRow : ISheetRow
        {
            public TestRow(string id, string name = null)
            {
                Id = id;
                Name = name;
            }

            public string Id { get; }

            public string Name { get; }
        }

        [Test]
        public void EmptyInputGivesEmptyIndex()
        {
            var index = RowIndex<TestRow>.Build(new TestRow[0]);

            Assert.That(index.Count, Is.EqualTo(0));
            Assert.That(index.InvalidRows, Is.Empty);
            Assert.That(index.DuplicateIds, Is.Empty);
        }

        [Test]
        public void NullInputGivesEmptyIndex()
        {
            var index = RowIndex<TestRow>.Build(null);

            Assert.That(index.Count, Is.EqualTo(0));
            Assert.That(index.InvalidRows, Is.Empty);
            Assert.That(index.DuplicateIds, Is.Empty);
            Assert.That(index.Contains("sword"), Is.False);
        }

        [Test]
        public void FindsExistingId()
        {
            var sword = new TestRow("sword");
            var index = RowIndex<TestRow>.Build(new[] { new TestRow("axe"), sword });

            Assert.That(index.Count, Is.EqualTo(2));
            Assert.That(index.TryGet("sword", out var found), Is.True);
            Assert.That(found, Is.SameAs(sword));
            Assert.That(index.Contains("sword"), Is.True);
        }

        [Test]
        public void MissingIdIsNotFound()
        {
            var index = RowIndex<TestRow>.Build(new[] { new TestRow("sword") });

            Assert.That(index.TryGet("bow", out var found), Is.False);
            Assert.That(found, Is.Null);
            Assert.That(index.Contains("bow"), Is.False);
        }

        [Test]
        public void LookupIsCaseSensitive()
        {
            var index = RowIndex<TestRow>.Build(new[] { new TestRow("Sword") });

            Assert.That(index.Contains("Sword"), Is.True);
            Assert.That(index.Contains("sword"), Is.False);
            Assert.That(index.TryGet("sword", out _), Is.False);
        }

        [Test]
        public void CaseVariantsAreDistinctRows()
        {
            var index = RowIndex<TestRow>.Build(new[] { new TestRow("Sword"), new TestRow("sword") });

            Assert.That(index.Count, Is.EqualTo(2));
            Assert.That(index.DuplicateIds, Is.Empty);
        }

        [Test]
        public void NullRowAndEmptyIdAreSkipped()
        {
            var rows = new[] { new TestRow("sword"), null, new TestRow(""), new TestRow(null), new TestRow("axe") };

            var index = RowIndex<TestRow>.Build(rows);

            Assert.That(index.Count, Is.EqualTo(2));
            Assert.That(index.InvalidRows, Is.EqualTo(new[] { 1, 2, 3 }));
            Assert.That(index.Contains(""), Is.False);
        }

        [Test]
        public void FirstDuplicateWins()
        {
            var first = new TestRow("sword", "first");
            var index = RowIndex<TestRow>.Build(new[] { first, new TestRow("sword", "second") });

            Assert.That(index.Count, Is.EqualTo(1));
            Assert.That(index.TryGet("sword", out var found), Is.True);
            Assert.That(found, Is.SameAs(first));
            Assert.That(index.DuplicateIds, Is.EqualTo(new[] { "sword" }));
        }

        [Test]
        public void DuplicateIdIsReportedOnceForThreeRepeats()
        {
            var rows = new[]
            {
                new TestRow("sword", "first"),
                new TestRow("axe"),
                new TestRow("sword", "second"),
                new TestRow("sword", "third"),
                new TestRow("axe"),
            };

            var index = RowIndex<TestRow>.Build(rows);

            Assert.That(index.DuplicateIds, Is.EqualTo(new[] { "sword", "axe" }));
            Assert.That(index.TryGet("sword", out var found), Is.True);
            Assert.That(found.Name, Is.EqualTo("first"));
        }

        [Test]
        public void NullIdIsNotFoundWithoutException()
        {
            var index = RowIndex<TestRow>.Build(new[] { new TestRow("sword") });

            Assert.That(index.TryGet(null, out var found), Is.False);
            Assert.That(found, Is.Null);
            Assert.That(index.Contains(null), Is.False);
        }
    }
}
