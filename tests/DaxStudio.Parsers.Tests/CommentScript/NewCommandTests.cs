using Antlr4.Runtime;
using Antlr4.Runtime.Tree;
using DaxStudio.Parsers.Dax;
using DaxStudio.Parsers.Dax;
using DaxStudio.Parsers.CommentScript;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;

using DaxStudio.Parsers.Grammars.Generated;
namespace DaxStudio.Parsers.Tests.CommentScript
{
    [TestClass]
    public class NewCommandTests
    {
        #region TRACE Tests

        [TestMethod]
        public void TraceServerTimingsOn()
        {
            var input = "--> TRACE SERVERTIMINGS ON\n" +
                "EVALUATE { 1 }\n";

            List<Error> errors = new List<Error>();
            var tree = Helpers.ConfigureLexerAndParser(input, ref errors);

            Assert.IsNull(tree.exception);
            Assert.IsEmpty(errors, "Should have no errors");

            Dictionary<string, List<string>> arrayParameters = new Dictionary<string, List<string>>();
            var batch = new List<ScriptBatch>();
            var listener = new PreProcessorListener(arrayParameters, batch);
            var walker = new ParseTreeWalker();
            walker.Walk(listener, tree);

            Assert.HasCount(1, batch[0].Commands);
            var cmd = batch[0].Commands[0] as TraceCommand;
            Assert.IsNotNull(cmd);
            Assert.AreEqual(TraceType.ServerTimings, cmd.TraceType);
            Assert.IsTrue(cmd.Enabled);
        }

        [TestMethod]
        public void TraceQueryPlanOff()
        {
            var input = "--> TRACE QUERYPLAN OFF\n" +
                "EVALUATE { 1 }\n";

            List<Error> errors = new List<Error>();
            var tree = Helpers.ConfigureLexerAndParser(input, ref errors);

            Assert.IsNull(tree.exception);
            Assert.IsEmpty(errors, "Should have no errors");

            Dictionary<string, List<string>> arrayParameters = new Dictionary<string, List<string>>();
            var batch = new List<ScriptBatch>();
            var listener = new PreProcessorListener(arrayParameters, batch);
            var walker = new ParseTreeWalker();
            walker.Walk(listener, tree);

            Assert.HasCount(1, batch[0].Commands);
            var cmd = batch[0].Commands[0] as TraceCommand;
            Assert.IsNotNull(cmd);
            Assert.AreEqual(TraceType.QueryPlan, cmd.TraceType);
            Assert.IsFalse(cmd.Enabled);
        }

        [TestMethod]
        public void TraceAllQueriesOn()
        {
            var input = "--> TRACE ALLQUERIES ON\n" +
                "EVALUATE { 1 }\n";

            List<Error> errors = new List<Error>();
            var tree = Helpers.ConfigureLexerAndParser(input, ref errors);

            Assert.IsNull(tree.exception);
            Assert.IsEmpty(errors, "Should have no errors");

            Dictionary<string, List<string>> arrayParameters = new Dictionary<string, List<string>>();
            var batch = new List<ScriptBatch>();
            var listener = new PreProcessorListener(arrayParameters, batch);
            var walker = new ParseTreeWalker();
            walker.Walk(listener, tree);

            Assert.HasCount(1, batch[0].Commands);
            var cmd = batch[0].Commands[0] as TraceCommand;
            Assert.IsNotNull(cmd);
            Assert.AreEqual(TraceType.AllQueries, cmd.TraceType);
            Assert.IsTrue(cmd.Enabled);
        }

        #endregion

        #region METRICS Tests

        [TestMethod]
        public void MetricsExportWithFilename()
        {
            var input = "--> METRICS EXPORT \"metrics.json\"\n" +
                "EVALUATE { 1 }\n";

            List<Error> errors = new List<Error>();
            var tree = Helpers.ConfigureLexerAndParser(input, ref errors);

            Assert.IsNull(tree.exception);
            Assert.IsEmpty(errors, "Should have no errors");

            Dictionary<string, List<string>> arrayParameters = new Dictionary<string, List<string>>();
            var batch = new List<ScriptBatch>();
            var listener = new PreProcessorListener(arrayParameters, batch);
            var walker = new ParseTreeWalker();
            walker.Walk(listener, tree);

            Assert.HasCount(1, batch[0].Commands);
            var cmd = batch[0].Commands[0] as MetricsCommand;
            Assert.IsNotNull(cmd);
            Assert.AreEqual(MetricsAction.Export, cmd.Action);
            Assert.AreEqual("metrics.json", cmd.FileName);
        }

        [TestMethod]
        public void MetricsExportWithUnquotedFilename()
        {
            var input = "--> METRICS EXPORT myfile\n" +
                "EVALUATE { 1 }\n";

            List<Error> errors = new List<Error>();
            var tree = Helpers.ConfigureLexerAndParser(input, ref errors);

            Assert.IsNull(tree.exception);
            Assert.IsEmpty(errors, "Should have no errors");

            Dictionary<string, List<string>> arrayParameters = new Dictionary<string, List<string>>();
            var batch = new List<ScriptBatch>();
            var listener = new PreProcessorListener(arrayParameters, batch);
            var walker = new ParseTreeWalker();
            walker.Walk(listener, tree);

            Assert.HasCount(1, batch[0].Commands);
            var cmd = batch[0].Commands[0] as MetricsCommand;
            Assert.IsNotNull(cmd);
            Assert.AreEqual(MetricsAction.Export, cmd.Action);
            Assert.AreEqual("myfile", cmd.FileName);
        }

