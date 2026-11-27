using System;
using System.Collections.Generic;

namespace Kentavr.SheetsToSO.Core
{
    /// <summary>
    /// Identifier rules shared by the header parser and the code generator.
    /// </summary>
    internal static class CSharpIdentifier
    {
        // Reserved C# keywords (77). Contextual keywords such as var, value, record are allowed.
        private static readonly HashSet<string> Keywords = new HashSet<string>(StringComparer.Ordinal)
        {
            "abstract", "as", "base", "bool", "break", "byte", "case", "catch", "char", "checked",
            "class", "const", "continue", "decimal", "default", "delegate", "do", "double", "else", "enum",
            "event", "explicit", "extern", "false", "finally", "fixed", "float", "for", "foreach", "goto",
            "if", "implicit", "in", "int", "interface", "internal", "is", "lock", "long", "namespace",
            "new", "null", "object", "operator", "out", "override", "params", "private", "protected", "public",
            "readonly", "ref", "return", "sbyte", "sealed", "short", "sizeof", "stackalloc", "static", "string",
            "struct", "switch", "this", "throw", "true", "try", "typeof", "uint", "ulong", "unchecked",
            "unsafe", "ushort", "using", "virtual", "void", "volatile", "while",
        };

        /// <summary>
        /// Latin letters, digits and '_', not starting with a digit.
        /// </summary>
        public static bool IsIdentifier(string value)
        {
            if (string.IsNullOrEmpty(value))
                return false;

            for (var i = 0; i < value.Length; i++)
            {
                var c = value[i];
                var isLetter = (c >= 'A' && c <= 'Z') || (c >= 'a' && c <= 'z') || c == '_';
                var isDigit = c >= '0' && c <= '9';
                if (!(isLetter || (i > 0 && isDigit)))
                    return false;
            }
            return true;
        }

        /// <summary>
        /// Whether the value is a reserved C# keyword.
        /// </summary>
        public static bool IsKeyword(string value)
        {
            return value != null && Keywords.Contains(value);
        }

        /// <summary>
        /// Identifier that is not a reserved keyword, i.e. usable as a name in generated code.
        /// </summary>
        public static bool IsValidName(string value)
        {
            return IsIdentifier(value) && !IsKeyword(value);
        }
    }
}
