using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using DaxStudio.UI.Utils;

namespace DaxStudio.Tests
{
    [TestClass]
    public class DaxLineParserTests
    {
        [TestMethod]
        public void TestParseLineWithOpenColumn()
        {
            Assert.AreEqual(LineState.Column, DaxLineParser.ParseLine("table[column",12,0).LineState);
        }

        [TestMethod]
        public void TestParseLineWithOpenColumnAndLeadingSpace()
        {
            var daxLine = DaxLineParser.ParseLine(" table[column", 12, 0);

            Assert.AreEqual(LineState.Column, daxLine.LineState);
            Assert.AreEqual("table", daxLine.TableName);
        }

        [TestMethod]
        public void TestParseLineWithOpenColumnAndLeadingTab()
        {
            var daxLine = DaxLineParser.ParseLine("\ttable[column", 12, 0);
            
            Assert.AreEqual(LineState.Column,daxLine.LineState);
            Assert.AreEqual("table", daxLine.TableName);
        }

        [TestMethod]
        public void TestParseLineWithOpenColumnAndPreceedingString()
        {
            var testText = "\"table[column\" 'table";
            Assert.AreEqual(LineState.Table, DaxLineParser.ParseLine(testText, testText.Length-1,0).LineState,"Table state not detected");
            Assert.AreEqual(LineState.String, DaxLineParser.ParseLine(testText, 10,0).LineState,"string state not detected");
        }

        [TestMethod]
        public void TestParseLineWithOpenTable()
        {
            Assert.AreEqual(LineState.Table, DaxLineParser.ParseLine("'table",6,0).LineState);
        }

        [TestMethod]
        public void TestFindTableNameSimple()
        {
            Assert.AreEqual("table", DaxLineParser.GetPreceedingTableName("filter( table"));
            Assert.AreEqual("table2", DaxLineParser.GetPreceedingTableName("evaluate filter( table2"));
        }

        [TestMethod]
        public void TestFindTableNameFunctionNoSpace()
        {
            Assert.AreEqual("table", DaxLineParser.GetPreceedingTableName("filter(table"));
        }

        [TestMethod]
        public void TestFindTableNameFunctionWithUnderscores()
        {
            Assert.AreEqual("Dim_D", DaxLineParser.GetPreceedingTableName("filter(Dim_D"));
        }

        [TestMethod]
        public void TestFindTableNameFunctionNoSpaceAndOperator()
        {
            Assert.AreEqual("table2", DaxLineParser.GetPreceedingTableName("filter(table, table1[col1]=table2"));
        }

        [TestMethod]
        public void TestFindTableNameQuotedFunctionNoSpaceAndOperator()
        {
            Assert.AreEqual("table2", DaxLineParser.GetPreceedingTableName("filter(table, table1[col1]='table2"));
        }

        [TestMethod]
        public void TestFindTableNameFunctionNoSpaceAndEvaluate()
        {
            Assert.AreEqual("table", DaxLineParser.GetPreceedingTableName("evaluate filter(table"));
        }
        
        [TestMethod]
        public void GetCompletionSegmentTest()
        {
            var daxState = DaxLineParser.ParseLine("table[column]",10,0);
            Assert.AreEqual(LineState.ColumnClosed, daxState.LineState );
            Assert.AreEqual("column", daxState.ColumnName);
            Assert.AreEqual(5, daxState.StartOffset, "StartOffset");
            Assert.AreEqual(13, daxState.EndOffset, "EndOffset");
        }

        [TestMethod]
        public void GetCompletionSegmentTestWithLeadingAndTrailingText()
        {
            //                   0123456789012345678901234567890123456789
            //         01234567891111111111222222222233333333334444444444
            var dax = "filter( table[column] , table[column]=\"red\"";
            //                       ^ 15                       ^ 41
            var daxState = DaxLineParser.ParseLine(dax, 15,0); 
            Assert.AreEqual(LineState.ColumnClosed, daxState.LineState);
            Assert.AreEqual(13, daxState.StartOffset, "StartOffset");
            Assert.AreEqual(21, daxState.EndOffset, "EndOffset");
            Assert.AreEqual("table", daxState.TableName);
            
            var daxState2 = DaxLineParser.ParseLine(dax, 41,0);
            Assert.AreEqual(LineState.String, daxState2.LineState);
            Assert.AreEqual(38, daxState2.StartOffset, "StartOffset String");
            Assert.AreEqual(42, daxState2.EndOffset, "EndOffset String");
            
            var daxState3 = DaxLineParser.ParseLine(dax, 3,0);
            Assert.AreEqual(LineState.LetterOrDigit, daxState3.LineState);
            Assert.AreEqual(0, daxState3.StartOffset, "StartOffset Filter");
            Assert.AreEqual(6, daxState3.EndOffset, "EndOffset Filter");

        }

