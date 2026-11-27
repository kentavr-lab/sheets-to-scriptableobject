using System;
using Kentavr.SheetsToSO.Core;
using NUnit.Framework;

namespace Kentavr.SheetsToSO.Core.Tests
{
    public class CellAddressTests
    {
        [TestCase(0, 1, "A1")]
        [TestCase(1, 1, "B1")]
        [TestCase(25, 1, "Z1")]
        [TestCase(26, 1, "AA1")]
        [TestCase(51, 1, "AZ1")]
        [TestCase(52, 1, "BA1")]
        [TestCase(701, 1, "ZZ1")]
        [TestCase(702, 1, "AAA1")]
        [TestCase(2, 7, "C7")]
        public void ToA1ConvertsIndexToLetters(int columnIndex, int rowNumber, string expected)
        {
            Assert.That(CellAddress.ToA1(columnIndex, rowNumber), Is.EqualTo(expected));
        }

        [TestCase(-1, 1)]
        [TestCase(0, 0)]
        public void ToA1RejectsOutOfRange(int columnIndex, int rowNumber)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => CellAddress.ToA1(columnIndex, rowNumber));
        }
    }
}
