using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using DaxStudio.UI.Utils;
using System.Text;
using DaxStudio.UI.Model;
using DaxStudio.Tests.Assertions;
using DaxStudio.Tests.Helpers;
using Caliburn.Micro;
using Moq;
using System.Collections.Generic;

namespace DaxStudio.Tests
{
    [TestClass]
    public class DaxParameterTests
    {
        string testParam = @"<Parameters xmlns:xsi=""http://www.w3.org/2001/XMLSchema-instance"" xmlns:xsd=""http://www.w3.org/2001/XMLSchema"" xmlns=""urn:schemas-microsoft-com:xml-analysis"">
        <Parameter>
          <Name>TechType</Name>
          <Value xsi:type=""xsd:string"">ADSL|Cable|Foxtel by T-Box|PSTN</Value>
        </Parameter>
        <Parameter>
          <Name>OrderChannel</Name>
          <Value xsi:type=""xsd:string"">Bigpond|BP-BPS|CCBSynchronizer|Door to Door|In store|Inbound|Kenan|MOEM|Not Available|Online|Outbound(OTM)|PPA|SDF-M-ESB|Unknown</Value>
        </Parameter>
        <Parameter>
          <Name>ActivationType</Name>
          <Value xsi:type=""xsd:string"">Automatic|Device Events|Device Events, Field Tasks|EP|EP, Device Events|EP, Device Events, Field Tasks|EP, Field Tasks|EP, Held|EP, Held, Device Events|EP, Held, Device Events, Field Tasks|EP, Held, Field Tasks|EP, PET|EP, PET, Device Events|EP, PET, Device Events, Field Tasks|EP, PET, Field Tasks|EP, PET, Held|EP, PET, Held, Device Events|EP, PET, Held, Device Events, Field Tasks|EP, PET, Held, Field Tasks|Field Tasks|Held|Held, Device Events|Held, Device Events, Field Tasks|Held, Field Tasks|PET|PET, Device Events|PET, Field Tasks|PET, Held|PET, Held, Device Events|PET, Held, Device Events, Field Tasks|PET, Held, Field Tasks|Unknown</Value>
        </Parameter>
        <Parameter>
          <Name>MultipleProductsFlag</Name>
          <Value xsi:type=""xsd:string"">0|1</Value>
        </Parameter>
        <Parameter>
          <Name>InstallType</Name>
          <Value xsi:type=""xsd:string"">N/A|PIK|SIK</Value>
        </Parameter>
        <Parameter>
          <Name>ASAPSetDate</Name>
          <Value xsi:type=""xsd:string"">1|0</Value>
        </Parameter>
        <Parameter>
          <Name>PeriodType</Name>
          <Value xsi:type=""xsd:string"">M</Value>
        </Parameter>
        <Parameter>
          <Name>ReportPeriod</Name>
          <Value xsi:type=""xsd:string"">July 2014</Value>
        </Parameter>
        <Parameter>
          <Name>CRFTMet</Name>
          <Value xsi:type=""xsd:string"">1|0</Value>
        </Parameter>
        <Parameter>
          <Name>BillDispute90Days</Name>
          <Value xsi:type=""xsd:string"">1|0</Value>
        </Parameter>
        <Parameter>
          <Name>OrderType</Name>
          <Value xsi:type=""xsd:string"">Add New Service</Value>
        </Parameter>
        <Parameter>
          <Name>CycleTimeMet</Name>
          <Value xsi:type=""xsd:string"">1|0|-1</Value>
        </Parameter>
        <Parameter>
          <Name>NoInteractions7Days</Name>
          <Value xsi:type=""xsd:string"">1|0</Value>
        </Parameter>
        <Parameter>
          <Name>NPSOutcome</Name>
          <Value xsi:type=""xsd:string"">Advocate|Detractor|No Survey Response|Passive</Value>
        </Parameter>
      </Parameters>";