        [TestMethod]
        public void MetricsView()
        {
            var input = "--> METRICS VIEW\n" +
                "EVALUATE { 1 }\n";

            List<Error> errors = new List<Error>();
            var tree = Helpers.ConfigureLexerAndParser(input, ref errors);

            Assert.IsNull(tree.exception);
            Assert.IsEmpty(errors, "Should have no errors");

            Dictionary<string, List<string>> arrayParameters = new Dictionary<string, List<string>>();
            var batch = new List<ScriptBatch>();
            var listener = new PreProcessorListener(arrayParameters, batch);
            var walker = new ParseTreeWalker();
            walker.Walk(listener, tree);

            Assert.HasCount(1, batch[0].Commands);
            var cmd = batch[0].Commands[0] as MetricsCommand;
            Assert.IsNotNull(cmd);
            Assert.AreEqual(MetricsAction.View, cmd.Action);
            Assert.IsNull(cmd.FileName);
        }

        #endregion

        #region ASSERT ROWCOUNT Tests

        [TestMethod]
        public void AssertRowcountEquals()
        {
            var input = "--> ASSERT ROWCOUNT = 10\n" +
                "EVALUATE { 1 }\n";

            List<Error> errors = new List<Error>();
            var tree = Helpers.ConfigureLexerAndParser(input, ref errors);

            Assert.IsNull(tree.exception);
            Assert.IsEmpty(errors, "Should have no errors");

            Dictionary<string, List<string>> arrayParameters = new Dictionary<string, List<string>>();
            var batch = new List<ScriptBatch>();
            var listener = new PreProcessorListener(arrayParameters, batch);
            var walker = new ParseTreeWalker();
            walker.Walk(listener, tree);

            Assert.HasCount(1, batch[0].Commands);
            var cmd = batch[0].Commands[0] as AssertRowcountCommand;
            Assert.IsNotNull(cmd);
            Assert.AreEqual("=", cmd.Comparison);
            Assert.AreEqual(10, cmd.Value);
        }

        [TestMethod]
        public void AssertRowcountGreaterThan()
        {
            var input = "--> ASSERT ROWCOUNT > 5\n" +
                "EVALUATE { 1 }\n";

            List<Error> errors = new List<Error>();
            var tree = Helpers.ConfigureLexerAndParser(input, ref errors);

            Assert.IsNull(tree.exception);
            Assert.IsEmpty(errors, "Should have no errors");

            Dictionary<string, List<string>> arrayParameters = new Dictionary<string, List<string>>();
            var batch = new List<ScriptBatch>();
            var listener = new PreProcessorListener(arrayParameters, batch);
            var walker = new ParseTreeWalker();
            walker.Walk(listener, tree);

            Assert.HasCount(1, batch[0].Commands);
            var cmd = batch[0].Commands[0] as AssertRowcountCommand;
            Assert.IsNotNull(cmd);
            Assert.AreEqual(">", cmd.Comparison);
            Assert.AreEqual(5, cmd.Value);
        }

        [TestMethod]
        public void AssertRowcountLessOrEqual()
        {
            var input = "--> ASSERT ROWCOUNT <= 100\n" +
                "EVALUATE { 1 }\n";

            List<Error> errors = new List<Error>();
            var tree = Helpers.ConfigureLexerAndParser(input, ref errors);

            Assert.IsNull(tree.exception);
            Assert.IsEmpty(errors, "Should have no errors");

            Dictionary<string, List<string>> arrayParameters = new Dictionary<string, List<string>>();
            var batch = new List<ScriptBatch>();
            var listener = new PreProcessorListener(arrayParameters, batch);
            var walker = new ParseTreeWalker();
            walker.Walk(listener, tree);

            Assert.HasCount(1, batch[0].Commands);
            var cmd = batch[0].Commands[0] as AssertRowcountCommand;
            Assert.IsNotNull(cmd);
            Assert.AreEqual("<=", cmd.Comparison);
            Assert.AreEqual(100, cmd.Value);
        }

        #endregion

        #region ASSERT TABLE Tests

