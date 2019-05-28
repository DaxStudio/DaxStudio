using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using DaxStudio.UI.Utils;
using System.Text;
using DaxStudio.UI.Model;
using DaxStudio.Tests.Assertions;
using DaxStudio.Tests.Helpers;
using Caliburn.Micro;
using Moq;

namespace DaxStudio.Tests
{
    [TestClass]
    public class DaxQueryPreProcessorTests
    {


        IEventAggregator mockEventAggregator;
        [TestInitialize]
        public void InitializeTest()
        {
            mockEventAggregator = new Mock<IEventAggregator>().Object;
        }



        [TestMethod]
        public void TestQueryInjectEvaluate()
        {
            var testQuery = @"FILTER(
table,
table[email] = ""abcdefg @gmail.com"")";
            var qi = new QueryInfo(testQuery, true, false, mockEventAggregator);
            //var dict = DaxHelper.ParseParams(testParam, new Mocks.MockEventAggregator() );
            Assert.AreEqual("EVALUATE " + testQuery, qi.ProcessedQuery );
        }

        [TestMethod]
        public void TestQueryInjectRow()
        {
            var testQuery = "1";
            var expectedQuery = "EVALUATE ROW(\"Value\", 1 )";
            var qi = new QueryInfo(testQuery, true, true, mockEventAggregator);
            //var dict = DaxHelper.ParseParams(testParam, new Mocks.MockEventAggregator() );
            Assert.AreEqual(expectedQuery, qi.ProcessedQuery);
        }
    }
}

