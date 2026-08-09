using DaxStudio.Common.Interfaces;
using Microsoft.Identity.Client;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace DaxStudio.Common.Authentication
{
    internal interface IEntraTokenClient
    {
        IAccount OperatingSystemAccount { get; }
        Task<IEnumerable<IAccount>> GetAccountsAsync();
        Task<EntraTokenResult> AcquireTokenSilentAsync(IEnumerable<string> scopes, IAccount account);
        Task<EntraTokenResult> AcquireTokenInteractiveAsync(
            IEnumerable<string> scopes, IAccount account, IntPtr ownerWindow);
    }

    internal sealed class EntraTokenResult
    {
        public EntraTokenResult(
            AuthenticationResult authenticationResult,
            string username)
        {
            AuthenticationResult = authenticationResult;
            Username = username;
        }

        public AuthenticationResult AuthenticationResult { get; }
        public string Username { get; }
    }

    internal sealed class EntraTokenAcquirer
    {
        private readonly IEntraTokenClient _client;

        public EntraTokenAcquirer(IEntraTokenClient client)
        {
            _client = client ?? throw new ArgumentNullException(nameof(client));
        }

        public async Task<EntraTokenResult> AcquireTokenAsync(
            IHaveLastUsedUPN options,
            IEnumerable<string> scopes,
            IntPtr ownerWindow)
        {
            var account = await SelectAccountAsync(options?.LastUsedUPN).ConfigureAwait(false);
            EntraTokenResult result;
            if (account == null)
            {
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
                catch (MsalUiRequiredException)
                {
                    result = await _client.AcquireTokenInteractiveAsync(scopes, account, ownerWindow)
                        .ConfigureAwait(false);
                }
            }

            if (options != null)
                options.LastUsedUPN = result.Username;
            return result;
        }

        private async Task<IAccount> SelectAccountAsync(string username)
        {
            var accounts = (await _client.GetAccountsAsync().ConfigureAwait(false)).ToArray();
            if (!string.IsNullOrEmpty(username))
            {
                return accounts.FirstOrDefault(candidate =>
                    string.Equals(candidate.Username, username, StringComparison.OrdinalIgnoreCase));
            }

            var account = accounts.FirstOrDefault();
            return account ?? _client.OperatingSystemAccount;
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
                result.Account?.Username);
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
                result.Account?.Username);
        }
    }
}
