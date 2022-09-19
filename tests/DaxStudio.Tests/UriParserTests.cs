using DaxStudio.Common;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using System;
using System.Web;
using System.Windows;

namespace DaxStudio.Tests
{
    [TestClass]
    public class UriParserTests
    {
        [TestMethod]
        public void TestMethod1()
        {
            var input = "daxstudio:?server=localhost:1234&Database=adventure%20Works%202020";
            var uri = new Uri(input);
            var queryParams = HttpUtility.ParseQueryString(uri.Query);
            Assert.AreEqual( "localhost:1234", queryParams["Server"]);
            Assert.AreEqual( "adventure Works 2020", queryParams["database"]);

        }

        [TestMethod]
        public void CmdLineArgMappingTest()
        {
            var input = "daxstudio:?server=localhost:1234&Database=adventure%20Works%202020";
            var mockApp = (new Mock<Application>()).Object;
            var args = new CmdLineArgs(mockApp);
            CmdLineArgs.ParseUri(ref mockApp, input);

            Assert.AreEqual("localhost:1234", args.Server, "Server parsed");
            Assert.AreEqual("adventure Works 2020", args.Database, "Database parsed");
        }

        [TestMethod]
        public void QueryParameterTest()
        {
            var input = "daxstudio:?server=powerbi%3a%2f%2fapi.powerbi.com%2fv1.0%2fmyorg%2fdgosbell%2520demo&database=Adv+Wrks+Azure+DQ&query=RVZBTFVBVEUgRGltUmVzZWxsZXI=";
            var mockApp = (new Mock<Application>()).Object;
            var args = new CmdLineArgs(mockApp);
            CmdLineArgs.ParseUri(ref mockApp, input);

            Assert.AreEqual("powerbi://api.powerbi.com/v1.0/myorg/dgosbell%20demo", args.Server, "Server parsed");
            Assert.AreEqual("Adv Wrks Azure DQ", args.Database, "Database parsed");
            Assert.AreEqual("EVALUATE DimReseller", args.Query, "Query parsed");
        }
    }
}
