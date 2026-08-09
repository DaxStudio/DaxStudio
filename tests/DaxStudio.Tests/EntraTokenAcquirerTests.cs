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
            var calls = new List<string>();
            client.AcquireTokenSilentAsync(Arg.Any<IEnumerable<string>>(), account)
                .Returns(_ =>
                {
                    calls.Add("silent");
                    return Task.FromResult(CreateResult(account.Username));
                });
            client.AcquireTokenInteractiveAsync(
                    Arg.Any<IEnumerable<string>>(), Arg.Any<IAccount>(), Arg.Any<IntPtr>())
                .Returns(_ =>
                {
                    calls.Add("interactive");
                    return Task.FromResult(CreateResult(account.Username));
                });

            var options = Substitute.For<IHaveLastUsedUPN>();
            options.LastUsedUPN.Returns(account.Username);
            var result = await new EntraTokenAcquirer(client).AcquireTokenAsync(
                options, Scopes, IntPtr.Zero, allowInteractive: true);

            Assert.AreEqual(account.Username, result.Username);
            CollectionAssert.AreEqual(new[] { "silent" }, calls);
        }

        [TestMethod]
        public async Task AcquireToken_bootstraps_interactively_once_after_silent_cache_miss()
        {
            var account = CreateAccount("user@example.com");
            var client = CreateClient(account);
            var calls = new List<string>();
            client.AcquireTokenSilentAsync(Arg.Any<IEnumerable<string>>(), account)
                .Returns(Task.FromException<EntraTokenResult>(
                    new MsalUiRequiredException("cache_miss", "No cached token is available.")));
            client.When(candidate => candidate.AcquireTokenSilentAsync(
                    Arg.Any<IEnumerable<string>>(), account))
                .Do(_ => calls.Add("silent"));
            client.AcquireTokenInteractiveAsync(
                    Arg.Any<IEnumerable<string>>(), account, Arg.Any<IntPtr>())
                .Returns(_ =>
                {
                    calls.Add("interactive");
                    return Task.FromResult(CreateResult(account.Username));
                });

            var result = await new EntraTokenAcquirer(client).AcquireTokenAsync(
                Substitute.For<IHaveLastUsedUPN>(), Scopes, IntPtr.Zero, allowInteractive: true);

            Assert.AreEqual(account.Username, result.Username);
            CollectionAssert.AreEqual(new[] { "silent", "interactive" }, calls);
        }

        [TestMethod]
        public async Task AcquireToken_non_interactive_cache_miss_fails_without_opening_ui()
        {
            var account = CreateAccount("user@example.com");
            var client = CreateClient(account);
            client.AcquireTokenSilentAsync(Arg.Any<IEnumerable<string>>(), account)
                .Returns<Task<EntraTokenResult>>(_ =>
                    throw new MsalUiRequiredException("cache_miss", "No cached token is available."));

            var exception = await AssertThrowsAsync<EntraInteractionRequiredException>(
                () => new EntraTokenAcquirer(client).AcquireTokenAsync(
                    Substitute.For<IHaveLastUsedUPN>(), Scopes, IntPtr.Zero, allowInteractive: false));

            StringAssert.Contains(exception.Message, "Run once without --non-interactive");
            await client.DidNotReceive().AcquireTokenInteractiveAsync(
                Arg.Any<IEnumerable<string>>(), Arg.Any<IAccount>(), Arg.Any<IntPtr>());
        }

        [TestMethod]
        public async Task AcquireToken_selects_the_cached_account_matching_last_used_upn()
        {
            var otherAccount = CreateAccount("other@example.com");
            var selectedAccount = CreateAccount("selected@example.com");
            var selectedUsername = selectedAccount.Username;
            var client = CreateClient(otherAccount, selectedAccount);
            client.AcquireTokenSilentAsync(Arg.Any<IEnumerable<string>>(), selectedAccount)
                .Returns(Task.FromResult(CreateResult(selectedUsername)));
            var options = Substitute.For<IHaveLastUsedUPN>();
            options.LastUsedUPN.Returns(selectedUsername);

            await new EntraTokenAcquirer(client).AcquireTokenAsync(
                options, Scopes, IntPtr.Zero, allowInteractive: false);

            await client.Received(1).AcquireTokenSilentAsync(
                Arg.Any<IEnumerable<string>>(), selectedAccount);
        }

        [TestMethod]
        public async Task AcquireToken_non_interactive_does_not_switch_to_another_cached_account()
        {
            var otherAccount = CreateAccount("other@example.com");
            var otherUsername = otherAccount.Username;
            var client = CreateClient(otherAccount);
            client.AcquireTokenSilentAsync(Arg.Any<IEnumerable<string>>(), otherAccount)
                .Returns(Task.FromResult(CreateResult(otherUsername)));
            var options = Substitute.For<IHaveLastUsedUPN>();
            options.LastUsedUPN.Returns("missing@example.com");

            await AssertThrowsAsync<EntraInteractionRequiredException>(
                () => new EntraTokenAcquirer(client).AcquireTokenAsync(
                    options, Scopes, IntPtr.Zero, allowInteractive: false));

            await client.DidNotReceive().AcquireTokenSilentAsync(
                Arg.Any<IEnumerable<string>>(), otherAccount);
        }

        [TestMethod]
        public async Task RefreshToken_cache_miss_fails_without_opening_ui()
        {
            var account = CreateAccount("user@example.com");
            var client = CreateClient(account);
            client.AcquireTokenSilentAsync(Arg.Any<IEnumerable<string>>(), account)
                .Returns<Task<EntraTokenResult>>(_ =>
                    throw new MsalUiRequiredException("cache_miss", "No cached token is available."));

            var exception = await AssertThrowsAsync<EntraInteractionRequiredException>(
                () => new EntraTokenAcquirer(client).RefreshTokenAsync(
                    account.Username, account.HomeAccountId.Identifier, Scopes));

            StringAssert.Contains(exception.Message, "renewal requires user interaction");
            await client.DidNotReceive().AcquireTokenInteractiveAsync(
                Arg.Any<IEnumerable<string>>(), Arg.Any<IAccount>(), Arg.Any<IntPtr>());
        }

        [TestMethod]
        public async Task RefreshToken_does_not_switch_to_another_cached_account()
        {
            var otherAccount = CreateAccount("other@example.com");
            var otherUsername = otherAccount.Username;
            var client = CreateClient(otherAccount);
            client.AcquireTokenSilentAsync(Arg.Any<IEnumerable<string>>(), otherAccount)
                .Returns(Task.FromResult(CreateResult(otherUsername)));

            await AssertThrowsAsync<EntraInteractionRequiredException>(
                () => new EntraTokenAcquirer(client).RefreshTokenAsync(
                    "missing@example.com", "missing-account-id", Scopes));

            await client.DidNotReceive().AcquireTokenSilentAsync(
                Arg.Any<IEnumerable<string>>(), otherAccount);
        }

        [TestMethod]
        public async Task RefreshToken_selects_the_exact_home_account_identifier()
        {
            var firstAccount = CreateAccount("shared@example.com", "account-1");
            var selectedAccount = CreateAccount("shared@example.com", "account-2");
            var selectedUsername = selectedAccount.Username;
            var selectedAccountIdentifier = selectedAccount.HomeAccountId.Identifier;
            var client = CreateClient(firstAccount, selectedAccount);
            client.AcquireTokenSilentAsync(Arg.Any<IEnumerable<string>>(), selectedAccount)
                .Returns(Task.FromResult(CreateResult(
                    selectedUsername, selectedAccountIdentifier)));

            await new EntraTokenAcquirer(client).RefreshTokenAsync(
                selectedUsername,
                selectedAccountIdentifier,
                Scopes);

            await client.Received(1).AcquireTokenSilentAsync(
                Arg.Any<IEnumerable<string>>(), selectedAccount);
            await client.DidNotReceive().AcquireTokenSilentAsync(
                Arg.Any<IEnumerable<string>>(), firstAccount);
        }

        private static IEntraTokenClient CreateClient(params IAccount[] accounts)
        {
            var client = Substitute.For<IEntraTokenClient>();
            client.GetAccountsAsync().Returns(Task.FromResult(accounts.AsEnumerable()));
            client.OperatingSystemAccount.Returns(accounts.FirstOrDefault());
            return client;
        }

        private static IAccount CreateAccount(string username, string accountIdentifier = null)
        {
            var account = Substitute.For<IAccount>();
            account.Username.Returns(username);
            account.HomeAccountId.Returns(new AccountId(accountIdentifier ?? username));
            return account;
        }

        private static EntraTokenResult CreateResult(
            string username, string accountIdentifier = null)
        {
            return new EntraTokenResult(null, username, accountIdentifier);
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
    }
}