        [TestMethod]
        public void AssertTableOrdered()
        {
            var input = "--> ASSERT TABLE\n" +
                "-->> | Color | Count |\n" +
                "-->> |-------|-------|\n" +
                "-->> | Red   | 5     |\n" +
                "-->> | Blue  | 3     |\n" +
                "EVALUATE { 1 }\n";

            List<Error> errors = new List<Error>();
            var tree = Helpers.ConfigureLexerAndParser(input, ref errors);

            Assert.IsNull(tree.exception);
            Assert.IsEmpty(errors, "Should have no errors");

            Dictionary<string, List<string>> arrayParameters = new Dictionary<string, List<string>>();
            var batch = new List<ScriptBatch>();
            var listener = new PreProcessorListener(arrayParameters, batch);
            var walker = new ParseTreeWalker();
            walker.Walk(listener, tree);

            Assert.HasCount(1, batch[0].Commands);
            var cmd = batch[0].Commands[0] as AssertTableCommand;
            Assert.IsNotNull(cmd);
            Assert.AreEqual(AssertTableMode.Ordered, cmd.Mode);
            Assert.AreEqual(2, cmd.Data.Columns.Count);
            Assert.AreEqual("Color", cmd.Data.Columns[0].ColumnName);
            Assert.AreEqual("Count", cmd.Data.Columns[1].ColumnName);
            Assert.AreEqual(2, cmd.Data.Rows.Count);
            Assert.AreEqual("Red", cmd.Data.Rows[0]["Color"]);
            Assert.AreEqual(5L, cmd.Data.Rows[0]["Count"]);
            Assert.AreEqual("Blue", cmd.Data.Rows[1]["Color"]);
            Assert.AreEqual(3L, cmd.Data.Rows[1]["Count"]);
            // Verify type inference
            Assert.AreEqual(typeof(string), cmd.Data.Columns["Color"].DataType);
            Assert.AreEqual(typeof(long), cmd.Data.Columns["Count"].DataType);
        }

        [TestMethod]
        public void AssertTableUnordered()
        {
            var input = "--> ASSERT TABLE UNORDERED\n" +
                "-->> | Name  |\n" +
                "-->> | Alice |\n" +
                "-->> | Bob   |\n" +
                "EVALUATE { 1 }\n";

            List<Error> errors = new List<Error>();
            var tree = Helpers.ConfigureLexerAndParser(input, ref errors);

            Assert.IsNull(tree.exception);
            Assert.IsEmpty(errors, "Should have no errors");

            Dictionary<string, List<string>> arrayParameters = new Dictionary<string, List<string>>();
            var batch = new List<ScriptBatch>();
            var listener = new PreProcessorListener(arrayParameters, batch);
            var walker = new ParseTreeWalker();
            walker.Walk(listener, tree);

            Assert.HasCount(1, batch[0].Commands);
            var cmd = batch[0].Commands[0] as AssertTableCommand;
            Assert.IsNotNull(cmd);
            Assert.AreEqual(AssertTableMode.Unordered, cmd.Mode);
            Assert.AreEqual(1, cmd.Data.Columns.Count);
            Assert.AreEqual("Name", cmd.Data.Columns[0].ColumnName);
            Assert.AreEqual(2, cmd.Data.Rows.Count);
        }

        [TestMethod]
        public void AssertTablePartial()
        {
            var input = "--> ASSERT TABLE PARTIAL\n" +
                "-->> | Color |\n" +
                "-->> | Red   |\n" +
                "EVALUATE { 1 }\n";

            List<Error> errors = new List<Error>();
            var tree = Helpers.ConfigureLexerAndParser(input, ref errors);

            Assert.IsNull(tree.exception);
            Assert.IsEmpty(errors, "Should have no errors");

            Dictionary<string, List<string>> arrayParameters = new Dictionary<string, List<string>>();
            var batch = new List<ScriptBatch>();
            var listener = new PreProcessorListener(arrayParameters, batch);
            var walker = new ParseTreeWalker();
            walker.Walk(listener, tree);

            Assert.HasCount(1, batch[0].Commands);
            var cmd = batch[0].Commands[0] as AssertTableCommand;
            Assert.IsNotNull(cmd);
            Assert.AreEqual(AssertTableMode.Partial, cmd.Mode);
        }

        [TestMethod]
        public void AssertTableWithEmptyCell()
        {
            var input = "--> ASSERT TABLE\n" +
                "-->> | Color | Count |\n" +
                "-->> | Red   |       |\n" +
                "EVALUATE { 1 }\n";

            List<Error> errors = new List<Error>();
            var tree = Helpers.ConfigureLexerAndParser(input, ref errors);

            Assert.IsNull(tree.exception);
            Assert.IsEmpty(errors, "Should have no errors");

            Dictionary<string, List<string>> arrayParameters = new Dictionary<string, List<string>>();
            var batch = new List<ScriptBatch>();
            var listener = new PreProcessorListener(arrayParameters, batch);
            var walker = new ParseTreeWalker();
            walker.Walk(listener, tree);

            Assert.HasCount(1, batch[0].Commands);
            var cmd = batch[0].Commands[0] as AssertTableCommand;
            Assert.IsNotNull(cmd);
            Assert.AreEqual(1, cmd.Data.Rows.Count);
            Assert.AreEqual("Red", cmd.Data.Rows[0]["Color"]);
            // Empty cell with type inference: Count column has one empty value, 
            // so it stays string (no values to infer from)
            Assert.AreEqual("", cmd.Data.Rows[0]["Count"]);
        }

