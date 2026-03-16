using Microsoft.VisualStudio.TestTools.UnitTesting;
using DaxStudio.CommandLine.Commands;

namespace DaxStudio.CommandLine.Tests
{
    [TestClass]
    public class BenchmarkCommandTests
    {
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
    }
}
