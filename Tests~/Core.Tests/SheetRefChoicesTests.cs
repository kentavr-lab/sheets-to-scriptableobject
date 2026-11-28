using System;
using System.Collections.Generic;
using Kentavr.SheetsToSO.Core;
using NUnit.Framework;

namespace Kentavr.SheetsToSO.Core.Tests
{
    public class SheetRefChoicesTests
    {
        [Test]
        public void BuildIdsKeepsRowOrder()
        {
            var ids = SheetRefChoices.BuildIds(new[] { "sword", "axe", "bow" });

            Assert.That(ids, Is.EqualTo(new[] { "sword", "axe", "bow" }));
        }

        [Test]
        public void BuildIdsKeepsFirstOfRepeatedIds()
        {
            var ids = SheetRefChoices.BuildIds(new[] { "sword", "axe", "sword", "bow", "axe" });

            Assert.That(ids, Is.EqualTo(new[] { "sword", "axe", "bow" }));
        }

        [Test]
        public void BuildIdsSkipsEmptyAndNullIds()
        {
            var ids = SheetRefChoices.BuildIds(new[] { "", "sword", null, "axe" });

            Assert.That(ids, Is.EqualTo(new[] { "sword", "axe" }));
        }

        [Test]
        public void BuildIdsTreatsCaseAsDifferent()
        {
            var ids = SheetRefChoices.BuildIds(new[] { "Sword", "sword" });

            Assert.That(ids, Is.EqualTo(new[] { "Sword", "sword" }));
        }

        [Test]
        public void BuildIdsOfNullIsEmpty()
        {
            Assert.That(SheetRefChoices.BuildIds(null), Is.Empty);
        }

        [TestCase(null)]
        [TestCase("")]
        public void EmptyValueIsEmpty(string value)
        {
            Assert.That(SheetRefChoices.Classify(value, Set("sword")), Is.EqualTo(SheetRefState.Empty));
        }

        [Test]
        public void EmptyValueIsEmptyWithoutIds()
        {
            Assert.That(SheetRefChoices.Classify("", null), Is.EqualTo(SheetRefState.Empty));
        }

        [Test]
        public void KnownValueIsValid()
        {
            Assert.That(SheetRefChoices.Classify("sword", Set("sword", "axe")), Is.EqualTo(SheetRefState.Valid));
        }

        [Test]
        public void UnknownValueIsMissing()
        {
            Assert.That(SheetRefChoices.Classify("bow", Set("sword", "axe")), Is.EqualTo(SheetRefState.Missing));
        }

        [Test]
        public void ValueWithOtherCaseIsMissing()
        {
            Assert.That(SheetRefChoices.Classify("Sword", Set("sword")), Is.EqualTo(SheetRefState.Missing));
        }

        [Test]
        public void ValueWithoutIdsIsMissing()
        {
            Assert.That(SheetRefChoices.Classify("sword", null), Is.EqualTo(SheetRefState.Missing));
        }

        [Test]
        public void NoAssetsIsError()
        {
            var pick = SheetRefChoices.PickAsset(new string[0], null, "WeaponsTable");

            Assert.That(pick.Path, Is.Null);
            Assert.That(pick.Error, Does.Contain("WeaponsTable"));
            Assert.That(pick.Warning, Is.Null);
        }

        [Test]
        public void NullPathsIsError()
        {
            var pick = SheetRefChoices.PickAsset(null, null, "WeaponsTable");

            Assert.That(pick.Path, Is.Null);
            Assert.That(pick.Error, Is.Not.Null);
        }

        [Test]
        public void SingleAssetIsPickedWithoutWarning()
        {
            var pick = SheetRefChoices.PickAsset(new[] { "Assets/Data/Weapons.asset" }, null, "WeaponsTable");

            Assert.That(pick.Path, Is.EqualTo("Assets/Data/Weapons.asset"));
            Assert.That(pick.Error, Is.Null);
            Assert.That(pick.Warning, Is.Null);
        }

        [Test]
        public void SeveralAssetsPickFirstByOrdinalPathWithWarning()
        {
            var pick = SheetRefChoices.PickAsset(
                new[] { "Assets/b/Weapons.asset", "Assets/a/Weapons.asset", "Assets/B/Weapons.asset" },
                null, "WeaponsTable");

            Assert.That(pick.Path, Is.EqualTo("Assets/B/Weapons.asset"));
            Assert.That(pick.Error, Is.Null);
            Assert.That(pick.Warning, Does.Contain("3 ассетов WeaponsTable"));
            Assert.That(pick.Warning, Does.Contain("Assets/B/Weapons.asset"));
        }

        [Test]
        public void RequestedAssetIsPickedWithoutWarning()
        {
            var pick = SheetRefChoices.PickAsset(
                new[] { "Assets/a/Weapons.asset", "Assets/b/Weapons.asset" }, "Assets/b/Weapons.asset", "WeaponsTable");

            Assert.That(pick.Path, Is.EqualTo("Assets/b/Weapons.asset"));
            Assert.That(pick.Error, Is.Null);
            Assert.That(pick.Warning, Is.Null);
        }

        [Test]
        public void RequestedAssetNotFoundIsErrorEvenWithOtherAssets()
        {
            var pick = SheetRefChoices.PickAsset(
                new[] { "Assets/a/Weapons.asset" }, "Assets/c/Weapons.asset", "WeaponsTable");

            Assert.That(pick.Path, Is.Null);
            Assert.That(pick.Error, Does.Contain("Assets/c/Weapons.asset"));
            Assert.That(pick.Warning, Is.Null);
        }

        [Test]
        public void RequestedAssetNotFoundWithoutAssetsIsError()
        {
            var pick = SheetRefChoices.PickAsset(new string[0], "Assets/a/Weapons.asset", "WeaponsTable");

            Assert.That(pick.Path, Is.Null);
            Assert.That(pick.Error, Does.Contain("Assets/a/Weapons.asset"));
        }

        [Test]
        public void RequestedPathIsCaseSensitive()
        {
            var pick = SheetRefChoices.PickAsset(
                new[] { "Assets/a/Weapons.asset" }, "Assets/A/Weapons.asset", "WeaponsTable");

            Assert.That(pick.Path, Is.Null);
            Assert.That(pick.Error, Is.Not.Null);
        }

        private static HashSet<string> Set(params string[] ids)
        {
            return new HashSet<string>(ids, StringComparer.Ordinal);
        }
    }
}
