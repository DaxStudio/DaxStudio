using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using DaxStudio.UI.ViewModels;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DaxStudio.Tests
{
    [TestClass]
    public class RefreshTraceTests
    {
        [TestMethod]
        public void TestPartitionObjectReferenceParsing()
        {
            var xml =
                "<Object><Partition>Date</Partition><Table>Date</Table><Model>Adventure Works Internet Sales Model</Model><Database>Adventure Works</Database></Object>";
            var dict = RefreshCommand.ParseObjectReference(xml);
            Assert.IsTrue(dict.ContainsKey("Partition"));
            Assert.AreEqual(dict["Partition"], "Date");
            Assert.AreEqual(dict["Table"], "Date");
            Assert.AreEqual(dict["Model"], "Adventure Works Internet Sales Model");
            Assert.AreEqual(dict["Database"], "Adventure Works");
        }

        [TestMethod]
        public void TestAttributeHierarchyObjectReferenceParsing()
        {
            var xml =
                "<Object><AttributeHierarchy/><Column>Day of Month</Column><Table>Date</Table><Model>Adventure Works Internet Sales Model</Model><Database>Adventure Works</Database></Object>";
            var dict = RefreshCommand.ParseObjectReference(xml);
            Assert.IsTrue(dict.ContainsKey("Column"));
            Assert.AreEqual(dict["AttributeHierarchy"], string.Empty);
            Assert.AreEqual(dict["Column"], "Day of Month");
            Assert.AreEqual(dict["Table"], "Date");
            Assert.AreEqual(dict["Model"], "Adventure Works Internet Sales Model");
            Assert.AreEqual(dict["Database"], "Adventure Works");
        }
    }
}
