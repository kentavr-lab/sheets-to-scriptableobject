# Changelog

Все заметные изменения пакета. Формат — [Keep a Changelog](https://keepachangelog.com/ru/1.1.0/), версии — [SemVer](https://semver.org/lang/ru/).

## [Unreleased]

- Разбор строки заголовка таблицы (`HeaderParser`): типы, массивы, обязательность, пропуск `#`, ошибки с адресом ячейки.
- Минимальная версия Unity — 2022.3 LTS (была 2021.3).
- Базовый ScriptableObject таблицы `SheetTable<TRow>`: сериализуемый список строк и поиск по `id`; индекс `RowIndex` в ядре.

## [0.1.0]

- Каркас пакета: `package.json`, сборки `Kentavr.SheetsToSO`, `Kentavr.SheetsToSO.Core`, `Kentavr.SheetsToSO.Editor`, dotnet-тесты ядра, описание формата данных. Логики импорта пока нет.
