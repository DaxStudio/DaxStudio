using DaxStudio.Tests.Assertions;
using DaxStudio.Tests.Helpers;
using DaxStudio.UI.Utils;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;

namespace DaxStudio.Tests
{
    [TestClass]
    public class DebugCommasTest
    {
        [TestMethod]
        public void TestMoveDebugCommas()
        {
            var input = @"Date =
VAR FirstYear = -- Customizes the first year to use
    YEAR ( MIN ( Sales[Order Date] ) )
RETURN
ADDCOLUMNS (
    FILTER (
        CALENDARAUTO (),
        YEAR ( [Date] ) >= FirstYear
    ),
    ""Year"", DATE ( YEAR ( [Date] ), 12, 31 ),
    ""Year Month"", EOMONTH( [Date], 0 ),
    ""Month"", FORMAT( [Date], ""mmm"" ),
    ""Month Number"", MONTH( [Date] ),
    ""Day of Week"", FORMAT( [Date], ""ddd"" ),
    ""Day of Week Number"", WEEKDAY( [Date], 1 )
)";
            var expected = @"Date =
VAR FirstYear = -- Customizes the first year to use
    YEAR ( MIN ( Sales[Order Date] ) )
RETURN
ADDCOLUMNS (
    FILTER (
        CALENDARAUTO ()
,        YEAR ( [Date] ) >= FirstYear
    )
,    ""Year"", DATE ( YEAR ( [Date] ), 12, 31 )
,    ""Year Month"", EOMONTH( [Date], 0 )
,    ""Month"", FORMAT( [Date], ""mmm"" )
,    ""Month Number"", MONTH( [Date] )
,    ""Day of Week"", FORMAT( [Date], ""ddd"" )
,    ""Day of Week Number"", WEEKDAY( [Date], 1 )
)";
            var actual = FormatDebugMode.MoveCommasToDebugMode(input);
            //Assert.AreEqual(expected, actual);
            StringAssertion.ShouldEqualWithDiff(expected.NormalizeNewline(), actual.NormalizeNewline(), DiffStyle.Full);
        }

        [TestMethod]
        public void TestMoveDebugCommasWithComment()
        {
            var input = @"Date =
VAR FirstYear = -- Customizes the first year to use
    YEAR ( MIN ( Sales[Order Date] ) )
RETURN
ADDCOLUMNS (
    FILTER (
        CALENDARAUTO (),
        YEAR ( [Date] ) >= FirstYear
    ),
    ""Year"", DATE ( YEAR ( [Date] ), 12, 31 ),
    ""Year Month"", EOMONTH( [Date], 0 ),
--    ""Month"", FORMAT( [Date], ""mmm"" ),
    ""Month Number"", MONTH( [Date] ),
    ""Day of Week"", FORMAT( [Date], ""ddd"" ),
    ""Day of Week Number"", WEEKDAY( [Date], 1 )
)";
            var expected = @"Date =
VAR FirstYear = -- Customizes the first year to use
    YEAR ( MIN ( Sales[Order Date] ) )
RETURN
ADDCOLUMNS (
    FILTER (
        CALENDARAUTO ()
,        YEAR ( [Date] ) >= FirstYear
    )
,    ""Year"", DATE ( YEAR ( [Date] ), 12, 31 )
,    ""Year Month"", EOMONTH( [Date], 0 )
--    ""Month"", FORMAT( [Date], ""mmm"" ),
,    ""Month Number"", MONTH( [Date] )
,    ""Day of Week"", FORMAT( [Date], ""ddd"" )
,    ""Day of Week Number"", WEEKDAY( [Date], 1 )
)";
            var actual = FormatDebugMode.MoveCommasToDebugMode(input);
            //Assert.AreEqual(expected, actual);
            StringAssertion.ShouldEqualWithDiff(expected.NormalizeNewline(), actual.NormalizeNewline(), DiffStyle.Full);
        }



     

