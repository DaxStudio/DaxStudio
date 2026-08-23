using Microsoft.VisualStudio.TestTools.UnitTesting;
using DaxStudio.CommandLine.Commands;
using Spectre.Console.Cli;
using System;
using System.Linq;
using ADOTabular.Utils;
#if NET8_0_OR_GREATER
using AccessToken = Microsoft.AnalysisServices.AccessToken;
#else
using AccessToken = Microsoft.AnalysisServices.AdomdClient.AccessToken;
#endif

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
            Assert.AreEqual("data source=localhost;User ID=testUser;Password=testPwd", settings.FullConnectionString, "connection strings don't match");
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
        public void access_token_command_can_use_the_default_power_bi_scope()
        {
            var settings = new AccessTokenCommand.Settings();
            var accessTokenCommand = new AccessTokenCommand();

            var validationResult = ((ICommand)accessTokenCommand).Validate(null, settings);

            Assert.IsTrue(validationResult.Successful, validationResult.Message);
            Assert.AreEqual("Data Source=powerbi://api.powerbi.com", AccessTokenCommand.GetTokenConnectionString(settings));
        }

        [TestMethod]
        public void auth_command_can_use_the_default_power_bi_scope()
        {
            var settings = new AuthCommand.Settings();
            var authCommand = new AuthCommand();

            var validationResult = ((ICommand)authCommand).Validate(null, settings);

            Assert.IsTrue(validationResult.Successful, validationResult.Message);
            Assert.AreEqual("Data Source=powerbi://api.powerbi.com", AuthCommand.GetAuthenticationConnectionString(settings));
        }

        [TestMethod]
        public void auth_command_accepts_a_server_without_a_database()
        {
            var settings = new AuthCommand.Settings
            {
                Server = "asazure://australiasoutheast.asazure.windows.net/myserver"
            };

            var validationResult = settings.Validate();

            Assert.IsTrue(validationResult.Successful, validationResult.Message);
        }

        [TestMethod]
        public void auth_list_rejects_authentication_options()
        {
            var settings = new AuthCommand.Settings
            {
                List = true,
                UserID = "user@domain.com"
            };
            var authCommand = new AuthCommand();

            var validationResult = ((ICommand)authCommand).Validate(null, settings);

            Assert.IsFalse(validationResult.Successful);
            Assert.AreEqual("--list cannot be combined with authentication options", validationResult.Message);
        }

        [TestMethod]
        public void auth_list_formats_account_tenant_and_source()
        {
            var accounts = new[]
            {
                new Common.AvailableEntraAccount(
                    "cached@contoso.com",
                    "tenant-1",
                    "account-1.tenant-1",
                    Common.EntraAccountSource.DaxStudioCache),
                new Common.AvailableEntraAccount(
                    "windows@contoso.com",
                    "tenant-2",
                    "account-2.tenant-2",
                    Common.EntraAccountSource.Windows)
            };

            var lines = AuthCommand.FormatAccountLines(accounts).ToArray();

            CollectionAssert.AreEqual(
                new[]
                {
                    "Account              Tenant    Source",
                    "cached@contoso.com   tenant-1  DAX Studio cache",
                    "windows@contoso.com  tenant-2  Windows"
                },
                lines);

            var tenantColumn = lines[0].IndexOf("Tenant", StringComparison.Ordinal);
            var sourceColumn = lines[0].IndexOf("Source", StringComparison.Ordinal);
            Assert.AreEqual(tenantColumn, lines[1].IndexOf("tenant-1", StringComparison.Ordinal));
            Assert.AreEqual(tenantColumn, lines[2].IndexOf("tenant-2", StringComparison.Ordinal));
            Assert.AreEqual(sourceColumn, lines[1].IndexOf("DAX Studio cache", StringComparison.Ordinal));
            Assert.AreEqual(sourceColumn, lines[2].IndexOf("Windows", StringComparison.Ordinal));
        }

        [TestMethod]
        public void auth_output_values_cannot_add_rows_or_columns()
        {
            Assert.AreEqual(
                "user next tenant",
                AuthCommand.SanitizeOutputValue("user\r\nnext\ttenant"));
        }

        [TestMethod]
        public void auth_expiration_uses_the_local_timezone_and_includes_its_offset()
        {
            var expiresOn = new DateTimeOffset(2026, 8, 17, 12, 34, 56, TimeSpan.Zero);
            var localExpiration = expiresOn.ToLocalTime();

            var formatted = AuthCommand.FormatExpiration(expiresOn);

            Assert.AreEqual(localExpiration.ToString("O"), formatted);
            StringAssert.EndsWith(formatted, localExpiration.ToString("zzz"));
        }

        [TestMethod]
        public void custom_trace_connection_event_carries_the_delegated_access_token()
        {
            var settings = new CustomTraceCommand.Settings { Database = "Model" };
            var token = new AccessToken("token", DateTimeOffset.UtcNow.AddHours(1), null);

            var connectionEvent = CustomTraceCommand.CreateConnectEvent(
                settings,
                "Data Source=powerbi://api.powerbi.com/v1.0/myorg/workspace;Initial Catalog=Model",
                token);

            Assert.AreEqual(token.Token, connectionEvent.AccessToken.Token);
            Assert.AreEqual(token.ExpirationTime, connectionEvent.AccessToken.ExpirationTime);
            Assert.AreEqual("Model", connectionEvent.DatabaseName);
            StringAssert.StartsWith(connectionEvent.ConnectionString, "Data Source=powerbi://");
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
            var builder = connectionString.ToConnectionStringBuilder();
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


