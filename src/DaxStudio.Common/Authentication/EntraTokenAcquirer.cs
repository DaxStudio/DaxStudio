using DaxStudio.Common.Interfaces;
using Microsoft.Identity.Client;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace DaxStudio.Common.Authentication
{
    public interface IEntraTokenClient
    {
        IAccount OperatingSystemAccount { get; }
        Task<IEnumerable<IAccount>> GetAccountsAsync();
        Task<EntraTokenResult> AcquireTokenSilentAsync(IEnumerable<string> scopes, IAccount account);
        Task<EntraTokenResult> AcquireTokenInteractiveAsync(
            IEnumerable<string> scopes, IAccount account, IntPtr ownerWindow);
    }

    public sealed class EntraTokenResult
    {
        public EntraTokenResult(
            AuthenticationResult authenticationResult,
            string username,
            string accountIdentifier)
        {
            AuthenticationResult = authenticationResult;
            Username = username;
            AccountIdentifier = accountIdentifier;
        }

        public AuthenticationResult AuthenticationResult { get; }
        public string Username { get; }
        public string AccountIdentifier { get; }
    }

    public sealed class EntraInteractionRequiredException : InvalidOperationException
    {
        public EntraInteractionRequiredException(string message, Exception innerException)
            : base(message, innerException)
        {
        }
    }

    public sealed class EntraTokenAcquirer
    {
        private readonly IEntraTokenClient _client;

        public EntraTokenAcquirer(IEntraTokenClient client)
        {
            _client = client ?? throw new ArgumentNullException(nameof(client));
        }

        public async Task<EntraTokenResult> AcquireTokenAsync(
            IHaveLastUsedUPN options,
            IEnumerable<string> scopes,
            IntPtr ownerWindow,
            bool allowInteractive)
        {
            var account = await SelectAccountAsync(options?.LastUsedUPN, null).ConfigureAwait(false);
            EntraTokenResult result;
            if (account == null)
            {
                var exception = new MsalUiRequiredException(
                    "remembered_account_not_found",
                    "The remembered account is not available in the token cache.");
                if (!allowInteractive)
                    throw CreateBootstrapRequiredException(exception);

                result = await _client.AcquireTokenInteractiveAsync(scopes, null, ownerWindow)
                    .ConfigureAwait(false);
            }
            else
            {
                try
                {
                    result = await _client.AcquireTokenSilentAsync(scopes, account)
                        .ConfigureAwait(false);
                }
                catch (MsalUiRequiredException exception)
                {
                    if (!allowInteractive)
                        throw CreateBootstrapRequiredException(exception);

                    result = await _client.AcquireTokenInteractiveAsync(scopes, account, ownerWindow)
                        .ConfigureAwait(false);
                }
            }

            if (options != null)
                options.LastUsedUPN = result.Username;
            return result;
        }

        public async Task<EntraTokenResult> RefreshTokenAsync(
            string username,
            string accountIdentifier,
            IEnumerable<string> scopes)
        {
            var account = await SelectAccountAsync(username, accountIdentifier).ConfigureAwait(false);
            if (account == null)
            {
                throw CreateRenewalInteractionRequiredException(
                    new MsalUiRequiredException(
                        "token_account_not_found",
                        "The token account is not available in the token cache."));
            }

            try
            {
                return await _client.AcquireTokenSilentAsync(scopes, account).ConfigureAwait(false);
            }
            catch (MsalUiRequiredException exception)
            {
                throw CreateRenewalInteractionRequiredException(exception);
            }
        }

        private async Task<IAccount> SelectAccountAsync(
            string username, string accountIdentifier)
        {
            var accounts = (await _client.GetAccountsAsync().ConfigureAwait(false)).ToArray();
            if (!string.IsNullOrEmpty(accountIdentifier))
            {
                return accounts.FirstOrDefault(candidate =>
                    string.Equals(
                        candidate.HomeAccountId?.Identifier,
                        accountIdentifier,
                        StringComparison.Ordinal));
            }

            if (!string.IsNullOrEmpty(username))
            {
                return accounts.FirstOrDefault(candidate =>
                    string.Equals(candidate.Username, username, StringComparison.OrdinalIgnoreCase));
            }

            var account = accounts.FirstOrDefault();
            return account ?? _client.OperatingSystemAccount;
        }

        private static EntraInteractionRequiredException CreateBootstrapRequiredException(
            MsalUiRequiredException innerException)
        {
            return new EntraInteractionRequiredException(
                "Authentication requires user interaction. Run once without --non-interactive to bootstrap the cached sign-in.",
                innerException);
        }

        private static EntraInteractionRequiredException CreateRenewalInteractionRequiredException(
            MsalUiRequiredException innerException)
        {
            return new EntraInteractionRequiredException(
                "Access-token renewal requires user interaction and cannot continue unattended.",
                innerException);
        }
    }

    internal sealed class MsalEntraTokenClient : IEntraTokenClient
    {
        private readonly IPublicClientApplication _application;
        private readonly IDictionary<string, (string value, bool includeInCacheKey)> _extraQueryParameters;

        public MsalEntraTokenClient(
            IPublicClientApplication application,
            IDictionary<string, (string value, bool includeInCacheKey)> extraQueryParameters)
        {
            _application = application ?? throw new ArgumentNullException(nameof(application));
            _extraQueryParameters = extraQueryParameters;
        }

        public IAccount OperatingSystemAccount => PublicClientApplication.OperatingSystemAccount;

        public async Task<IEnumerable<IAccount>> GetAccountsAsync()
        {
            return await _application.GetAccountsAsync().ConfigureAwait(false);
        }

        public async Task<EntraTokenResult> AcquireTokenSilentAsync(
            IEnumerable<string> scopes, IAccount account)
        {
            var result = await _application.AcquireTokenSilent(scopes, account)
                .ExecuteAsync()
                .ConfigureAwait(false);
            return new EntraTokenResult(
                result,
                result.Account?.Username,
                result.Account?.HomeAccountId?.Identifier);
        }

        public async Task<EntraTokenResult> AcquireTokenInteractiveAsync(
            IEnumerable<string> scopes, IAccount account, IntPtr ownerWindow)
        {
            var result = await _application.AcquireTokenInteractive(scopes)
                .WithAccount(account)
                .WithParentActivityOrWindow(ownerWindow)
                .WithExtraQueryParameters(_extraQueryParameters)
                .WithPrompt(Prompt.SelectAccount)
                .ExecuteAsync()
                .ConfigureAwait(false);
            return new EntraTokenResult(
                result,
                result.Account?.Username,
                result.Account?.HomeAccountId?.Identifier);
        }
    }
}
