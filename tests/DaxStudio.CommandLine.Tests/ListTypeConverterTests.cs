using DaxStudio.CommandLine.Converters;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Collections.Generic;

namespace DaxStudio.CommandLine.Tests
{
    [TestClass]
    public class ListTypeConverterTests
    {
        [TestMethod]
        public void SingleItemShouldRetunListWithOneItem()
        {
            var conv = new StringListTypeConverter();
            List<string> result = (List<string>)conv.ConvertFromString("Table1");
            Assert.HasCount(1, result, "List should have 1 item");
            Assert.AreEqual("Table1", result[0], "List item should be \"Table1\"");

        }

        [TestMethod]
        public void ThreeItemShouldRetunListWithThreeItems()
        {
            var conv = new StringListTypeConverter();
            List<string> result = (List<string>)conv.ConvertFromString("Table1,Table2,Table3");
            Assert.HasCount(3, result, "List should have 1 item");
            Assert.AreEqual("Table1", result[0], "List item should be Table1");
            Assert.AreEqual("Table1", result[0], "List item should be Table2");
            Assert.AreEqual("Table1", result[0], "List item should be Table3");
        }

        [TestMethod]
        public void ThreeItemWithQuotesShouldRetunListWithThreeItems()
        {
            var conv = new StringListTypeConverter();
            List<string> result = (List<string>)conv.ConvertFromString("Table1,\"Table2\",'Table3'");
            Assert.HasCount(3, result, "List should have 1 item");
            Assert.AreEqual("Table1", result[0], "List item should be Table1");
            Assert.AreEqual("\"Table2\"", result[1], "List item should be \"Table2\"");
            Assert.AreEqual("'Table3'", result[2], "List item should be 'Table3'");

        }

        [TestMethod]
        public void ThreeItemWithEscapedCommasShouldRetunListWithThreeItems()
        {
            var conv = new StringListTypeConverter();
            List<string> result = (List<string>)conv.ConvertFromString("Table1,\"Table,,2\",'Table3'");
            Assert.HasCount(3, result, "List should have 1 item");
            Assert.AreEqual("Table1", result[0], "List item should be Table1");
            Assert.AreEqual("\"Table,2\"", result[1], "List item should be \"Table,2\"");
            Assert.AreEqual("'Table3'", result[2], "List item should be 'Table3'");

        }

        [TestMethod]
        public void ThreeItemWithEscapedCommasShouldRetunListWithThreeItemsWithoutLeadingOrTrailingWhitespace()
        {
            var conv = new StringListTypeConverter();
            List<string> result = (List<string>)conv.ConvertFromString("Table1 , \"Table,,2\" , 'Table3'");
            Assert.HasCount(3, result, "List should have 1 item");
            Assert.AreEqual("Table1", result[0], "List item should be Table1");
            Assert.AreEqual("\"Table,2\"", result[1], "List item should be \"Table,2\"");
            Assert.AreEqual("'Table3'", result[2], "List item should be 'Table3'");

        }

    }
}
