using DaxStudio.Common.Authentication;
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
    public class EntraTokenAcquirerTests
    {
        private static readonly string[] Scopes = { "https://analysis.windows.net/powerbi/api/.default" };

        [TestMethod]
        public async Task AcquireToken_uses_cached_token_without_opening_interactive_authentication()
        {
            var account = CreateAccount("user@example.com");
            var client = CreateClient(account);
            client.SilentResult = (_, __) => Task.FromResult(CreateResult(account.Username));
            client.InteractiveResult = (_, __, ___) => Task.FromResult(CreateResult(account.Username));

            var options = new LastUsedUpnOptions { LastUsedUPN = account.Username };
            var result = await new EntraTokenAcquirer(client).AcquireTokenAsync(
                options, Scopes, IntPtr.Zero);

            Assert.AreEqual(account.Username, result.Username);
            CollectionAssert.AreEqual(new[] { "silent" }, client.Calls);
        }

        [TestMethod]
        public async Task AcquireToken_bootstraps_interactively_once_after_silent_cache_miss()
        {
            var account = CreateAccount("user@example.com");
            var client = CreateClient(account);
            client.SilentResult = (_, __) => Task.FromException<EntraTokenResult>(
                new MsalUiRequiredException("cache_miss", "No cached token is available."));
            client.InteractiveResult = (_, __, ___) =>
                Task.FromResult(CreateResult(account.Username));

            var result = await new EntraTokenAcquirer(client).AcquireTokenAsync(
                new LastUsedUpnOptions(), Scopes, IntPtr.Zero);

            Assert.AreEqual(account.Username, result.Username);
            CollectionAssert.AreEqual(new[] { "silent", "interactive" }, client.Calls);
        }

        [TestMethod]
        public async Task AcquireToken_selects_the_cached_account_matching_last_used_upn()
        {
            var otherAccount = CreateAccount("other@example.com");
            var selectedAccount = CreateAccount("selected@example.com");
            var selectedUsername = selectedAccount.Username;
            var client = CreateClient(otherAccount, selectedAccount);
            client.SilentResult = (_, __) => Task.FromResult(CreateResult(selectedUsername));
            var options = new LastUsedUpnOptions { LastUsedUPN = selectedUsername };

            await new EntraTokenAcquirer(client).AcquireTokenAsync(
                options, Scopes, IntPtr.Zero);

            Assert.AreSame(selectedAccount, client.SilentAccounts.Single());
            Assert.AreEqual(selectedUsername, options.LastUsedUPN);
        }

        private static FakeEntraTokenClient CreateClient(params IAccount[] accounts)
        {
            return new FakeEntraTokenClient(accounts);
        }

        private static IAccount CreateAccount(string username)
        {
            var account = Substitute.For<IAccount>();
            account.Username.Returns(username);
            return account;
        }

        private static EntraTokenResult CreateResult(string username)
        {
            return new EntraTokenResult(null, username);
        }

        private sealed class LastUsedUpnOptions : IHaveLastUsedUPN
        {
            public string LastUsedUPN { get; set; }
        }

        private sealed class FakeEntraTokenClient : IEntraTokenClient
        {
            private readonly IEnumerable<IAccount> _accounts;

            public FakeEntraTokenClient(IEnumerable<IAccount> accounts)
            {
                _accounts = accounts;
                OperatingSystemAccount = accounts.FirstOrDefault();
            }

            public IAccount OperatingSystemAccount { get; }
            public List<string> Calls { get; } = new List<string>();
            public List<IAccount> SilentAccounts { get; } = new List<IAccount>();
            public Func<IEnumerable<string>, IAccount, Task<EntraTokenResult>> SilentResult { get; set; }
            public Func<IEnumerable<string>, IAccount, IntPtr, Task<EntraTokenResult>> InteractiveResult { get; set; }

            public Task<IEnumerable<IAccount>> GetAccountsAsync()
            {
                return Task.FromResult(_accounts);
            }

            public Task<EntraTokenResult> AcquireTokenSilentAsync(
                IEnumerable<string> scopes, IAccount account)
            {
                Calls.Add("silent");
                SilentAccounts.Add(account);
                return SilentResult(scopes, account);
            }

            public Task<EntraTokenResult> AcquireTokenInteractiveAsync(
                IEnumerable<string> scopes, IAccount account, IntPtr ownerWindow)
            {
                Calls.Add("interactive");
                return InteractiveResult(scopes, account, ownerWindow);
            }
        }
    }
}
