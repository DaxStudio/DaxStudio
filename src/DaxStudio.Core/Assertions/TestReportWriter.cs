using DaxStudio.Parsers.CommentScript;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml.Linq;

namespace DaxStudio.Core.Assertions
{
    public static class TestReportWriter
    {
        public static void Write(IReadOnlyList<TestResult> results, TestReportFormat format, string fileName, string sourceName = null)
        {
            if (results == null || results.Count == 0)
                throw new InvalidOperationException("A test report requires at least one assertion result.");
            if (results.Any(r => r == null || r.Outcome == TestOutcome.Pending || r.Outcome == TestOutcome.Running))
                throw new InvalidOperationException("A test report can only be written after all assertions have completed.");
            if (string.IsNullOrWhiteSpace(fileName))
                throw new ArgumentException("A test report file name is required.", nameof(fileName));
            if (GetFormatFromFileName(fileName) != format)
                throw new ArgumentException("The test report format does not match the file extension.", nameof(fileName));

            var report = format switch
            {
                TestReportFormat.Json => WriteJson(results, sourceName),
                TestReportFormat.Junit => WriteJunit(results, sourceName),
                TestReportFormat.Trx => WriteTrx(results, sourceName),
                _ => throw new ArgumentOutOfRangeException(nameof(format), format, "Unsupported test report format."),
            };

            var directory = Path.GetDirectoryName(Path.GetFullPath(fileName));
            if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);
            File.WriteAllText(fileName, report, new UTF8Encoding(false));
        }

        public static TestReportFormat GetFormatFromFileName(string fileName)
        {
            switch (Path.GetExtension(fileName)?.ToLowerInvariant())
            {
                case ".json": return TestReportFormat.Json;
                case ".xml": return TestReportFormat.Junit;
                case ".trx": return TestReportFormat.Trx;
                default: throw new ArgumentException("Test report files must use the .json, .xml, or .trx extension.", nameof(fileName));
            }
        }

