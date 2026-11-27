using Kentavr.SheetsToSO.Core;
using NUnit.Framework;

namespace Kentavr.SheetsToSO.Core.Tests
{
    public class SmokeTests
    {
        [Test]
        public void FormatVersionIsOne()
        {
            Assert.That(FormatInfo.Version, Is.EqualTo(1));
        }
    }
}
