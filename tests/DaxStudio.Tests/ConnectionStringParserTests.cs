using ADOTabular.Utils;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DaxStudio.Tests
{
    [TestClass]
    public class ConnectionStringParserTests
    {
        [TestMethod]
        public void ParseSimpleConnStrTest()
        {
            var connstr = "Data Source=localhost;initial catalog=test";
            var results = ConnectionStringParser.Parse(connstr);
            Assert.AreEqual(2, results.Count, "2 key value pairs");
            Assert.AreEqual("localhost", results["Data Source"]);
            Assert.AreEqual("test", results["initial catalog"]);
        }

        [TestMethod]
        public void ParsePowerPivotConnStrTest()
        {
            var connstr = "Data Source=http://localhost:9000/xmla;Application Name=DAX Studio (Power Pivot) - 2ba50c5f-fef0-4142-a896-e53ca929899e;Location=\"D:\\Data\\Dax Examples\\02 DAX filter similar - 'Copy.xlsx\";Extended Properties=\"Location=D:\\Data\\Dax Examples\\02 DAX filter similar - 'Copy.xlsx;\";Workstation ID=\"D:\\Data\\Dax Examples\\02 DAX filter similar - 'Copy.xlsx\"";
            var results = ConnectionStringParser.Parse(connstr);
            Assert.AreEqual(5, results.Count, "5 key value pairs");
            Assert.AreEqual("http://localhost:9000/xmla", results["Data Source"]);
            Assert.AreEqual("DAX Studio (Power Pivot) - 2ba50c5f-fef0-4142-a896-e53ca929899e", results["Application Name"]);
        }

    }
}