        string testQuery = @"EVALUATE
CALCULATETABLE(
SUMMARIZE (
	'Orders'
	, ""ESQ Maybe Pct"", 'Orders'[Product Episode ESQ Maybe Results %]
	, ""ESQ Maybe Count"", 'Orders'[Product Episode ESQ Maybe Results Cnt]
	, ""ESQ Maybe NPS Respondent Count"", CALCULATE ( [NPS Respondent], 'Orders'[ESQMaybeFlag] = 1 )
	, ""ESQ Maybe Episode NPS"" , CALCULATE( [Episode NPS], 'Orders'[ESQMaybeFlag] = 1 )
	, ""Credit Overrides Pct"" , 'Orders'[Product Episode Credit Overrides %]
	, ""Credit Overrides Count"",	'Orders'[Product Episode Credit Overrides Cnt] 
	, ""Credit Overrides NPS Respondent Count"", CALCULATE ( [NPS Respondent], 'Orders'[CreditOverrideFlag] = 1 )
	, ""Credit Overrides Episode NPS"" , CALCULATE( [Episode NPS], 'Orders'[CreditOverrideFlag] = 1 )
	)
	, IF(""Y"" = @PeriodType, 'Date'[Calendar Year] = VALUE(@ReportPeriod), 'Date'[Calendar Year] <> BLANK())
	, IF(""Q"" = @PeriodType, 'Date'[Calendar Quarter] = @ReportPeriod, 'Date'[Calendar Quarter] <> BLANK())
	, IF(""M"" = @PeriodType, 'Date'[Calendar Month] = @ReportPeriod, 'Date'[Calendar Month] <> BLANK())
        , 'Order Type'[Order Type] = @OrderType --Add New Service--
        , pathcontains(@CRFTMet, 'Orders'[CRFT Plus 7]) --1|0--
        , pathcontains(@BillDispute90Days, 'Orders'[BillDisputeWithin90DaysFlag]) --1|0--
	, pathcontains(@TechType,'Tech Type'[Product Name]) --ADSL--
	, pathcontains(@OrderChannel, 'Order Channel'[Order Channel]) --Bigpond|Online--
	, pathcontains(@ActivationType, 'Activation Type'[Activation Type]) --Automatic|EP--
	, pathcontains(@MultipleProductsFlag, 'Orders'[MultipleProductsFlag]) --1|0
	, pathcontains(@InstallType, 'Installation Type'[Install Type]) --PIK|SIK--
	, pathcontains(@ASAPSetDate, 'Orders'[ASAPEpisodeFlag]) --1|0--
        , pathcontains(@CycleTimeMet, 'Orders'[Met Cycle Time Target]) --1|0--
        , pathcontains(@NoInteractions7Days, 'Orders'[Interactions7DaysFlag]) --0|1--
        , pathcontains(@NPSOutcome, 'NPS Result'[NPS Result]) --Advocate--
)";

        string expectedQry = @"EVALUATE
CALCULATETABLE(
SUMMARIZE (
	'Orders'
	, ""ESQ Maybe Pct"", 'Orders'[Product Episode ESQ Maybe Results %]
	, ""ESQ Maybe Count"", 'Orders'[Product Episode ESQ Maybe Results Cnt]
	, ""ESQ Maybe NPS Respondent Count"", CALCULATE ( [NPS Respondent], 'Orders'[ESQMaybeFlag] = 1 )
	, ""ESQ Maybe Episode NPS"" , CALCULATE( [Episode NPS], 'Orders'[ESQMaybeFlag] = 1 )
	, ""Credit Overrides Pct"" , 'Orders'[Product Episode Credit Overrides %]
	, ""Credit Overrides Count"",	'Orders'[Product Episode Credit Overrides Cnt] 
	, ""Credit Overrides NPS Respondent Count"", CALCULATE ( [NPS Respondent], 'Orders'[CreditOverrideFlag] = 1 )
	, ""Credit Overrides Episode NPS"" , CALCULATE( [Episode NPS], 'Orders'[CreditOverrideFlag] = 1 )
	)
	, IF(""Y"" = ""M"", 'Date'[Calendar Year] = VALUE(""July 2014""), 'Date'[Calendar Year] <> BLANK())
	, IF(""Q"" = ""M"", 'Date'[Calendar Quarter] = ""July 2014"", 'Date'[Calendar Quarter] <> BLANK())
	, IF(""M"" = ""M"", 'Date'[Calendar Month] = ""July 2014"", 'Date'[Calendar Month] <> BLANK())
        , 'Order Type'[Order Type] = ""Add New Service"" --Add New Service--
        , pathcontains(""1|0"", 'Orders'[CRFT Plus 7]) --1|0--
        , pathcontains(""1|0"", 'Orders'[BillDisputeWithin90DaysFlag]) --1|0--
	, pathcontains(""ADSL|Cable|Foxtel by T-Box|PSTN"",'Tech Type'[Product Name]) --ADSL--
	, pathcontains(""Bigpond|BP-BPS|CCBSynchronizer|Door to Door|In store|Inbound|Kenan|MOEM|Not Available|Online|Outbound(OTM)|PPA|SDF-M-ESB|Unknown"", 'Order Channel'[Order Channel]) --Bigpond|Online--
	, pathcontains(""Automatic|Device Events|Device Events, Field Tasks|EP|EP, Device Events|EP, Device Events, Field Tasks|EP, Field Tasks|EP, Held|EP, Held, Device Events|EP, Held, Device Events, Field Tasks|EP, Held, Field Tasks|EP, PET|EP, PET, Device Events|EP, PET, Device Events, Field Tasks|EP, PET, Field Tasks|EP, PET, Held|EP, PET, Held, Device Events|EP, PET, Held, Device Events, Field Tasks|EP, PET, Held, Field Tasks|Field Tasks|Held|Held, Device Events|Held, Device Events, Field Tasks|Held, Field Tasks|PET|PET, Device Events|PET, Field Tasks|PET, Held|PET, Held, Device Events|PET, Held, Device Events, Field Tasks|PET, Held, Field Tasks|Unknown"", 'Activation Type'[Activation Type]) --Automatic|EP--
	, pathcontains(""0|1"", 'Orders'[MultipleProductsFlag]) --1|0
	, pathcontains(""N/A|PIK|SIK"", 'Installation Type'[Install Type]) --PIK|SIK--
	, pathcontains(""1|0"", 'Orders'[ASAPEpisodeFlag]) --1|0--
        , pathcontains(""1|0|-1"", 'Orders'[Met Cycle Time Target]) --1|0--
        , pathcontains(""1|0"", 'Orders'[Interactions7DaysFlag]) --0|1--
        , pathcontains(""Advocate|Detractor|No Survey Response|Passive"", 'NPS Result'[NPS Result]) --Advocate--
)";

