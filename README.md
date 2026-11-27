# Sheets to ScriptableObject

Unity-пакет для импорта таблиц Google Sheets и CSV в типизированные ScriptableObject-конфиги. Типы столбцов задаются в заголовке таблицы (тогда классы генерируются) или в вашем коде. Перед импортом данные проверяются, а изменения показываются в виде diff — применяются только после подтверждения.

> **Статус: каркас (0.1.0).** Структура пакета и сборки готовы, логики импорта пока нет. Формат данных утверждён — см. [Documentation~/data-format.md](Documentation~/data-format.md).

## Требования

- Unity 2022.3 LTS или новее.
- Сторонних зависимостей нет (ни Odin, ни SDK Google, ни Newtonsoft).

## Подключение

**По git URL.** *Window → Package Manager → + → Add package from git URL…* и вставьте:

```
https://github.com/kentavr-lab/sheets-to-scriptableobject.git
```

Конкретная версия — с тегом (теги появляются с релизами; тега `v0.1.0` пока нет, до релиза используйте URL без тега):

```
https://github.com/kentavr-lab/sheets-to-scriptableobject.git#v0.1.0
```

**Через `Packages/manifest.json`.** Добавьте строку в `dependencies`:

```json
{
  "dependencies": {
    "com.kentavr-lab.sheets-to-scriptableobject": "https://github.com/kentavr-lab/sheets-to-scriptableobject.git"
  }
}
```

После релиза можно закрепить версию, дописав к URL `#v0.1.0`.

**С диска.** Склонируйте репозиторий, затем *Package Manager → + → Add package from disk…* и выберите `package.json` в корне клона.

## Использование

Класс строки — `[Serializable]` с `ISheetRow`, таблица — наследник `SheetTable<>`:

```csharp
using System;
using Kentavr.SheetsToSO;
using Kentavr.SheetsToSO.Core;
using UnityEngine;

[Serializable]
public class UnitRow : ISheetRow
{
    [SerializeField] private string id;
    public int hp;

    public string Id => id;
}

[CreateAssetMenu(menuName = "Tables/Units")]
public sealed class UnitsTable : SheetTable<UnitRow> {}
```

Поиск по `id` (регистр важен):

```csharp
if (unitsTable.TryGet("knight", out var knight))
{
    Debug.Log(knight.hp);
}
```

Строки без `id` в поиск не попадают; при повторе `id` используется первая строка. Об обоих случаях пишется ошибка в консоль со ссылкой на ассет.

## Консольная проверка (CI)

Приложение `Cli~/` загружает вкладки, разбирает их и проверяет ссылки и enum тем же кодом ядра, что и редактор, и печатает отчёт в консоль. Нужен .NET SDK 8.0 или новее. Из корня репозитория:

```
dotnet run --project "Cli~" -- --source csv --file Units.csv
```

Три источника:

```
# CSV/TSV-файлы: одна вкладка = один файл, имя вкладки — имя файла без расширения; .tsv — с табуляцией
dotnet run --project "Cli~" -- --source csv --file Data/Units.csv --file Data/Weapons.tsv

# Google Sheets API: без --sheet проверяются все вкладки, кроме начинающихся с #
dotnet run --project "Cli~" -- --source api --spreadsheet <ID> [--sheet Units --sheet Weapons]

# Экспорт CSV по ссылке: у ссылки нет имени вкладки, поэтому вкладка задаётся как имя=gid
dotnet run --project "Cli~" -- --source export --spreadsheet <ID> --sheet Units=0 --sheet Weapons=123456789
```

API-ключ для `--source api` берётся из переменной окружения `SHEETS_TO_SO_API_KEY` (другое имя — `--api-key-env <ИМЯ>`). В аргументах ключ не принимается и в вывод не попадает. Справка — `--help`.

Отчёт — по строке на проблему, в конце итог:

```
error: Units!C2: Column 'hp': 'abc' is not a valid int
error: Units!D2: Column 'weapon': 'axe': no row with this id in sheet 'Weapons'
Sheets: 2, errors: 2, warnings: 0
```

| Код выхода | Значение |
|---|---|
| `0` | Ошибок нет (предупреждения, например о `,` в числах, на код не влияют). |
| `1` | Ошибки данных: заголовок, ячейки, `id`, синтаксис CSV, `ref`/`enum`. |
| `2` | Ошибки аргументов или доступа: неизвестный ключ, нет файла, не задан ключ API, ответ 403, HTML вместо CSV. Сообщение — в stderr, разбор не выполняется. |

## Документация

- [Формат данных](Documentation~/data-format.md) — источники, правила таблицы, типы, что считается ошибкой.

## Структура

| Сборка | Папка | За что отвечает |
|---|---|---|
| `Kentavr.SheetsToSO.Core` | `Runtime/Core/` | Весь конвейер без Unity: источники, разбор, проверка, diff. Не ссылается на `UnityEngine`/`UnityEditor` и сторонние библиотеки, поэтому работает в редакторе, в консоли и в CI. |
| `Kentavr.SheetsToSO` | `Runtime/` | Рантайм: базовый ScriptableObject таблицы с поиском по `id`. |
| `Kentavr.SheetsToSO.Editor` | `Editor/` | Только редактор: окно импорта, кодогенерация, хранение API-ключа. |

| — | `Cli~/` | Консольное приложение `sheets-to-so` для CI: проверка вкладок с кодом выхода. Ссылается на ядро через `Tests~/Core`, в пакет для Unity не попадает. |

Папки `Cli~/`, `Documentation~/` и `Tests~/` Unity не импортирует.

## Тесты

Ядро тестируется без Unity: проект `Tests~/Core` компилирует те же исходники из `Runtime/Core/`, что и Unity. Из корня репозитория:

```
dotnet test "Tests~/Core.Tests"
dotnet test "Tests~/Cli.Tests"
```

Нужен .NET SDK 8.0 или новее.

## Лицензия

[MIT](LICENSE)
