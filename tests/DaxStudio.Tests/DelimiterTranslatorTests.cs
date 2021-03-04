using DaxStudio.Interfaces.Enums;
using DaxStudio.UI.Utils.DelimiterTranslator;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DaxStudio.Tests
{
    [TestClass]
    public class DelimiterTranslatorTests
    {
        /*
        [TestMethod]
        public void BasicTranslation1Test()
        {
            string input = "Evaluate Filter(Values('Product'[Categories]), Product[Categories] = \"Bikes, Helmets\")";
            string actual = DelimiterTranslator.Translate(input);
            string expected = "Evaluate Filter(Values('Product'[Categories]); Product[Categories] = \"Bikes, Helmets\")";
            Assert.AreEqual(expected, actual);
        }

        [TestMethod]
        public void BasicTranslation2Test()
        {
            string input = "Evaluate Filter(Values('Product'[Categories]); Product[Prod ,;. Categories] = \"Bikes,;. Helmets\")";
            string actual = DelimiterTranslator.Translate(input);
            string expected = "Evaluate Filter(Values('Product'[Categories]), Product[Prod ,;. Categories] = \"Bikes,;. Helmets\")";
            Assert.AreEqual(expected, actual);
        }

        [TestMethod]
        public void BasicTranslation3Test()
        {
            string input = "Evaluate Filter(Values('Product'[Categories]); Product[Prod ,;. Rank] = 1,0)";
            string actual = DelimiterTranslator.Translate(input);
            string expected = "Evaluate Filter(Values('Product'[Categories]), Product[Prod ,;. Rank] = 1.0)";
            Assert.AreEqual(expected, actual);
        }
        */


        [TestMethod]
        public void BasicTranslation1Test_2()
        {
            string input = "Evaluate Filter(Values('Product'[Categories]), Product[Categories] = \"Bikes, Helmets\")";
            var dsm = new DelimiterStateMachine() ;
            string actual = dsm.ProcessString(input);
            string expected = "Evaluate Filter(Values('Product'[Categories]); Product[Categories] = \"Bikes, Helmets\")";
            Assert.AreEqual(expected, actual);
        }

        [TestMethod]
        public void BasicTranslation2Test_2()
        {
            string input = "Evaluate Filter(Values('Product'[Categories]); Product[Prod ,;. Categories] = \"Bikes,;. Helmets\")";
            var dsm = new DelimiterStateMachine();
            string actual = dsm.ProcessString(input);
            string expected = "Evaluate Filter(Values('Product'[Categories]), Product[Prod ,;. Categories] = \"Bikes,;. Helmets\")";
            Assert.AreEqual(expected, actual);
        }

        [TestMethod]
        public void BasicTranslation3Test_2()
        {
            string input = "Evaluate Filter(Values('Product'[Categories]); Product[Prod ,;. Rank] = 1,0)";
            var dsm = new DelimiterStateMachine();
            string actual = dsm.ProcessString(input);
            string expected = "Evaluate Filter(Values('Product'[Categories]), Product[Prod ,;. Rank] = 1.0)";
            Assert.AreEqual(expected, actual);
        }


        [TestMethod]
        public void BasicTranslation3Test_3()
        {
            string input = "Evaluate Filter(Values('Product'[Categories]); Product[Prod ,;. Rank] = 1,0)";
            var dsm = new DelimiterStateMachine(DelimiterType.Comma);
            string actual = dsm.ProcessString(input);
            string expected = "Evaluate Filter(Values('Product'[Categories]), Product[Prod ,;. Rank] = 1.0)";
            Assert.AreEqual(expected, actual);
        }

        [TestMethod]
        public void DashCommentTest()
        {
            string input = @"
-- this is a comment with ; semi colon and comma , .
Evaluate Filter(Values('Product'[Categories]); Product[Prod ,;. Rank] = 1,0)";
            var dsm = new DelimiterStateMachine(DelimiterType.Comma);
            string actual = dsm.ProcessString(input);
            string expected = @"
-- this is a comment with ; semi colon and comma , .
Evaluate Filter(Values('Product'[Categories]), Product[Prod ,;. Rank] = 1.0)";
            Assert.AreEqual(expected, actual);
        }

        [TestMethod]
        public void SlashCommentTest()
        {
            string input = @"
// this is a comment with ; semi colon and comma , .
Evaluate Filter(Values('Product'[Categories]); Product[Prod ,;. Rank] = 1,0)";
            var dsm = new DelimiterStateMachine(DelimiterType.Comma);
            string actual = dsm.ProcessString(input);
            string expected = @"
// this is a comment with ; semi colon and comma , .
Evaluate Filter(Values('Product'[Categories]), Product[Prod ,;. Rank] = 1.0)";
            Assert.AreEqual(expected, actual);
        }

        [TestMethod]
        public void BlockCommentTest()
        {
            string input = @"
/* this is a comment with ; semi colon and comma , . */
Evaluate Filter(Values('Product'[Categories]); Product[Prod ,;. Rank] = 1,0)";
            var dsm = new DelimiterStateMachine(DelimiterType.Comma);
            string actual = dsm.ProcessString(input);
            string expected = @"
/* this is a comment with ; semi colon and comma , . */
Evaluate Filter(Values('Product'[Categories]), Product[Prod ,;. Rank] = 1.0)";
            Assert.AreEqual(expected, actual);
        }

        [TestMethod]
        public void QueryBuilderTest()
        {
            var input = @"/* START QUERY BUILDER */
DEFINE
MEASURE Customer[MyMeasure] = SUM( Sales[Sales Amount])
EVALUATE
SUMMARIZECOLUMNS(
    Customer[City],
    ""MyMeasure"", [MyMeasure]
)
/* END QUERY BUILDER */
";
            var dsm = new DelimiterStateMachine(DelimiterType.Unknown);
            var actual = dsm.ProcessString(input);
            var expected = @"/* START QUERY BUILDER */
DEFINE
MEASURE Customer[MyMeasure] = SUM( Sales[Sales Amount])
EVALUATE
SUMMARIZECOLUMNS(
    Customer[City];
    ""MyMeasure""; [MyMeasure]
)
/* END QUERY BUILDER */
";
            StringAssert.Equals(expected, actual);
        }


        [TestMethod]
        public void FunctionWithPeriodsTest()
        {
            string input = "PERCENTILE.EXC( 1.0 )";
            var dsm = new DelimiterStateMachine(DelimiterType.Unknown);
            string actual = dsm.ProcessString(input);
            string expected = "PERCENTILE.EXC( 1,0 )";

            Assert.AreEqual(expected, actual);
            // convert back
            dsm = new DelimiterStateMachine(DelimiterType.Unknown);
            actual = dsm.ProcessString(actual);
            Assert.AreEqual(input, actual, "Toggle the delimiters back the original state");
        }

        [TestMethod]
        public void FunctionsWithPeriodsTest()
        {
            string input = @"
Evaluate ROW(""Test"", PERCENTILE.EXC( 1.0 ) )";
            var dsm = new DelimiterStateMachine(DelimiterType.Unknown);
            string actual = dsm.ProcessString(input);
            string expected = @"
Evaluate ROW(""Test""; PERCENTILE.EXC( 1,0 ) )";

            Assert.AreEqual(expected, actual);
            // convert back
            dsm = new DelimiterStateMachine(DelimiterType.Unknown);
            actual = dsm.ProcessString(actual);
            Assert.AreEqual(input, actual,"Toggle the delimiters back the original state");
        }

        [TestMethod]
        public void SwitchRefreshSessionQuery()
        {
            string input = Common.Constants.RefreshSessionQuery;

            var dsm = new DelimiterStateMachine(DelimiterType.SemiColon);
            string actual = dsm.ProcessString(input);
            string expected = "EVALUATE /* <<DAX Studio Internal>> */ ROW(\"DAX Studio Session Refresh\";0)";

            Assert.AreEqual(expected, actual);

        }


    }
}