        IEventAggregator mockEventAggregator;
        [TestInitialize]
        public void InitializeTest()
        {
            mockEventAggregator = new Mock<IEventAggregator>().Object;
        }

        [TestMethod]
        public void TestQueryParamParsing()
        {
            var qi = new QueryInfo(testParam, false,false, new Mocks.MockEventAggregator());
            //var dict = DaxHelper.ParseParams(testParam, new Mocks.MockEventAggregator() );
            Assert.AreEqual(14, qi.Parameters.Count);
        }

        [TestMethod]
        public void TestQueryParamParsingShouldSkipStrings()
        {
            var testQuery2 = @"FILTER(
table,
table[email] = ""abcdefg @gmail.com"" || table[email] = @param)";
            var qi = new QueryInfo(testQuery2, false, false, mockEventAggregator);
            //var dict = DaxHelper.ParseParams(testParam, new Mocks.MockEventAggregator() );
            Assert.AreEqual(1, qi.Parameters.Count);
        }


        [TestMethod]
        public void TestQueryParamParsingShouldSkipColumnNameStrings()
        {
            var testQuery = @"EVALUATE
ADDCOLUMNS ( {""Hello""}, ""@test"", 42 )";
            var qi = new QueryInfo(testQuery, false, false, mockEventAggregator);
            //var dict = DaxHelper.ParseParams(testParam, new Mocks.MockEventAggregator() );
            Assert.AreEqual(0, qi.Parameters.Count);
        }

