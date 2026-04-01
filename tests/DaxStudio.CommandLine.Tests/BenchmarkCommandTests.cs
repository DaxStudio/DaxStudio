using Microsoft.VisualStudio.TestTools.UnitTesting;
using DaxStudio.CommandLine.Commands;

namespace DaxStudio.CommandLine.Tests
{
    [TestClass]
    public class BenchmarkCommandTests
    {
    private static BenchmarkCommand CreateCommand() => new BenchmarkCommand(null, null);

        // Tests validate the Settings class directly (same pattern as
        // CommandLineParameterTests). BenchmarkCommand constructor requires
        // DI-injected IEventAggregator/IGlobalOptions, so we test Settings.Validate()
        // which tests the connection string validation inherited from CommandSettingsRawBase.

        [TestMethod]
        public void Benchmark_settings_with_server_database_should_succeed()
        {
            var settings = new BenchmarkCommand.Settings();
            settings.Server = "localhost";
            settings.Database = "Adventure Works";
            settings.OutputFile = "c:\\temp\\results.csv";

            var result = settings.Validate();
            Assert.IsTrue(result.Successful, result.Message);
        }

        [TestMethod]
        public void Benchmark_settings_with_connectionstring_should_succeed()
        {
            var settings = new BenchmarkCommand.Settings();
            settings.ConnectionString = "Data Source=localhost;Initial Catalog=Adventure Works";
            settings.OutputFile = "c:\\temp\\results.csv";

            var result = settings.Validate();
            Assert.IsTrue(result.Successful, result.Message);
        }

        [TestMethod]
        public void Benchmark_settings_only_servername_should_fail()
        {
            var settings = new BenchmarkCommand.Settings();
            settings.Server = "localhost";
            settings.OutputFile = "c:\\temp\\results.csv";

            var result = settings.Validate();
            Assert.IsFalse(result.Successful);
            Assert.AreEqual("You must specify a <database> when using the <server> parameter and not connecting to a .pbix/.pbip file", result.Message);
        }

        [TestMethod]
        public void Benchmark_settings_server_with_connectionstring_should_fail()
        {
            var settings = new BenchmarkCommand.Settings();
            settings.Server = "localhost";
            settings.ConnectionString = "Data Source=localhost";
            settings.OutputFile = "c:\\temp\\results.csv";

            var result = settings.Validate();
            Assert.IsFalse(result.Successful);
            Assert.AreEqual("You cannot specify a <Server> or <Database> when passing a <ConnectionString>", result.Message);
        }

        [TestMethod]
        public void Benchmark_settings_only_database_should_fail()
        {
            var settings = new BenchmarkCommand.Settings();
            settings.Database = "Adventure Works";
            settings.OutputFile = "c:\\temp\\results.csv";

            var result = settings.Validate();
            Assert.IsFalse(result.Successful);
            Assert.AreEqual("You must specify a <server> when using the <database> parameter", result.Message);
        }

        [TestMethod]
        public void Benchmark_validate_requires_file_or_query()
        {
            var settings = new BenchmarkCommand.Settings();
            settings.Server = "localhost";
            settings.Database = "Adventure Works";
            settings.OutputFile = "c:\\temp\\results.csv";

            var result = CreateCommand().Validate(null, settings);
            Assert.IsFalse(result.Successful);
            Assert.AreEqual("You must specify either a --file or --query option", result.Message);
        }

        [TestMethod]
        public void Benchmark_validate_rejects_file_and_query_together()
        {
            var settings = new BenchmarkCommand.Settings();
            settings.Server = "localhost";
            settings.Database = "Adventure Works";
            settings.OutputFile = "c:\\temp\\results.csv";
            settings.File = "query.dax";
            settings.Query = "EVALUATE ROW(\"x\", 1)";

            var result = CreateCommand().Validate(null, settings);
            Assert.IsFalse(result.Successful);
            Assert.AreEqual("You cannot specify both --file and --query", result.Message);
        }

        [TestMethod]
        public void Benchmark_validate_rejects_both_run_counts_zero()
        {
            var settings = new BenchmarkCommand.Settings();
            settings.Server = "localhost";
            settings.Database = "Adventure Works";
            settings.OutputFile = "c:\\temp\\results.csv";
            settings.Query = "EVALUATE ROW(\"x\", 1)";
            settings.ColdRuns = 0;
            settings.WarmRuns = 0;

            var result = CreateCommand().Validate(null, settings);
            Assert.IsFalse(result.Successful);
            Assert.AreEqual("You must run at least one cold or warm iteration", result.Message);
        }

        [TestMethod]
        public void Benchmark_validate_rejects_negative_cold()
        {
            var settings = new BenchmarkCommand.Settings();
            settings.Server = "localhost";
            settings.Database = "Adventure Works";
            settings.OutputFile = "c:\\temp\\results.csv";
            settings.Query = "EVALUATE ROW(\"x\", 1)";
            settings.ColdRuns = -1;

            var result = CreateCommand().Validate(null, settings);
            Assert.IsFalse(result.Successful);
            Assert.AreEqual("--cold must be >= 0", result.Message);
        }

        [TestMethod]
        public void Benchmark_validate_rejects_negative_warm()
        {
            var settings = new BenchmarkCommand.Settings();
            settings.Server = "localhost";
            settings.Database = "Adventure Works";
            settings.OutputFile = "c:\\temp\\results.csv";
            settings.Query = "EVALUATE ROW(\"x\", 1)";
            settings.WarmRuns = -1;

            var result = CreateCommand().Validate(null, settings);
            Assert.IsFalse(result.Successful);
            Assert.AreEqual("--warm must be >= 0", result.Message);
        }
    }
}