        [TestMethod]
        public void AssertTableWithDoubleValues()
        {
            var input = "--> ASSERT TABLE\n" +
                "-->> | Name | Score |\n" +
                "-->> | A    | 3.14  |\n" +
                "-->> | B    | 2.71  |\n" +
                "EVALUATE { 1 }\n";

            List<Error> errors = new List<Error>();
            var tree = Helpers.ConfigureLexerAndParser(input, ref errors);

            Assert.IsNull(tree.exception);
            Assert.IsEmpty(errors, "Should have no errors");

            Dictionary<string, List<string>> arrayParameters = new Dictionary<string, List<string>>();
            var batch = new List<ScriptBatch>();
            var listener = new PreProcessorListener(arrayParameters, batch);
            var walker = new ParseTreeWalker();
            walker.Walk(listener, tree);

            var cmd = batch[0].Commands[0] as AssertTableCommand;
            Assert.IsNotNull(cmd);
            Assert.AreEqual(typeof(double), cmd.Data.Columns["Score"].DataType);
            Assert.AreEqual(3.14, cmd.Data.Rows[0]["Score"]);
            Assert.AreEqual(2.71, cmd.Data.Rows[1]["Score"]);
        }

        [TestMethod]
        public void AssertTableMixedTypesStayString()
        {
            var input = "--> ASSERT TABLE\n" +
                "-->> | Value |\n" +
                "-->> | 5     |\n" +
                "-->> | hello |\n" +
                "EVALUATE { 1 }\n";

            List<Error> errors = new List<Error>();
            var tree = Helpers.ConfigureLexerAndParser(input, ref errors);

            Assert.IsNull(tree.exception);
            Assert.IsEmpty(errors, "Should have no errors");

            Dictionary<string, List<string>> arrayParameters = new Dictionary<string, List<string>>();
            var batch = new List<ScriptBatch>();
            var listener = new PreProcessorListener(arrayParameters, batch);
            var walker = new ParseTreeWalker();
            walker.Walk(listener, tree);

            var cmd = batch[0].Commands[0] as AssertTableCommand;
            Assert.IsNotNull(cmd);
            // Column has mixed int/string values, so stays string
            Assert.AreEqual(typeof(string), cmd.Data.Columns["Value"].DataType);
            Assert.AreEqual("5", cmd.Data.Rows[0]["Value"]);
            Assert.AreEqual("hello", cmd.Data.Rows[1]["Value"]);
        }

        [TestMethod]
        public void AssertTableWithBooleanValues()
        {
            var input = "--> ASSERT TABLE\n" +
                "-->> | Name | IsActive |\n" +
                "-->> | A    | TRUE     |\n" +
                "-->> | B    | FALSE    |\n" +
                "EVALUATE { 1 }\n";

            List<Error> errors = new List<Error>();
            var tree = Helpers.ConfigureLexerAndParser(input, ref errors);

            Assert.IsNull(tree.exception);
            Assert.IsEmpty(errors, "Should have no errors");

            Dictionary<string, List<string>> arrayParameters = new Dictionary<string, List<string>>();
            var batch = new List<ScriptBatch>();
            var listener = new PreProcessorListener(arrayParameters, batch);
            var walker = new ParseTreeWalker();
            walker.Walk(listener, tree);

            var cmd = batch[0].Commands[0] as AssertTableCommand;
            Assert.IsNotNull(cmd);
            Assert.AreEqual(typeof(bool), cmd.Data.Columns["IsActive"].DataType);
            Assert.AreEqual(true, cmd.Data.Rows[0]["IsActive"]);
            Assert.AreEqual(false, cmd.Data.Rows[1]["IsActive"]);
        }

        [TestMethod]
        public void AssertTableWithDateTimeValues()
        {
            var input = "--> ASSERT TABLE\n" +
                "-->> | Name | OrderDate  |\n" +
                "-->> | A    | 2024-01-15 |\n" +
                "-->> | B    | 2024-06-30 |\n" +
                "EVALUATE { 1 }\n";

            List<Error> errors = new List<Error>();
            var tree = Helpers.ConfigureLexerAndParser(input, ref errors);

            Assert.IsNull(tree.exception);
            Assert.IsEmpty(errors, "Should have no errors");

            Dictionary<string, List<string>> arrayParameters = new Dictionary<string, List<string>>();
            var batch = new List<ScriptBatch>();
            var listener = new PreProcessorListener(arrayParameters, batch);
            var walker = new ParseTreeWalker();
            walker.Walk(listener, tree);

            var cmd = batch[0].Commands[0] as AssertTableCommand;
            Assert.IsNotNull(cmd);
            Assert.AreEqual(typeof(DateTime), cmd.Data.Columns["OrderDate"].DataType);
            Assert.AreEqual(new DateTime(2024, 1, 15), cmd.Data.Rows[0]["OrderDate"]);
            Assert.AreEqual(new DateTime(2024, 6, 30), cmd.Data.Rows[1]["OrderDate"]);
        }

        [TestMethod]
        public void AssertTableWithDateTimeAndTime()
        {
            var input = "--> ASSERT TABLE\n" +
                "-->> | Event | Timestamp           |\n" +
                "-->> | A     | 2024-01-15 10:30:00 |\n" +
                "-->> | B     | 2024-06-30 14:45:00 |\n" +
                "EVALUATE { 1 }\n";

            List<Error> errors = new List<Error>();
            var tree = Helpers.ConfigureLexerAndParser(input, ref errors);

            Assert.IsNull(tree.exception);
            Assert.IsEmpty(errors, "Should have no errors");

            Dictionary<string, List<string>> arrayParameters = new Dictionary<string, List<string>>();
            var batch = new List<ScriptBatch>();
            var listener = new PreProcessorListener(arrayParameters, batch);
            var walker = new ParseTreeWalker();
            walker.Walk(listener, tree);

            var cmd = batch[0].Commands[0] as AssertTableCommand;
            Assert.IsNotNull(cmd);
            Assert.AreEqual(typeof(DateTime), cmd.Data.Columns["Timestamp"].DataType);
            Assert.AreEqual(new DateTime(2024, 1, 15, 10, 30, 0), cmd.Data.Rows[0]["Timestamp"]);
            Assert.AreEqual(new DateTime(2024, 6, 30, 14, 45, 0), cmd.Data.Rows[1]["Timestamp"]);
        }

