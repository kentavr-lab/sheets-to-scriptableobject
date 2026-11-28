using System.Collections.Generic;

namespace Kentavr.SheetsToSO.Editor
{
    internal static partial class EditorText
    {
        private const string RussianName = "Русский";

        private static Dictionary<TextKey, string> CreateRussianTable()
        {
            return new Dictionary<TextKey, string>
            {
                [TextKey.Source] = "Источник",
                [TextKey.SourceGoogleApi] = "Google API",
                [TextKey.SourceExport] = "Export",
                [TextKey.SourceCsvFiles] = "CSV-файлы",
                [TextKey.SpreadsheetId] = "ID таблицы",
                [TextKey.ApiKey] = "Ключ API",
                [TextKey.Clear] = "Очистить",
                [TextKey.ApiKeyHint] = "Ключ хранится в настройках редактора (EditorPrefs), не в проекте.",
                [TextKey.RefreshSheetList] = "Обновить список вкладок",
                [TextKey.GoogleSheetsEmpty] = "Нажмите «Обновить список вкладок».",
                [TextKey.ExportHint] = "Ключ не нужен: таблица должна быть открыта по ссылке на просмотр.",
                [TextKey.SheetName] = "Имя вкладки",
                [TextKey.TableType] = "Тип таблицы",
                [TextKey.GenerateCode] = "Генерировать код",
                [TextKey.TableTypeNotFound] = "{0} (не найден)",
                [TextKey.AddSheet] = "Добавить вкладку",
                [TextKey.CsvFolder] = "Папка с CSV",
                [TextKey.Browse] = "Выбрать…",
                [TextKey.CsvFolderPanelTitle] = "Папка с .csv / .tsv",
                [TextKey.RefreshFileList] = "Обновить список файлов",
                [TextKey.CsvFilesEmpty] = "Выберите папку с файлами .csv / .tsv.",
                [TextKey.Sheets] = "Вкладки",
                [TextKey.All] = "Все",
                [TextKey.None] = "Ничего",
                [TextKey.AssetsFolder] = "Папка ассетов",
                [TextKey.CodeFolder] = "Папка кода",
                [TextKey.Check] = "Проверить",
                [TextKey.Apply] = "Применить",
                [TextKey.Cancel] = "Отмена",

                [TextKey.Loading] = "Загрузка…",
                [TextKey.LoadingSheetList] = "Загрузка списка вкладок…",
                [TextKey.LoadingCancelled] = "Загрузка отменена",
                [TextKey.CsvFolderNotFound] = "Папка '{0}' не найдена",
                [TextKey.CsvFolderUnreadable] = "Папка '{0}' не читается: {1}",
                [TextKey.CsvFolderNoFiles] = "В папке '{0}' нет файлов .csv / .tsv",
                [TextKey.DiffHeader] = "Изменения относительно последнего импорта",
                [TextKey.ErrorCount] = "Ошибок: {0}. Применение недоступно.",
                [TextKey.NoErrors] = "Ошибок нет — можно применять.",
                [TextKey.ImportWaitingForReload] = "Код обновлён, ассеты обновятся после перекомпиляции.",
                [TextKey.ImportFinishedWithErrors] = "Импорт завершён с ошибками, подробности ниже и в Console.",
                [TextKey.ImportApplied] = "Импорт применён.",
                [TextKey.RemovalsDialog] = "В diff есть удалённые строки или вкладки. Удалённые строки пропадут из ассетов; " +
                    "ассеты удалённых вкладок останутся на месте.\n\nПрименить импорт?",

                [TextKey.ApiKeyMissing] = "API-ключ не задан",
                [TextKey.SpreadsheetIdMissing] = "ID таблицы не задан",
                [TextKey.NetworkError] = "Сетевая ошибка: {0}",
                [TextKey.Timeout] = "Время ожидания ответа истекло",

                [TextKey.SheetRefEmpty] = "(пусто)",
                [TextKey.SheetRefNotString] = "[SheetRef] работает только с полями string, string[] и List<string>",
                [TextKey.SheetRefInvalidType] = "[SheetRef]: тип {0} должен быть неабстрактным наследником SheetTable<>",
                [TextKey.SheetRefMissingRow] = "'{0}': нет строки в {1} ({2})",
                [TextKey.SheetRefNoAssetAtPath] = "нет ассета {0} по пути '{1}'",
                [TextKey.SheetRefNoAsset] = "нет ассета {0} в проекте",
                [TextKey.SheetRefSeveralAssets] = "найдено {0} ассетов {1}, используется '{2}'; укажите Asset в атрибуте",

                [TextKey.MenuImportCsvFolder] = "Импортировать сюда папку CSV…",
            };
        }
    }
}
