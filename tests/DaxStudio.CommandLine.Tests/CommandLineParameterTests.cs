using Microsoft.VisualStudio.TestTools.UnitTesting;
using DaxStudio.CommandLine.Commands;
using DaxStudio.CommandLine.Helpers;
using DaxStudio.CommandLine.Interfaces;
using DaxStudio.CommandLine.UIStubs;
using DaxStudio.CommandLine.Infrastructure;
using DaxStudio.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using Spectre.Console.Cli;
using System;
using System.Data.OleDb;
using System.Threading.Tasks;

namespace DaxStudio.CommandLine.Tests
{
    [TestClass]
    public class CommandLineParameterTests
    {
        [TestMethod]
        public void TestServerDatabaseNames()
        {
            var settings = new FileCommand.Settings
            {
                Server = "localhost",
                Database = "Adventure Works"
            };

            var validationResult = settings.Validate();
            Assert.IsTrue(validationResult.Successful, "Validation result should be successful");
        }

        [TestMethod]
        public void Using_only_servername_should_fail()
        {
            var settings = new FileCommand.Settings
            {
                Server = "localhost"
            };


            var validationResult = settings.Validate();
            Assert.IsFalse(validationResult.Successful, validationResult.Message);
            Assert.AreEqual("You must specify a <database> when using the <server> parameter and not connecting to a .pbix/.pbip file", validationResult.Message);
        }

        [TestMethod]
        public void Using_only_databasename_should_fail()
        {
            var settings = new FileCommand.Settings
            {
                Database = "Adventure Works"
            };


            var validationResult = settings.Validate();
            Assert.IsFalse(validationResult.Successful, validationResult.Message);
            Assert.AreEqual("You must specify a <server> when using the <database> parameter", validationResult.Message);
        }

        [TestMethod]
        public void Using_only_servername_with_connectionstring_should_fail()
        {
            var settings = new FileCommand.Settings
            {
                Server = "localhost",
                ConnectionString = "data source=localhost"
            };

            var validationResult = settings.Validate();
            Assert.IsFalse(validationResult.Successful, validationResult.Message);
            Assert.AreEqual("You cannot specify a <Server> or <Database> when passing a <ConnectionString>", validationResult.Message);
        }

        [TestMethod]
        public void Using_only_connectionstring_should_suceed()
        {
            var settings = new FileCommand.Settings
            {
                ConnectionString = "data source=localhost"
            };

            var validationResult = settings.Validate();
            Assert.IsTrue(validationResult.Successful, validationResult.Message);
            Assert.IsNull(validationResult.Message);
        }

        [TestMethod]
        public void Using_connectionstring_and_user_should_succeed()
        {
            var settings = new FileCommand.Settings
            {
                ConnectionString = "data source=localhost",
                UserID = "testUser",
                Password = "testPwd"
            };

            var validationResult = settings.Validate();
            Assert.IsTrue(validationResult.Successful, validationResult.Message);
            Assert.IsNull(validationResult.Message);
            Assert.AreEqual("Data Source=localhost;User ID=testUser;Password=testPwd", settings.FullConnectionString, "connection strings don't match");
        }

        [TestMethod]
        public void Access_token_with_server_should_succeed()
        {
            var settings = new AccessTokenCommand.Settings
            {
                Server = "asazure://australiasoutheast.asazure.windows.net/myserver",
                Database = "mydatabase"
            };
            var validationResult = settings.Validate();
            Assert.IsTrue(validationResult.Successful, validationResult.Message);
            Assert.IsNull(validationResult.Message);
        }

        [TestMethod]
        public void Non_interactive_routes_all_registered_commands_to_silent_only_authentication()
        {
            var settings = new ISettingsConnection[]
            {
                new ExportSqlCommand.Settings { NonInteractive = true },
                new ExportCsvCommand.Settings { NonInteractive = true },
                new ExportParquetCommand.Settings { NonInteractive = true },
                new FileCommand.Settings { NonInteractive = true },
                new XlsxCommand.Settings { NonInteractive = true },
                new VpaxCommand.Settings { NonInteractive = true },
                new AccessTokenCommand.Settings { NonInteractive = true },
                new AuthCommand.Settings { NonInteractive = true },
                new BenchmarkCommand.Settings { NonInteractive = true },
                new CustomTraceCommand.Settings { NonInteractive = true },
            };

            foreach (var commandSettings in settings)
            {
                Assert.IsTrue(
                    AccessTokenHelper.UsesSilentOnlyAuthentication(commandSettings),
                    commandSettings.GetType().Name);
            }
        }

        [TestMethod]
        public void Default_routes_all_registered_commands_to_interactive_fallback_authentication()
        {
            var settings = new ISettingsConnection[]
            {
                new ExportSqlCommand.Settings(),
                new ExportCsvCommand.Settings(),
                new ExportParquetCommand.Settings(),
                new FileCommand.Settings(),
                new XlsxCommand.Settings(),
                new VpaxCommand.Settings(),
                new AccessTokenCommand.Settings(),
                new AuthCommand.Settings(),
                new BenchmarkCommand.Settings(),
                new CustomTraceCommand.Settings(),
            };

            foreach (var commandSettings in settings)
            {
                Assert.IsFalse(
                    AccessTokenHelper.UsesSilentOnlyAuthentication(commandSettings),
                    commandSettings.GetType().Name);
            }
        }

