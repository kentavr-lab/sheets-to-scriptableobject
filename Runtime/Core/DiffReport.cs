using System;
using System.Globalization;
using System.Text;

namespace Kentavr.SheetsToSO.Core
{
    /// <summary>
    /// Short text report of a <see cref="SnapshotDiff"/>. See Documentation~/data-format.md, "Diff".
    /// </summary>
    public static class DiffReport
    {
        internal const string Arrow =" → ";
        private const string MissingText = "(missing)";

        /// <summary>
        /// Formats the diff: one block per sheet, <c>\n</c> line breaks, no trailing line break.
        /// Returns <c>No changes</c> when the diff is empty.
        /// </summary>
        public static string Format(SnapshotDiff diff)
        {
            if (diff == null)
                throw new ArgumentNullException(nameof(diff));
            if (!diff.HasChanges)
                return "No changes";

            var sb = new StringBuilder();
            foreach (var sheet in diff.Sheets)
            {
                if (sb.Length > 0)
                    sb.Append('\n');

                switch (sheet.Kind)
                {
                    case SheetDiffKind.Added:
                        sb.Append("+ ").Append(sheet.SheetName).Append(" (new sheet, ")
                            .Append(RowCount(sheet.AddedRows.Count)).Append(')');
                        break;
                    case SheetDiffKind.Removed:
                        sb.Append("- ").Append(sheet.SheetName).Append(" (removed sheet, ")
                            .Append(RowCount(sheet.RemovedRows.Count)).Append(')');
                        break;
                    default:
                        AppendChangedSheet(sb, sheet);
                        break;
                }
            }
            return sb.ToString();
        }

        private static void AppendChangedSheet(StringBuilder sb, SheetDiff sheet)
        {
            sb.Append(sheet.SheetName).Append(": +")
                .Append(sheet.AddedRows.Count.ToString(CultureInfo.InvariantCulture)).Append(" -")
                .Append(sheet.RemovedRows.Count.ToString(CultureInfo.InvariantCulture)).Append(" ~")
                .Append(sheet.ChangedRows.Count.ToString(CultureInfo.InvariantCulture));

            foreach (var row in sheet.AddedRows)
                sb.Append("\n  + ").Append(row.Id);
            foreach (var row in sheet.RemovedRows)
                sb.Append("\n  - ").Append(row.Id);
            foreach (var row in sheet.ChangedRows)
            {
                sb.Append("\n  ~ ").Append(row.Id).Append(": ");
                for (var i = 0; i < row.Fields.Count; i++)
                {
                    if (i > 0)
                        sb.Append("; ");
                    var field = row.Fields[i];
                    sb.Append(field.Column).Append(": ").Append(field.OldText).Append(Arrow).Append(field.NewText);
                }
            }
        }

        private static string RowCount(int count)
        {
            return count.ToString(CultureInfo.InvariantCulture) + (count == 1 ? " row" : " rows");
        }

        /// <summary>
        /// Display text of a cell value: <c>"Sword"</c>, <c>120</c>, <c>1.5</c>, <c>true</c>, <c>null</c>,
        /// <c>["melee", "starter"]</c>, <c>(missing)</c>.
        /// </summary>
        internal static string FormatValue(object value, bool missing)
        {
            if (missing)
                return MissingText;

            var sb = new StringBuilder();
            AppendValue(sb, value);
            return sb.ToString();
        }

        private static void AppendValue(StringBuilder sb, object value)
        {
            switch (value)
            {
                case null:
                    sb.Append("null");
                    break;
                case string s:
                    sb.Append('"').Append(s).Append('"');
                    break;
                case bool b:
                    sb.Append(b ? "true" : "false");
                    break;
                case int i:
                    sb.Append(i.ToString(CultureInfo.InvariantCulture));
                    break;
                case float f:
                    sb.Append(f.ToString("R", CultureInfo.InvariantCulture));
                    break;
                case Array array:
                    sb.Append('[');
                    for (var k = 0; k < array.Length; k++)
                    {
                        if (k > 0)
                            sb.Append(", ");
                        AppendValue(sb, array.GetValue(k));
                    }
                    sb.Append(']');
                    break;
                case IFormattable formattable:
                    sb.Append(formattable.ToString(null, CultureInfo.InvariantCulture));
                    break;
                default:
                    sb.Append(value);
                    break;
            }
        }
    }
}