        private static string WriteJson(IReadOnlyList<TestResult> results, string sourceName)
        {
            return JsonConvert.SerializeObject(new
            {
                schemaVersion = 1,
                generatedAt = DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture),
                source = sourceName,
                summary = Summary(results),
                results = results.Select(r => new
                {
                    testName = r.TestName,
                    kind = r.Kind.ToString(),
                    description = r.Description,
                    expected = r.Expected,
                    actual = r.Actual,
                    outcome = r.Outcome.ToString(),
                    message = r.Message,
                    line = r.Line,
                    batchIndex = r.BatchIndex,
                }),
            }, Formatting.Indented);
        }

        private static string WriteJunit(IReadOnlyList<TestResult> results, string sourceName)
        {
            var failures = results.Count(r => r.Outcome == TestOutcome.Failed);
            var errors = results.Count(r => r.Outcome == TestOutcome.Error);
            var suite = new XElement("testsuite",
                new XAttribute("name", sourceName ?? "DAX Studio Assertions"),
                new XAttribute("tests", results.Count),
                new XAttribute("failures", failures),
                new XAttribute("errors", errors),
                new XAttribute("timestamp", DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture)));

            foreach (var result in results)
            {
                var testCase = new XElement("testcase",
                    new XAttribute("name", TestCaseName(result)),
                    new XAttribute("classname", sourceName ?? "DAX Studio"));
                testCase.Add(new XElement("properties",
                    Property("kind", result.Kind.ToString()),
                    Property("expected", result.Expected),
                    Property("actual", result.Actual),
                    Property("line", result.Line.ToString(CultureInfo.InvariantCulture)),
                    Property("batchIndex", result.BatchIndex.ToString(CultureInfo.InvariantCulture))));
                if (result.Outcome == TestOutcome.Failed)
                    testCase.Add(new XElement("failure", new XAttribute("message", result.Message ?? "Assertion failed."), Details(result)));
                else if (result.Outcome == TestOutcome.Error)
                    testCase.Add(new XElement("error", new XAttribute("message", result.Message ?? "Assertion evaluation failed."), Details(result)));
                suite.Add(testCase);
            }

            return new XDocument(new XDeclaration("1.0", "utf-8", null), new XElement("testsuites", suite)).ToString();
        }

        private static string WriteTrx(IReadOnlyList<TestResult> results, string sourceName)
        {
            XNamespace ns = "http://microsoft.com/schemas/VisualStudio/TeamTest/2010";
            var runId = Guid.NewGuid();
            var now = DateTime.UtcNow;
            var definitions = new XElement(ns + "TestDefinitions");
            var entries = new XElement(ns + "TestEntries");
            var testResults = new XElement(ns + "Results");
            foreach (var result in results)
            {
                var testId = Guid.NewGuid();
                var executionId = Guid.NewGuid();
                var testName = TestCaseName(result);
                definitions.Add(new XElement(ns + "UnitTest",
                    new XAttribute("name", testName), new XAttribute("id", testId),
                    new XElement(ns + "Execution", new XAttribute("id", executionId)),
                    new XElement(ns + "TestMethod", new XAttribute("codeBase", sourceName ?? string.Empty), new XAttribute("className", "DaxStudio.Assertions"), new XAttribute("name", testName))));
                entries.Add(new XElement(ns + "TestEntry", new XAttribute("testId", testId), new XAttribute("executionId", executionId), new XAttribute("testListId", "19431567-8539-422a-85d7-44ee4e166bda")));
                var outcome = result.Outcome == TestOutcome.Passed ? "Passed" : result.Outcome == TestOutcome.Failed ? "Failed" : "Error";
                var unitResult = new XElement(ns + "UnitTestResult",
                    new XAttribute("executionId", executionId), new XAttribute("testId", testId), new XAttribute("testName", testName),
                    new XAttribute("computerName", Environment.MachineName), new XAttribute("outcome", outcome),
                    new XAttribute("startTime", now.ToString("o", CultureInfo.InvariantCulture)), new XAttribute("endTime", now.ToString("o", CultureInfo.InvariantCulture)), new XAttribute("duration", "00:00:00"));
                if (result.Outcome != TestOutcome.Passed)
                    unitResult.Add(new XElement(ns + "Output", new XElement(ns + "ErrorInfo", new XElement(ns + "Message", result.Message ?? "Assertion failed."), new XElement(ns + "StackTrace", Details(result)))));
                testResults.Add(unitResult);
            }

            var failed = results.Count(r => r.Outcome == TestOutcome.Failed || r.Outcome == TestOutcome.Error);
            var document = new XDocument(new XDeclaration("1.0", "utf-8", null),
                new XElement(ns + "TestRun", new XAttribute("id", runId), new XAttribute("name", sourceName ?? "DAX Studio Assertions"),
                    new XElement(ns + "Times", new XAttribute("creation", now.ToString("o", CultureInfo.InvariantCulture)), new XAttribute("queuing", now.ToString("o", CultureInfo.InvariantCulture)), new XAttribute("start", now.ToString("o", CultureInfo.InvariantCulture)), new XAttribute("finish", now.ToString("o", CultureInfo.InvariantCulture))),
                    new XElement(ns + "TestSettings", new XAttribute("name", "DAX Studio"), new XAttribute("id", Guid.NewGuid())),
                    definitions, entries, testResults,
                    new XElement(ns + "TestLists", new XElement(ns + "TestList", new XAttribute("name", "Results Not in a List"), new XAttribute("id", "19431567-8539-422a-85d7-44ee4e166bda"))),
                    new XElement(ns + "ResultSummary", new XAttribute("outcome", failed == 0 ? "Passed" : "Failed"), new XElement(ns + "Counters", new XAttribute("total", results.Count), new XAttribute("executed", results.Count), new XAttribute("passed", results.Count - failed), new XAttribute("failed", failed), new XAttribute("error", results.Count(r => r.Outcome == TestOutcome.Error))))));
            return document.ToString();
        }

        private static object Summary(IReadOnlyList<TestResult> results) => new
        {
            total = results.Count,
            passed = results.Count(r => r.Outcome == TestOutcome.Passed),
            failed = results.Count(r => r.Outcome == TestOutcome.Failed),
            errors = results.Count(r => r.Outcome == TestOutcome.Error),
        };

        private static XElement Property(string name, string value) => new XElement("property", new XAttribute("name", name), new XAttribute("value", value ?? string.Empty));

        private static string TestCaseName(TestResult result) => string.IsNullOrWhiteSpace(result.TestName) ? result.Description ?? "Assertion" : result.TestName + ": " + (result.Description ?? "Assertion");

        private static string Details(TestResult result) => string.Format(CultureInfo.InvariantCulture, "Expected: {0}{1}Actual: {2}{1}Kind: {3}{1}Line: {4}", result.Expected, Environment.NewLine, result.Actual, result.Kind, result.Line);
    }
}