        [TestMethod]
        public void TestToggleDebugCommas()
        {
            var input = @"Date =
VAR FirstYear = -- Customizes the first year to use
    YEAR ( MIN ( Sales[Order Date] ) )
RETURN
ADDCOLUMNS (
    FILTER (
        CALENDARAUTO (),
        YEAR ( [Date] ) >= FirstYear
    ),
    ""Year"", DATE ( YEAR ( [Date] ), 12, 31 ),
    ""Year Month"", EOMONTH( [Date], 0 ),
    ""Month"", FORMAT( [Date], ""mmm"" ),
    ""Month Number"", MONTH( [Date] ),
    ""Day of Week"", FORMAT( [Date], ""ddd"" ),
    ""Day of Week Number"", WEEKDAY( [Date], 1 )
)";
            var expected = @"Date =
VAR FirstYear = -- Customizes the first year to use
    YEAR ( MIN ( Sales[Order Date] ) )
RETURN
ADDCOLUMNS (
    FILTER (
        CALENDARAUTO ()
,        YEAR ( [Date] ) >= FirstYear
    )
,    ""Year"", DATE ( YEAR ( [Date] ), 12, 31 )
,    ""Year Month"", EOMONTH( [Date], 0 )
,    ""Month"", FORMAT( [Date], ""mmm"" )
,    ""Month Number"", MONTH( [Date] )
,    ""Day of Week"", FORMAT( [Date], ""ddd"" )
,    ""Day of Week Number"", WEEKDAY( [Date], 1 )
)";
            var actual = FormatDebugMode.ToggleDebugCommas(input);
            //Assert.AreEqual(expected, actual);
            StringAssertion.ShouldEqualWithDiff(expected.NormalizeNewline(), actual.NormalizeNewline(), DiffStyle.Full);

            actual = FormatDebugMode.ToggleDebugCommas(actual);
            //Assert.AreEqual(expected, actual);
            StringAssertion.ShouldEqualWithDiff(input.NormalizeNewline(), actual.NormalizeNewline(), DiffStyle.Full);

        }

        [TestMethod]
        public void TestToggleDebugCommasWithComments()
        {
            var input = @"Date =
VAR FirstYear = -- Customizes the first year to use
    YEAR ( MIN ( Sales[Order Date] ) )
RETURN
ADDCOLUMNS (
    FILTER (
        CALENDARAUTO (),
        YEAR ( [Date] ) >= FirstYear
    ),
    ""Year"", DATE ( YEAR ( [Date] ), 12, 31 ),
//    ""Year Month"", EOMONTH( [Date], 0 ),
    ""Month"", FORMAT( [Date], ""mmm"" ),
//    ""Month Number"", MONTH( [Date] ),
    ""Day of Week"", FORMAT( [Date], ""ddd"" ),
    ""Day of Week Number"", WEEKDAY( [Date], 1 )
)";
            var expected = @"Date =
VAR FirstYear = -- Customizes the first year to use
    YEAR ( MIN ( Sales[Order Date] ) )
RETURN
ADDCOLUMNS (
    FILTER (
        CALENDARAUTO ()
,        YEAR ( [Date] ) >= FirstYear
    )
,    ""Year"", DATE ( YEAR ( [Date] ), 12, 31 )
//    ""Year Month"", EOMONTH( [Date], 0 ),
,    ""Month"", FORMAT( [Date], ""mmm"" )
//    ""Month Number"", MONTH( [Date] ),
,    ""Day of Week"", FORMAT( [Date], ""ddd"" )
,    ""Day of Week Number"", WEEKDAY( [Date], 1 )
)";
            var actual = FormatDebugMode.ToggleDebugCommas(input);
            //Assert.AreEqual(expected, actual);
            StringAssertion.ShouldEqualWithDiff(expected.NormalizeNewline(), actual.NormalizeNewline(), DiffStyle.Full);

            actual = FormatDebugMode.ToggleDebugCommas(actual);
            //Assert.AreEqual(expected, actual);
            StringAssertion.ShouldEqualWithDiff(input.NormalizeNewline(), actual.NormalizeNewline(), DiffStyle.Full);

        }


        [TestMethod]
        public void TestToggleDebugCommasWithSimpleIndentedComments()
        {
            var input = @"Line 1,
  -- comment with leading space
Line 2,
Line 3";
            var expected = @"Line 1
  -- comment with leading space
,Line 2
,Line 3";
            var actual = FormatDebugMode.ToggleDebugCommas(input);
            //Assert.AreEqual(expected, actual);
            StringAssertion.ShouldEqualWithDiff(expected.NormalizeNewline(), actual.NormalizeNewline(), DiffStyle.Full);

            actual = FormatDebugMode.ToggleDebugCommas(actual);
            //Assert.AreEqual(expected, actual);
            StringAssertion.ShouldEqualWithDiff(input.NormalizeNewline(), actual.NormalizeNewline(), DiffStyle.Full);
        }

