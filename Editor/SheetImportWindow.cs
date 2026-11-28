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

        private static readonly string[] MonoFonts = { "Consolas", "Menlo", "DejaVu Sans Mono", "Courier New" };

        private SheetSourceLoader _loader;
        private CancellationTokenSource _cancel;
        private string _busyText;
        private string _apiKey = string.Empty;

        // Last check; cleared by any change of the settings or check boxes. Not kept over a script reload.
        private SheetImportRequest _checkedRequest;
        private SheetLoadResult _loaded;
        private SheetImportCheck _check;
        private string _sourceMessage;

        // Last import result; kept over the script reload that follows a code change.
        [SerializeField] private string _importText;
        [SerializeField] private bool _importHasErrors;
        [SerializeField] private bool _waitingForReload;
        [SerializeField] private Vector2 _scroll;

        private Font _monoFont;
        private GUIStyle _monoStyle;

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
        }

        private void OnGUI()
        {
            var settings = SheetImportSettings.instance;

            using (new EditorGUI.DisabledScope(IsBusy))
            {
                EditorGUI.BeginChangeCheck();

                settings.source = (SheetSourceKind)EditorGUILayout.EnumPopup("Источник", settings.source);
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
                settings.assetFolder = EditorGUILayout.TextField("Папка ассетов", settings.assetFolder);
                settings.codeFolder = EditorGUILayout.TextField("Папка кода", settings.codeFolder);
                settings.@namespace = EditorGUILayout.TextField("Namespace", settings.@namespace);

                if (EditorGUI.EndChangeCheck())
                    OnSettingsChanged();
            }

            EditorGUILayout.Space();
            DrawButtons();
            EditorGUILayout.Space();
            DrawReport();
        }

        private void DrawGoogle(SheetImportSettings settings)
        {
            settings.spreadsheetId = EditorGUILayout.TextField("ID таблицы", settings.spreadsheetId);

            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUI.BeginChangeCheck();
                var key = EditorGUILayout.PasswordField("Ключ API", _apiKey);
                if (EditorGUI.EndChangeCheck())
                {
                    _apiKey = key;
                    ApiKeyStore.Save(key);
                }
                if (GUILayout.Button("Очистить", GUILayout.Width(80)))
                {
                    _apiKey = string.Empty;
                    ApiKeyStore.Save(string.Empty);
                    GUI.FocusControl(null);
                    OnSettingsChanged();
                }
            }
            EditorGUILayout.LabelField(" ", "Ключ хранится в настройках редактора (EditorPrefs), не в проекте.",
                EditorStyles.miniLabel);

            if (GUILayout.Button("Обновить список вкладок"))
                RefreshGoogleSheets(settings);

            DrawToggles(settings.googleSheets, "Нажмите «Обновить список вкладок».");
        }

        private void DrawExport(SheetImportSettings settings)
        {
            settings.spreadsheetId = EditorGUILayout.TextField("ID таблицы", settings.spreadsheetId);
            EditorGUILayout.LabelField(" ", "Ключ не нужен: таблица должна быть открыта по ссылке на просмотр.",
                EditorStyles.miniLabel);

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
                    EditorGUILayout.LabelField("Имя вкладки", EditorStyles.miniBoldLabel);
                    EditorGUILayout.LabelField("gid", EditorStyles.miniBoldLabel, GUILayout.Width(110));
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
                    if (GUILayout.Button("−", GUILayout.Width(24)))
                        remove = i;
                }
            }

            if (remove >= 0)
            {
                settings.exportSheets.RemoveAt(remove);
                OnSettingsChanged();
            }

            if (GUILayout.Button("Добавить вкладку"))
            {
                settings.exportSheets.Add(new ExportSheetEntry { name = string.Empty, gid = string.Empty });
                OnSettingsChanged();
            }
        }

        private void DrawCsvFiles(SheetImportSettings settings)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                settings.csvFolder = EditorGUILayout.TextField("Папка с CSV", settings.csvFolder);
                if (GUILayout.Button("Выбрать…", GUILayout.Width(80)))
                {
                    var folder = EditorUtility.OpenFolderPanel("Папка с .csv / .tsv", settings.csvFolder, string.Empty);
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

            if (GUILayout.Button("Обновить список файлов"))
                RefreshCsvFiles(settings);

            DrawToggles(settings.csvFiles, "Выберите папку с файлами .csv / .tsv.", CsvFolderSource.SheetName);
        }

        private void DrawToggles(List<SheetToggle> list, string emptyText, Func<string, string> label = null)
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
                var text = label?.Invoke(item.name) ?? item.name;
                item.selected = EditorGUILayout.ToggleLeft(new GUIContent(text, item.name), item.selected);
            }
        }

        private void DrawAllNone(bool visible, Action<bool> setAll)
        {
            if (!visible)
                return;

            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField("Вкладки", EditorStyles.boldLabel);
                if (GUILayout.Button("Все", EditorStyles.miniButtonLeft, GUILayout.Width(70)))
                {
                    setAll(true);
                    OnSettingsChanged();
                }
                if (GUILayout.Button("Ничего", EditorStyles.miniButtonRight, GUILayout.Width(70)))
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
                    if (GUILayout.Button("Проверить", GUILayout.Height(24)))
                        RunCheck(SheetImportSettings.instance);
                }

                using (new EditorGUI.DisabledScope(!CanApply))
                {
                    if (GUILayout.Button("Применить", GUILayout.Height(24)))
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
                    EditorGUILayout.LabelField(_busyText);
                    if (GUILayout.Button("Отмена", GUILayout.Width(80)))
                        CancelLoading();
                }
            }
        }

        private void DrawReport()
        {
            _scroll = EditorGUILayout.BeginScrollView(_scroll);

            if (!string.IsNullOrEmpty(_sourceMessage))
                EditorGUILayout.HelpBox(_sourceMessage, MessageType.Error);

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
                    EditorGUILayout.LabelField("Изменения относительно последнего импорта", EditorStyles.boldLabel);
                    DrawMono(_check.DiffText);
                }

                var errorCount = _check.Errors.Count + (_loaded?.Errors.Count ?? 0);
                if (errorCount > 0)
                    EditorGUILayout.HelpBox($"Ошибок: {errorCount}. Применение недоступно.", MessageType.Error);
                else
                    EditorGUILayout.HelpBox("Ошибок нет — можно применять.", MessageType.Info);
            }

            if (!string.IsNullOrEmpty(_importText))
            {
                EditorGUILayout.Space();
                if (_waitingForReload)
                    EditorGUILayout.HelpBox("Код обновлён, ассеты обновятся после перекомпиляции.", MessageType.Info);
                else if (_importHasErrors)
                    EditorGUILayout.HelpBox("Импорт завершён с ошибками, подробности ниже и в Console.", MessageType.Error);
                else
                    EditorGUILayout.HelpBox("Импорт применён.", MessageType.Info);
                DrawMono(_importText);
            }

            EditorGUILayout.EndScrollView();
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
                _sourceMessage = $"Папка '{folder}' не найдена";
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
                _sourceMessage = $"Папка '{folder}' не читается: {e.Message}";
                return;
            }

            SheetImportSettings.Merge(settings.csvFiles, files);
            OnSettingsChanged();
            if (files.Count == 0)
                _sourceMessage = $"В папке '{folder}' нет файлов .csv / .tsv";
        }

        private async void RefreshGoogleSheets(SheetImportSettings settings)
        {
            var token = BeginLoading("Загрузка списка вкладок…");
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
                _sourceMessage = result.Error;
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

            var token = BeginLoading("Загрузка…");
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
                    _sourceMessage = "Загрузка отменена";
                }
                return;
            }

            if (this == null)
                return;
            EndLoading();

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

            if (_check.HasRemovals && !EditorUtility.DisplayDialog(Title,
                    "В diff есть удалённые строки или вкладки. Удалённые строки пропадут из ассетов; " +
                    "ассеты удалённых вкладок останутся на месте.\n\nПрименить импорт?",
                    "Применить", "Отмена"))
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
        private CancellationToken BeginLoading(string text)
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