        [TestMethod]
        public void Cli_account_selection_does_not_force_an_interactive_prompt()
        {
            var options = new HaveLastUsedUPNStub();

            Assert.AreEqual(string.Empty, options.LastUsedUPN);
        }

        [TestMethod]
        public void Query_runner_initializes_and_persists_cli_account_selection()
        {
            var settings = new FileCommand.Settings
            {
                Server = "localhost",
                Database = "Model"
            };
            var settingProvider = Substitute.For<ISettingProvider>();

            var runner = new QueryRunner(settings, settingProvider);
            runner.Options.LastUsedUPN = "selected@example.com";

            settingProvider.Received(1).Initialize(runner.Options);
            settingProvider.Received(1).SetValue(
                nameof(runner.Options.LastUsedUPN),
                "selected@example.com",
                false,
                runner.Options,
                nameof(runner.Options.LastUsedUPN));
        }

        [TestMethod]
        public void Using_server_and_password_should_succeed()
        {
            var settings = new FileCommand.Settings
            {
                Server = "localhost",
                Database = "Adventure Works",
                Password = "testPwd"
            };


            var validationResult = settings.Validate();
            Assert.IsTrue(validationResult.Successful, validationResult.Message);
            Assert.IsNull(validationResult.Message);
        }

        [TestMethod]
        public void access_token_command_validation_should_succeed()
        {
            var settings = new AccessTokenCommand.Settings
            {
                Server = "asazure://australiasoutheast.asazure.windows.net/myserver",
                Database = "mydatabase"
            };
            var validationResult = settings.Validate();
            var accessTokenCommand = new AccessTokenCommand();
            var cmdValidationResult = ((ICommand)accessTokenCommand).Validate(null , settings);
            Assert.IsTrue(validationResult.Successful, validationResult.Message);
            Assert.IsTrue(cmdValidationResult.Successful, cmdValidationResult.Message);
            Assert.IsNull(validationResult.Message);
        }

        [TestMethod]
        public async Task Auth_command_is_registered()
        {
            var app = Program.CreateCommands(
                new TypeRegistrar(new ServiceCollection()));

            var exitCode = await app.RunAsync(new[] { "auth", "--help" });

            Assert.AreEqual(0, exitCode);
        }

        [TestMethod]
        public void Auth_command_accepts_server_without_database()
        {
            var settings = new AuthCommand.Settings
            {
                Server = "powerbi://api.powerbi.com/v1.0/myorg/workspace"
            };

            var validationResult = settings.Validate();

            Assert.IsTrue(validationResult.Successful, validationResult.Message);
            Assert.AreEqual(
                "powerbi://api.powerbi.com/v1.0/myorg/workspace",
                ParseValue(settings.FullConnectionString, "Data Source"));
        }

        [TestMethod]
        public void Auth_command_accepts_connection_string_without_database()
        {
            var settings = new AuthCommand.Settings
            {
                ConnectionString =
                    "Data Source=powerbi://api.powerbi.com/v1.0/myorg/workspace"
            };

            var validationResult = settings.Validate();

            Assert.IsTrue(validationResult.Successful, validationResult.Message);
        }

        [TestMethod]
        public void Auth_command_rejects_missing_source()
        {
            var validationResult = new AuthCommand.Settings().Validate();

            Assert.IsFalse(validationResult.Successful);
            Assert.AreEqual(
                "You must specify either <server> or <connectionstring>",
                validationResult.Message);
        }

        [TestMethod]
        public void Auth_command_rejects_server_and_connection_string()
        {
            var settings = new AuthCommand.Settings
            {
                Server = "powerbi://api.powerbi.com/v1.0/myorg/workspace",
                ConnectionString =
                    "Data Source=powerbi://api.powerbi.com/v1.0/myorg/workspace"
            };

            var validationResult = settings.Validate();

            Assert.IsFalse(validationResult.Successful);
            Assert.AreEqual(
                "You cannot specify both <server> and <connectionstring>",
                validationResult.Message);
        }

        [TestMethod]
        public void Auth_command_propagates_non_interactive_authentication()
        {
            var settings = new AuthCommand.Settings
            {
                Server = "powerbi://api.powerbi.com/v1.0/myorg/workspace",
                NonInteractive = true
            };

            Assert.IsTrue(
                AccessTokenHelper.UsesSilentOnlyAuthentication(settings));
        }

        [TestMethod]
        public void Auth_command_defaults_to_interactive_fallback_authentication()
        {
            var settings = new AuthCommand.Settings
            {
                Server = "powerbi://api.powerbi.com/v1.0/myorg/workspace"
            };

            Assert.IsFalse(
                AccessTokenHelper.UsesSilentOnlyAuthentication(settings));
        }

