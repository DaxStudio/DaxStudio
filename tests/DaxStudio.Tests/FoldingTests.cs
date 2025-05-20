using DaxStudio.Tests.Mocks;
using DaxStudio.UI.Model;
using ICSharpCode.AvalonEdit.Folding;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DaxStudio.Tests
{
    [TestClass]
    public class FoldingTests
    {

        [TestMethod]
        public void BasicFoldingTest() {
            var qry =     "11111111" + Environment.NewLine
                        + " 2222222" + Environment.NewLine
                        + "  333333" + Environment.NewLine
                        + " 4444444" + Environment.NewLine
                        + "55555555" + Environment.NewLine;

            var doc = new ICSharpCode.AvalonEdit.Document.TextDocument(qry);  
            var foldingStrategy = new IndentFoldingStrategy();
            var foldings = foldingStrategy.CreateNewFoldings(doc);

            Assert.AreEqual(2, foldings.Count());

        }

        [TestMethod]
        public void DoubleUnindentFoldingTest()
        {
            var qry = "11111111" + Environment.NewLine
                    + " 2222222" + Environment.NewLine
                    + "  333333" + Environment.NewLine
                    + "  444444" + Environment.NewLine
                    + "55555555" + Environment.NewLine;

            var doc = new ICSharpCode.AvalonEdit.Document.TextDocument(qry);
            var foldingStrategy = new IndentFoldingStrategy();
            var foldings = foldingStrategy.CreateNewFoldings(doc);

            Assert.AreEqual(2, foldings.Count());

        }

        [TestMethod]
        public void TripleFoldingTest()
        {
            var qry = "11111111" + Environment.NewLine
                    + " 2222222" + Environment.NewLine
                    + "  333333" + Environment.NewLine
                    + "  444444" + Environment.NewLine
                    + " 5555555" + Environment.NewLine
                    + "  666666" + Environment.NewLine
                    + "77777777" + Environment.NewLine;

            var doc = new ICSharpCode.AvalonEdit.Document.TextDocument(qry);
            var foldingStrategy = new IndentFoldingStrategy();
            var foldings = foldingStrategy.CreateNewFoldings(doc);

            Assert.AreEqual(3, foldings.Count());
            var foldingArray = foldings.ToArray();
            Assert.AreEqual(8, foldingArray[0].StartOffset, "Start of first fold");
            Assert.AreEqual(58, foldingArray[0].EndOffset, "End of first fold");
            Assert.AreEqual(18, foldingArray[1].StartOffset, "Start of second fold");
            Assert.AreEqual(38, foldingArray[1].EndOffset, "End of second fold");
            Assert.AreEqual(48, foldingArray[2].StartOffset, "Start of third fold");
            Assert.AreEqual(58, foldingArray[2].EndOffset, "End of third fold");
        }

        [TestMethod]
        public void TestCrashingMeasure()
        {
            var qry = @"    MEASURE '1-Calculate WAC'[FinalWac] =
        CALCULATE (
            SUMX (
                ADDCOLUMNS (
                    SUMMARIZE ( 'Qty By Period 1', 'Qty By Period 1'[InventoryNoFK] ),
                    // ""Total"", CALCULATE ( IF ( [Component1Wac] = 0, [ItemWac], [Component1Wac] ) )
                    ""Total"", VAR _Component1Wac = [Component1Wac] RETURN IF ( _Component1Wac = 0, [ItemWac], _Component1Wac )
                ),
                IF ( [LastQty] <> 0, [Total], 0 )
            )
        )
";
            var doc = new ICSharpCode.AvalonEdit.Document.TextDocument(qry);
            var foldingStrategy = new IndentFoldingStrategy();
            var foldings = foldingStrategy.CreateNewFoldings(doc);

            Assert.AreEqual(5, foldings.Count());
            var prevStart = 0;
            var foldCnt = 0; ;
            foreach ( var folding in foldings)
            {

                if (folding.StartOffset >= prevStart)
                {
                    prevStart = folding.StartOffset;
                    foldCnt++;
                    continue;
                }
                Assert.Fail($"Folding {foldCnt} has a start of {prevStart} which is less than the current fold start of {folding.StartOffset}");
            }

            var foldingArray = foldings.ToArray();

        }

        [TestMethod]
        public void TestMeasureWithBlankLines()
        {
            var qry = @"    MEASURE '1-Calculate WAC'[FinalWac] =

        CALCULATE (

            SUMX (

                ADDCOLUMNS (

                    SUMMARIZE ( 'Qty By Period 1', 'Qty By Period 1'[InventoryNoFK] ),

                    // ""Total"", CALCULATE ( IF ( [Component1Wac] = 0, [ItemWac], [Component1Wac] ) )

                    ""Total"", VAR _Component1Wac = [Component1Wac] RETURN IF ( _Component1Wac = 0, [ItemWac], _Component1Wac )

                ),

                IF ( [LastQty] <> 0, [Total], 0 )

            )

        )
";
            var doc = new ICSharpCode.AvalonEdit.Document.TextDocument(qry);
            var foldingStrategy = new IndentFoldingStrategy();
            var foldings = foldingStrategy.CreateNewFoldings(doc);

            Assert.AreEqual(5, foldings.Count());
            var prevStart = 0;
            var foldCnt = 0; ;
            foreach (var folding in foldings)
            {

                if (folding.StartOffset >= prevStart)
                {
                    prevStart = folding.StartOffset;
                    foldCnt++;
                    continue;
                }
                Assert.Fail($"Folding {foldCnt} has a start of {prevStart} which is less than the current fold start of {folding.StartOffset}");
            }

            var foldingArray = foldings.ToArray();

        }

        [TestMethod]
        public void MeasureFoldingTest()
        {

            var tabsAndSpaces = 
                "MEASURE 'BDW_VM VW_SS_ISP_STTUS_COMN_HST_01'[GODWFLAG3_1] = " + Environment.NewLine +
                "    IF(COUNTROWS(" + Environment.NewLine +
                "        FILTER(" + Environment.NewLine +
                "            'BDW_VM VW_SS_ISP_STTUS_COMN_HST_01'," + Environment.NewLine +
                "\t\t\t(" + Environment.NewLine +
                "\t\t\t\tRELATED( 'BDW_DS_DB SD_CRCLT_CPNT_BAS'[MPHON_CH_TYPE_ITG_CD]) = \"10003\"" + Environment.NewLine +
                "\t\t\t\t|| RELATED('BDW_DS_DB SD_CRCLT_CPNT_BAS'[MPHON_SALE_PATH_ITG_CD]) = \"10035\"" + Environment.NewLine +
                "\t\t\t)" + Environment.NewLine +
                "\t\t\t&& 'BDW_VM VW_SS_ISP_STTUS_COMN_HST_01'[SALE_CPNT_SBT_ID] <> 1000274156" + Environment.NewLine +
                "\t\t)" + Environment.NewLine +
                "    ) > 0,1,0)";
            var tabsOnly= 
                "MEASURE 'BDW_VM VW_SS_ISP_STTUS_COMN_HST_01'[GODWFLAG3_1] = " + Environment.NewLine +
                "\tIF(COUNTROWS(" + Environment.NewLine +
                "\t\tFILTER(" + Environment.NewLine +
                "\t\t\t'BDW_VM VW_SS_ISP_STTUS_COMN_HST_01'," + Environment.NewLine +
                "\t\t\t(" + Environment.NewLine +
                "\t\t\t\tRELATED( 'BDW_DS_DB SD_CRCLT_CPNT_BAS'[MPHON_CH_TYPE_ITG_CD]) = \"10003\"" + Environment.NewLine +
                "\t\t\t\t|| RELATED('BDW_DS_DB SD_CRCLT_CPNT_BAS'[MPHON_SALE_PATH_ITG_CD]) = \"10035\"" + Environment.NewLine +
                "\t\t\t)" + Environment.NewLine +
                "\t\t\t&& 'BDW_VM VW_SS_ISP_STTUS_COMN_HST_01'[SALE_CPNT_SBT_ID] <> 1000274156" + Environment.NewLine +
                "\t\t)" + Environment.NewLine +
                "\t) > 0,1,0)";
            
            var docTabsOnly = new ICSharpCode.AvalonEdit.Document.TextDocument(tabsOnly);
            var docTabsAndSpaces = new ICSharpCode.AvalonEdit.Document.TextDocument(tabsAndSpaces);
            var foldingStrategy = new IndentFoldingStrategy();
            var tabsOnlyFoldings = foldingStrategy.CreateNewFoldings(docTabsOnly);
            var tabsAndSpacesFoldings = foldingStrategy.CreateNewFoldings(docTabsAndSpaces);

            Assert.AreEqual(tabsAndSpacesFoldings.Count(), tabsOnlyFoldings.Count(), "tab and space indenting different");

            foldingStrategy.TabIndent = 1;
            tabsOnlyFoldings = foldingStrategy.CreateNewFoldings(docTabsOnly);
            tabsAndSpacesFoldings = foldingStrategy.CreateNewFoldings(docTabsAndSpaces);

            Assert.AreNotEqual(tabsAndSpacesFoldings.Count(), tabsOnlyFoldings.Count(), "tab and space indenting should not be the same when tabs are only 1 wide");
        }
    }
}