        [TestMethod]
        public void AssertTableWithDecimalValues()
        {
            var input = "--> ASSERT TABLE\n" +
                "-->> | Product | Price    |\n" +
                "-->> | Widget  | 1,234.56 |\n" +
                "-->> | Gadget  | 789.00   |\n" +
                "EVALUATE { 1 }\n";

            List<Error> errors = new List<Error>();
            var tree = Helpers.ConfigureLexerAndParser(input, ref errors);

            Assert.IsNull(tree.exception);
            Assert.IsEmpty(errors, "Should have no errors");

            Dictionary<string, List<string>> arrayParameters = new Dictionary<string, List<string>>();
            var batch = new List<ScriptBatch>();
            var listener = new PreProcessorListener(arrayParameters, batch);
            var walker = new ParseTreeWalker();
            walker.Walk(listener, tree);

            var cmd = batch[0].Commands[0] as AssertTableCommand;
            Assert.IsNotNull(cmd);
            // Values with commas parse as decimal (not double, since NumberStyles.Number allows grouping)
            Assert.AreEqual(typeof(decimal), cmd.Data.Columns["Price"].DataType);
            Assert.AreEqual(1234.56m, cmd.Data.Rows[0]["Price"]);
            Assert.AreEqual(789.00m, cmd.Data.Rows[1]["Price"]);
        }

        [TestMethod]
        public void AssertTableWithNullableColumn()
        {
            var input = "--> ASSERT TABLE\n" +
                "-->> | Name | Age |\n" +
                "-->> | A    | 25  |\n" +
                "-->> | B    |     |\n" +
                "EVALUATE { 1 }\n";

            List<Error> errors = new List<Error>();
            var tree = Helpers.ConfigureLexerAndParser(input, ref errors);

            Assert.IsNull(tree.exception);
            Assert.IsEmpty(errors, "Should have no errors");

            Dictionary<string, List<string>> arrayParameters = new Dictionary<string, List<string>>();
            var batch = new List<ScriptBatch>();
            var listener = new PreProcessorListener(arrayParameters, batch);
            var walker = new ParseTreeWalker();
            walker.Walk(listener, tree);

            var cmd = batch[0].Commands[0] as AssertTableCommand;
            Assert.IsNotNull(cmd);
            // Column has int value and empty value - should still infer as long
            Assert.AreEqual(typeof(long), cmd.Data.Columns["Age"].DataType);
            Assert.AreEqual(25L, cmd.Data.Rows[0]["Age"]);
            Assert.AreEqual(DBNull.Value, cmd.Data.Rows[1]["Age"]);
        }

        [TestMethod]
        public void AssertTableAllTypesInOneTable()
        {
            var input = "--> ASSERT TABLE\n" +
                "-->> | Name | Count | Score | Active | OrderDate  |\n" +
                "-->> |------|-------|-------|--------|------------|\n" +
                "-->> | A    | 10    | 3.14  | TRUE   | 2024-01-15 |\n" +
                "-->> | B    | 20    | 2.71  | FALSE  | 2024-06-30 |\n" +
                "EVALUATE { 1 }\n";

            List<Error> errors = new List<Error>();
            var tree = Helpers.ConfigureLexerAndParser(input, ref errors);

            Assert.IsNull(tree.exception);
            Assert.IsEmpty(errors, "Should have no errors");

            Dictionary<string, List<string>> arrayParameters = new Dictionary<string, List<string>>();
            var batch = new List<ScriptBatch>();
            var listener = new PreProcessorListener(arrayParameters, batch);
            var walker = new ParseTreeWalker();
            walker.Walk(listener, tree);

            var cmd = batch[0].Commands[0] as AssertTableCommand;
            Assert.IsNotNull(cmd);
            Assert.AreEqual(typeof(string), cmd.Data.Columns["Name"].DataType);
            Assert.AreEqual(typeof(long), cmd.Data.Columns["Count"].DataType);
            Assert.AreEqual(typeof(double), cmd.Data.Columns["Score"].DataType);
            Assert.AreEqual(typeof(bool), cmd.Data.Columns["Active"].DataType);
            Assert.AreEqual(typeof(DateTime), cmd.Data.Columns["OrderDate"].DataType);
        }