        [TestMethod]
        public void TestToggleDebugCommasWithTrailingComment()
        {
            var input = @"Line 1,
Line 2, -- trailing comment
Line 3";
            var expected = @"Line 1
,Line 2 -- trailing comment
,Line 3";
            var actual = FormatDebugMode.ToggleDebugCommas(input);
            //Assert.AreEqual(expected, actual);
            StringAssertion.ShouldEqualWithDiff(expected.NormalizeNewline(), actual.NormalizeNewline(), DiffStyle.Full);

            actual = FormatDebugMode.ToggleDebugCommas(actual);
            //Assert.AreEqual(expected, actual);
            StringAssertion.ShouldEqualWithDiff(input.NormalizeNewline(), actual.NormalizeNewline(), DiffStyle.Full);
        }

        [TestMethod]
        public void TestToggleDebugCommasWithLeadingBlockComment()
        {
            var input = @"Line 1,
/* leading comment */ Line 2,
Line 3";
            var expected = @"Line 1
,/* leading comment */ Line 2
,Line 3";
            var actual = FormatDebugMode.ToggleDebugCommas(input);
            //Assert.AreEqual(expected, actual);
            StringAssertion.ShouldEqualWithDiff(expected.NormalizeNewline(), actual.NormalizeNewline(), DiffStyle.Full);

            actual = FormatDebugMode.ToggleDebugCommas(actual);
            //Assert.AreEqual(expected, actual);
            StringAssertion.ShouldEqualWithDiff(input.NormalizeNewline(), actual.NormalizeNewline(), DiffStyle.Full);
        }

        [TestMethod]
        public void TestToggleDebugCommasWithAllComments()
        {
            var input = @"   Line 1,
   -- comment with leading space
   Line 2, // trailing comment
   /* leading comment */ Line 3,
   Line 4";
            var expected = @"   Line 1
   -- comment with leading space
,   Line 2 // trailing comment
,   /* leading comment */ Line 3
,   Line 4";
            var actual = FormatDebugMode.ToggleDebugCommas(input);
            //Assert.AreEqual(expected, actual);
            StringAssertion.ShouldEqualWithDiff(expected.NormalizeNewline(), actual.NormalizeNewline(), DiffStyle.Full);

            actual = FormatDebugMode.ToggleDebugCommas(actual);
            //Assert.AreEqual(expected, actual);
            StringAssertion.ShouldEqualWithDiff(input.NormalizeNewline(), actual.NormalizeNewline(), DiffStyle.Full);
        }

        [TestMethod]
        public void TestToggleDebugCommasWithIndentedComments()
        {
            var input = @"Date =
VAR FirstYear = -- Customizes the first year to use
    YEAR ( MIN ( Sales[Order Date] ) )
RETURN
ADDCOLUMNS (
    FILTER (
        CALENDARAUTO (),
        YEAR ( [Date] ) >= FirstYear
    ),
    ""Year"", DATE ( YEAR ( [Date] ), 12, 31 ),
    //""Year Month"", EOMONTH( [Date], 0 ),
    ""Month"", FORMAT( [Date], ""mmm"" ),
    //""Month Number"", MONTH( [Date] ),
    ""Day of Week"", FORMAT( [Date], ""ddd"" ),
    ""Day of Week Number"", WEEKDAY( [Date], 1 )
)";
            var expected = @"Date =
VAR FirstYear = -- Customizes the first year to use
    YEAR ( MIN ( Sales[Order Date] ) )
RETURN
ADDCOLUMNS (
    FILTER (
        CALENDARAUTO ()
,        YEAR ( [Date] ) >= FirstYear
    )
,    ""Year"", DATE ( YEAR ( [Date] ), 12, 31 )
    //""Year Month"", EOMONTH( [Date], 0 ),
,    ""Month"", FORMAT( [Date], ""mmm"" )
    //""Month Number"", MONTH( [Date] ),
,    ""Day of Week"", FORMAT( [Date], ""ddd"" )
,    ""Day of Week Number"", WEEKDAY( [Date], 1 )
)";
            var actual = FormatDebugMode.ToggleDebugCommas(input);
            //Assert.AreEqual(expected, actual);
            StringAssertion.ShouldEqualWithDiff(expected.NormalizeNewline(), actual.NormalizeNewline(), DiffStyle.Full);

            actual = FormatDebugMode.ToggleDebugCommas(actual);
            //Assert.AreEqual(expected, actual);
            StringAssertion.ShouldEqualWithDiff(input.NormalizeNewline(), actual.NormalizeNewline(), DiffStyle.Full);

        }
    }
}
