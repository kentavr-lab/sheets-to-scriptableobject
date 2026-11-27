using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace Kentavr.SheetsToSO.Core
{
    /// <summary>
    /// Reads CSV/TSV text (RFC 4180) into rows of string cells.
    /// Data errors are collected into the result, never thrown.
    /// </summary>
    public static class CsvReader
    {
        private const int EndOfText = -1;
        private const char ByteOrderMark = '﻿';
        private const char Quote = '"';

        private enum FieldEnd
        {
            Delimiter,
            EndOfRecord,
            EndOfText,
        }

        public static CsvReadResult Read(string text, CsvDelimiter delimiter)
        {
            if (text == null)
                throw new ArgumentNullException(nameof(text));

            using (var reader = new StringReader(text))
                return Read(reader, delimiter);
        }

        public static CsvReadResult Read(TextReader reader, CsvDelimiter delimiter)
        {
            if (reader == null)
                throw new ArgumentNullException(nameof(reader));

            var state = new State(reader, ToChar(delimiter));
            state.ReadAll();
            return new CsvReadResult(state.Rows.AsReadOnly(), state.Errors.AsReadOnly());
        }

        private static char ToChar(CsvDelimiter delimiter)
        {
            switch (delimiter)
            {
                case CsvDelimiter.Comma: return ',';
                case CsvDelimiter.Tab: return '\t';
                default: throw new ArgumentOutOfRangeException(nameof(delimiter), delimiter, "Unsupported delimiter");
            }
        }

        private sealed class State
        {
            private readonly TextReader _reader;
            private readonly char _delimiter;
            private readonly StringBuilder _value = new StringBuilder();
            private int _line = 1;
            private int _rowNumber;

            public State(TextReader reader, char delimiter)
            {
                _reader = reader;
                _delimiter = delimiter;
            }

            public List<CsvRow> Rows { get; } = new List<CsvRow>();

            public List<CsvError> Errors { get; } = new List<CsvError>();

            public void ReadAll()
            {
                if (_reader.Peek() == ByteOrderMark)
                    _reader.Read();

                while (_reader.Peek() != EndOfText)
                {
                    _rowNumber++;
                    var recordLine = _line;
                    var cells = new List<string>();

                    FieldEnd end;
                    do
                    {
                        end = ReadField();
                        cells.Add(_value.ToString());
                    }
                    while (end == FieldEnd.Delimiter);

                    var count = cells.Count;
                    while (count > 0 && cells[count - 1].Length == 0)
                        count--;
                    cells.RemoveRange(count, cells.Count - count);

                    Rows.Add(new CsvRow(_rowNumber, recordLine, cells.AsReadOnly()));

                    if (end == FieldEnd.EndOfText)
                        break;
                }

                var rowCount = Rows.Count;
                while (rowCount > 0 && Rows[rowCount - 1].Cells.Count == 0)
                    rowCount--;
                Rows.RemoveRange(rowCount, Rows.Count - rowCount);
            }

            private FieldEnd ReadField()
            {
                _value.Clear();

                if (_reader.Peek() == Quote)
                {
                    _reader.Read();
                    if (!ReadQuoted())
                        return FieldEnd.EndOfText;

                    var next = _reader.Peek();
                    if (next == '\r')
                    {
                        // Only "\r\n" ends the record; a lone '\r' is ordinary text after the quote.
                        _reader.Read();
                        if (_reader.Peek() == '\n')
                        {
                            _reader.Read();
                            _line++;
                            return FieldEnd.EndOfRecord;
                        }

                        AddTextAfterQuoteError();
                        _value.Append('\r');
                    }
                    else if (next != EndOfText && next != _delimiter && next != '\n')
                        AddTextAfterQuoteError();
                }

                return ReadUnquoted();
            }

            private void AddTextAfterQuoteError()
            {
                Errors.Add(new CsvError(CsvErrorCode.TextAfterClosingQuote, _rowNumber, _line,
                    $"Unexpected text after closing quote in row {_rowNumber}"));
            }

            /// <summary>Reads a quoted value after the opening quote. Returns false if the text ends inside quotes.</summary>
            private bool ReadQuoted()
            {
                var quoteLine = _line;

                while (true)
                {
                    var c = _reader.Read();
                    switch (c)
                    {
                        case EndOfText:
                            Errors.Add(new CsvError(CsvErrorCode.UnclosedQuote, _rowNumber, quoteLine,
                                $"Unclosed quote in row {_rowNumber}"));
                            return false;

                        case Quote:
                            if (_reader.Peek() != Quote)
                                return true;
                            _reader.Read();
                            _value.Append(Quote);
                            break;

                        case '\r':
                            if (_reader.Peek() == '\n')
                            {
                                _reader.Read();
                                _value.Append('\n');
                                _line++;
                            }
                            else
                                _value.Append('\r');
                            break;

                        case '\n':
                            _value.Append('\n');
                            _line++;
                            break;

                        default:
                            _value.Append((char)c);
                            break;
                    }
                }
            }

            /// <summary>Appends characters up to the end of the field and consumes the terminator.</summary>
            private FieldEnd ReadUnquoted()
            {
                while (true)
                {
                    var c = _reader.Read();
                    if (c == EndOfText)
                        return FieldEnd.EndOfText;
                    if (c == _delimiter)
                        return FieldEnd.Delimiter;
                    if (c == '\n')
                    {
                        _line++;
                        return FieldEnd.EndOfRecord;
                    }
                    if (c == '\r' && _reader.Peek() == '\n')
                    {
                        _reader.Read();
                        _line++;
                        return FieldEnd.EndOfRecord;
                    }

                    _value.Append((char)c);
                }
            }
        }
    }
}
