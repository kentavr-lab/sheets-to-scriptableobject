using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Kentavr.SheetsToSO.Core;
using UnityEditor;
using UnityEngine;

namespace Kentavr.SheetsToSO.Editor
{
    /// <summary>
    /// Import window: loads the selected sheets from a source, checks them, shows errors and the diff,
    /// and imports exactly the data that passed the check.
    /// </summary>
    internal sealed class SheetImportWindow : EditorWindow
    {
        private const string MenuPath = "Window/Sheets to SO/Import";
        private const string Title = "Sheets to SO";

        private const float TableTypeWidth = 200;
        private const float LanguageWidth = 70;

        private static readonly string[] MonoFonts = { "Consolas", "Menlo", "DejaVu Sans Mono", "Courier New" };

        private static readonly SheetSourceKind[] SourceKinds =
            { SheetSourceKind.GoogleApi, SheetSourceKind.Export, SheetSourceKind.CsvFiles };

        private static readonly TextKey[] SourceLabels =
            { TextKey.SourceGoogleApi, TextKey.SourceExport, TextKey.SourceCsvFiles };

        private static readonly string[] LanguageCodes =
            EditorText.Languages.Select(language => language.Code.ToUpperInvariant()).ToArray();

        private SheetSourceLoader _loader;
        private CancellationTokenSource _cancel;
        private TextKey? _busyText;
        private string _apiKey = string.Empty;

        // Last check; cleared by any change of the settings or check boxes. Not kept over a script reload.
        private SheetImportRequest _checkedRequest;
        private SheetLoadResult _loaded;
        private SheetImportCheck _check;
        // Built on each draw, so messages of the window follow the language switch.
        private Func<string> _sourceMessage;

        // Last import result; kept over the script reload that follows a code change.
        [SerializeField] private string _importText;
        [SerializeField] private bool _importHasErrors;
        [SerializeField] private bool _waitingForReload;
        [SerializeField] private Vector2 _scroll;

        private Font _monoFont;
        private GUIStyle _monoStyle;
        private GUIStyle _hintStyle;

        // Full names of the SheetTable<> subclasses of the project; refreshed in OnEnable, i.e. after each recompile.
        private List<string> _tableTypes = new List<string>();

        private bool IsBusy => _busyText != null;

        private bool CanApply => !IsBusy && _check != null && !_check.HasErrors && _loaded != null && _loaded.Errors.Count == 0;

        [MenuItem(MenuPath)]
        private static void Open()
        {
            GetWindow<SheetImportWindow>(Title).Show();
        }

        private void OnEnable()
        {
            titleContent = new GUIContent(Title);
            _loader = new SheetSourceLoader();
            _apiKey = ApiKeyStore.Load();
            _tableTypes = SheetAssetImporter.FindTableTypes().Select(type => type.FullName).ToList();
            SheetAssetImporter.ImportContinued += OnImportContinued;
        }

        private void OnDisable()
        {
            SheetAssetImporter.ImportContinued -= OnImportContinued;
            CancelLoading();
            _loader?.Dispose();
            _loader = null;
            if (_monoFont != null)
                DestroyImmediate(_monoFont);
            _monoFont = null;
            _monoStyle = null;
            _hintStyle = null;
        }

        private void OnGUI()
        {
            var settings = SheetImportSettings.instance;

            EditorGUI.BeginChangeCheck();

            using (new EditorGUILayout.HorizontalScope())
            {
                using (new EditorGUI.DisabledScope(IsBusy))
                    DrawSource(settings);
                DrawLanguage();
            }

            using (new EditorGUI.DisabledScope(IsBusy))
            {
                EditorGUILayout.Space();

                switch (settings.source)
                {
                    case SheetSourceKind.GoogleApi:
                        DrawGoogle(settings);
                        break;
                    case SheetSourceKind.Export:
                        DrawExport(settings);
                        break;
                    default:
                        DrawCsvFiles(settings);
                        break;
                }

                EditorGUILayout.Space();
                settings.assetFolder = EditorGUILayout.TextField(L.T(TextKey.AssetsFolder), settings.assetFolder);
                settings.codeFolder = EditorGUILayout.TextField(L.T(TextKey.CodeFolder), settings.codeFolder);
                settings.@namespace = EditorGUILayout.TextField("Namespace", settings.@namespace);
            }

            if (EditorGUI.EndChangeCheck())
                OnSettingsChanged();

            EditorGUILayout.Space();
            DrawButtons();
            EditorGUILayout.Space();
            DrawReport();
        }