        [TestMethod]
        public void GetCompletionSegmentTestWithQuotedTableName()
        {
            var dax = "evaluate filter('my table', 'my table'[column name";
            //                                                ^ 39
            var daxState = DaxLineParser.ParseLine(dax, dax.Length-1,0);
            Assert.AreEqual(LineState.Column, daxState.LineState);
            Assert.AreEqual(38, daxState.StartOffset, "StartOffset");
            Assert.AreEqual(dax.Length-1, daxState.EndOffset, "EndOffset");
            Assert.AreEqual("my table", daxState.TableName);
        }

        [TestMethod]
        public void GetCompletionSegmentTestWithUnderscoreTableName()
        {
            var dax = "filter(Dim_D";
            //                                         ^ 32
            var daxState = DaxLineParser.ParseLine(dax, dax.Length - 1, 0);
            Assert.AreEqual(LineState.LetterOrDigit, daxState.LineState);
            Assert.AreEqual(dax.Length , daxState.EndOffset, "EndOffset");
            Assert.AreEqual(dax.Length - "Dim_D".Length, daxState.StartOffset, "StartOffset");
            
            //Assert.AreEqual("my table", daxState.TableName);
        }

        [TestMethod]
        public void GetCompletionSegmentMidColumn()
        {
            //                   01234567890123 
            //         012345678911111111112222
            var dax = "'my table'[column name], blah";
            //                         ^16
            var daxState = DaxLineParser.ParseLine(dax, 11, 0);
            Assert.AreEqual(LineState.ColumnClosed, daxState.LineState);
            Assert.AreEqual(10, daxState.StartOffset, "StartOffset");
            Assert.AreEqual(23, daxState.EndOffset, "EndOffset");
            Assert.AreEqual("my table", daxState.TableName);
        }


        [TestMethod]
        public void GetCompletionSegmentTestWithTabBeforeTableName()
        {
            var dax = "\t'table";
            //               ^ 5
            var daxState = DaxLineParser.ParseLine(dax, dax.Length - 1, 0);
            Assert.AreEqual(LineState.Table, daxState.LineState);
            Assert.AreEqual(1, daxState.StartOffset, "StartOffset");
            Assert.AreEqual(dax.Length - 1, daxState.EndOffset, "EndOffset");
            Assert.AreEqual("table", daxState.TableName);
        }
        [TestMethod]
        public void MultiLineParsing()
        {
            var daxState = DaxLineParser.ParseLine(" e", 2,6);
            Assert.AreEqual(LineState.LetterOrDigit, daxState.LineState);
            Assert.AreEqual(7, daxState.StartOffset, "StartOffset");
            Assert.AreEqual(8, daxState.EndOffset, "EndOffset");
        }

        [TestMethod]
        public void DmvParsing()
        {
            var dax = "SELECT * FROM $SYSTEM.dis";
            var daxState = DaxLineParser.ParseLine(dax, 24, 0);
            Assert.AreEqual(LineState.Dmv, daxState.LineState);
            Assert.AreEqual(22, daxState.StartOffset, "StartOffset");
        }

        [TestMethod]
        public void SimpleDmvParsing()
        {
            var dax = "$SYSTEM.dis";
            var daxState = DaxLineParser.ParseLine(dax, 11, 0);
            Assert.AreEqual(LineState.Dmv, daxState.LineState);
            Assert.AreEqual(8, daxState.StartOffset, "StartOffset");
        }


        [TestMethod]
        public void TestFindTableNameEuropeanListSeparator()
        {
            Assert.AreEqual("table1", DaxLineParser.GetPreceedingTableName("filter(table; table1["));
        }

        [TestMethod]
        public void TestFindTableNameUsListSeparator()
        {
            Assert.AreEqual("table1", DaxLineParser.GetPreceedingTableName("filter(table, table1["));
        }

