using DaxStudio.Tests.Assertions;
using DaxStudio.UI.Utils;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DaxStudio.Tests
{
    [TestClass]
    public class LongLineStateMachineTests
    {
        [TestMethod]
        public void TestBasicLineBreaking()
        {
            var sm = new LongLineStateMachine(25);
            var input = "var _var2 = {(1,123),(2,456),(3,789),(4,123),(5,456),(6,789)}";
            var actual = sm.ProcessString(input);
            var expected = "var _var2 = {(1,123),(2,456)\n" +
                           ",(3,789),(4,123),(5,456),(6,789)\n" +
                           "}"; 
            StringAssertion.ShouldEqualWithDiff(expected, actual,DiffStyle.Compact);
        }

        [TestMethod]
        public void TestDontBreakInStrings()
        {
            var sm = new LongLineStateMachine(25);
            var input = "var _var2 = {(1,123),(2,456),(3,789),(4,123),(5,456),\"(6,789) this is a long string with the following chars )} which would normally trigger a line break\" }";
            var actual = sm.ProcessString(input);
            var expected = "var _var2 = {(1,123),(2,456)\n" +
                           ",(3,789),(4,123),(5,456),\"(6,789) this is a long string with the following chars )} which would normally trigger a line break\" \n" +
                           "}"; 
            StringAssertion.ShouldEqualWithDiff(expected, actual, DiffStyle.Compact);
        }

        [TestMethod]
        public void TestDontBreakInBlockComments()
        {
            var sm = new LongLineStateMachine(25);
            var input = "var _var2 = {(1,123),(2,456),(3,789),(4,123),(5,456) /*,(6,789) this is a long comment with the following chars )} which would normally trigger a line break */ }";
            var actual = sm.ProcessString(input);
            var expected = "var _var2 = {(1,123),(2,456)\n" +
                           ",(3,789),(4,123),(5,456) /*,(6,789) this is a long comment with the following chars )} which would normally trigger a line break */ \n" +
                           "}"; 
            StringAssertion.ShouldEqualWithDiff(expected, actual, DiffStyle.Compact);
        }

        [TestMethod]
        public void TestDontBreakInDashComments()
        {
            var sm = new LongLineStateMachine(25);
            var input = "var _var2 = {(1,123),(2,456),(3,789),(4,123),(5,456) --,(6,789) this is a long comment with the following chars )} which would normally trigger a line break \n }";
            var actual = sm.ProcessString(input);
            var expected = "var _var2 = {(1,123),(2,456)\n" +
                           ",(3,789),(4,123),(5,456) --,(6,789) this is a long comment with the following chars )} which would normally trigger a line break \n" +
                           " }"; 
            StringAssertion.ShouldEqualWithDiff(expected, actual, DiffStyle.Compact);
        }

        [TestMethod]
        public void TestDontBreakInSlashComments()
        {
            var sm = new LongLineStateMachine(25);
            var input = "var _var2 = {(1,123),(2,456),(3,789),(4,123),(5,456) //,(6,789) this is a long comment with the following chars )} which would normally trigger a line break \n }";
            var actual = sm.ProcessString(input);
            var expected = "var _var2 = {(1,123),(2,456)\n" +
                           ",(3,789),(4,123),(5,456) //,(6,789) this is a long comment with the following chars )} which would normally trigger a line break \n" +
                           " }"; 
            StringAssertion.ShouldEqualWithDiff(expected, actual, DiffStyle.Compact);
        }


        [TestMethod]
        public void FindSqlQueryComment()
        {
            var sm = new LongLineStateMachine(25);
            var input = "EVALUATE {123}\n" +
                        "// Direct Query\n" +
                        "SELECT * FROM Table";
            sm.ProcessString(input);
            Assert.AreEqual(15, sm.SqlQueryCommentPosition);
        }

        [TestMethod]
        public void StripSqlQuery()
        {
            var sm = new LongLineStateMachine(25);
            var input = "EVALUATE {123}\n" +
                        "// Direct Query\n" +
                        "SELECT * FROM Table";
            var actual = sm.ProcessString(input).Substring(0,sm.SqlQueryCommentPosition);
            var expected = "EVALUATE {123}\n";
            Assert.AreEqual(expected, actual);
        }
    }
}