        private static void DrawSource(SheetImportSettings settings)
        {
            var labels = SourceLabels.Select(key => L.T(key)).ToArray();
            var index = Array.IndexOf(SourceKinds, settings.source);
            var selected = EditorGUILayout.Popup(L.T(TextKey.Source), index, labels);
            if (selected >= 0 && selected != index)
                settings.source = SourceKinds[selected];
        }

        // EN | RU switch. Enabled while loading; it is not a setting, so it neither resets the check nor the import result.
        private void DrawLanguage()
        {
            var changed = GUI.changed;
            var index = Math.Max(0, IndexOf(EditorText.Languages, EditorLocale.Current));
            var selected = GUILayout.Toolbar(index, LanguageCodes, EditorStyles.miniButton, GUILayout.Width(LanguageWidth));
            GUI.changed = changed;

            if (selected != index)
            {
                EditorLocale.Set(EditorText.Languages[selected]);
                Repaint();
            }
        }

        private static int IndexOf(IReadOnlyList<EditorLanguage> languages, EditorLanguage language)
        {
            for (var i = 0; i < languages.Count; i++)
            {
                if (languages[i] == language)
                    return i;
            }

            return -1;
        }

        private void DrawGoogle(SheetImportSettings settings)
        {
            settings.spreadsheetId = EditorGUILayout.TextField(L.T(TextKey.SpreadsheetId), settings.spreadsheetId);

            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUI.BeginChangeCheck();
                var key = EditorGUILayout.PasswordField(L.T(TextKey.ApiKey), _apiKey);
                if (EditorGUI.EndChangeCheck())
                {
                    _apiKey = key;
                    ApiKeyStore.Save(key);
                }
                if (GUILayout.Button(L.T(TextKey.Clear), GUILayout.Width(80)))
                {
                    _apiKey = string.Empty;
                    ApiKeyStore.Save(string.Empty);
                    GUI.FocusControl(null);
                    OnSettingsChanged();
                }
            }
            DrawHint(L.T(TextKey.ApiKeyHint));

            if (GUILayout.Button(L.T(TextKey.RefreshSheetList)))
                RefreshGoogleSheets(settings);

            DrawToggles(settings, settings.googleSheets, name => name, L.T(TextKey.GoogleSheetsEmpty));
        }