        [TestMethod]
        public void AssertTableWithExplicitTypeRow()
        {
            var input = "--> ASSERT TABLE\n" +
                "-->> | Name   | Price    | OrderDate  |\n" +
                "-->> | STRING | CURRENCY | DATETIME   |\n" +
                "-->> |--------|----------|------------|\n" +
                "-->> | Widget | 19.99    | 2024-01-15 |\n" +
                "-->> | Gadget | 9.99     | 2024-06-30 |\n" +
                "EVALUATE { 1 }\n";

            List<Error> errors = new List<Error>();
            var tree = Helpers.ConfigureLexerAndParser(input, ref errors);

            Assert.IsNull(tree.exception);
            Assert.IsEmpty(errors, "Should have no errors");

            Dictionary<string, List<string>> arrayParameters = new Dictionary<string, List<string>>();
            var batch = new List<ScriptBatch>();
            var listener = new PreProcessorListener(arrayParameters, batch);
            var walker = new ParseTreeWalker();
            walker.Walk(listener, tree);

            var cmd = batch[0].Commands[0] as AssertTableCommand;
            Assert.IsNotNull(cmd);
            // Type row forces CURRENCY → decimal instead of inferred double
            Assert.AreEqual(typeof(string), cmd.Data.Columns["Name"].DataType);
            Assert.AreEqual(typeof(decimal), cmd.Data.Columns["Price"].DataType);
            Assert.AreEqual(typeof(DateTime), cmd.Data.Columns["OrderDate"].DataType);
            Assert.AreEqual(2, cmd.Data.Rows.Count);
            Assert.AreEqual(19.99m, cmd.Data.Rows[0]["Price"]);
            Assert.AreEqual(new DateTime(2024, 1, 15), cmd.Data.Rows[0]["OrderDate"]);
        }

        [TestMethod]
        public void AssertTableTypeRowWithAllDaxTypes()
        {
            var input = "--> ASSERT TABLE\n" +
                "-->> | A      | B     | C      | D        | E        | F      |\n" +
                "-->> | STRING | INT64 | DOUBLE | CURRENCY | BOOLEAN  | DATETIME |\n" +
                "-->> | hello  | 42    | 3.14   | 99.95    | TRUE     | 2024-03-27 |\n" +
                "EVALUATE { 1 }\n";

            List<Error> errors = new List<Error>();
            var tree = Helpers.ConfigureLexerAndParser(input, ref errors);

            Assert.IsNull(tree.exception);
            Assert.IsEmpty(errors, "Should have no errors");

            Dictionary<string, List<string>> arrayParameters = new Dictionary<string, List<string>>();
            var batch = new List<ScriptBatch>();
            var listener = new PreProcessorListener(arrayParameters, batch);
            var walker = new ParseTreeWalker();
            walker.Walk(listener, tree);

            var cmd = batch[0].Commands[0] as AssertTableCommand;
            Assert.IsNotNull(cmd);
            Assert.AreEqual(typeof(string), cmd.Data.Columns["A"].DataType);
            Assert.AreEqual(typeof(long), cmd.Data.Columns["B"].DataType);
            Assert.AreEqual(typeof(double), cmd.Data.Columns["C"].DataType);
            Assert.AreEqual(typeof(decimal), cmd.Data.Columns["D"].DataType);
            Assert.AreEqual(typeof(bool), cmd.Data.Columns["E"].DataType);
            Assert.AreEqual(typeof(DateTime), cmd.Data.Columns["F"].DataType);
            Assert.AreEqual(1, cmd.Data.Rows.Count);
            Assert.AreEqual("hello", cmd.Data.Rows[0]["A"]);
            Assert.AreEqual(42L, cmd.Data.Rows[0]["B"]);
            Assert.AreEqual(3.14, cmd.Data.Rows[0]["C"]);
            Assert.AreEqual(99.95m, cmd.Data.Rows[0]["D"]);
            Assert.AreEqual(true, cmd.Data.Rows[0]["E"]);
            Assert.AreEqual(new DateTime(2024, 3, 27), cmd.Data.Rows[0]["F"]);
        }

        [TestMethod]
        public void AssertTableTypeRowNotConfusedWithData()
        {
            // "Red" is not a valid type name, so this row should be treated as data
            var input = "--> ASSERT TABLE\n" +
                "-->> | Color | Count |\n" +
                "-->> | Red   | 5     |\n" +
                "EVALUATE { 1 }\n";

            List<Error> errors = new List<Error>();
            var tree = Helpers.ConfigureLexerAndParser(input, ref errors);

            Assert.IsNull(tree.exception);
            Assert.IsEmpty(errors, "Should have no errors");

            Dictionary<string, List<string>> arrayParameters = new Dictionary<string, List<string>>();
            var batch = new List<ScriptBatch>();
            var listener = new PreProcessorListener(arrayParameters, batch);
            var walker = new ParseTreeWalker();
            walker.Walk(listener, tree);

            var cmd = batch[0].Commands[0] as AssertTableCommand;
            Assert.IsNotNull(cmd);
            // Should have 1 data row (not treated as type row)
            Assert.AreEqual(1, cmd.Data.Rows.Count);
            Assert.AreEqual("Red", cmd.Data.Rows[0]["Color"]);
        }

        #endregion

        #region Combined Command Tests

