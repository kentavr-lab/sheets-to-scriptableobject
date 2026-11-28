using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;

namespace Kentavr.SheetsToSO.Editor.Tests
{
    public class EditorTextTests
    {
        private static readonly Regex Placeholder = new Regex(@"\{(\d+)", RegexOptions.CultureInvariant);

        private static IEnumerable<string> LanguageCodes => EditorText.Languages.Select(language => language.Code);

        private static EditorLanguage Language(string code) =>
            EditorText.Languages.Single(language => language.Code == code);

        private static IEnumerable<TextKey> AllKeys => Enum.GetValues(typeof(TextKey)).Cast<TextKey>();

        [TestCaseSource(nameof(LanguageCodes))]
        public void TableHasNonEmptyTextForEveryKey(string code)
        {
            var table = Language(code).Table;

            var missing = AllKeys.Where(key => !table.TryGetValue(key, out var text) || string.IsNullOrWhiteSpace(text));

            Assert.That(missing, Is.Empty);
        }

        [TestCaseSource(nameof(LanguageCodes))]
        public void TableHasOnlyKnownKeys(string code)
        {
            Assert.That(Language(code).Table.Keys, Is.SubsetOf(AllKeys));
        }

        [TestCaseSource(nameof(LanguageCodes))]
        public void PlaceholdersMatchEnglish(string code)
        {
            var table = Language(code).Table;

            var different = AllKeys
                .Where(key => table.ContainsKey(key))
                .Where(key => !Placeholders(table[key]).SetEquals(Placeholders(EditorText.English.Table[key])))
                .ToList();

            Assert.That(different, Is.Empty);
        }

        [TestCaseSource(nameof(LanguageCodes))]
        public void EveryTextFormatsWithItsArguments(string code)
        {
            var language = Language(code);
            foreach (var key in AllKeys)
            {
                var placeholders = Placeholders(EditorText.English.Table[key]);
                var count = placeholders.Count == 0 ? 0 : placeholders.Max() + 1;
                var formatArgs = Enumerable.Range(0, count).Select(n => (object)("#" + n)).ToArray();

                Assert.DoesNotThrow(() => EditorText.Get(language, key, formatArgs), key.ToString());
            }
        }

        [Test]
        public void LanguageCodesAreUnique()
        {
            Assert.That(LanguageCodes, Is.Unique);
        }

        [Test]
        public void FirstLanguageIsEnglish()
        {
            Assert.That(EditorText.Languages[0], Is.SameAs(EditorText.English));
            Assert.That(EditorText.Languages[0].Code, Is.EqualTo("en"));
        }

        [Test]
        public void GetFormatsWithInvariantCulture()
        {
            var text = EditorText.Get(EditorText.English, TextKey.ErrorCount, 3);

            Assert.That(text, Is.EqualTo("Errors: 3. Apply is not available."));
        }

        [Test]
        public void GetTakesTextOfTheLanguage()
        {
            Assert.That(EditorText.Get(EditorText.Russian, TextKey.GenerateCode), Is.EqualTo("Генерировать код"));
            Assert.That(EditorText.Get(EditorText.English, TextKey.GenerateCode), Is.EqualTo("Generate code"));
        }

        [Test]
        public void GetFallsBackToEnglishForMissingKey()
        {
            var partial = new EditorLanguage("xx", "Test",
                new Dictionary<TextKey, string> { [TextKey.Apply] = "Apply-xx" });

            Assert.That(EditorText.Get(partial, TextKey.Apply), Is.EqualTo("Apply-xx"));
            Assert.That(EditorText.Get(partial, TextKey.Cancel), Is.EqualTo(EditorText.English.Table[TextKey.Cancel]));
            Assert.That(EditorText.Get(partial, TextKey.CsvFolderNotFound, "C:/Data"),
                Is.EqualTo("Folder 'C:/Data' not found"));
        }

        [Test]
        public void GetFallsBackToEnglishWithoutLanguage()
        {
            Assert.That(EditorText.Get(null, TextKey.Apply), Is.EqualTo("Apply"));
        }

        private static HashSet<int> Placeholders(string text)
        {
            return new HashSet<int>(Placeholder.Matches(text).Select(match => int.Parse(match.Groups[1].Value)));
        }
    }
}
