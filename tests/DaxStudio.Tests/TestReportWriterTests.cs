using System;
using System.Collections.Generic;
using System.IO;
using System.Xml.Linq;
using DaxStudio.Core.Assertions;
using DaxStudio.Parsers.CommentScript;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Newtonsoft.Json.Linq;
using DaxTestResult = DaxStudio.Core.Assertions.TestResult;

namespace DaxStudio.Tests
{
    [TestClass]
    public class TestReportWriterTests
    {
        private string _tempDir;

        [TestInitialize]
        public void Setup()
        {
            _tempDir = Path.Combine(Path.GetTempPath(), "DaxStudioReportWriterTests_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_tempDir);
        }

        [TestCleanup]
        public void Cleanup()
        {
            if (Directory.Exists(_tempDir))
            {
                try { Directory.Delete(_tempDir, true); } catch { }
            }
        }

        [TestMethod]
        public void GetFormatFromFileName_ValidExtensions()
        {
            Assert.AreEqual(TestReportFormat.Json, TestReportWriter.GetFormatFromFileName("report.json"));
            Assert.AreEqual(TestReportFormat.Junit, TestReportWriter.GetFormatFromFileName("report.xml"));
            Assert.AreEqual(TestReportFormat.Trx, TestReportWriter.GetFormatFromFileName("report.trx"));
        }

        [TestMethod]
        public void GetFormatFromFileName_InvalidExtension_Throws()
        {
            try
            {
                TestReportWriter.GetFormatFromFileName("report.csv");
                Assert.Fail("Expected ArgumentException was not thrown.");
            }
            catch (ArgumentException) { }
        }

        [TestMethod]
        public void Write_JsonReport_ValidStructure()
        {
            var filePath = Path.Combine(_tempDir, "results.json");
            IReadOnlyList<DaxTestResult> results = new[]
            {
                new DaxTestResult { TestName = "Test1", Kind = AssertionKind.RowCount, Description = "ROWCOUNT = 10", Expected = "10", Actual = "10", Outcome = TestOutcome.Passed, Line = 5 },
                new DaxTestResult { TestName = "Test2", Kind = AssertionKind.Performance, Description = "DURATION <= 1000", Expected = "<= 1000", Actual = "1500", Outcome = TestOutcome.Failed, Message = "Duration exceeded threshold", Line = 12 }
            };

            TestReportWriter.Write(results, TestReportFormat.Json, filePath, "my_script.dax");

            Assert.IsTrue(File.Exists(filePath));
            var jsonText = File.ReadAllText(filePath);
            var root = JObject.Parse(jsonText);

            Assert.AreEqual(1, root["schemaVersion"]?.Value<int>());
            Assert.AreEqual("my_script.dax", root["source"]?.Value<string>());
            Assert.AreEqual(2, root["summary"]?["total"]?.Value<int>());
            Assert.AreEqual(1, root["summary"]?["passed"]?.Value<int>());
            Assert.AreEqual(1, root["summary"]?["failed"]?.Value<int>());

            var jsonResults = root["results"] as JArray;
            Assert.IsNotNull(jsonResults);
            Assert.AreEqual(2, jsonResults.Count);
            Assert.AreEqual("Test1", jsonResults[0]["testName"]?.Value<string>());
            Assert.AreEqual("Passed", jsonResults[0]["outcome"]?.Value<string>());
            Assert.AreEqual("Failed", jsonResults[1]["outcome"]?.Value<string>());
        }

        [TestMethod]
        public void Write_JunitReport_ValidXml()
        {
            var filePath = Path.Combine(_tempDir, "results.xml");
            IReadOnlyList<DaxTestResult> results = new[]
            {
                new DaxTestResult { TestName = "Suite1", Kind = AssertionKind.RowCount, Description = "ROWCOUNT = 5", Expected = "5", Actual = "5", Outcome = TestOutcome.Passed, Line = 2 },
                new DaxTestResult { TestName = "Suite1", Kind = AssertionKind.Table, Description = "TABLE Baseline", Expected = "Match", Actual = "Mismatch", Outcome = TestOutcome.Failed, Message = "Row 2 col 1 mismatch", Line = 8 }
            };

            TestReportWriter.Write(results, TestReportFormat.Junit, filePath, "my_script.dax");

            Assert.IsTrue(File.Exists(filePath));
            var doc = XDocument.Load(filePath);
            var suite = doc.Root?.Element("testsuite");

            Assert.IsNotNull(suite);
            Assert.AreEqual("2", suite.Attribute("tests")?.Value);
            Assert.AreEqual("1", suite.Attribute("failures")?.Value);
            Assert.AreEqual("0", suite.Attribute("errors")?.Value);

            var testcases = suite.Elements("testcase");
            Assert.AreEqual(2, System.Linq.Enumerable.Count(testcases));
        }

        [TestMethod]
        public void Write_TrxReport_ValidXml()
        {
            var filePath = Path.Combine(_tempDir, "results.trx");
            IReadOnlyList<DaxTestResult> results = new[]
            {
                new DaxTestResult { TestName = "PerfCheck", Kind = AssertionKind.Performance, Description = "SE_QUERIES <= 2", Expected = "<= 2", Actual = "1", Outcome = TestOutcome.Passed, Line = 15 }
            };

            TestReportWriter.Write(results, TestReportFormat.Trx, filePath, "perf.dax");

            Assert.IsTrue(File.Exists(filePath));
            var doc = XDocument.Load(filePath);
            XNamespace ns = "http://microsoft.com/schemas/VisualStudio/TeamTest/2010";

            var summary = doc.Root?.Element(ns + "ResultSummary");
            Assert.IsNotNull(summary);
            Assert.AreEqual("Passed", summary.Attribute("outcome")?.Value);
        }

        [TestMethod]
        public void Write_PendingResults_Throws()
        {
            var filePath = Path.Combine(_tempDir, "pending.json");
            IReadOnlyList<DaxTestResult> results = new[]
            {
                new DaxTestResult { Kind = AssertionKind.RowCount, Outcome = TestOutcome.Pending }
            };

            try
            {
                TestReportWriter.Write(results, TestReportFormat.Json, filePath);
                Assert.Fail("Expected InvalidOperationException was not thrown.");
            }
            catch (InvalidOperationException) { }
        }

        [TestMethod]
        public void Write_FormatExtensionMismatch_Throws()
        {
            var filePath = Path.Combine(_tempDir, "results.xml");
            IReadOnlyList<DaxTestResult> results = new[]
            {
                new DaxTestResult { Kind = AssertionKind.RowCount, Outcome = TestOutcome.Passed }
            };

            try
            {
                TestReportWriter.Write(results, TestReportFormat.Json, filePath);
                Assert.Fail("Expected ArgumentException was not thrown.");
            }
            catch (ArgumentException) { }
        }
    }
}