        [TestMethod]
        public void TraceAndAssertRowcountCombined()
        {
            var input = "--> TRACE SERVERTIMINGS ON\n" +
                "--> ASSERT ROWCOUNT = 3\n" +
                "EVALUATE { 1 }\n";

            List<Error> errors = new List<Error>();
            var tree = Helpers.ConfigureLexerAndParser(input, ref errors);

            Assert.IsNull(tree.exception);
            Assert.IsEmpty(errors, "Should have no errors");

            Dictionary<string, List<string>> arrayParameters = new Dictionary<string, List<string>>();
            var batch = new List<ScriptBatch>();
            var listener = new PreProcessorListener(arrayParameters, batch);
            var walker = new ParseTreeWalker();
            walker.Walk(listener, tree);

            Assert.HasCount(2, batch[0].Commands);
            Assert.IsInstanceOfType(batch[0].Commands[0], typeof(TraceCommand));
            Assert.IsInstanceOfType(batch[0].Commands[1], typeof(AssertRowcountCommand));
        }

        [TestMethod]
        public void MultipleTraceCommandsInOneBatch()
        {
            // The execution layer collects every TraceCommand in the batch and toggles each
            // matching trace watcher, so a single script may enable more than one trace at once.
            var input = "--> TRACE SERVERTIMINGS ON\n" +
                "--> TRACE QUERYPLAN ON\n" +
                "--> TRACE ALLQUERIES OFF\n" +
                "EVALUATE { 1 }\n";

            List<Error> errors = new List<Error>();
            var tree = Helpers.ConfigureLexerAndParser(input, ref errors);

            Assert.IsNull(tree.exception);
            Assert.IsEmpty(errors, "Should have no errors");

            Dictionary<string, List<string>> arrayParameters = new Dictionary<string, List<string>>();
            var batch = new List<ScriptBatch>();
            var listener = new PreProcessorListener(arrayParameters, batch);
            var walker = new ParseTreeWalker();
            walker.Walk(listener, tree);

            var traceCommands = batch[0].Commands.OfType<TraceCommand>().ToList();
            Assert.HasCount(3, traceCommands);

            Assert.AreEqual(TraceType.ServerTimings, traceCommands[0].TraceType);
            Assert.IsTrue(traceCommands[0].Enabled);

            Assert.AreEqual(TraceType.QueryPlan, traceCommands[1].TraceType);
            Assert.IsTrue(traceCommands[1].Enabled);

            Assert.AreEqual(TraceType.AllQueries, traceCommands[2].TraceType);
            Assert.IsFalse(traceCommands[2].Enabled);
        }

        [TestMethod]
        public void ConnectUseAndTraceCombined()
        {
            // Mirrors the CONNECT -> USE -> TRACE ordering that the execution layer relies on.
            var input = "--> CONNECT SERVER localhost\\tab19\n" +
                "--> USE \"Adventure Works\"\n" +
                "--> TRACE SERVERTIMINGS ON\n" +
                "EVALUATE { 1 }\n";

            List<Error> errors = new List<Error>();
            var tree = Helpers.ConfigureLexerAndParser(input, ref errors);

            Assert.IsNull(tree.exception);
            Assert.IsEmpty(errors, "Should have no errors");

            Dictionary<string, List<string>> arrayParameters = new Dictionary<string, List<string>>();
            var batch = new List<ScriptBatch>();
            var listener = new PreProcessorListener(arrayParameters, batch);
            var walker = new ParseTreeWalker();
            walker.Walk(listener, tree);

            Assert.HasCount(3, batch[0].Commands);

            var connCmd = batch[0].Commands[0] as ConnectCommand;
            Assert.IsNotNull(connCmd);
            Assert.AreEqual(ConnectionType.SERVER, connCmd.ConnectionType);

            var useCmd = batch[0].Commands[1] as UseCommand;
            Assert.IsNotNull(useCmd);
            Assert.AreEqual("Adventure Works", useCmd.DatabaseName);

            var traceCmd = batch[0].Commands[2] as TraceCommand;
            Assert.IsNotNull(traceCmd);
            Assert.AreEqual(TraceType.ServerTimings, traceCmd.TraceType);
            Assert.IsTrue(traceCmd.Enabled);
        }

        #endregion

        [TestMethod]
        public void UseUnquotedMultiWordDatabase()
        {
            // A database name with spaces does not have to be quoted - the USE command captures the
            // rest of the line (e.g. a Power BI dataset named "AW Internet Sales").
            var input = "--> CONNECT SERVER localhost\n" +
                "--> USE AW Internet Sales\n" +
                "EVALUATE { 1 }\n";

            var result = DaxStudio.Parsers.PreProcessor.AntlrPreProcessor.Parse(input);
            Assert.IsFalse(result.HasErrors, "an unquoted multi-word database name should parse cleanly");

            var commands = result.Batches.SelectMany(b => b.Commands).ToList();
            var useCmd = commands.OfType<UseCommand>().FirstOrDefault();
            Assert.IsNotNull(useCmd, "the USE command must not be dropped for an unquoted multi-word name");
            Assert.AreEqual("AW Internet Sales", useCmd.DatabaseName);
        }

        [TestMethod]
        public void UseUnquotedDatabaseWithTrailingNumber()
        {
            // Digits are separate tokens from identifiers, so a name like "Adventure Works 2022" also
            // exercises the "capture the rest of the line" behaviour.
            var input = "--> USE Adventure Works 2022\nEVALUATE { 1 }\n";

            var result = DaxStudio.Parsers.PreProcessor.AntlrPreProcessor.Parse(input);
            Assert.IsFalse(result.HasErrors);

            var useCmd = result.Batches.SelectMany(b => b.Commands).OfType<UseCommand>().FirstOrDefault();
            Assert.IsNotNull(useCmd);
            Assert.AreEqual("Adventure Works 2022", useCmd.DatabaseName);
        }

