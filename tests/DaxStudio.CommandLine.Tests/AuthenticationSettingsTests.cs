using DaxStudio.CommandLine.Commands;
using DaxStudio.CommandLine.Interfaces;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Serilog;
using Serilog.Core;
using Serilog.Events;
using System;
using System.Collections.Generic;
using ADOTabular.Utils;
using System.Linq;

namespace DaxStudio.CommandLine.Tests
{
    /// <summary>
    /// Covers how dscmd resolves the account to authenticate as, and whether it is allowed to
    /// prompt. The governing rule is that dscmd should not prompt when it has enough information to
    /// proceed unambiguously.
    /// </summary>
    /// <remarks>
    /// Not parallelized: these tests manipulate environment variables and the global Serilog
    /// logger, both of which are process wide.
    /// </remarks>
    [TestClass]
    [DoNotParallelize]
    public class AuthenticationSettingsTests
    {
        private const string UserVariable = "DSCMD_USER";
        private const string NonInteractiveVariable = "DSCMD_NON_INTERACTIVE";

        private string _originalUser;
        private string _originalNonInteractive;

        [TestInitialize]
        public void Setup()
        {
            _originalUser = Environment.GetEnvironmentVariable(UserVariable);
            _originalNonInteractive = Environment.GetEnvironmentVariable(NonInteractiveVariable);
            Environment.SetEnvironmentVariable(UserVariable, null);
            Environment.SetEnvironmentVariable(NonInteractiveVariable, null);
        }

        [TestCleanup]
        public void Cleanup()
        {
            Environment.SetEnvironmentVariable(UserVariable, _originalUser);
            Environment.SetEnvironmentVariable(NonInteractiveVariable, _originalNonInteractive);
        }

        #region Account selection

        [TestMethod]
        public void UserId_Argument_IsUsedToSelectTheAccount()
        {
            var settings = new FileCommand.Settings { UserID = "user@contoso.com" };

            Assert.AreEqual("user@contoso.com", settings.ResolvedUserID);
        }

        [TestMethod]
        public void UserId_FallsBackToTheEnvironmentVariable()
        {
            Environment.SetEnvironmentVariable(UserVariable, "env@contoso.com");
            var settings = new FileCommand.Settings();

            Assert.AreEqual("env@contoso.com", settings.ResolvedUserID);
        }

        [TestMethod]
        public void UserId_Argument_TakesPrecedenceOverTheEnvironmentVariable()
        {
            Environment.SetEnvironmentVariable(UserVariable, "env@contoso.com");
            var settings = new FileCommand.Settings { UserID = "arg@contoso.com" };

            Assert.AreEqual("arg@contoso.com", settings.ResolvedUserID);
        }

        [TestMethod]
        public void UserId_IsEmpty_WhenNeitherIsSupplied()
        {
            var settings = new FileCommand.Settings();

            Assert.AreEqual(string.Empty, settings.ResolvedUserID);
        }

        #endregion

        #region Interactivity policy

        private static IEnumerable<ISettingsConnection> ConnectionSettings(string userId = null, bool nonInteractive = false)
        {
            return new ISettingsConnection[]
            {
                new ExportSqlCommand.Settings { UserID = userId, NonInteractive = nonInteractive },
                new ExportCsvCommand.Settings { UserID = userId, NonInteractive = nonInteractive },
                new ExportParquetCommand.Settings { UserID = userId, NonInteractive = nonInteractive },
                new FileCommand.Settings { UserID = userId, NonInteractive = nonInteractive },
                new XlsxCommand.Settings { UserID = userId, NonInteractive = nonInteractive },
                new VpaxCommand.Settings { UserID = userId, NonInteractive = nonInteractive },
                new AccessTokenCommand.Settings { UserID = userId, NonInteractive = nonInteractive },
                new BenchmarkCommand.Settings { UserID = userId, NonInteractive = nonInteractive },
                new CustomTraceCommand.Settings { UserID = userId, NonInteractive = nonInteractive }
            };
        }

