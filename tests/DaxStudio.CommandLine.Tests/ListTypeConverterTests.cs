using DaxStudio.CommandLine.Converters;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

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
            Assert.AreEqual(1, result.Count,"List should have 1 item");
            Assert.AreEqual("Table1", result[0], "List item should be \"Table1\"");

        }

        [TestMethod]
        public void ThreeItemShouldRetunListWithThreeItems()
        {
            var conv = new StringListTypeConverter();
            List<string> result = (List<string>)conv.ConvertFromString("Table1,Table2,Table3");
            Assert.AreEqual(3, result.Count, "List should have 1 item");
            Assert.AreEqual("Table1", result[0], "List item should be Table1");
            Assert.AreEqual("Table1", result[0], "List item should be Table2");
            Assert.AreEqual("Table1", result[0], "List item should be Table3");
        }

        [TestMethod]
        public void ThreeItemWithQuotesShouldRetunListWithThreeItems()
        {
            var conv = new StringListTypeConverter();
            List<string> result = (List<string>)conv.ConvertFromString("Table1,\"Table2\",'Table3'");
            Assert.AreEqual(3, result.Count, "List should have 1 item");
            Assert.AreEqual("Table1", result[0], "List item should be Table1");
            Assert.AreEqual("\"Table2\"", result[1], "List item should be \"Table2\"");
            Assert.AreEqual("'Table3'", result[2], "List item should be 'Table3'");

        }

        [TestMethod]
        public void ThreeItemWithEscapedCommasShouldRetunListWithThreeItems()
        {
            var conv = new StringListTypeConverter();
            List<string> result = (List<string>)conv.ConvertFromString("Table1,\"Table,,2\",'Table3'");
            Assert.AreEqual(3, result.Count, "List should have 1 item");
            Assert.AreEqual("Table1", result[0], "List item should be Table1");
            Assert.AreEqual("\"Table,2\"", result[1], "List item should be \"Table,2\"");
            Assert.AreEqual("'Table3'", result[2], "List item should be 'Table3'");

        }

        [TestMethod]
        public void ThreeItemWithEscapedCommasShouldRetunListWithThreeItemsWithoutLeadingOrTrailingWhitespace()
        {
            var conv = new StringListTypeConverter();
            List<string> result = (List<string>)conv.ConvertFromString("Table1 , \"Table,,2\" , 'Table3'");
            Assert.AreEqual(3, result.Count, "List should have 1 item");
            Assert.AreEqual("Table1", result[0], "List item should be Table1");
            Assert.AreEqual("\"Table,2\"", result[1], "List item should be \"Table,2\"");
            Assert.AreEqual("'Table3'", result[2], "List item should be 'Table3'");

        }

    }
}