        [TestMethod]
        public void TestParseOpenColumnWithTrailingSpace()
        {
            var testText = "EVALUE FILTER('Products', 'product'[Product ";
            var daxState = DaxLineParser.ParseLine(testText, testText.Length - 1, 0);
            Assert.AreEqual(LineState.Column, daxState.LineState, "Column state not detected");
            Assert.AreEqual("Product ", daxState.ColumnName, "preceeding word not correct");

            Assert.AreEqual(LineState.TableClosed, DaxLineParser.ParseLine(testText, 30, 0).LineState, "string state not detected");
        }



        [TestMethod]
        public void TestFindFunctionNameSimple()
        {
            Assert.AreEqual("filter", DaxLineParser.GetPreceedingWord("evaluate filter"));
            
        }

        [TestMethod]
        public void TestFindFunctionNameWithSpace()
        {
            Assert.AreEqual("filter", DaxLineParser.GetPreceedingWord("evaluate filter "));

        }

        [TestMethod]
        public void TestParseOpenTable()
        {
            //                        0
            //              01234567891  
            var testText = "FILTER('Pr";
            var daxState = DaxLineParser.ParseLine(testText, testText.Length, 0);
            Assert.AreEqual(LineState.Table, daxState.LineState, "Table state not detected");
            //Assert.AreEqual(7, daxState.StartOffset);
            Assert.AreEqual(10, daxState.EndOffset);


        }

        [TestMethod]
        public void TestMidStatementParsing()
        {
            var qry = "EVALUATE FILTER(Reseller, Reseller[Reselle]= \"bob\")";
            var daxState = DaxLineParser.ParseLine(qry, 42, 0);
            Assert.AreEqual(LineState.ColumnClosed, daxState.LineState,"LineState");
            Assert.AreEqual(43, daxState.EndOffset,"EndOffset");
            Assert.AreEqual(34, daxState.StartOffset, "StartOffset");
        }

        [TestMethod]
        public void TestParsingToFindFunction()
        {
            var qry = "EVALUATE FILTER(Reseller, Reseller[Reselle]= \"bob\")";
            //                    ^
            var daxState = DaxLineParser.ParseLine(qry, 11, 0);
            Assert.AreEqual(LineState.LetterOrDigit, daxState.LineState, "LineState");
            Assert.AreEqual(15, daxState.EndOffset, "EndOffset");
            Assert.AreEqual(9, daxState.StartOffset, "StartOffset");
            Assert.AreEqual("FILTER", qry.Substring(daxState.StartOffset, daxState.EndOffset - daxState.StartOffset));
        
        }

        [TestMethod]
        public void TestParsingToFindFunctionWithPeriod()
        {
            var qry = "EVALUATE EXPON.DIST(123";
            //                    ^
            var daxState = DaxLineParser.ParseLine(qry, 11, 0);
            Assert.AreEqual(LineState.LetterOrDigit, daxState.LineState, "LineState");
            Assert.AreEqual(19, daxState.EndOffset, "EndOffset");
            Assert.AreEqual(9, daxState.StartOffset, "StartOffset");
            Assert.AreEqual("EXPON.DIST", qry.Substring(daxState.StartOffset, daxState.EndOffset - daxState.StartOffset));
        }

        [TestMethod]
        public void TestParsingToFindFunctionWithTrailingSpace()
        {
            var qry = "EVALUATE ";
            //                    ^
            var daxState = DaxLineParser.ParseLine(qry, 1, 0);
            Assert.AreEqual(LineState.LetterOrDigit, daxState.LineState, "LineState");
            Assert.AreEqual(0, daxState.StartOffset, "StartOffset");
            Assert.AreEqual(8, daxState.EndOffset, "EndOffset");
            
            Assert.AreEqual("EVALUATE", qry.Substring(daxState.StartOffset, daxState.EndOffset - daxState.StartOffset));
        }

        [TestMethod]
        public void TestParsingToFindFunctionWithoutTrailingSpace()
        {
            var qry = "EVALUATE";
            //                    ^
            var daxState = DaxLineParser.ParseLine(qry, 1, 0);
            Assert.AreEqual(LineState.LetterOrDigit, daxState.LineState, "LineState");
            Assert.AreEqual(0, daxState.StartOffset, "StartOffset");
            Assert.AreEqual(8, daxState.EndOffset, "EndOffset");

            Assert.AreEqual("EVALUATE", qry.Substring(daxState.StartOffset, daxState.EndOffset - daxState.StartOffset));
        }
    }
}
