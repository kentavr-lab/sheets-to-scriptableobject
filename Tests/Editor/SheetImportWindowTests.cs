using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;

namespace Kentavr.SheetsToSO.Editor.Tests
{
    /// <summary>
    /// The import window is created and closed without exceptions or console errors in every editor language.
    /// OnGUI is not called directly: outside of a GUI event it throws.
    /// </summary>
    public sealed class SheetImportWindowTests : LocaleRestoringFixture
    {
        private static readonly string[] Languages = { "en", "ru" };

        [Test]
        public void CreateAndDestroyWithoutErrors([ValueSource(nameof(Languages))] string language)
        {
            SwitchTo(Find(language));

            // CreateInstance calls OnEnable, DestroyImmediate calls OnDisable; works in batchmode too.
            var window = ScriptableObject.CreateInstance<SheetImportWindow>();
            try
            {
                Assert.IsNotNull(window);
            }
            finally
            {
                Object.DestroyImmediate(window);
            }

            LogAssert.NoUnexpectedReceived();
        }

        [Test]
        public void OpenAndCloseWithoutErrors([ValueSource(nameof(Languages))] string language)
        {
            if (Application.isBatchMode)
                Assert.Ignore("no editor windows in batchmode: opening and drawing are checked in the Test Runner");

            SwitchTo(Find(language));

            // A separate instance, not GetWindow: an import window the owner has open stays as it is.
            var window = ScriptableObject.CreateInstance<SheetImportWindow>();
            try
            {
                window.Show();
                window.Repaint();
            }
            finally
            {
                window.Close();
            }

            LogAssert.NoUnexpectedReceived();
        }
    }
}
