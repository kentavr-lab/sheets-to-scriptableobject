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

### Импорт CSV из папки (временная команда)

До появления окна импорта: правый клик по папке в окне Project → *Sheets to SO → Import CSV folder here…* → выберите папку на диске с `.csv`/`.tsv`. Каждый файл — вкладка (имя файла без расширения; файлы, начинающиеся с `#`, пропускаются). Ассеты создаются в выбранной папке (`Units.asset`), код — в её подпапке `Generated/`, namespace — `SheetsData`. Повторный запуск обновляет ассеты на месте. Подробности — в разделе «Импорт» [формата данных](Documentation~/data-format.md).

## Документация

- [Формат данных](Documentation~/data-format.md) — источники, правила таблицы, типы, что считается ошибкой.

## Структура

| Сборка | Папка | За что отвечает |
|---|---|---|
| `Kentavr.SheetsToSO.Core` | `Runtime/Core/` | Весь конвейер без Unity: источники, разбор, проверка, diff. Не ссылается на `UnityEngine`/`UnityEditor` и сторонние библиотеки, поэтому работает в редакторе, в консоли и в CI. |
| `Kentavr.SheetsToSO` | `Runtime/` | Рантайм: базовый ScriptableObject таблицы с поиском по `id`. |
| `Kentavr.SheetsToSO.Editor` | `Editor/` | Только редактор: окно импорта, кодогенерация, хранение API-ключа. |

Папки `Documentation~/` и `Tests~/` Unity не импортирует.

## Тесты

Ядро тестируется без Unity: проект `Tests~/Core` компилирует те же исходники из `Runtime/Core/`, что и Unity. Из корня репозитория:

```
dotnet test "Tests~/Core.Tests"
```

Нужен .NET SDK 8.0 или новее.

## Лицензия

[MIT](LICENSE)
