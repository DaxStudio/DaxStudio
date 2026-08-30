using Microsoft.VisualStudio.TestTools.UnitTesting;
using DaxStudio.CommandLine.Commands;
using DaxStudio.CommandLine.Helpers;
using DaxStudio.Core.Events;
using DaxStudio.Interfaces;
using Spectre.Console.Cli;
using System;
using System.Threading;
#if NET8_0_OR_GREATER
using AccessToken = Microsoft.AnalysisServices.AccessToken;
#else
using AccessToken = Microsoft.AnalysisServices.AdomdClient.AccessToken;
#endif

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
            var settings = new BenchmarkCommand.Settings
            {
                Server = "localhost",
                Database = "Adventure Works",
                OutputFile = "c:\\temp\\results.csv"
            };

            var result = settings.Validate();
            Assert.IsTrue(result.Successful, result.Message);
        }

        [TestMethod]
        public void Benchmark_settings_with_connectionstring_should_succeed()
        {
            var settings = new BenchmarkCommand.Settings
            {
                ConnectionString = "Data Source=localhost;Initial Catalog=Adventure Works",
                OutputFile = "c:\\temp\\results.csv"
            };

            var result = settings.Validate();
            Assert.IsTrue(result.Successful, result.Message);
        }

        [TestMethod]
        public void Benchmark_settings_only_servername_should_fail()
        {
            var settings = new BenchmarkCommand.Settings
            {
                Server = "localhost",
                OutputFile = "c:\\temp\\results.csv"
            };

            var result = settings.Validate();
            Assert.IsFalse(result.Successful);
            Assert.AreEqual("You must specify a <database> when using the <server> parameter and not connecting to a .pbix/.pbip file", result.Message);
        }

        [TestMethod]
        public void Benchmark_settings_server_with_connectionstring_should_fail()
        {
            var settings = new BenchmarkCommand.Settings
            {
                Server = "localhost",
                ConnectionString = "Data Source=localhost",
                OutputFile = "c:\\temp\\results.csv"
            };

            var result = settings.Validate();
            Assert.IsFalse(result.Successful);
            Assert.AreEqual("You cannot specify a <Server> or <Database> when passing a <ConnectionString>", result.Message);
        }

        [TestMethod]
        public void Benchmark_settings_only_database_should_fail()
        {
            var settings = new BenchmarkCommand.Settings
            {
                Database = "Adventure Works",
                OutputFile = "c:\\temp\\results.csv"
            };

            var result = settings.Validate();
            Assert.IsFalse(result.Successful);
            Assert.AreEqual("You must specify a <server> when using the <database> parameter", result.Message);
        }

        [TestMethod]
        public void Benchmark_validate_requires_file_or_query()
        {
            var settings = new BenchmarkCommand.Settings
            {
                Server = "localhost",
                Database = "Adventure Works",
                OutputFile = "c:\\temp\\results.csv"
            };

            var result = ((ICommand)CreateCommand()).Validate(null, settings);
            Assert.IsFalse(result.Successful);
            Assert.AreEqual("You must specify either a --file or --query option", result.Message);
        }

        [TestMethod]
        public void Benchmark_validate_rejects_file_and_query_together()
        {
            var settings = new BenchmarkCommand.Settings
            {
                Server = "localhost",
                Database = "Adventure Works",
                OutputFile = "c:\\temp\\results.csv",
                File = "query.dax",
                Query = "EVALUATE ROW(\"x\", 1)"
            };

            var result = ((ICommand)CreateCommand()).Validate(null, settings);
            Assert.IsFalse(result.Successful);
            Assert.AreEqual("You cannot specify both --file and --query", result.Message);
        }

        [TestMethod]
        public void Benchmark_validate_rejects_both_run_counts_zero()
        {
            var settings = new BenchmarkCommand.Settings
            {
                Server = "localhost",
                Database = "Adventure Works",
                OutputFile = "c:\\temp\\results.csv",
                Query = "EVALUATE ROW(\"x\", 1)",
                ColdRuns = 0,
                WarmRuns = 0
            };

            var result = ((ICommand)CreateCommand()).Validate(null, settings);
            Assert.IsFalse(result.Successful);
            Assert.AreEqual("You must run at least one cold or warm iteration", result.Message);
        }

        [TestMethod]
        public void Benchmark_validate_rejects_negative_cold()
        {
            var settings = new BenchmarkCommand.Settings
            {
                Server = "localhost",
                Database = "Adventure Works",
                OutputFile = "c:\\temp\\results.csv",
                Query = "EVALUATE ROW(\"x\", 1)",
                ColdRuns = -1
            };

            var result = ((ICommand)CreateCommand()).Validate(null, settings);
            Assert.IsFalse(result.Successful);
            Assert.AreEqual("--cold must be >= 0", result.Message);
        }

        [TestMethod]
        public void Benchmark_validate_rejects_negative_warm()
        {
            var settings = new BenchmarkCommand.Settings
            {
                Server = "localhost",
                Database = "Adventure Works",
                OutputFile = "c:\\temp\\results.csv",
                Query = "EVALUATE ROW(\"x\", 1)",
                WarmRuns = -1
            };

            var result = ((ICommand)CreateCommand()).Validate(null, settings);
            Assert.IsFalse(result.Successful);
            Assert.AreEqual("--warm must be >= 0", result.Message);
        }

        [TestMethod]
        public void Benchmark_connection_events_reuse_the_same_access_token()
        {
            var settings = new BenchmarkCommand.Settings
            {
                Database = "Adventure Works",
                PowerBIFileName = "model.pbix"
            };
            var accessToken = new AccessToken(
                "access-token", DateTimeOffset.UtcNow.AddHours(1), null);

            var queryEvent = BenchmarkCommand.CreateConnectEvent(
                settings, "Data Source=powerbi://example;Roles=Sales", "DAX Studio Command Line", accessToken);
            var adminEvent = BenchmarkCommand.CreateConnectEvent(
                settings, "Data Source=powerbi://example", "DAX Studio Command Line (admin)", accessToken);

            Assert.AreEqual(accessToken.Token, queryEvent.AccessToken.Token);
            Assert.AreEqual(accessToken.ExpirationTime, queryEvent.AccessToken.ExpirationTime);
            Assert.AreEqual(accessToken.UserContext, queryEvent.AccessToken.UserContext);
            Assert.AreEqual(accessToken.Token, adminEvent.AccessToken.Token);
            Assert.AreEqual(accessToken.ExpirationTime, adminEvent.AccessToken.ExpirationTime);
            Assert.AreEqual(accessToken.UserContext, adminEvent.AccessToken.UserContext);
        }

        [TestMethod]
        public void Benchmark_service_principal_certificate_does_not_require_access_token()
        {
            const string connectionString =
                "Data Source=powerbi://api.powerbi.com/v1.0/myorg/workspace;" +
                "User ID=app:client-id@tenant-id;Password=cert:thumbprint";

            Assert.IsFalse(AccessTokenHelper.IsAccessTokenNeeded(connectionString));
        }

        [TestMethod]
        public void Benchmark_server_timings_handler_returns_processed_snapshot()
        {
            using var handler = new BenchmarkCommand.ServerTimingsHandler(new Caliburn.Micro.EventAggregator());
            var timings = new ServerTimingsEvent(new TestServerTimes
            {
                TotalDuration = 700,
                FormulaEngineDuration = 300,
                StorageEngineDuration = 400,
                StorageEngineQueryCount = 5,
                StorageEngineCpu = 350,
                TotalCpuDuration = 650,
                VertipaqCacheMatches = 2
            });

            handler.HandleAsync(timings, CancellationToken.None).GetAwaiter().GetResult();

            Assert.AreSame(timings, handler.Wait(0));
            Assert.IsInstanceOfType<TestServerTimes>(timings.Source);
        }

        [TestMethod]
        public void Benchmark_server_timings_handler_reset_discards_previous_snapshot()
        {
            using var handler = new BenchmarkCommand.ServerTimingsHandler(new Caliburn.Micro.EventAggregator());
            var timings = new ServerTimingsEvent(new TestServerTimes { TotalDuration = 700 });
            handler.HandleAsync(timings, CancellationToken.None).GetAwaiter().GetResult();

            handler.Reset();

            Assert.IsNull(handler.Wait(0));
        }

        private sealed class TestServerTimes : IServerTimes
        {
            public long FormulaEngineDuration { get; set; }
            public long StorageEngineCpu { get; set; }
            public double StorageEngineCpuFactor { get; set; }
            public long StorageEngineDuration { get; set; }
            public long StorageEngineQueryCount { get; set; }
            public long TotalDirectQueryDuration { get; set; }
            public long TotalCpuDuration { get; set; }
            public double TotalCpuFactor { get; set; }
            public long TotalDuration { get; set; }
            public int VertipaqCacheMatches { get; set; }
        }
    }
}
