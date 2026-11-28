using System;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;

namespace Kentavr.SheetsToSO.Editor.Tests
{
    /// <summary>
    /// Restores the editor language of the owner after each test: the language, the Assets menu item and the exact
    /// <see cref="EditorPrefs"/> state (the key is deleted again when it was not there before the test).
    /// </summary>
    public abstract class LocaleRestoringFixture
    {
        // Same as the private key of EditorLocale; kept as a literal so the test does not widen its visibility.
        protected const string PrefsKey = "Kentavr.SheetsToSO.Language";

        private bool _hadKey;
        private string _storedCode;
        private EditorLanguage _language;

        [SetUp]
        public void RememberLanguage()
        {
            _hadKey = EditorPrefs.HasKey(PrefsKey);
            _storedCode = EditorPrefs.GetString(PrefsKey, string.Empty);
            _language = EditorLocale.Current;
        }

        [TearDown]
        public void RestoreLanguage()
        {
            // Set ignores the language that is already current: go through another one so the menu item follows.
            if (EditorLocale.Current == _language)
                EditorLocale.Set(Other(_language));
            EditorLocale.Set(_language);

            if (_hadKey)
                EditorPrefs.SetString(PrefsKey, _storedCode);
            else
                EditorPrefs.DeleteKey(PrefsKey);
        }

        private protected static EditorLanguage Other(EditorLanguage language)
        {
            foreach (var candidate in EditorText.Languages)
            {
                if (candidate != language)
                    return candidate;
            }

            throw new InvalidOperationException("only one editor language");
        }

        /// <summary>Language with the code <paramref name="code"/>; test parameters are codes, the type is internal.</summary>
        private protected static EditorLanguage Find(string code)
        {
            foreach (var language in EditorText.Languages)
            {
                if (language.Code == code)
                    return language;
            }

            throw new ArgumentException("unknown editor language: " + code);
        }

        /// <summary>Makes <paramref name="language"/> current even when it is current already.</summary>
        private protected static void SwitchTo(EditorLanguage language)
        {
            if (EditorLocale.Current == language)
                EditorLocale.Set(Other(language));
            EditorLocale.Set(language);
        }
    }

    public sealed class EditorLocaleTests : LocaleRestoringFixture
    {
        private const string MenuFolder = "Assets/Sheets to SO/";
        private const BindingFlags InternalStatic = BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public;

        [Test]
        public void SetStoresLanguageInEditorPrefs()
        {
            foreach (var language in EditorText.Languages)
            {
                EditorLocale.Set(Other(language));

                var raised = 0;
                Action onChanged = () => raised++;
                EditorLocale.Changed += onChanged;
                try
                {
                    EditorLocale.Set(language);
                }
                finally
                {
                    EditorLocale.Changed -= onChanged;
                }

                Assert.AreSame(language, EditorLocale.Current, language.Code);
                Assert.AreEqual(language.Code, EditorPrefs.GetString(PrefsKey, string.Empty), language.Code);
                Assert.AreEqual(1, raised, "Changed raised for " + language.Code);
            }
        }

        [Test]
        public void MenuItemFollowsLanguage()
        {
            var exists = typeof(Menu).GetMethod("MenuItemExists", InternalStatic, null, new[] { typeof(string) }, null);
            if (exists == null)
                Assert.Ignore("UnityEditor.Menu.MenuItemExists is missing in this Unity version");
            var add = typeof(Menu).GetMethod("AddMenuItem", InternalStatic, null,
                new[] { typeof(string), typeof(string), typeof(bool), typeof(int), typeof(Action), typeof(Func<bool>) },
                null);
            if (add == null)
                Assert.Ignore("UnityEditor.Menu.AddMenuItem is missing: the menu item is not translated in this Unity version");

            var english = MenuFolder + EditorText.Get(EditorText.English, TextKey.MenuImportCsvFolder);
            var russian = MenuFolder + EditorText.Get(EditorText.Russian, TextKey.MenuImportCsvFolder);
            Func<string, bool> inMenu = path => exists.Invoke(null, new object[] { path }) is bool found && found;

            SwitchTo(EditorText.English);
            if (!inMenu(english))
                Assert.Ignore("Assets menu is not built (no '" + english + "' item), e.g. in batchmode");

            EditorLocale.Set(EditorText.Russian);
            Assert.IsTrue(inMenu(russian), "Russian item after switching to Russian");
            Assert.IsFalse(inMenu(english), "English item after switching to Russian");

            EditorLocale.Set(EditorText.English);
            Assert.IsTrue(inMenu(english), "English item after switching to English");
            Assert.IsFalse(inMenu(russian), "Russian item after switching to English");
        }
    }
}