        [TestMethod]
        public void UseQuotedDatabaseStillWorks()
        {
            // The quoted form must keep working and the surrounding quotes must be stripped.
            var input = "--> USE \"AW Internet Sales\"\nEVALUATE { 1 }\n";

            var result = DaxStudio.Parsers.PreProcessor.AntlrPreProcessor.Parse(input);
            Assert.IsFalse(result.HasErrors);

            var useCmd = result.Batches.SelectMany(b => b.Commands).OfType<UseCommand>().FirstOrDefault();
            Assert.IsNotNull(useCmd);
            Assert.AreEqual("AW Internet Sales", useCmd.DatabaseName);
        }

        [TestMethod]
        public void ConnectPbixUnquotedMultiWordReportName()
        {
            // Power BI Desktop report names frequently contain spaces; the unquoted form must capture
            // the whole name rather than only the first word.
            var input = "--> CONNECT PBIX My Sales Report\nEVALUATE { 1 }\n";

            var result = DaxStudio.Parsers.PreProcessor.AntlrPreProcessor.Parse(input);
            Assert.IsFalse(result.HasErrors);

            var connCmd = result.Batches.SelectMany(b => b.Commands).OfType<ConnectCommand>().FirstOrDefault();
            Assert.IsNotNull(connCmd);
            Assert.AreEqual(ConnectionType.PBIX, connCmd.ConnectionType);
            Assert.AreEqual("My Sales Report", connCmd.InstanceName);
        }

        [TestMethod]
        public void UseWithNoDatabase_SurfacesHelpfulCommandError()
        {
            var input = "--> USE\nEVALUATE { 1 }\n";
            var result = DaxStudio.Parsers.PreProcessor.AntlrPreProcessor.Parse(input);

            Assert.IsTrue(result.HasCommandErrors, "a USE with no database name must be reported as a command error");
            var err = result.CommandErrors.First();
            StringAssert.Contains(err.Msg, "USE command");
            Assert.AreEqual(1, err.Line, "the error should point at the command line");
        }

        [TestMethod]
        public void TraceWithInvalidType_SurfacesHelpfulCommandError()
        {
            var input = "--> TRACE BADTYPE ON\nEVALUATE { 1 }\n";
            var result = DaxStudio.Parsers.PreProcessor.AntlrPreProcessor.Parse(input);

            Assert.IsTrue(result.HasCommandErrors);
            StringAssert.Contains(result.CommandErrors.First().Msg, "not a valid TraceType");
        }

        [TestMethod]
        public void TraceMissingOnOff_DoesNotThrow_SurfacesHelpfulCommandError()
        {
            // Regression: a TRACE with no ON/OFF flag used to crash the walker with an
            // ArgumentOutOfRangeException that was swallowed and replaced with a vague warning.
            var input = "--> TRACE SERVERTIMINGS\nEVALUATE { 1 }\n";
            var result = DaxStudio.Parsers.PreProcessor.AntlrPreProcessor.Parse(input);

            Assert.IsTrue(result.HasCommandErrors);
            StringAssert.Contains(result.CommandErrors.First().Msg, "TRACE");
        }

        [TestMethod]
        public void ConnectWrongArguments_SurfacesHelpfulCommandError()
        {
            var input = "--> CONNECT XX localhost\nEVALUATE { 1 }\n";
            var result = DaxStudio.Parsers.PreProcessor.AntlrPreProcessor.Parse(input);

            Assert.IsTrue(result.HasCommandErrors);
            StringAssert.Contains(result.CommandErrors.First().Msg, "CONNECT command");
        }

        [TestMethod]
        public void UnknownCommand_SurfacesCommandError()
        {
            var input = "--> BOGUS foo\nEVALUATE { 1 }\n";
            var result = DaxStudio.Parsers.PreProcessor.AntlrPreProcessor.Parse(input);

            Assert.IsTrue(result.HasCommandErrors, "an unrecognised command must be reported as a command error");
        }

        [TestMethod]
        public void ValidCommands_HaveNoCommandErrors()
        {
            var input = "--> TRACE SERVERTIMINGS ON\n--> USE Adventure Works\nEVALUATE { 1 }\n";
            var result = DaxStudio.Parsers.PreProcessor.AntlrPreProcessor.Parse(input);

            Assert.IsFalse(result.HasCommandErrors, "valid commands must not be reported as errors");
            Assert.IsFalse(result.HasErrors);
        }

        [TestMethod]
        public void DaxBodySyntaxError_IsNotPromotedToCommandError()
        {
            // A parser error on a DAX-body line (not a "-->" command line) must remain a soft error so
            // the classic pre-processor fallback still applies - it must NOT become a hard command error.
            var input = "--> USE Adventure Works\n@@@ not valid dax @@@\n";
            var result = DaxStudio.Parsers.PreProcessor.AntlrPreProcessor.Parse(input);

            Assert.IsFalse(result.HasCommandErrors,
                "a syntax error on a DAX-body line must not be treated as a comment-script command error");
        }
    }
}
