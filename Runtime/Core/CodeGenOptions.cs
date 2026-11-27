using System;

namespace Kentavr.SheetsToSO.Core
{
    /// <summary>
    /// Immutable code generation settings.
    /// </summary>
    public sealed class CodeGenOptions
    {
        /// <param name="namespace">Namespace of the generated code, e.g. <c>My.Game.Data</c>.</param>
        /// <param name="rowClassTemplate">Row class template text; <c>null</c> uses <see cref="CodeTemplates.DefaultRowClass"/>.</param>
        /// <param name="enumTemplate">Enum template text; <c>null</c> uses <see cref="CodeTemplates.DefaultEnum"/>.</param>
        public CodeGenOptions(string @namespace, string rowClassTemplate = null, string enumTemplate = null)
        {
            if (@namespace == null)
                throw new ArgumentNullException(nameof(@namespace));
            if (!IsValidNamespace(@namespace))
                throw new ArgumentException(
                    $"Invalid namespace '{@namespace}': use identifiers separated by '.', not C# keywords",
                    nameof(@namespace));

            Namespace = @namespace;
            RowClassTemplate = rowClassTemplate ?? CodeTemplates.DefaultRowClass;
            EnumTemplate = enumTemplate ?? CodeTemplates.DefaultEnum;
        }

        /// <summary>Namespace of the generated code.</summary>
        public string Namespace { get; }

        /// <summary>Row class template text.</summary>
        public string RowClassTemplate { get; }

        /// <summary>Enum template text.</summary>
        public string EnumTemplate { get; }

        private static bool IsValidNamespace(string value)
        {
            var parts = value.Split('.');
            foreach (var part in parts)
            {
                if (!CSharpIdentifier.IsValidName(part))
                    return false;
            }
            return true;
        }
    }
}