        [TestMethod]
        public void AllConnectionCommands_UseTheExplicitAccountSelection()
        {
            foreach (var settings in ConnectionSettings(userId: "user@contoso.com"))
                Assert.AreEqual("user@contoso.com", settings.ResolvedUserID, settings.GetType().Name);
        }

        [TestMethod]
        public void AllConnectionCommands_HonourNonInteractiveMode()
        {
            foreach (var settings in ConnectionSettings(nonInteractive: true))
                Assert.IsTrue(settings.IsNonInteractive, settings.GetType().Name);
        }

        [TestMethod]
        public void NonInteractive_Flag_IsHonoured()
        {
            var settings = new FileCommand.Settings { NonInteractive = true };

            Assert.IsTrue(settings.IsNonInteractive);
        }

        [TestMethod]
        public void NonInteractive_EnvironmentVariable_IsHonoured()
        {
            foreach (var value in new[] { "1", "true", "TRUE", "yes" })
            {
                Environment.SetEnvironmentVariable(NonInteractiveVariable, value);
                var settings = new FileCommand.Settings();

                Assert.IsTrue(settings.IsNonInteractive, $"DSCMD_NON_INTERACTIVE={value} should disable prompting");
            }
        }

        [TestMethod]
        public void NonInteractive_EnvironmentVariable_IgnoresOtherValues()
        {
            // Only an affirmative value should suppress prompting; an unrelated value must not
            // silently change the failure behaviour of a job.
            Environment.SetEnvironmentVariable(NonInteractiveVariable, "0");
            var settings = new FileCommand.Settings();

            // The result still depends on whether this test host has a console window, so assert
            // only that the variable itself did not force the non-interactive path.
            Assert.AreEqual(settings.IsNonInteractive, !Environment.UserInteractive || !HasConsoleWindow(),
                "A non-affirmative DSCMD_NON_INTERACTIVE value should not on its own suppress prompting");
        }

        #endregion

        #region Connection string

        private const string PowerBiServer = "powerbi://api.powerbi.com/v1.0/myorg/ws";

        [TestMethod]
        public void UserId_WithoutAPassword_StillRequiresAnAccessToken()
        {
            // A user id alone selects the Entra account; it is not a credential, so the delegated
            // token path must still be used.
            var settings = new FileCommand.Settings
            {
                Server = PowerBiServer,
                Database = "model",
                UserID = "user@contoso.com"
            };

            Assert.IsTrue(Helpers.AccessTokenHelperAccessor.IsAccessTokenNeeded(settings.FullConnectionString));
        }

        [TestMethod]
        public void UserId_WithAPassword_BypassesTheDelegatedTokenPath()
        {
            var settings = new FileCommand.Settings
            {
                Server = PowerBiServer,
                Database = "model",
                UserID = "user@contoso.com",
                Password = "secret"
            };

            Assert.IsFalse(Helpers.AccessTokenHelperAccessor.IsAccessTokenNeeded(settings.FullConnectionString));
        }

        [TestMethod]
        public void UserId_IsNotEmittedOnTheConnectionString_WhenATokenWillBeUsed()
        {
            // MSOLAP/AMO treat User ID as a credential. If it survives onto a connection string
            // that is authenticated with a token, AMO attempts a username+password sign-in: it
            // prompts for a password, or fails with AADSTS50052 ("the password entered exceeds the
            // maximum length") once the JWT is supplied in the Password keyword.
            var settings = new FileCommand.Settings
            {
                Server = PowerBiServer,
                Database = "model",
                UserID = "user@contoso.com"
            };

            var builder = settings.FullConnectionString.ToConnectionStringBuilder();

            Assert.IsFalse(builder.ContainsKey("User ID"), settings.FullConnectionString);
            Assert.IsFalse(builder.ContainsKey("UID"), settings.FullConnectionString);
            Assert.AreEqual("user@contoso.com", settings.ResolvedUserID, "the account selection must survive even though the keyword does not");
        }

        [TestMethod]
        public void UserId_IsEmittedOnTheConnectionString_WhenAPasswordIsSupplied()
        {
            // With a password this really is a username/password sign-in, so both belong on the
            // connection string.
            var settings = new FileCommand.Settings
            {
                Server = PowerBiServer,
                Database = "model",
                UserID = "user@contoso.com",
                Password = "secret"
            };

            var builder = settings.FullConnectionString.ToConnectionStringBuilder();

            Assert.AreEqual("user@contoso.com", builder["User ID"]);
        }

