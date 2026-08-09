using DaxStudio.Common;
using DaxStudio.Common.Interfaces;
using Microsoft.Identity.Client;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace DaxStudio.Tests
{
    [TestClass]
    public class AccessTokenTests
    {
        [TestMethod]
        public void HostPostfixTest()
        {
            var testCases = new Dictionary<string, Tuple<string,string>>
            {
                { "asazure://swedencentral.asazure.windows.net/contoso", new Tuple<string,string>("asazure.windows.net", "cf710c6e-dfcc-4fa8-a093-d47294e44c66") },
                { "asazure://southafricanorth.asazure.windows.net/contoso", new Tuple<string,string>("asazure.windows.net", "cf710c6e-dfcc-4fa8-a093-d47294e44c66") },
                { "powerbi://api.powerbi.com/v1.0/myorg/Contoso", new Tuple<string,string>("api.powerbi.com", "cf710c6e-dfcc-4fa8-a093-d47294e44c66") },
                { "powerbi://api.powerbi.com/v1.0/myorg/", new Tuple<string,string>("api.powerbi.com", "cf710c6e-dfcc-4fa8-a093-d47294e44c66") },
                { "powerbi://pbidedicated.usgovcloudapi.net/myorg/", new Tuple<string,string>("pbidedicated.usgovcloudapi.net", "ec3681c2-6e7d-472a-b23b-8be15bd25c15") },
                { "powerbi://api.powerbigov.us/v1.0/myorg/DanE%20PBICAT%20demo", new Tuple<string,string>("api.powerbigov.us", "ec3681c2-6e7d-472a-b23b-8be15bd25c15")  }
            };

            foreach (var testCase in testCases)
            {
                var found = DaxStudio.Common.EntraIdHelper.TryFindAuthenticationInformation(new Uri(testCase.Key),out var record);
                Assert.IsTrue(found, $"Failed to find authentication information for {testCase.Key}");
                Assert.AreEqual(testCase.Value.Item1, record.DomainPostfix, "DomainPostFix mismatch");
                Assert.AreEqual(testCase.Value.Item2, record.ApplicationId,"ApplicationId mismatch");
            }
        }

        [TestMethod]
        public void StorageScopeIgnoresConnectionSpecificContextScope()
        {
            var context = new AccessTokenContext
            {
                Scope = new[] { "https://analysis.windows.net/powerbi/api/.default" }
            };

            var scope = EntraIdHelper.GetScope(AccessTokenScope.Storage, context).ToArray();

            CollectionAssert.AreEqual(
                new[] { "https://storage.azure.com/.default" },
                scope);
        }

        [TestMethod]
        public void ExistingTokenContextsKeepInteractiveRenewalFallback()
        {
            var context = new AccessTokenContext();

            Assert.IsFalse(EntraIdHelper.IsSilentOnlyRenewal(context));

            context.RenewalMode = EntraTokenAcquisitionMode.SilentOnly;
            Assert.IsTrue(EntraIdHelper.IsSilentOnlyRenewal(context));
        }

        [TestMethod]
        public async Task SilentOnlyAcquisitionCacheMissFailsWithoutAnInteractivePath()
        {
            var account = CreateAccount("user@example.com", "account-1");
            var client = new FakeSilentTokenClient(account)
            {
                SilentResult = (_, __) => Task.FromException<AuthenticationResult>(
                    new MsalUiRequiredException(
                        "cache_miss",
                        "No cached token is available."))
            };

            var exception = await AssertThrowsAsync<InvalidOperationException>(
                () => EntraIdHelper.AcquireTokenSilentOnlyAsync(
                    new LastUsedUpnOptions(),
                    new[] { "scope" },
                    new AccessTokenContext(),
                    client));

            StringAssert.Contains(exception.Message, "Run once without --non-interactive");
            CollectionAssert.AreEqual(new[] { account }, client.SilentAccounts);
        }

        [TestMethod]
        public async Task SilentOnlyAcquisitionDoesNotSwitchRememberedAccounts()
        {
            var otherAccount = CreateAccount("other@example.com", "account-1");
            var client = new FakeSilentTokenClient(otherAccount);

            await AssertThrowsAsync<InvalidOperationException>(
                () => EntraIdHelper.AcquireTokenSilentOnlyAsync(
                    new LastUsedUpnOptions { LastUsedUPN = "missing@example.com" },
                    new[] { "scope" },
                    new AccessTokenContext(),
                    client));

            Assert.AreEqual(0, client.SilentAccounts.Count);
        }

        [TestMethod]
        public async Task SilentOnlyRenewalFailureDoesNotHaveAnInteractivePath()
        {
            var account = CreateAccount("user@example.com", "account-1");
            var client = new FakeSilentTokenClient(account)
            {
                SilentResult = (_, __) => Task.FromException<AuthenticationResult>(
                    new MsalUiRequiredException(
                        "cache_miss",
                        "No cached token is available."))
            };
            var context = new AccessTokenContext
            {
                Username = account.Username,
                AccountIdentifier = account.HomeAccountId.Identifier,
                Scope = new[] { "scope" },
                RenewalMode = EntraTokenAcquisitionMode.SilentOnly
            };

            var exception = await AssertThrowsAsync<InvalidOperationException>(
                () => EntraIdHelper.RefreshTokenSilentOnlyAsync(context, client));

            StringAssert.Contains(exception.Message, "renewal requires user interaction");
            CollectionAssert.AreEqual(new[] { account }, client.SilentAccounts);
        }

        [TestMethod]
        public async Task SilentOnlyRenewalSelectsExactHomeAccountIdentifier()
        {
            var firstAccount = CreateAccount("shared@example.com", "account-1");
            var selectedAccount = CreateAccount("shared@example.com", "account-2");
            var client = new FakeSilentTokenClient(firstAccount, selectedAccount)
            {
                SilentResult = (_, __) => Task.FromException<AuthenticationResult>(
                    new MsalUiRequiredException(
                        "cache_miss",
                        "No cached token is available."))
            };
            var context = new AccessTokenContext
            {
                Username = selectedAccount.Username,
                AccountIdentifier = selectedAccount.HomeAccountId.Identifier,
                Scope = new[] { "scope" },
                RenewalMode = EntraTokenAcquisitionMode.SilentOnly
            };

            await AssertThrowsAsync<InvalidOperationException>(
                () => EntraIdHelper.RefreshTokenSilentOnlyAsync(context, client));

            CollectionAssert.AreEqual(new[] { selectedAccount }, client.SilentAccounts);
        }

        private static IAccount CreateAccount(string username, string accountIdentifier)
        {
            var account = Substitute.For<IAccount>();
            account.Username.Returns(username);
            account.HomeAccountId.Returns(new AccountId(accountIdentifier));
            return account;
        }

        private static async Task<TException> AssertThrowsAsync<TException>(Func<Task> action)
            where TException : Exception
        {
            try
            {
                await action();
            }
            catch (TException exception)
            {
                return exception;
            }

            Assert.Fail($"Expected exception of type {typeof(TException).Name}.");
            return null;
        }

        private sealed class LastUsedUpnOptions : IHaveLastUsedUPN
        {
            public string LastUsedUPN { get; set; }
        }

        private sealed class FakeSilentTokenClient : EntraIdHelper.ISilentTokenClient
        {
            private readonly IEnumerable<IAccount> _accounts;

            public FakeSilentTokenClient(params IAccount[] accounts)
            {
                _accounts = accounts;
                OperatingSystemAccount = accounts.FirstOrDefault();
            }

            public IAccount OperatingSystemAccount { get; }
            public List<IAccount> SilentAccounts { get; } = new List<IAccount>();
            public Func<IEnumerable<string>, IAccount, Task<AuthenticationResult>> SilentResult { get; set; }

            public Task<IEnumerable<IAccount>> GetAccountsAsync()
            {
                return Task.FromResult(_accounts);
            }

            public Task<AuthenticationResult> AcquireTokenSilentAsync(
                IEnumerable<string> scope,
                IAccount account)
            {
                SilentAccounts.Add(account);
                return SilentResult(scope, account);
            }
        }
    }
}
