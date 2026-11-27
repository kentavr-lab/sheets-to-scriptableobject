namespace Kentavr.SheetsToSO.Core
{
    /// <summary>
    /// Default code generation templates.
    /// Placeholders are replaced as plain text; unknown <c>{{...}}</c> are left as is.
    /// </summary>
    public static class CodeTemplates
    {
        /// <summary>Placeholder for the auto-generated header comment.</summary>
        public const string HeaderPlaceholder = "{{Header}}";

        /// <summary>Placeholder for the namespace.</summary>
        public const string NamespacePlaceholder = "{{Namespace}}";

        /// <summary>Placeholder for the row class name.</summary>
        public const string ClassNamePlaceholder = "{{ClassName}}";

        /// <summary>Placeholder for the row class fields, each line indented by 8 spaces.</summary>
        public const string FieldsPlaceholder = "{{Fields}}";

        /// <summary>Placeholder for the enum name.</summary>
        public const string EnumNamePlaceholder = "{{EnumName}}";

        /// <summary>Placeholder for the enum members, each line indented by 8 spaces.</summary>
        public const string MembersPlaceholder = "{{Members}}";

        /// <summary>Placeholder for the table class name.</summary>
        public const string TableClassNamePlaceholder = "{{TableClassName}}";

        /// <summary>Placeholder for the row class name in the table class template.</summary>
        public const string RowClassNamePlaceholder = "{{RowClassName}}";

        /// <summary>Default row class template.</summary>
        public const string DefaultRowClass =
            "{{Header}}\n" +
            "namespace {{Namespace}}\n" +
            "{\n" +
            "    [global::System.Serializable]\n" +
            "    public sealed partial class {{ClassName}}\n" +
            "    {\n" +
            "{{Fields}}\n" +
            "    }\n" +
            "}\n";

        /// <summary>Default enum template.</summary>
        public const string DefaultEnum =
            "{{Header}}\n" +
            "namespace {{Namespace}}\n" +
            "{\n" +
            "    public enum {{EnumName}}\n" +
            "    {\n" +
            "{{Members}}\n" +
            "    }\n" +
            "}\n";

        /// <summary>
        /// Default table class template: makes the row class an <c>ISheetRow</c> and closes <c>SheetTable&lt;&gt;</c>.
        /// </summary>
        public const string DefaultTableClass =
            "{{Header}}\n" +
            "namespace {{Namespace}}\n" +
            "{\n" +
            "    public sealed partial class {{RowClassName}} : global::Kentavr.SheetsToSO.Core.ISheetRow\n" +
            "    {\n" +
            "        string global::Kentavr.SheetsToSO.Core.ISheetRow.Id => id;\n" +
            "    }\n" +
            "\n" +
            "    public sealed class {{TableClassName}} : global::Kentavr.SheetsToSO.SheetTable<{{RowClassName}}>\n" +
            "    {\n" +
            "    }\n" +
            "}\n";
    }
}
