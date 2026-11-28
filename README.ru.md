# Sheets to ScriptableObject

[English version](README.md)

Unity-пакет для импорта таблиц Google Sheets и CSV в типизированные ScriptableObject-конфиги. Типы столбцов задаются в заголовке таблицы (тогда классы генерируются) или в вашем коде. Перед импортом данные проверяются, а изменения показываются в виде diff — применяются только после подтверждения.

## Требования

- Unity 2022.3 LTS или новее.
- Сторонних зависимостей нет (ни Odin, ни SDK Google, ни Newtonsoft).

## Подключение

**По git-ссылке.** *Window → Package Manager → + → Add package from git URL…* и вставьте:

```
https://github.com/kentavr-lab/sheets-to-scriptableobject.git#v0.4.0
```

**Из `.unitypackage`.** Скачайте `SheetsToSO-<версия>.unitypackage` со страницы [Releases](https://github.com/kentavr-lab/sheets-to-scriptableobject/releases), затем *Assets → Import Package → Custom Package…* и выберите файл. Файлы лягут в `Assets/SheetsToSO/`. Для обновления удалите `Assets/SheetsToSO/` и импортируйте новый `.unitypackage`.

> Ставьте пакет только одним способом. Пакет через Package Manager и `.unitypackage` в одном проекте дают дубли классов и GUID.

Другие способы (через `Packages/manifest.json`, с диска) — в разделе [Installation](README.md#installation).

## Минимальный пример

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

## Окно импорта

*Window → Sheets to SO → Import*: выберите источник (Google API, Export или CSV-файлы) и вкладки, нажмите «Проверить» — увидите ошибки и diff, затем «Применить». Язык интерфейса переключается в самом окне (EN/RU), по умолчанию английский. Подробности — в разделе [Import window](README.md#import-window).

## Документация

Полная документация — на английском:

- [README](README.md): [Usage](README.md#usage), [`id` dropdown for your own fields](README.md#id-dropdown-for-your-own-fields), [Command-line check (CI)](README.md#command-line-check-ci).
- [Data format](Documentation~/data-format.md) — источники, правила таблицы, типы, что считается ошибкой.
- [Changelog](CHANGELOG.md).

## Лицензия

[MIT](LICENSE)
