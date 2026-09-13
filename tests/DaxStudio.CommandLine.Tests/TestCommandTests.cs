using System;
using System.IO;
using System.Linq;
using DaxStudio.CommandLine.Commands;
using DaxStudio.Core.Assertions;
using DaxStudio.Parsers.CommentScript;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Spectre.Console.Cli;
using DaxCmdTestCommand = DaxStudio.CommandLine.Commands.TestCommand;

namespace DaxStudio.CommandLine.Tests
{
    [TestClass]
    public class TestCommandTests
    {
        [TestMethod]
        public void Validate_RequiresFileOrQuery()
        {
            var cmd = new DaxCmdTestCommand();
            var settings = new DaxCmdTestCommand.Settings();

            var result = cmd.ValidatePublic(null, settings);

            Assert.IsFalse(result.Successful);
            Assert.IsTrue(result.Message.Contains("either a --file or --query"));
        }

        [TestMethod]
        public void Validate_RejectsBothFileAndQuery()
        {
            var cmd = new DaxCmdTestCommand();
            var settings = new DaxCmdTestCommand.Settings
            {
                File = "test.dax",
                Query = "EVALUATE {1}"
            };

            var result = cmd.ValidatePublic(null, settings);

            Assert.IsFalse(result.Successful);
            Assert.IsTrue(result.Message.Contains("cannot specify both"));
        }

        [TestMethod]
        public void Validate_ValidWithOnlyFile()
        {
            var cmd = new DaxCmdTestCommand();
            var settings = new DaxCmdTestCommand.Settings
            {
                File = "test.dax",
                Server = "localhost",
                Database = "Model"
            };

            var result = cmd.ValidatePublic(null, settings);

            Assert.IsTrue(result.Successful);
        }

        [TestMethod]
        public void WriteTestReports_CommandLineOverridesEmbeddedDirectives()
        {
            var tempDir = Path.Combine(Path.GetTempPath(), "TestCommandOverride_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempDir);
            try
            {
                var cliReport = Path.Combine(tempDir, "cli.json");
                var embeddedReport = Path.Combine(tempDir, "embedded.xml");

                var results = new[]
                {
                    new DaxStudio.Core.Assertions.TestResult
                    {
                        TestName = "T1", Kind = AssertionKind.RowCount, Description = "ROWCOUNT = 1",
                        Expected = "1", Actual = "1", Outcome = TestOutcome.Passed
                    }
                };

                var reportCmds = new[]
                {
                    new ExportCommand(TestReportFormat.Junit, embeddedReport)
                };

                FileCommand.WriteTestReports(results, cliReport, reportCmds, tempDir, "script.dax");

                Assert.IsTrue(File.Exists(cliReport), "CLI report should be written.");
                Assert.IsFalse(File.Exists(embeddedReport), "Embedded report should be ignored when CLI report is supplied.");
            }
            finally
            {
                if (Directory.Exists(tempDir))
                {
                    try { Directory.Delete(tempDir, true); } catch { }
                }
            }
        }

        [TestMethod]
        public void WriteTestReports_UsesEmbeddedDirectivesWhenNoCliReport()
        {
            var tempDir = Path.Combine(Path.GetTempPath(), "TestCommandEmbedded_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempDir);
            try
            {
                var embeddedReport = Path.Combine(tempDir, "embedded.xml");

                var results = new[]
                {
                    new DaxStudio.Core.Assertions.TestResult
                    {
                        TestName = "T1", Kind = AssertionKind.RowCount, Description = "ROWCOUNT = 1",
                        Expected = "1", Actual = "1", Outcome = TestOutcome.Passed
                    }
                };

                var reportCmds = new[]
                {
                    new ExportCommand(TestReportFormat.Junit, embeddedReport)
                };

                FileCommand.WriteTestReports(results, null, reportCmds, tempDir, "script.dax");

                Assert.IsTrue(File.Exists(embeddedReport), "Embedded report should be written when no CLI report is specified.");
            }
            finally
            {
                if (Directory.Exists(tempDir))
                {
                    try { Directory.Delete(tempDir, true); } catch { }
                }
            }
        }
    }
}
