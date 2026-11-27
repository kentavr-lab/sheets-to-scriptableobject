using System;
using System.Text;

namespace Kentavr.SheetsToSO.Core
{
    /// <summary>
    /// Converts zero-based column indices and one-based row numbers to A1-style cell addresses.
    /// </summary>
    public static class CellAddress
    {
        /// <summary>
        /// Returns the A1-style address, e.g. <c>ToA1(1, 1)</c> → <c>"B1"</c>, <c>ToA1(26, 3)</c> → <c>"AA3"</c>.
        /// </summary>
        /// <param name="columnIndex">Zero-based column index.</param>
        /// <param name="rowNumber">One-based row number.</param>
        public static string ToA1(int columnIndex, int rowNumber)
        {
            if (columnIndex < 0)
                throw new ArgumentOutOfRangeException(nameof(columnIndex), columnIndex, "Column index must be non-negative.");
            if (rowNumber < 1)
                throw new ArgumentOutOfRangeException(nameof(rowNumber), rowNumber, "Row number must be positive.");

            return ColumnLetters(columnIndex) + rowNumber.ToString(System.Globalization.CultureInfo.InvariantCulture);
        }

        private static string ColumnLetters(int columnIndex)
        {
            var builder = new StringBuilder();
            var n = columnIndex + 1;
            while (n > 0)
            {
                n--;
                builder.Insert(0, (char)('A' + n % 26));
                n /= 26;
            }
            return builder.ToString();
        }
    }
}
