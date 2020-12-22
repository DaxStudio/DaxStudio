using ADOTabular.Utils;
using Microsoft.VisualStudio.TestTools.UnitTesting;

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

        [TestMethod]
        public void parseComplicatedPath()
        {
            var connstr = "Data Source=http://localhost:9000/xmla;Application Name=DAX Studio (Power Pivot) - 37fad17f-7a7a-4f60-a758-4340f9e3b478;;Location=\"C:\\Users\\darren.gosbell\\Documents\\PowerPivot's Test\\PwrPvtTest =- 'ćopy.xlsx;.xlsx\";Extended Properties='Location=\"C:\\Users\\darren.gosbell\\Documents\\PowerPivot''s Test\\PwrPvtTest = -''ćopy.xlsx;.xlsx\"';Workstation ID=\"C:\\Users\\darren.gosbell\\Documents\\PowerPivot's Test\\PwrPvtTest =- 'ćopy.xlsx;.xlsx\"";
            var results = ConnectionStringParser.Parse(connstr);
            Assert.AreEqual(5, results.Count, "5 key value pairs");
            Assert.AreEqual("http://localhost:9000/xmla", results["Data Source"]);
            Assert.AreEqual("DAX Studio (Power Pivot) - 37fad17f-7a7a-4f60-a758-4340f9e3b478", results["Application Name"]);
        }

        [TestMethod]
        public void TestCaseInsensitiveConnectionString()
        {
            var input = "data SOURCE=MyServer;InItIAl CataLoG=MyDatabase";
            var results = ConnectionStringParser.Parse(input);
            Assert.AreEqual(2, results.Count, "2 key value pairs");
            Assert.AreEqual("MyServer", results["Data Source"]);
            Assert.AreEqual("MyDatabase", results["Initial Catalog"]);
        }

        [TestMethod]
        public void TestPastedPowerBIDatasetConnection()
        {
            var input = "powerbi://api.powerbi.com/v1.0/myorg/xxx Dashboard;initial catalog=xxx Dashboard";
            var results = ConnectionStringParser.Parse(input);
            Assert.AreEqual(2, results.Count, "2 key value pairs");
            Assert.AreEqual("powerbi://api.powerbi.com/v1.0/myorg/xxx Dashboard", results["Data Source"]);
            Assert.AreEqual("xxx Dashboard", results["Initial Catalog"]);
        }

    }
}