        private void DrawExport(SheetImportSettings settings)
        {
            settings.spreadsheetId = EditorGUILayout.TextField(L.T(TextKey.SpreadsheetId), settings.spreadsheetId);
            DrawHint(L.T(TextKey.ExportHint));

            DrawAllNone(settings.exportSheets.Count > 0, selected =>
            {
                foreach (var sheet in settings.exportSheets)
                    sheet.selected = selected;
            });

            if (settings.exportSheets.Count > 0)
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    GUILayout.Space(22);
                    EditorGUILayout.LabelField(L.T(TextKey.SheetName), EditorStyles.miniBoldLabel);
                    EditorGUILayout.LabelField("gid", EditorStyles.miniBoldLabel, GUILayout.Width(110));
                    EditorGUILayout.LabelField(L.T(TextKey.TableType), EditorStyles.miniBoldLabel,
                        GUILayout.Width(TableTypeWidth));
                    GUILayout.Space(28);
                }
            }

            var remove = -1;
            for (var i = 0; i < settings.exportSheets.Count; i++)
            {
                var sheet = settings.exportSheets[i];
                using (new EditorGUILayout.HorizontalScope())
                {
                    sheet.selected = EditorGUILayout.Toggle(sheet.selected, GUILayout.Width(18));
                    sheet.name = EditorGUILayout.TextField(sheet.name);
                    sheet.gid = EditorGUILayout.TextField(sheet.gid, GUILayout.Width(110));
                    DrawTableType(settings, sheet.name);
                    if (GUILayout.Button("−", GUILayout.Width(24)))
                        remove = i;
                }
            }

            if (remove >= 0)
            {
                settings.exportSheets.RemoveAt(remove);
                OnSettingsChanged();
            }

            if (GUILayout.Button(L.T(TextKey.AddSheet)))
            {
                settings.exportSheets.Add(new ExportSheetEntry { name = string.Empty, gid = string.Empty });
                OnSettingsChanged();
            }
        }

        private void DrawCsvFiles(SheetImportSettings settings)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                settings.csvFolder = EditorGUILayout.TextField(L.T(TextKey.CsvFolder), settings.csvFolder);
                if (GUILayout.Button(L.T(TextKey.Browse), GUILayout.Width(80)))
                {
                    var folder = EditorUtility.OpenFolderPanel(L.T(TextKey.CsvFolderPanelTitle), settings.csvFolder,
                        string.Empty);
                    if (!string.IsNullOrEmpty(folder))
                    {
                        settings.csvFolder = folder;
                        GUI.FocusControl(null);
                        RefreshCsvFiles(settings);
                    }
                    // The modal panel breaks the current layout pass.
                    GUIUtility.ExitGUI();
                }
            }

            if (GUILayout.Button(L.T(TextKey.RefreshFileList)))
                RefreshCsvFiles(settings);

            // Sheets of CSV files are named without the extension, as SheetSourceLoader.LoadCsvFolder names them.
            DrawToggles(settings, settings.csvFiles, CsvFolderSource.SheetName, L.T(TextKey.CsvFilesEmpty));
        }

        private void DrawToggles(SheetImportSettings settings, List<SheetToggle> list, Func<string, string> sheetName,
            string emptyText)
        {
            if (list.Count == 0)
            {
                EditorGUILayout.HelpBox(emptyText, MessageType.None);
                return;
            }

            DrawAllNone(true, selected =>
            {
                foreach (var item in list)
                    item.selected = selected;
            });

            foreach (var item in list)
            {
                var sheet = item.name != null ? sheetName(item.name) : null;
                using (new EditorGUILayout.HorizontalScope())
                {
                    // The toggle shows the sheet name; the stored name (file name for CSV) is in the tooltip.
                    item.selected = EditorGUILayout.ToggleLeft(new GUIContent(sheet ?? item.name, item.name),
                        item.selected);
                    DrawTableType(settings, sheet);
                }
            }
        }

        // "Table type" of a sheet: code generation or a SheetTable<> subclass of the project (schema from code).
        // Changes set GUI.changed, so OnGUI saves the settings and resets the check.
        private void DrawTableType(SheetImportSettings settings, string sheet)
        {
            var current = settings.GetTableType(sheet);
            var labels = new List<string>(_tableTypes.Count + 2) { L.T(TextKey.GenerateCode) };
            labels.AddRange(_tableTypes);

            var index = 0;
            if (current.Length > 0)
            {
                index = _tableTypes.IndexOf(current) + 1;
                if (index == 0)
                {
                    // The type is gone from the project: keep the binding, the check reports the error.
                    labels.Add(L.T(TextKey.TableTypeNotFound, current));
                    index = labels.Count - 1;
                }
            }

            using (new EditorGUI.DisabledScope(string.IsNullOrEmpty(sheet)))
            {
                var selected = EditorGUILayout.Popup(index, labels.ToArray(), GUILayout.Width(TableTypeWidth));
                if (selected == index)
                    return;

                if (selected == 0)
                    settings.SetTableType(sheet, string.Empty);
                else if (selected <= _tableTypes.Count)
                    settings.SetTableType(sheet, _tableTypes[selected - 1]);
            }
        }

        private void DrawAllNone(bool visible, Action<bool> setAll)
        {
            if (!visible)
                return;

            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField(L.T(TextKey.Sheets), EditorStyles.boldLabel);
                if (GUILayout.Button(L.T(TextKey.All), EditorStyles.miniButtonLeft, GUILayout.Width(70)))
                {
                    setAll(true);
                    OnSettingsChanged();
                }
                if (GUILayout.Button(L.T(TextKey.None), EditorStyles.miniButtonRight, GUILayout.Width(70)))
                {
                    setAll(false);
                    OnSettingsChanged();
                }
            }
        }

        private void DrawButtons()
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                using (new EditorGUI.DisabledScope(IsBusy))
                {
                    if (GUILayout.Button(L.T(TextKey.Check), GUILayout.Height(24)))
                        RunCheck(SheetImportSettings.instance);
                }

                using (new EditorGUI.DisabledScope(!CanApply))
                {
                    if (GUILayout.Button(L.T(TextKey.Apply), GUILayout.Height(24)))
                    {
                        Apply();
                        // A dialog or an asset refresh may have run inside this layout pass.
                        GUIUtility.ExitGUI();
                    }
                }
            }

            if (IsBusy)
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.LabelField(L.T(_busyText.Value));
                    if (GUILayout.Button(L.T(TextKey.Cancel), GUILayout.Width(80)))
                        CancelLoading();
                }
            }
        }

        private void DrawReport()
        {
            _scroll = EditorGUILayout.BeginScrollView(_scroll);

            var sourceMessage = _sourceMessage?.Invoke();
            if (!string.IsNullOrEmpty(sourceMessage))
                EditorGUILayout.HelpBox(sourceMessage, MessageType.Error);

            if (_loaded != null)
            {
                foreach (var error in _loaded.Errors)
                    EditorGUILayout.HelpBox(error, MessageType.Error);
                foreach (var warning in _loaded.Warnings)
                    EditorGUILayout.HelpBox(warning, MessageType.Warning);
            }

            if (_check != null)
            {
                foreach (var error in _check.Errors)
                    EditorGUILayout.HelpBox(error, MessageType.Error);

                if (_check.DiffText != null)
                {
                    EditorGUILayout.LabelField(L.T(TextKey.DiffHeader), EditorStyles.boldLabel);
                    DrawMono(_check.DiffText);
                }

                var errorCount = _check.Errors.Count + (_loaded?.Errors.Count ?? 0);
                if (errorCount > 0)
                    EditorGUILayout.HelpBox(L.T(TextKey.ErrorCount, errorCount), MessageType.Error);
                else
                    EditorGUILayout.HelpBox(L.T(TextKey.NoErrors), MessageType.Info);
            }

            if (!string.IsNullOrEmpty(_importText))
            {
                EditorGUILayout.Space();
                if (_waitingForReload)
                    EditorGUILayout.HelpBox(L.T(TextKey.ImportWaitingForReload), MessageType.Info);
                else if (_importHasErrors)
                    EditorGUILayout.HelpBox(L.T(TextKey.ImportFinishedWithErrors), MessageType.Error);
                else
                    EditorGUILayout.HelpBox(L.T(TextKey.ImportApplied), MessageType.Info);
                DrawMono(_importText);
            }

            EditorGUILayout.EndScrollView();
        }

        // A hint under a field, aligned with the field column. LabelField(" ", text, style) reserves exactly one line,
        // so a wrapped hint would be cut; a plain wrapped label gets the height it needs.
        private void DrawHint(string text)
        {
            if (_hintStyle == null)
                _hintStyle = new GUIStyle(EditorStyles.miniLabel) { wordWrap = true };

            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.Space(EditorGUIUtility.labelWidth + 2);
                GUILayout.Label(text, _hintStyle);
            }
        }

        private void DrawMono(string text)
        {
            if (_monoStyle == null)
            {
                _monoFont = Font.CreateDynamicFontFromOSFont(MonoFonts, 12);
                if (_monoFont != null)
                    _monoFont.hideFlags = HideFlags.HideAndDontSave;
                _monoStyle = new GUIStyle(EditorStyles.label) { font = _monoFont, wordWrap = false, richText = false };
            }

            var size = _monoStyle.CalcSize(new GUIContent(text));
            EditorGUILayout.SelectableLabel(text, _monoStyle, GUILayout.MinWidth(size.x), GUILayout.Height(size.y));
        }

        private void OnSettingsChanged()
        {
            SheetImportSettings.instance.SaveSettings();
            ResetCheck();
            _sourceMessage = null;
        }

        private void ResetCheck()
        {
            _checkedRequest = null;
            _loaded = null;
            _check = null;
        }

        private void RefreshCsvFiles(SheetImportSettings settings)
        {
            var folder = settings.csvFolder;
            if (string.IsNullOrWhiteSpace(folder) || !Directory.Exists(folder))
            {
                settings.csvFiles.Clear();
                OnSettingsChanged();
                _sourceMessage = () => L.T(TextKey.CsvFolderNotFound, folder);
                return;
            }

            List<string> files;
            try
            {
                files = CsvFolderSource.ListFiles(folder, true);
            }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException)
            {
                OnSettingsChanged();
                var reason = e.Message;
                _sourceMessage = () => L.T(TextKey.CsvFolderUnreadable, folder, reason);
                return;
            }

            SheetImportSettings.Merge(settings.csvFiles, files);
            OnSettingsChanged();
            if (files.Count == 0)
                _sourceMessage = () => L.T(TextKey.CsvFolderNoFiles, folder);
        }

        private async void RefreshGoogleSheets(SheetImportSettings settings)
        {
            var token = BeginLoading(TextKey.LoadingSheetList);
            SheetListResult result;
            try
            {
                result = await _loader.ListGoogleSheetsAsync(settings.spreadsheetId, _apiKey, token);
            }
            catch (OperationCanceledException)
            {
                if (this != null)
                    EndLoading();
                return;
            }

            if (this == null)
                return;
            EndLoading();

            if (result.Error != null)
            {
                ResetCheck();
                var error = result.Error;
                _sourceMessage = () => error;
                return;
            }

            SheetImportSettings.Merge(settings.googleSheets, result.Titles);
            OnSettingsChanged();
        }

        private async void RunCheck(SheetImportSettings settings)
        {
            ResetCheck();
            _sourceMessage = null;
            ClearImportResult();

            var request = new SheetImportRequest
            {
                assetFolder = settings.assetFolder,
                codeFolder = settings.codeFolder,
                @namespace = settings.@namespace,
            };

            var token = BeginLoading(TextKey.Loading);
            SheetLoadResult loaded;
            try
            {
                loaded = await LoadAsync(settings, token);
            }
            catch (OperationCanceledException)
            {
                if (this != null)
                {
                    EndLoading();
                    _sourceMessage = () => L.T(TextKey.LoadingCancelled);
                }
                return;
            }

            if (this == null)
                return;
            EndLoading();

            foreach (var sheet in loaded.Sheets)
                sheet.tableType = settings.GetTableType(sheet.name);
            request.sheets = loaded.Sheets;
            _check = SheetAssetImporter.Check(request);
            // Nothing loaded because of load errors: they already say why, the empty set error repeats them.
            if (loaded.Sheets.Count == 0 && loaded.Errors.Count > 0)
                _check.Errors.Remove(SheetSetChecker.NoSheetsError);
            _loaded = loaded;
            _checkedRequest = request;
        }

        private Task<SheetLoadResult> LoadAsync(SheetImportSettings settings, CancellationToken token)
        {
            switch (settings.source)
            {
                case SheetSourceKind.GoogleApi:
                    var titles = settings.googleSheets.Where(sheet => sheet.selected).Select(sheet => sheet.name).ToList();
                    if (titles.Count == 0)
                        return Task.FromResult(new SheetLoadResult());
                    return _loader.LoadGoogleAsync(settings.spreadsheetId, _apiKey, titles, token);

                case SheetSourceKind.Export:
                    var sheets = settings.exportSheets.Where(sheet => sheet.selected).ToList();
                    return _loader.LoadExportAsync(settings.spreadsheetId, sheets, token);

                default:
                    var files = settings.csvFiles.Where(file => file.selected).Select(file => file.name).ToList();
                    return Task.FromResult(SheetSourceLoader.LoadCsvFolder(settings.csvFolder, files));
            }
        }

        private void Apply()
        {
            if (!CanApply)
                return;

            if (_check.HasRemovals && !EditorUtility.DisplayDialog(Title, L.T(TextKey.RemovalsDialog),
                    L.T(TextKey.Apply), L.T(TextKey.Cancel)))
                return;

            var request = _checkedRequest;
            ResetCheck();
            ShowImportResult(SheetAssetImporter.Import(request));
        }

        private void OnImportContinued(SheetImportReport report)
        {
            ShowImportResult(report);
        }

        private void ShowImportResult(SheetImportReport report)
        {
            _importText = report.ToString();
            _importHasErrors = report.HasErrors;
            _waitingForReload = report.WaitingForReload;
            Repaint();
        }

        private void ClearImportResult()
        {
            _importText = null;
            _importHasErrors = false;
            _waitingForReload = false;
        }

        // Controls are disabled while loading, so only one loading runs at a time.
        private CancellationToken BeginLoading(TextKey text)
        {
            _cancel = new CancellationTokenSource();
            _busyText = text;
            Repaint();
            return _cancel.Token;
        }

        private void EndLoading()
        {
            _cancel?.Dispose();
            _cancel = null;
            _busyText = null;
            Repaint();
        }

        private void CancelLoading()
        {
            _cancel?.Cancel();
        }
    }
}
