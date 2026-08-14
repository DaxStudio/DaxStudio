using DaxStudio.CommandLine.Commands;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;

namespace DaxStudio.CommandLine.Tests
{
    /// <summary>
    /// Covers how dscmd resolves the account to authenticate as, and whether it is allowed to
    /// prompt. The governing rule is that dscmd should not prompt when it has enough information to
    /// proceed unambiguously.
    /// </summary>
    [TestClass]
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

        [TestMethod]
        public void UserId_WithoutAPassword_StillRequiresAnAccessToken()
        {
            // A user id alone selects the Entra account; it is not a credential, so the delegated
            // token path must still be used.
            var settings = new FileCommand.Settings
            {
                Server = "powerbi://api.powerbi.com/v1.0/myorg/ws",
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
                Server = "powerbi://api.powerbi.com/v1.0/myorg/ws",
                Database = "model",
                UserID = "user@contoso.com",
                Password = "secret"
            };

            Assert.IsFalse(Helpers.AccessTokenHelperAccessor.IsAccessTokenNeeded(settings.FullConnectionString));
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
