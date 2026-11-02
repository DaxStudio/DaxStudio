using DaxStudio.Parsers.Dax;
using Microsoft.VisualStudio.TestTools.UnitTesting;

using DaxStudio.Parsers.Grammars.Generated;
namespace DaxStudio.Parsers.Tests
{
    [TestClass]
    public class ToggleCommentTests
    {
        [TestMethod]
        public void ToggleCommentTest()
        {
            var input = @"
-- test
something
// more tests
";

            var output = DaxProcessor.ToggleComments2(input);
            var expected = @"
-- -- test
-- something
-- // more tests
";
            Assert.AreEqual(expected, output, "all lines should be commented");

            var output2 = DaxProcessor.ToggleComments2(output);
            
            Assert.AreEqual(input, output2,"First level of comments should be removed");

        }

        [TestMethod]
        public void ToggleCommentWithBlockCommentTest()
        {
            var input = @"
-- test
/* 
and another thing
on multiple
lines
*/
something
// more tests
";

            var output = DaxProcessor.ToggleComments2(input);
            var expected = @"
-- -- test
/* 
and another thing
on multiple
lines
*/
-- something
-- // more tests
";
            Assert.AreEqual(expected, output, "all lines should be commented");

            var output2 = DaxProcessor.ToggleComments2(output);

            Assert.AreEqual(input, output2, "First level of comments should be removed");

        }

        [TestMethod]
        public void CallStackTest()
        {
            var input = @" -- TOPN( test,
EVALUATE
    ADDCOLUMNS(
        Filter( table, table,column[mea
";

            var output = DaxProcessor.BuildCallStack(input);
            Assert.HasCount(2, output);
            Assert.AreEqual("Filter", output.Peek(), "Top of callstack");
        }

        [TestMethod]
        public void CallStackTest2()
        {
            var input = @" -- TOPN( test,
EVALUATE
    ADDCOLUMNS(
        Filter( table, table,column[mea
";

            var output = DaxProcessor.BuildCallStack2(input);
            Assert.AreEqual("Filter", output.CurrentFunction,  "Top of callstack");
        }

        [TestMethod]
        public void BigCallStackTest()
        {
            var input = DaxDateTable.DateTable.Substring(1, DaxDateTable.DateTable.Length - 25);

            var output = DaxProcessor.BuildCallStack(input);
            Assert.HasCount(1, output);
            Assert.AreEqual("SELECTCOLUMNS", output.Peek(), "Top of callstack");
        }

        [TestMethod]
        public void BigCallStackTest2()
        {
            var input = DaxDateTable.DateTable.Substring(1, DaxDateTable.DateTable.Length -28);

            var output = DaxProcessor.BuildCallStack2(input);

            Assert.AreEqual("SELECTCOLUMNS",output.CurrentFunction, "Top of callstack");
            Assert.AreEqual(234, output.ArgumentIndex, "Argument Index");
            Assert.AreEqual(EditState.PartialMeasure, output.State, "Edit state");
            Assert.HasCount(62, output.Variables, "Variable Count");
        }

        [TestMethod]
        public void VariableStackTest()
        {
            var input = @" -- TOPN( test,
EVALUATE
    VAR _table = table
    RETURN ADDCOLUMNS(
        Filter( _table, table,column[mea
";

            var output = DaxProcessor.BuildCallStack2(input);
            Assert.AreEqual("Filter", output.CurrentFunction, "Top of callstack");
            Assert.HasCount(1, output.Variables, "Count of Variables");
            Assert.AreEqual("_table", output.Variables[0], "First Variable");
        }

        [TestMethod]
        public void NestedVariableStackTest()
        {
            var input = @" -- TOPN( test,
EVALUATE
    VAR _outer = table
    RETURN ADDCOLUMNS(
        Filter( var _inner RETURN _outer, table,column[mea
";

            var output = DaxProcessor.BuildCallStack2(input);
            Assert.AreEqual("Filter", output.CurrentFunction, "Top of callstack");
            Assert.HasCount(2, output.Variables, "Count of Variables");
            Assert.AreEqual("_inner", output.Variables[0], "First Variable");
            Assert.AreEqual("_outer", output.Variables[1], "second Variable");
        }

        [TestMethod]
        public void NestedVariableStackTest2()
        {
            var input = @" -- TOPN( test,
DEFINE
    VAR _outer = table
    RETURN ADDCOLUMNS(
        Filter( var _inner RETURN _outer), table,column[measure])
EVALUATE Filter(_outer, 
";

            var output = DaxProcessor.BuildCallStack2(input);
            Assert.AreEqual("Filter", output.CurrentFunction, "Top of callstack");
            Assert.HasCount(1, output.Variables, "Count of Variables");
            Assert.AreEqual("_outer", output.Variables[0], "only Variable in scope");
        }
    }
}
