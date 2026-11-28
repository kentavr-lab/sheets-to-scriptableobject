using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace Kentavr.SheetsToSO.Editor
{
    /// <summary>
    /// Temporary command: imports every .csv/.tsv file of a folder on disk into the selected project folder.
    /// The main way to import is <see cref="SheetImportWindow"/>.
    /// </summary>
    /// <remarks>
    /// The <see cref="MenuItem"/> path is fixed at compile time (English). For another editor language the item is
    /// replaced with a translated one through the internal <c>UnityEditor.Menu</c> API; when that API is missing,
    /// the English item stays.
    /// </remarks>
    [InitializeOnLoad]
    internal static class ImportCsvFolderMenu
    {
        private const string MenuFolder = "Assets/Sheets to SO/";
        private const string MenuPath = MenuFolder + "Import CSV folder here…";
        private const int MenuPriority = 1000; // Default priority of [MenuItem].
        private const string Namespace = "SheetsData";
        private const string CodeSubfolder = "Generated";
        private const string LogPrefix = "Sheets to SO: ";

        private const BindingFlags InternalStatic = BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public;

        private static readonly MethodInfo AddMenuItemMethod = FindMenuMethod("AddMenuItem",
            typeof(string), typeof(string), typeof(bool), typeof(int), typeof(Action), typeof(Func<bool>));

        private static readonly MethodInfo RemoveMenuItemMethod = FindMenuMethod("RemoveMenuItem", typeof(string));

        private static readonly MethodInfo MenuItemExistsMethod = FindMenuMethod("MenuItemExists", typeof(string));

        // Path of the item now in the menu; after a domain reload the menu has the [MenuItem] path.
        private static string shownPath = MenuPath;

        static ImportCsvFolderMenu()
        {
            EditorLocale.Changed += UpdateMenuItem;
            // Menus are rebuilt from [MenuItem] attributes after the domain is loaded.
            EditorApplication.delayCall += UpdateMenuItem;
        }

        private static void UpdateMenuItem()
        {
            var wanted = MenuFolder + L.T(TextKey.MenuImportCsvFolder);
            if (wanted == shownPath || AddMenuItemMethod == null || RemoveMenuItemMethod == null)
                return;

            // Add first: if adding fails, the old item stays; if removing the old one fails (or the old one is still
            // in the menu), the new one is taken back, so the menu never has both.
            if (!TryInvoke(AddMenuItemMethod, wanted, string.Empty, false, MenuPriority, (Action)ImportHere,
                    (Func<bool>)CanImport))
                return;

            if (TryInvoke(RemoveMenuItemMethod, shownPath) && !StillInMenu(shownPath))
                shownPath = wanted;
            else
                TryInvoke(RemoveMenuItemMethod, wanted);
        }

        // RemoveMenuItem may return silently without removing the item; check it when Unity lets us.
        // Without MenuItemExists the removal is trusted.
        private static bool StillInMenu(string path)
        {
            if (MenuItemExistsMethod == null)
                return false;

            try
            {
                return MenuItemExistsMethod.Invoke(null, new object[] { path }) is bool exists && exists;
            }
            catch (Exception)
            {
                return false;
            }
        }

        private static MethodInfo FindMenuMethod(string name, params Type[] parameters)
        {
            try
            {
                return typeof(Menu).GetMethod(name, InternalStatic, null, parameters, null);
            }
            catch (Exception)
            {
                return null;
            }
        }

        private static bool TryInvoke(MethodInfo method, params object[] args)
        {
            try
            {
                method.Invoke(null, args);
                return true;
            }
            catch (Exception)
            {
                // Internal API of another Unity version: keep the menu as it is.
                return false;
            }
        }

        [MenuItem(MenuPath, true)]
        private static bool CanImport()
        {
            return GetSelectedFolder() != null;
        }

        [MenuItem(MenuPath, false)]
        private static void ImportHere()
        {
            var target = GetSelectedFolder();
            if (target == null)
                return;

            var source = EditorUtility.OpenFolderPanel(L.T(TextKey.CsvFolderPanelTitle), string.Empty, string.Empty);
            if (string.IsNullOrEmpty(source))
                return;

            var files = CsvFolderSource.ListFiles(source, false);
            if (files.Count == 0)
            {
                Debug.LogError(LogPrefix + $"no .csv or .tsv files in '{source}'");
                return;
            }

            var request = new SheetImportRequest
            {
                assetFolder = target,
                codeFolder = target + "/" + CodeSubfolder,
                @namespace = Namespace,
            };

            var errors = new List<string>();
            foreach (var file in files)
            {
                var sheet = CsvFolderSource.Read(Path.Combine(source, file), errors);
                if (sheet != null)
                    request.sheets.Add(sheet);
            }

            if (errors.Count > 0)
            {
                foreach (var error in errors)
                    Debug.LogError(LogPrefix + error);
                Debug.LogError(LogPrefix + "import cancelled: fix the CSV errors above");
                return;
            }

            SheetAssetImporter.Import(request);
        }

        private static string GetSelectedFolder()
        {
            var guids = Selection.assetGUIDs;
            if (guids == null || guids.Length != 1)
                return null;
            var path = AssetDatabase.GUIDToAssetPath(guids[0]);
            return AssetDatabase.IsValidFolder(path) ? path : null;
        }
    }
}