        [TestMethod]
        public void Auth_command_rejects_service_principal_connection_string()
        {
            var settings = new AuthCommand.Settings
            {
                ConnectionString =
                    "Data Source=powerbi://api.powerbi.com/v1.0/myorg/workspace;" +
                    "User ID=app:client-id@tenant-id"
            };

            var validationResult = settings.Validate();

            Assert.IsFalse(validationResult.Successful);
            StringAssert.Contains(
                validationResult.Message,
                "password and service-principal connection strings are not supported");
        }

        [TestMethod]
        public void Auth_command_rejects_password_connection_string()
        {
            var settings = new AuthCommand.Settings
            {
                ConnectionString =
                    "Data Source=powerbi://api.powerbi.com/v1.0/myorg/workspace;" +
                    "Password=not-a-real-password"
            };

            var validationResult = settings.Validate();

            Assert.IsFalse(validationResult.Successful);
            StringAssert.Contains(
                validationResult.Message,
                "password and service-principal connection strings are not supported");
        }

        [TestMethod]
        public void Auth_command_success_message_contains_only_safe_metadata()
        {
            var message = AuthCommand.CreateSuccessMessage(
                "account@example.com",
                new DateTimeOffset(2026, 8, 10, 1, 2, 3, TimeSpan.Zero));

            StringAssert.Contains(message, "account@example.com");
            StringAssert.Contains(message, "2026-08-10 01:02:03Z");
            Assert.IsFalse(
                message.IndexOf("token", StringComparison.OrdinalIgnoreCase) >= 0);
        }

        // The following tests verify that values containing characters which are
        // significant inside a connection string (';', '=', single/double quotes,
        // leading/trailing whitespace) are escaped correctly when FullConnectionString
        // is built from individual Server/Database/UserID/Password parameters.
        // We round-trip through OleDbConnectionStringBuilder to assert the value
        // can be parsed back to its original form, regardless of which quoting
        // form (single/double quotes) the builder picks.

        private static string ParseValue(string connectionString, string key)
        {
            var builder = new OleDbConnectionStringBuilder(connectionString);
            return builder.ContainsKey(key) ? (string)builder[key] : null;
        }

        [TestMethod]
        public void Database_with_semicolon_should_be_quoted_in_connection_string()
        {
            var settings = new FileCommand.Settings
            {
                Server = "localhost",
                Database = "Foo;Bar"
            };

            Assert.AreEqual("Foo;Bar", ParseValue(settings.FullConnectionString, "Initial Catalog"));
            Assert.AreEqual("localhost", ParseValue(settings.FullConnectionString, "Data Source"));
        }

        [TestMethod]
        public void Database_with_single_quote_should_round_trip()
        {
            var settings = new FileCommand.Settings
            {
                Server = "localhost",
                Database = "O'Brien"
            };

            Assert.AreEqual("O'Brien", ParseValue(settings.FullConnectionString, "Initial Catalog"));
        }

        [TestMethod]
        public void Database_with_double_quote_should_round_trip()
        {
            var settings = new FileCommand.Settings
            {
                Server = "localhost",
                Database = "weird\"name"
            };

            Assert.AreEqual("weird\"name", ParseValue(settings.FullConnectionString, "Initial Catalog"));
        }

        [TestMethod]
        public void Database_with_equals_sign_should_round_trip()
        {
            var settings = new FileCommand.Settings
            {
                Server = "localhost",
                Database = "Foo=Bar"
            };

            Assert.AreEqual("Foo=Bar", ParseValue(settings.FullConnectionString, "Initial Catalog"));
        }

        [TestMethod]
        public void Server_with_semicolon_should_be_quoted_in_connection_string()
        {
            var settings = new FileCommand.Settings
            {
                Server = "weird;server",
                Database = "Adventure Works"
            };

            Assert.AreEqual("weird;server", ParseValue(settings.FullConnectionString, "Data Source"));
            Assert.AreEqual("Adventure Works", ParseValue(settings.FullConnectionString, "Initial Catalog"));
        }

        [TestMethod]
        public void Password_with_semicolon_should_be_quoted_in_connection_string()
        {
            var settings = new FileCommand.Settings
            {
                Server = "localhost",
                Database = "Adventure Works",
                UserID = "alice",
                Password = "p;ss\"wo'rd"
            };

            Assert.AreEqual("p;ss\"wo'rd", ParseValue(settings.FullConnectionString, "Password"));
            Assert.AreEqual("alice", ParseValue(settings.FullConnectionString, "User ID"));
        }

        [TestMethod]
        public void Plain_server_and_database_should_produce_unquoted_connection_string()
        {
            // Regression check: a value with no special characters at all should
            // round-trip cleanly and not have any extraneous keys added.
            var settings = new FileCommand.Settings
            {
                Server = "localhost",
                Database = "AdventureWorks"
            };

            Assert.AreEqual("Data Source=localhost;Initial Catalog=AdventureWorks", settings.FullConnectionString);
        }

    }
}
