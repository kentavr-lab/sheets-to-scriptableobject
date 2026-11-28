using System;
using UnityEditor;
using UnityEditorInternal;

namespace Kentavr.SheetsToSO.Editor
{
    /// <summary>
    /// Language of the package editor interface, per computer (<see cref="EditorPrefs"/>). Messages about the data,
    /// the diff and the Console report are always in English and do not depend on it.
    /// </summary>
    internal static class EditorLocale
    {
        private const string PrefsKey = "Kentavr.SheetsToSO.Language";

        private static EditorLanguage current;

        /// <summary>Raised after the language is changed by <see cref="Set"/>.</summary>
        public static event Action Changed;

        /// <summary>Selected language; English when nothing is stored or the stored code is unknown.</summary>
        public static EditorLanguage Current => current ?? (current = Find(EditorPrefs.GetString(PrefsKey, string.Empty)));

        public static void Set(EditorLanguage language)
        {
            if (language == null || language == Current)
                return;

            current = language;
            EditorPrefs.SetString(PrefsKey, language.Code);
            Changed?.Invoke();
            // Inspectors with [SheetRef] fields redraw only on repaint.
            InternalEditorUtility.RepaintAllViews();
        }

        private static EditorLanguage Find(string code)
        {
            foreach (var language in EditorText.Languages)
            {
                if (string.Equals(language.Code, code, StringComparison.Ordinal))
                    return language;
            }

            return EditorText.Languages[0];
        }
    }

    /// <summary>Short calls for the texts of the current language.</summary>
    internal static class L
    {
        public static string T(TextKey key, params object[] args) => EditorText.Get(EditorLocale.Current, key, args);
    }
}