        [TestMethod]
        public void UserId_IsEmittedOnTheConnectionString_ForServersThatDoNotUseEntra()
        {
            var settings = new FileCommand.Settings
            {
                Server = "localhost\\tabular",
                Database = "Adventure Works",
                UserID = "testUser"
            };

            var builder = settings.FullConnectionString.ToConnectionStringBuilder();

            Assert.AreEqual("testUser", builder["User ID"]);
        }

        [TestMethod]
        public void UserId_OnASuppliedConnectionString_SelectsTheAccountAndIsRemoved()
        {
            var settings = new FileCommand.Settings
            {
                ConnectionString = $"Data Source={PowerBiServer};Initial Catalog=model;User ID=user@contoso.com"
            };

            var builder = settings.FullConnectionString.ToConnectionStringBuilder();

            Assert.AreEqual("user@contoso.com", settings.ResolvedUserID, "a user id on the connection string still names the account");
            Assert.IsFalse(builder.ContainsKey("User ID"), settings.FullConnectionString);
        }

        [TestMethod]
        public void UserId_Argument_TakesPrecedenceOverTheConnectionString()
        {
            var settings = new FileCommand.Settings
            {
                ConnectionString = $"Data Source={PowerBiServer};Initial Catalog=model;User ID=connstr@contoso.com",
                UserID = "arg@contoso.com"
            };

            Assert.AreEqual("arg@contoso.com", settings.ResolvedUserID);
        }

        #endregion

        #region Resolution is not repeated

        [TestMethod]
        public void UserId_IsResolvedOnce_NoMatterHowManyTimesItIsRead()
        {
            // Commands read the user id and the connection string separately, which used to log
            // "Using UserID argument" once per read and made the console output look like two
            // sign-in attempts were happening.
            var sink = new CountingSink();
            var original = Log.Logger;
            Log.Logger = new LoggerConfiguration().MinimumLevel.Verbose().WriteTo.Sink(sink).CreateLogger();

            try
            {
                var settings = new FileCommand.Settings
                {
                    Server = PowerBiServer,
                    Database = "model",
                    UserID = "user@contoso.com"
                };

                _ = settings.ResolvedUserID;
                _ = settings.FullConnectionString;
                _ = settings.FullConnectionString;
                _ = settings.ResolvedUserID;

                Assert.AreEqual(1, sink.CountResolutionsOf("UserID"),
                    "the user id should be resolved, and therefore logged, exactly once per command");
            }
            finally
            {
                Log.Logger = original;
            }
        }

        private sealed class CountingSink : ILogEventSink
        {
            private readonly List<LogEvent> _events = new List<LogEvent>();

            public void Emit(LogEvent logEvent)
            {
                lock (_events) { _events.Add(logEvent); }
            }

            /// <summary>
            /// Counts "Using {propertyName} argument" events for a given property. Matching on the
            /// template and property rather than the rendered text avoids depending on how Serilog
            /// quotes string values.
            /// </summary>
            public int CountResolutionsOf(string propertyName)
            {
                lock (_events)
                {
                    return _events.Count(e =>
                        e.MessageTemplate.Text == "Using {propertyName} argument"
                        && e.Properties.TryGetValue("propertyName", out var value)
                        && (value as ScalarValue)?.Value as string == propertyName);
                }
            }
        }

        #endregion

        private static bool HasConsoleWindow()
            => DaxStudio.CommandLine.Helpers.NativeMethods.GetConsoleWindow() != IntPtr.Zero;
    }
}

namespace DaxStudio.CommandLine.Tests.Helpers
{
    /// <summary>
    /// Thin accessor so the tests read clearly without importing the helper namespace everywhere.
    /// </summary>
    internal static class AccessTokenHelperAccessor
    {
        public static bool IsAccessTokenNeeded(string connectionString)
            => DaxStudio.CommandLine.Helpers.AccessTokenHelper.IsAccessTokenNeeded(connectionString);
    }
}
