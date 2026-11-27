using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace Kentavr.SheetsToSO.Core
{
    /// <summary>JSON number kept as its source text, so no precision or locale issues.</summary>
    internal sealed class JsonNumber
    {
        public JsonNumber(string text)
        {
            Text = text ?? throw new ArgumentNullException(nameof(text));
        }

        public string Text { get; }

        public override string ToString() => Text;
    }

    /// <summary>
    /// Minimal JSON parser. Produces <see cref="Dictionary{TKey,TValue}"/> (string → object) for objects,
    /// <see cref="List{T}"/> of object for arrays, <see cref="string"/>, <see cref="JsonNumber"/>,
    /// <see cref="bool"/> and <c>null</c>. Malformed input throws <see cref="FormatException"/>.
    /// </summary>
    internal static class JsonReader
    {
        public static object Parse(string text)
        {
            if (text == null)
                throw new ArgumentNullException(nameof(text));

            var parser = new Parser(text);
            parser.SkipWhitespace();
            var value = parser.ReadValue(0);
            parser.SkipWhitespace();
            if (!parser.AtEnd)
                throw parser.Error("Unexpected text after JSON value");
            return value;
        }

        private sealed class Parser
        {
            private const int MaxDepth = 256;

            private readonly string _text;
            private readonly StringBuilder _buffer = new StringBuilder();
            private int _pos;

            public Parser(string text)
            {
                _text = text;
                if (_text.Length > 0 && _text[0] == '﻿')
                    _pos = 1;
            }

            public bool AtEnd => _pos >= _text.Length;

            public FormatException Error(string message) =>
                new FormatException($"{message} at position {_pos}");

            public void SkipWhitespace()
            {
                while (_pos < _text.Length)
                {
                    var c = _text[_pos];
                    if (c != ' ' && c != '\t' && c != '\n' && c != '\r')
                        break;
                    _pos++;
                }
            }

            public object ReadValue(int depth)
            {
                if (depth > MaxDepth)
                    throw Error("JSON is nested too deeply");
                if (AtEnd)
                    throw Error("Unexpected end of JSON");

                var c = _text[_pos];
                switch (c)
                {
                    case '{': return ReadObject(depth);
                    case '[': return ReadArray(depth);
                    case '"': return ReadString();
                    case 't': ReadLiteral("true"); return true;
                    case 'f': ReadLiteral("false"); return false;
                    case 'n': ReadLiteral("null"); return null;
                    default:
                        if (c == '-' || (c >= '0' && c <= '9'))
                            return ReadNumber();
                        throw Error($"Unexpected character '{c}'");
                }
            }

            private Dictionary<string, object> ReadObject(int depth)
            {
                _pos++; // {
                var result = new Dictionary<string, object>(StringComparer.Ordinal);
                SkipWhitespace();
                if (Peek() == '}')
                {
                    _pos++;
                    return result;
                }

                while (true)
                {
                    SkipWhitespace();
                    if (Peek() != '"')
                        throw Error("Expected property name");
                    var key = ReadString();
                    SkipWhitespace();
                    Expect(':');
                    SkipWhitespace();
                    result[key] = ReadValue(depth + 1);
                    SkipWhitespace();

                    var c = Next();
                    if (c == '}')
                        return result;
                    if (c != ',')
                        throw Error("Expected ',' or '}'");
                }
            }

            private List<object> ReadArray(int depth)
            {
                _pos++; // [
                var result = new List<object>();
                SkipWhitespace();
                if (Peek() == ']')
                {
                    _pos++;
                    return result;
                }

                while (true)
                {
                    SkipWhitespace();
                    result.Add(ReadValue(depth + 1));
                    SkipWhitespace();

                    var c = Next();
                    if (c == ']')
                        return result;
                    if (c != ',')
                        throw Error("Expected ',' or ']'");
                }
            }

            private string ReadString()
            {
                _pos++; // opening quote
                _buffer.Clear();

                while (true)
                {
                    if (AtEnd)
                        throw Error("Unterminated string");

                    var c = _text[_pos++];
                    if (c == '"')
                        return _buffer.ToString();
                    if (c < ' ')
                        throw Error("Control character in string");
                    if (c != '\\')
                    {
                        _buffer.Append(c);
                        continue;
                    }

                    if (AtEnd)
                        throw Error("Unterminated escape sequence");

                    var e = _text[_pos++];
                    switch (e)
                    {
                        case '"': _buffer.Append('"'); break;
                        case '\\': _buffer.Append('\\'); break;
                        case '/': _buffer.Append('/'); break;
                        case 'b': _buffer.Append('\b'); break;
                        case 'f': _buffer.Append('\f'); break;
                        case 'n': _buffer.Append('\n'); break;
                        case 'r': _buffer.Append('\r'); break;
                        case 't': _buffer.Append('\t'); break;
                        case 'u': ReadUnicodeEscape(); break;
                        default: throw Error($"Invalid escape sequence '\\{e}'");
                    }
                }
            }

            private void ReadUnicodeEscape()
            {
                var high = ReadHex4();
                if (!char.IsHighSurrogate(high))
                {
                    if (char.IsLowSurrogate(high))
                        throw Error("Unpaired low surrogate");
                    _buffer.Append(high);
                    return;
                }

                if (_pos + 1 >= _text.Length || _text[_pos] != '\\' || _text[_pos + 1] != 'u')
                    throw Error("Unpaired high surrogate");
                _pos += 2;
                var low = ReadHex4();
                if (!char.IsLowSurrogate(low))
                    throw Error("Unpaired high surrogate");
                _buffer.Append(high).Append(low);
            }

            private char ReadHex4()
            {
                if (_pos + 4 > _text.Length)
                    throw Error("Invalid \\u escape");
                if (!int.TryParse(_text.Substring(_pos, 4), NumberStyles.AllowHexSpecifier,
                        CultureInfo.InvariantCulture, out var code))
                    throw Error("Invalid \\u escape");
                _pos += 4;
                return (char)code;
            }

            private JsonNumber ReadNumber()
            {
                var start = _pos;
                if (Peek() == '-')
                    _pos++;

                if (Peek() == '0')
                    _pos++;
                else if (IsDigit(Peek()))
                    SkipDigits();
                else
                    throw Error("Invalid number");

                if (Peek() == '.')
                {
                    _pos++;
                    if (!IsDigit(Peek()))
                        throw Error("Invalid number");
                    SkipDigits();
                }

                if (Peek() == 'e' || Peek() == 'E')
                {
                    _pos++;
                    if (Peek() == '+' || Peek() == '-')
                        _pos++;
                    if (!IsDigit(Peek()))
                        throw Error("Invalid number");
                    SkipDigits();
                }

                return new JsonNumber(_text.Substring(start, _pos - start));
            }

            private void SkipDigits()
            {
                while (IsDigit(Peek()))
                    _pos++;
            }

            private static bool IsDigit(int c) => c >= '0' && c <= '9';

            private void ReadLiteral(string literal)
            {
                if (string.CompareOrdinal(_text, _pos, literal, 0, literal.Length) != 0)
                    throw Error("Invalid literal");
                _pos += literal.Length;
            }

            private void Expect(char expected)
            {
                if (Next() != expected)
                    throw Error($"Expected '{expected}'");
            }

            private int Peek() => AtEnd ? -1 : _text[_pos];

            private int Next() => AtEnd ? -1 : _text[_pos++];
        }
    }
}