        [TestMethod]
        public void TestQueryParamParsingShouldSkipQuotedTables()
        {
            var testQuery = @"FILTER(
table,
't@ble'[email] = ""abcdefg@gmail.com"" || table[email] = @param)";
            var qi = new QueryInfo(testQuery, false, false, mockEventAggregator);
            //var dict = DaxHelper.ParseParams(testParam, new Mocks.MockEventAggregator() );
            Assert.AreEqual(1, qi.Parameters.Count);
        }

        [TestMethod]
        public void TestQueryParamParsingShouldSkipColumnsOrMeasures()
        {
            var testQuery = @"FILTER(
table,
table[em@il] = ""abcdefg@gmail.com"" || table[email] = @param)";
            var qi = new QueryInfo(testQuery, false, false, mockEventAggregator);
            //var dict = DaxHelper.ParseParams(testParam, new Mocks.MockEventAggregator() );
            Assert.AreEqual(1, qi.Parameters.Count);
        }

        [TestMethod]
        public void TestQueryParamReplacement()
        {
            var qi = new QueryInfo(testQuery + "\n" + testParam, false, false, new Mocks.MockEventAggregator());
            //var dict = DaxHelper.ParseParams(testParam, new Mocks.MockEventAggregator());
            //var finalQry = DaxHelper.replaceParamsInQuery(new StringBuilder(testQuery), dict);
            var actualQry = qi.QueryWithMergedParameters;//.Replace("\n", "");
            StringAssertion.ShouldEqualWithDiff(expectedQry.NormalizeNewline(), actualQry.NormalizeNewline(), DiffStyle.Full);
        }

        [TestMethod]
        public void TestPreProcessQuery()
        {
            var qi = new QueryInfo(testQuery + "\n" + testParam,false, false, new Mocks.MockEventAggregator());
            //var finalQry = DaxHelper.PreProcessQuery(testQuery + "\n" + testParam, new Mocks.MockEventAggregator());
            StringAssertion.ShouldEqualWithDiff(expectedQry.NormalizeNewline(), qi.QueryWithMergedParameters.NormalizeNewline(), DiffStyle.Full);
            
        }

        [TestMethod]
        public void TestAmbiguousParams()
        {
            string testAmbiguousParam = @"<Parameters xmlns:xsi=""http://www.w3.org/2001/XMLSchema-instance"" xmlns:xsd=""http://www.w3.org/2001/XMLSchema"" xmlns=""urn:schemas-microsoft-com:xml-analysis"">
        <Parameter>
          <Name>Test</Name>
          <Value xsi:type=""xsd:string"">Value1</Value>
        </Parameter>
        <Parameter>
          <Name>Test1</Name>
          <Value xsi:type=""xsd:string"">Value2</Value>
        </Parameter></Parameters>";
            var testQuery = "[value1]:@Test [value2]:@Test1 [value2]:(@test1) [value1]:@test, @test";
            //var dict = DaxHelper.ParseParams(testAmbiguousParam, new Mocks.MockEventAggregator());
            //var finalQuery = DaxHelper.replaceParamsInQuery(new StringBuilder(testQuery), dict);
            var qi = new QueryInfo(testQuery + "\n" + testAmbiguousParam, false, false, new Mocks.MockEventAggregator());

            Assert.AreEqual("[value1]:\"Value1\" [value2]:\"Value2\" [value2]:(\"Value2\") [value1]:\"Value1\", \"Value1\"", qi.QueryWithMergedParameters);
            //              "[value1]:Value1 [value2]:Value2 [value2]:(Value2) [value1]:Value1, Value1"
        }

        [TestMethod]
        public void TestParamsWithNoNamespace()
        {
            string testAmbiguousParam = @"<Parameters>
        <Parameter>
          <Name>Test</Name>
          <Value>Value1</Value>
        </Parameter>
        <Parameter>
          <Name>Test1</Name>
          <Value>Value2</Value>
        </Parameter></Parameters>";
            var testQuery = "[value1]:@Test [value2]:@Test1 [value2]:(@test1) [value1]:@test, @test";
            var qi = new QueryInfo(testQuery + "\n" + testAmbiguousParam,false, false, new Mocks.MockEventAggregator());

            //var dict = DaxHelper.ParseParams(testAmbiguousParam, new Mocks.MockEventAggregator());
            //var finalQuery = DaxHelper.replaceParamsInQuery(new StringBuilder(testQuery), dict);
            var finalQuery = qi.QueryWithMergedParameters;

            Assert.AreEqual("[value1]:\"Value1\" [value2]:\"Value2\" [value2]:(\"Value2\") [value1]:\"Value1\", \"Value1\"", finalQuery);
            Assert.AreEqual(false, qi.NeedsParameterValues);
        }

        [TestMethod]
        public void TestParamInTableConstructor()
        {
            string testQuery = @"evaluate {@tmp}
<Parameters xmlns:xsi=""http://www.w3.org/2001/XMLSchema-instance"" xmlns:xsd=""http://www.w3.org/2001/XMLSchema"" xmlns=""urn:schemas-microsoft-com:xml-analysis"">  <Parameter>
    <Name>tmp</Name>
    <Value xsi:type=""xsd:string"">1</Value>
    </Parameter>
  </Parameters>";
            var qi = new QueryInfo(testQuery , false, false, new Mocks.MockEventAggregator());

            //var dict = DaxHelper.ParseParams(testAmbiguousParam, new Mocks.MockEventAggregator());
            //var finalQuery = DaxHelper.replaceParamsInQuery(new StringBuilder(testQuery), dict);
            var finalQuery = qi.QueryWithMergedParameters;

            Assert.AreEqual(false, qi.NeedsParameterValues);
            Assert.AreEqual("evaluate {\"1\"}", finalQuery);
            
        }

        [TestMethod]
        public void TestStronglyTypedParams()
        {
            string testQuery = @"<Parameters xmlns:xsi=""http://www.w3.org/2001/XMLSchema-instance"" xmlns:xsd=""http://www.w3.org/2001/XMLSchema"" xmlns=""urn:schemas-microsoft-com:xml-analysis"">
  <Parameter>
    <Name>p1</Name>
    <Value xsi:type=""xsd:string"">abc</Value>
  </Parameter>
  <Parameter>
    <Name>p2</Name>
    <Value xsi:type=""xsd:int"">1</Value>
  </Parameter>
  <Parameter>
    <Name>p3</Name>
    <Value xsi:type=""xsd:double"">2.0</Value>
  </Parameter>
</Parameters>";
            //var qi = new QueryInfo(testQuery, false, false, new Mocks.MockEventAggregator());
            var dict = new Dictionary<string, QueryParameter>();
            DaxHelper.ParseParams(testQuery,dict, new Mocks.MockEventAggregator());
            //var finalQuery = DaxHelper.replaceParamsInQuery(new StringBuilder(testQuery), dict);
            //var finalQuery = qi.ProcessedQuery;

            Assert.AreEqual("abc", dict["p1"].Value);
            Assert.AreEqual(1, dict["p2"].Value);
            Assert.AreEqual(2.0d, dict["p3"].Value);
        }

        [TestMethod]
        public void TestParamIntParamInTableConstructor()
        {
            string testQuery = @"evaluate {@tmp}
<Parameters xmlns:xsi=""http://www.w3.org/2001/XMLSchema-instance"" xmlns:xsd=""http://www.w3.org/2001/XMLSchema"" xmlns=""urn:schemas-microsoft-com:xml-analysis"">  <Parameter>
    <Name>tmp</Name>
    <Value xsi:type=""xsd:int"">1</Value>
    </Parameter>
  </Parameters>";
            var qi = new QueryInfo(testQuery, false, false, new Mocks.MockEventAggregator());

            //var dict = DaxHelper.ParseParams(testAmbiguousParam, new Mocks.MockEventAggregator());
            //var finalQuery = DaxHelper.replaceParamsInQuery(new StringBuilder(testQuery), dict);
            var finalQuery = qi.QueryWithMergedParameters;

            Assert.AreEqual(false, qi.NeedsParameterValues);
            Assert.AreEqual("evaluate {1}", finalQuery);

        }

        [TestMethod]
        public void TestQueryWithIntYearParameter()
        {
            string testQuery = @"
EVALUATE
SUMMARIZECOLUMNS (
    ROLLUPADDISSUBTOTAL ( 'Date'[Calendar Year], ""Date_Calendar_Year_IsSubtotal\"" ),
    FILTER(
        ALL('Date'[Calendar Year]),
        (('Date'[Calendar Year] = @PARAMFBA4F4C0))
    ),
    ""Internet Total Sales"", [Internet Total Sales]
)
ORDER BY
    [Date_Calendar_Year_IsSubtotal] DESC,
    'Date'[Calendar Year]

<Parameters xmlns:xsi=""http://www.w3.org/2001/XMLSchema-instance"" xmlns:xsd=""http://www.w3.org/2001/XMLSchema"" xmlns=""urn:schemas-microsoft-com:xml-analysis"" >     
    <Parameter>
        <Name>PARAMFBA4F4C0</Name>
        <Value xsi:type=""xsd:int"">2003</Value>
    </Parameter>
</Parameters>
";

            string expectedQuery = @"
EVALUATE
SUMMARIZECOLUMNS (
    ROLLUPADDISSUBTOTAL ( 'Date'[Calendar Year], ""Date_Calendar_Year_IsSubtotal\"" ),
    FILTER(
        ALL('Date'[Calendar Year]),
        (('Date'[Calendar Year] = 2003))
    ),
    ""Internet Total Sales"", [Internet Total Sales]
)
ORDER BY
    [Date_Calendar_Year_IsSubtotal] DESC,
    'Date'[Calendar Year]";
            var qi = new QueryInfo(testQuery, false, false, new Mocks.MockEventAggregator());

            //var dict = DaxHelper.ParseParams(testAmbiguousParam, new Mocks.MockEventAggregator());
            //var finalQuery = DaxHelper.replaceParamsInQuery(new StringBuilder(testQuery), dict);
            var finalQuery = qi.QueryWithMergedParameters;

            Assert.AreEqual(false, qi.NeedsParameterValues);
            Assert.AreEqual(expectedQuery, finalQuery);

        }
    }
}

