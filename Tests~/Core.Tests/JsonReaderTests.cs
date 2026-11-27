using System;
using System.Collections.Generic;
using NUnit.Framework;

namespace Kentavr.SheetsToSO.Core.Tests
{
    public class JsonReaderTests
    {
        [Test]
        public void ParsesObjectWithAllValueKinds()
        {
            var root = (Dictionary<string, object>)JsonReader.Parse(
                " { \"s\": \"text\", \"n\": -12.5e3, \"t\": true, \"f\": false, \"z\": null, \"a\": [1, \"x\"], \"o\": {} } ");

            Assert.That(root["s"], Is.EqualTo("text"));
            Assert.That(((JsonNumber)root["n"]).Text, Is.EqualTo("-12.5e3"));
            Assert.That(root["t"], Is.EqualTo(true));
            Assert.That(root["f"], Is.EqualTo(false));
            Assert.That(root["z"], Is.Null);
            var array = (List<object>)root["a"];
            Assert.That(array, Has.Count.EqualTo(2));
            Assert.That(((JsonNumber)array[0]).Text, Is.EqualTo("1"));
            Assert.That(array[1], Is.EqualTo("x"));
            Assert.That((Dictionary<string, object>)root["o"], Is.Empty);
        }

        [TestCase("0", "0")]
        [TestCase("120", "120")]
        [TestCase("3.5", "3.5")]
        [TestCase("46349.5", "46349.5")]
        [TestCase("12345678901234567890.123456789", "12345678901234567890.123456789")]
        [TestCase("1E+2", "1E+2")]
        public void KeepsNumberSourceText(string json, string expected)
        {
            Assert.That(((JsonNumber)JsonReader.Parse(json)).Text, Is.EqualTo(expected));
        }

        [Test]
        public void DecodesEscapes()
        {
            var value = JsonReader.Parse("\"q\\\" b\\\\ s\\/ \\b\\f\\n\\r\\t \\u0416 \\ud83d\\ude00\"");

            Assert.That(value, Is.EqualTo("q\" b\\ s/ \b\f\n\r\t Ж \U0001F600"));
        }

        [Test]
        public void KeepsNonAsciiText()
        {
            Assert.That(JsonReader.Parse("\"Оружие it's\""), Is.EqualTo("Оружие it's"));
        }

        [Test]
        public void SkipsByteOrderMark()
        {
            Assert.That(JsonReader.Parse("\uFEFF[]"), Is.Empty);
        }

        [TestCase("")]
        [TestCase("   ")]
        [TestCase("{")]
        [TestCase("{\"a\":1,}")]
        [TestCase("[1,]")]
        [TestCase("[1 2]")]
        [TestCase("{\"a\" 1}")]
        [TestCase("{a:1}")]
        [TestCase("\"unterminated")]
        [TestCase("\"bad \\x escape\"")]
        [TestCase("\"\\u12\"")]
        [TestCase("\"\\ud83d\"")]
        [TestCase("\"\\ude00\"")]
        [TestCase("\"line\nbreak\"")]
        [TestCase("01")]
        [TestCase("1.")]
        [TestCase("-")]
        [TestCase("1e")]
        [TestCase("+1")]
        [TestCase("tru")]
        [TestCase("nul")]
        [TestCase("{} {}")]
        [TestCase("<html>")]
        public void RejectsMalformedJson(string json)
        {
            Assert.Throws<FormatException>(() => JsonReader.Parse(json));
        }

        [Test]
        public void RejectsTooDeepNesting()
        {
            Assert.Throws<FormatException>(() => JsonReader.Parse(new string('[', 1000) + new string(']', 1000)));
        }
    }
}
