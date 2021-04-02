using DaxStudio.Tests.Assertions;
using DaxStudio.UI.Model;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DaxStudio.Tests
{
    [TestClass]
    public class DaxHelperTests
    {
        [TestMethod]
        public void TestParameters()
        {
            string qry = "EVALUTE FILTER(table, column = @param";
            var mockEventAgg = new Mocks.MockEventAggregator();
            var queryInfo = new QueryInfo(qry, false, false, mockEventAgg);
            
            Assert.AreEqual(1, queryInfo.Parameters.Count);
            Assert.AreEqual("param", queryInfo.Parameters["param"].Name);
        }

        [TestMethod]
        public void TestCommentedParameters()
        {
            string qry = "EVALUTE\n"+
                "-- FILTER(table, column = @param1)\n"+
                 "FILTER(table, column = @param2)";
            var mockEventAgg = new Mocks.MockEventAggregator();
            var queryInfo = new QueryInfo(qry, false, false, mockEventAgg);

            Assert.AreEqual(1, queryInfo.Parameters.Count);
            Assert.AreEqual("param2", queryInfo.Parameters["param2"].Name);
        }

        [TestMethod]
        public void TestBlockCommentedParameters()
        {
            string qry = "/* " +
                "* FILTER(table, column = @param1)\n" +
                "*/" +
                "EVALUTE\n" +
                "-- FILTER(table, column = @param2)\n" +
                 "FILTER(table, column = @param3)";
            var mockEventAgg = new Mocks.MockEventAggregator();
            var queryInfo = new QueryInfo(qry, false,false, mockEventAgg);

            Assert.AreEqual(1, queryInfo.Parameters.Count);
            Assert.AreEqual("param3", queryInfo.Parameters["param3"].Name);
        }

        [TestMethod]
        public void TestCommentedXmlParameters()
        {
            string qry = "/* " +
                "* FILTER(table, column = @param1)\n" +
                "*/" +
                "EVALUTE\n" +
                "-- FILTER(table, column = @param2)\n" +
                 "IF(LEN(@param3)>0\n" +
                 ",FILTER(table, column = @param3))\n" +
                 "<Parameters>\n" +
                 "<Parameter>\n" +
                 "<Name>param3</Name>\n" +
                 "<Value>value3</Value>\n" +
                 "</Parameter>\n" +
                 "</Parameters>\n";
            var mockEventAgg = new Mocks.MockEventAggregator();
            var queryInfo = new QueryInfo(qry, false, false, mockEventAgg);

            string expectedQry = "/* " +
                "* FILTER(table, column = @param1)\n" +
                "*/" +
                "EVALUTE\n" +
                "-- FILTER(table, column = @param2)\n" +
                "IF(LEN(\"value3\")>0\n" +
                 ",FILTER(table, column = \"value3\"))";

            Assert.AreEqual(1, queryInfo.Parameters.Count);
            Assert.AreEqual("param3", queryInfo.Parameters["param3"].Name);
            StringAssertion.ShouldEqualWithDiff(expectedQry, queryInfo.QueryWithMergedParameters,DiffStyle.Full);
            
        }



    }
}
