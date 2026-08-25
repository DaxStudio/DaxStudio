using DaxStudio.Common;
using DaxStudio.Common.Extensions;
using DaxStudio.Common.Interfaces;
using Microsoft.AnalysisServices.AdomdClient;
using Microsoft.Identity.Client;
using Microsoft.Identity.Client.Broker;
using Microsoft.Identity.Client.Extensions.Msal;
using Microsoft.PowerBI.Api;
using Microsoft.PowerBI.Api.Models;
#if NET8_0_OR_GREATER
using AccessToken = Microsoft.AnalysisServices.AccessToken;
#endif
using Microsoft.Win32.SafeHandles;
using Serilog;
using System;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Security.Claims;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Xml;
using Tom = Microsoft.AnalysisServices;
using Adomd = Microsoft.AnalysisServices.AdomdClient;
using Microsoft.IdentityModel.Tokens;

namespace DaxStudio.Common
{
    public static class EntraIdHelper
    {

        // Dictionary form required by the non-obsolete MSAL WithExtraQueryParameters overload.
        // The bool indicates whether the parameter should be included in the token cache key.
        private static readonly IDictionary<string, (string value, bool includeInCacheKey)> MicrosoftAccountOnlyQueryParameters
            = new Dictionary<string, (string value, bool includeInCacheKey)>
            {
                ["msafed"] = ("0", false)
            };
        // Shared HttpClient for outbound HTTPS calls (replaces obsolete WebClient/HttpWebRequest usage).
        private static readonly HttpClient _httpClient = new HttpClient();
        private static IPublicClientApplication _clientApp;
        //private static string ClientId = "90fd9dec-463e-4e03-8cbe-8f0baa9bb7e8";
        //private static string ClientId = "7f67af8a-fedc-4b08-8b4e-37c4d127b6cf";  // PBI Desktop Client ID
        private static string DefaultClientId = "cf710c6e-dfcc-4fa8-a093-d47294e44c66"; // ADOMD Client ID
        private static string DefaultAuthority = "https://login.microsoftonline.com/organizations";
        
        //private static Regex regexGuid = new Regex(@"[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        private static Regex regexGuid = new Regex(@"(?<scheme>powerbi)://(?<host>.*)/v(?<version>\d+\.\d+)/(?<tenant>.*)/.*$", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        //private static string Instance = "https://login.microsoftonline.com/common/oauth2/nativeclient";
        private static readonly string[] powerbiScope = new [] { "https://analysis.windows.net/powerbi/api/.default" };
        private static readonly string[] asazureScope = new [] { "https://*.asazure.windows.net/.default" };
        private static readonly string[] storageScope = new [] { "https://storage.azure.com/.default" };
        private static readonly string[] graphScope = new [] { "https://graph.microsoft.com/User.Read" };

        // Microsoft Graph Command Line Tools - a Microsoft first-party PUBLIC client that registers a
        // WAM/broker-compatible redirect URI, so it works with our .WithBroker() + .WithDefaultRedirectUri()
        // setup and can silently reuse the Windows account. (Other well-known ids such as Microsoft Office
        // do NOT register a compatible public-client redirect and make WAM return 'IncorrectConfiguration'.)
        // The ADOMD/Power BI client id is not authorized for Microsoft Graph, so reusing it fails the same way.
        private const string GraphClientId = "14d82eec-204b-4c2f-b7e8-296a70dab67e";
        private static IPublicClientApplication _graphClientApp;

        /// <summary>
        /// Returns a non-zero, <b>visible</b> top-level window handle to own the interactive
        /// MSAL/WAM sign-in dialog. The WAM broker parents its native dialog to this handle;
        /// when it receives IntPtr.Zero (or the handle of a hidden window - e.g. a dialog that
        /// has hidden itself before prompting) the sign-in dialog is not kept in front of the
        /// DAX Studio main window and can be pushed behind it. We therefore reject a hidden or
        /// zero handle and fall back to the current foreground window and then the process main
        /// window so a usable owner handle is always supplied.
        /// </summary>
        private static IntPtr GetOwnerWindowHandle(IntPtr? preferredHwnd)
        {
            if (preferredHwnd.HasValue && preferredHwnd.Value != IntPtr.Zero
                && NativeMethods.IsWindowVisible(preferredHwnd.Value))
                return preferredHwnd.Value;

            var foreground = NativeMethods.GetForegroundWindow();
            if (foreground != IntPtr.Zero)
                return foreground;

            return Process.GetCurrentProcess().MainWindowHandle;
        }

        /// <summary>
        /// Resolves which Entra account should be used, <b>before</b> any token is requested.
        /// <para>
        /// The governing rule is that a token is acquired silently whenever the identity is
        /// unambiguous - either because the caller named it, or because there is only one account
        /// to choose from. Prompting is reserved for genuine ambiguity or a genuine Entra
        /// requirement.
        /// </para>
        /// </summary>
        public static async Task<AccountSelectionResult> SelectAccountAsync(AccessTokenContext context, AuthenticationOptions authOptions)
        {
            if (authOptions == null) throw new ArgumentNullException(nameof(authOptions));

            // Accounts that have deliberately signed in to DAX Studio. Excludes Windows accounts,
            // so a machine with many work/school accounts can still have exactly one obvious choice.
            var cacheApp = await GetPublicClientAppAsync(context, listOperatingSystemAccounts: false);
            var cachedAccounts = (await cacheApp.GetAccountsAsync()).ToList();

            // Only widen the search to Windows work/school accounts when there is a specific UPN to
            // look for - they must never contribute to the "exactly one account" rule.
            List<IAccount> brokerAccounts = null;
            if (authOptions.HasRequestedUpn
                && !cachedAccounts.Any(acct => string.Equals(acct.Username, authOptions.RequestedUpn.Trim(), StringComparison.OrdinalIgnoreCase)))
            {
                var brokerApp = await GetPublicClientAppAsync(context, listOperatingSystemAccounts: true);
                brokerAccounts = (await brokerApp.GetAccountsAsync()).ToList();
            }

            return SelectAccountFrom(cachedAccounts, brokerAccounts, authOptions.RequestedUpn);
        }

        public static async Task<IReadOnlyList<AvailableEntraAccount>> GetAvailableAccountsAsync()
        {
            var context = CreateDefaultContext(AccessTokenScope.PowerBI);
            var cacheApp = await GetPublicClientAppAsync(context, listOperatingSystemAccounts: false);
            var cachedAccounts = (await cacheApp.GetAccountsAsync()).ToList();

            var brokerApp = await GetPublicClientAppAsync(context, listOperatingSystemAccounts: true);
            var brokerAccounts = (await brokerApp.GetAccountsAsync()).ToList();

            return MergeAvailableAccounts(cachedAccounts, brokerAccounts);
        }

        internal static IReadOnlyList<AvailableEntraAccount> MergeAvailableAccounts(
            IReadOnlyList<IAccount> cachedAccounts,
            IReadOnlyList<IAccount> brokerAccounts)
        {
            var accounts = new Dictionary<string, AvailableEntraAccount>(StringComparer.OrdinalIgnoreCase);

            AddAvailableAccounts(accounts, brokerAccounts, EntraAccountSource.Windows);
            AddAvailableAccounts(accounts, cachedAccounts, EntraAccountSource.DaxStudioCache);

            return accounts.Values
                .OrderBy(account => account.Username, StringComparer.OrdinalIgnoreCase)
                .ThenBy(account => account.TenantId, StringComparer.OrdinalIgnoreCase)
                .ThenBy(account => account.HomeAccountId, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        private static void AddAvailableAccounts(
            IDictionary<string, AvailableEntraAccount> accounts,
            IReadOnlyList<IAccount> sourceAccounts,
            EntraAccountSource source)
        {
            if (sourceAccounts == null) return;

            foreach (var account in sourceAccounts.Where(account => account != null))
            {
                var username = account.Username ?? string.Empty;
                var tenantId = account.HomeAccountId?.TenantId ?? string.Empty;
                var homeAccountId = account.HomeAccountId?.Identifier ?? string.Empty;
                var key = !string.IsNullOrWhiteSpace(homeAccountId)
                    ? homeAccountId
                    : $"{username}|{tenantId}|{account.Environment}";

                accounts[key] = new AvailableEntraAccount(username, tenantId, homeAccountId, source);
            }
        }

        /// <summary>
        /// The account selection rules, isolated from MSAL so they can be tested directly.
        /// </summary>
        /// <param name="cachedAccounts">Accounts from the DAX Studio token cache.</param>
        /// <param name="brokerAccounts">
        /// Accounts from the Windows broker, or null when the broker was not consulted. These are
        /// only ever used to match an explicitly requested UPN - counting them would make the
        /// "exactly one account" rule useless on a machine with several Windows accounts.
        /// </param>
        /// <param name="requestedUpn">The UPN the caller asked for, if any.</param>
        internal static AccountSelectionResult SelectAccountFrom(
            IReadOnlyList<IAccount> cachedAccounts,
            IReadOnlyList<IAccount> brokerAccounts,
            string requestedUpn)
        {
            cachedAccounts = cachedAccounts ?? new List<IAccount>();

            if (!string.IsNullOrWhiteSpace(requestedUpn))
            {
                var upn = requestedUpn.Trim();

                var match = cachedAccounts.FirstOrDefault(acct => string.Equals(acct.Username, upn, StringComparison.OrdinalIgnoreCase));
                if (match != null) return AccountSelectionResult.Matched(match);

                if (brokerAccounts != null)
                {
                    match = brokerAccounts.FirstOrDefault(acct => string.Equals(acct.Username, upn, StringComparison.OrdinalIgnoreCase));
                    if (match != null) return AccountSelectionResult.Matched(match);
                }

                // Nothing to try silently. Substituting any other account here is how a job ends up
                // running as the wrong identity with no prompt and no error.
                return AccountSelectionResult.RequestedAccountNotFound(
                    (brokerAccounts != null && brokerAccounts.Count > 0) ? brokerAccounts : cachedAccounts);
            }

            if (cachedAccounts.Count == 1) return AccountSelectionResult.Matched(cachedAccounts[0]);
            if (cachedAccounts.Count > 1) return AccountSelectionResult.Ambiguous(cachedAccounts);

            return AccountSelectionResult.NoCachedAccounts();
        }

        public static async Task<AuthenticationResult> AcquireTokenAsync(IntPtr? hwnd, IHaveLastUsedUPN options, AccessTokenScope tokenScope, AccessTokenContext context)
        {
            // The desktop app treats the last used UPN as a convenience hint, never as an assertion -
            // the user is always free to pick a different account in the sign-in dialog.
            var authOptions = AuthenticationOptions.ForInteractiveUser(options.LastUsedUPN, hwnd);

            try
            {
                var authResult = await AcquireTokenCoreAsync(context, GetScope(tokenScope), authOptions, serverName: null);
                if (authResult?.Account != null) options.LastUsedUPN = authResult.Account.Username;
                return authResult;
            }
            catch (Exception ex)
            {
                Log.Error(ex, Constants.LogMessageTemplate, nameof(EntraIdHelper), nameof(AcquireTokenAsync), "Error Acquiring Token");
                return null;
            }
        }

        /// <summary>
        /// Acquires a token for a specific server, resolving the account from
        /// <paramref name="authOptions"/> rather than from any shared/persisted setting. This is the
        /// entry point used by the command line, where identity must be deterministic across
        /// concurrent processes.
        /// </summary>
        public static async Task<(AuthenticationResult, AccessTokenContext)> AcquireTokenForConnectionAsync(
            AccessTokenScope tokenScope,
            string serverName,
            AuthenticationOptions authOptions)
        {
            if (authOptions == null) throw new ArgumentNullException(nameof(authOptions));

            IEnumerable<string> scope = GetScope(tokenScope);
            var tenantId = GetTenantIdFromServerName(serverName);
            var authInfo = GetAuthenticationInformationFromUri(new Uri(serverName));

            // Override the scope if the authentication information contains a ResourceId, except for
            // the storage scope which must be preserved for OneLake connections.
            if (!string.IsNullOrEmpty(authInfo.ResourceId) && tokenScope != AccessTokenScope.Storage)
                scope = authInfo.GetDefaultScopes();

            var context = new AccessTokenContext
            {
                TokenScope = tokenScope,
                TenantId = tenantId,
                DomainPostfix = authInfo.DomainPostfix,
                Scope = scope
            };

            var authResult = await AcquireTokenCoreAsync(context, scope, authOptions, serverName);

            context.Username = authResult?.Account?.Username;
            // Bind any later renewal to the exact identity that was authenticated here, so a
            // long-running job cannot silently renew as a different account.
            context.AccountIdentifier = authResult?.Account?.HomeAccountId?.Identifier;

            return (authResult, context);
        }

        /// <summary>
        /// The single token acquisition path. Interactivity is a policy applied at one seam rather
        /// than a separate implementation, so silent and interactive callers cannot drift apart.
        /// </summary>
        private static async Task<AuthenticationResult> AcquireTokenCoreAsync(
            AccessTokenContext context,
            IEnumerable<string> scope,
            AuthenticationOptions authOptions,
            string serverName)
        {
            var selection = await SelectAccountAsync(context, authOptions);

            // The account set must come from an application configured the same way as the one that
            // found it, otherwise a broker-only account cannot be used.
            var needsBrokerAccounts = authOptions.HasRequestedUpn;
            var app = await GetPublicClientAppAsync(context, listOperatingSystemAccounts: needsBrokerAccounts);

            var silentAccount = selection.Account;

            // Falling back to the Windows account is only safe when no specific account was asked
            // for AND the cache is empty - there is then no other identity that could be silently
            // substituted. Doing this when a UPN was requested is how a job ends up running as the
            // wrong user with no prompt and no error.
            if (silentAccount == null
                && selection.Status == AccountSelectionStatus.NoCachedAccounts
                && !authOptions.HasRequestedUpn)
            {
                silentAccount = PublicClientApplication.OperatingSystemAccount;
            }

            AuthenticationResult authResult = null;

            if (silentAccount != null)
            {
                try
                {
                    authResult = await app.AcquireTokenSilent(scope, silentAccount).ExecuteAsync().ConfigureAwait(false);
                    return AssertRequestedIdentity(authResult, authOptions);
                }
                catch (MsalUiRequiredException ex)
                {
                    Log.Information(Constants.LogMessageTemplate, nameof(EntraIdHelper), nameof(AcquireTokenCoreAsync),
                        $"Silent token acquisition requires interaction: {ex.Message}");
                }
            }

            authResult = await AcquireTokenInteractivelyAsync(app, context, scope, authOptions, selection, serverName);
            return AssertRequestedIdentity(authResult, authOptions);
        }

        /// <summary>
        /// The one place where the user can be prompted. Everything above this point is identical
        /// regardless of whether interaction is permitted.
        /// </summary>
        private static async Task<AuthenticationResult> AcquireTokenInteractivelyAsync(
            IPublicClientApplication app,
            AccessTokenContext context,
            IEnumerable<string> scope,
            AuthenticationOptions authOptions,
            AccountSelectionResult selection,
            string serverName)
        {
            if (!authOptions.AllowInteractivePrompt)
            {
                throw EntraAuthenticationException.InteractionRequired(
                    DescribeWhyInteractionIsNeeded(selection, authOptions),
                    authOptions.RequestedUpn,
                    selection.Candidates,
                    serverName);
            }

            var builder = app.AcquireTokenInteractive(scope)
                .WithParentActivityOrWindow(GetOwnerWindowHandle(authOptions.OwnerWindowHandle)) // owner for the WAM sign-in dialog so it stays in front of the DAX Studio window
                .WithExtraQueryParameters(MicrosoftAccountOnlyQueryParameters);

            if (selection.Account != null)
            {
                // Targeted at a known account, so no picker is needed.
                builder = builder.WithAccount(selection.Account);
            }
            else if (authOptions.HasRequestedUpn)
            {
                // Pre-fill the requested account. Prompt.SelectAccount must NOT be combined with a
                // login hint - the service shows the picker and ignores the hint.
                builder = builder.WithLoginHint(authOptions.RequestedUpn);
            }
            else
            {
                builder = builder.WithPrompt(Prompt.SelectAccount);
            }

            try
            {
                return await builder.ExecuteAsync().ConfigureAwait(false);
            }
            catch (MsalException msalex)
            {
                Log.Error(msalex, Constants.LogMessageTemplate, nameof(EntraIdHelper), nameof(AcquireTokenInteractivelyAsync), "Error Acquiring Token Interactively");
                throw;
            }
        }

        internal static string DescribeWhyInteractionIsNeeded(AccountSelectionResult selection, AuthenticationOptions authOptions)
        {
            switch (selection.Status)
            {
                case AccountSelectionStatus.RequestedAccountNotFound:
                    return $"The account '{authOptions.RequestedUpn}' is not signed in on this machine.";
                case AccountSelectionStatus.Ambiguous:
                    return "Several accounts are available and no account was specified, so the identity to use is ambiguous. Specify one with -u|--userid.";
                case AccountSelectionStatus.NoCachedAccounts:
                    return "No accounts are signed in on this machine.";
                default:
                    return $"The cached sign-in for '{selection.Account?.Username ?? authOptions.RequestedUpn}' has expired or requires re-authentication.";
            }
        }

        /// <summary>
        /// Verifies that the identity actually authenticated is the one that was demanded. Login
        /// hints are best-effort and an operator can choose a different account in the picker, so
        /// this check - not the UI branching above - is what guarantees correctness.
        /// </summary>
        private static AuthenticationResult AssertRequestedIdentity(AuthenticationResult authResult, AuthenticationOptions authOptions)
        {
            if (authResult == null) return null;

            var actualUpn = authResult.Account?.Username;
            if (IsAcceptableIdentity(authOptions.RequestedUpn, actualUpn, authOptions.EnforceRequestedUpn)) return authResult;

            Log.Error(Constants.LogMessageTemplate, nameof(EntraIdHelper), nameof(AssertRequestedIdentity),
                $"Requested account '{authOptions.RequestedUpn}' but authenticated as '{actualUpn}'");
            throw EntraAuthenticationException.IdentityMismatch(authOptions.RequestedUpn, actualUpn);
        }

        /// <summary>
        /// True when the authenticated identity is acceptable. Isolated from MSAL so it can be
        /// tested directly. A requested UPN that is only a hint (the desktop app) never fails here.
        /// </summary>
        internal static bool IsAcceptableIdentity(string requestedUpn, string actualUpn, bool enforce)
        {
            if (!enforce) return true;
            if (string.IsNullOrWhiteSpace(requestedUpn)) return true;

            return string.Equals(actualUpn, requestedUpn.Trim(), StringComparison.OrdinalIgnoreCase);
        }


        /// <summary>
        /// SILENTLY acquires a Microsoft Graph token for an account that already signed in for the
        /// Power BI scope. The Power BI/ADOMD client id is not authorized for Microsoft Graph (WAM
        /// returns 'IncorrectConfiguration'), so this uses a separate Graph-capable first-party client
        /// built WITH the WAM broker. Because the broker shares Windows accounts across apps, it can
        /// reuse the same signed-in account via SSO without prompting. This NEVER prompts the user
        /// (no interactive/consent UI), so it returns null whenever a token cannot be obtained purely
        /// silently (e.g. consent has not yet been granted for this client) - the token is optional.
        /// </summary>
        public static async Task<AuthenticationResult> AcquireGraphTokenSilentAsync(IAccount account)
        {
            try
            {
                var app = await GetGraphClientAppAsync();

                // Prefer the account from the Power BI sign-in; fall back to the broker's OS account
                // (the PBI client signs in through WAM, so the account is shared at the Windows level).
                var graphAccount = account
                    ?? (await app.GetAccountsAsync()).FirstOrDefault()
                    ?? PublicClientApplication.OperatingSystemAccount;

                return await app.AcquireTokenSilent(graphScope, graphAccount).ExecuteAsync();
            }
            catch (MsalUiRequiredException ex)
            {
                // Interaction/consent would be required - skip rather than prompting just for an avatar.
                Log.Warning(ex, Constants.LogMessageTemplate, nameof(EntraIdHelper), nameof(AcquireGraphTokenSilentAsync),
                    "Could not silently acquire a Microsoft Graph token");
                return null;
            }
            catch (Exception ex)
            {
                Log.Error(ex, Constants.LogMessageTemplate, nameof(EntraIdHelper), nameof(AcquireGraphTokenSilentAsync),
                    "Error acquiring Microsoft Graph token");
                return null;
            }
        }

        /// <summary>
        /// Builds (and caches) the public client application used for Microsoft Graph. It uses a
        /// Graph-capable first-party client id and the WAM broker so it can silently reuse the
        /// Windows account that signed in for Power BI.
        /// </summary>
        private static async Task<IPublicClientApplication> GetGraphClientAppAsync()
        {
            if (_graphClientApp != null) return _graphClientApp;

            var brokerOptions = new BrokerOptions(BrokerOptions.OperatingSystems.Windows);

            _graphClientApp = PublicClientApplicationBuilder.Create(GraphClientId)
                .WithAuthority(DefaultAuthority)
                .WithDefaultRedirectUri()
                .WithBroker(brokerOptions)
                .Build();

            // Share the same on-disk MSAL cache as the rest of the application.
            MsalCacheHelper cacheHelper = await CreateCacheHelperAsync();
            cacheHelper.RegisterCache(_graphClientApp.UserTokenCache);

            return _graphClientApp;
        }

        /// <summary>
        /// Creates an AccessTokenContext for a given token scope using the Power BI API endpoint as the default server
        /// </summary>
        /// <param name="tokenScope">The scope for which to create the context</param>
        /// <returns>An AccessTokenContext configured for the specified scope</returns>
        public static AccessTokenContext CreateDefaultContext(AccessTokenScope tokenScope)
        {
            // Use the Power BI API endpoint as the default for context creation
            var defaultServerName = "powerbi://api.powerbi.com";
            
            try
            {
                var tenantId = string.Empty; // Empty tenant ID will use the default/organizations endpoint
                var authInfo = GetAuthenticationInformationFromUri(new Uri(defaultServerName));
                
                IEnumerable<string> scope = GetScope(tokenScope);

                // Override the scope if the authentication information contains a ResourceId, except for
                // the storage scope which must be preserved for OneLake connections. Without this guard a
                // storage context is built holding the Power BI scope, which is then used to renew the
                // token - producing a Power BI token that OneLake rejects.
                if (!string.IsNullOrEmpty(authInfo.ResourceId) && tokenScope != AccessTokenScope.Storage)
                    scope = authInfo.GetDefaultScopes();
                
                var context = new AccessTokenContext
                {
                    TokenScope = tokenScope,
                    TenantId = tenantId,
                    DomainPostfix = authInfo.DomainPostfix,
                    Scope = scope
                };
                
                return context;
            }
            catch (Exception ex)
            {
                Log.Error(ex, Constants.LogMessageTemplate, nameof(EntraIdHelper), nameof(CreateDefaultContext), 
                    $"Error creating default context for {tokenScope}");
                throw;
            }
        }

        public static async Task<IPublicClientApplication> GetPublicClientAppAsync(AccessTokenContext context)
        {
            return await GetPublicClientAppAsync(context, listOperatingSystemAccounts: false);
        }

        /// <summary>
        /// Builds the public client application for the supplied context.
        /// </summary>
        /// <param name="listOperatingSystemAccounts">
        /// Controls <see cref="BrokerOptions.ListOperatingSystemAccounts"/>, which decides what
        /// <c>GetAccountsAsync()</c> returns. MSAL merges the accounts held in the DAX Studio token
        /// cache with those reported by the WAM broker, and the broker returns nothing at all unless
        /// this flag is set. That gives two useful account sets from one API:
        /// <list type="bullet">
        /// <item><description><c>false</c> - only accounts that have deliberately signed in to DAX
        /// Studio. Used to decide whether there is exactly one obvious account, so the "single
        /// account" rule still fires on a machine with many Windows accounts.</description></item>
        /// <item><description><c>true</c> - also every Windows work/school account, so an explicitly
        /// requested UPN can be matched without a prior DAX Studio sign-in.</description></item>
        /// </list>
        /// Rebuilding the application is cheap here because it is not cached between calls.
        /// </param>
        public static async Task<IPublicClientApplication> GetPublicClientAppAsync(AccessTokenContext context, bool listOperatingSystemAccounts)
        {
            //if (_clientApp != null) return _clientApp;

            BrokerOptions brokerOptions = new BrokerOptions(BrokerOptions.OperatingSystems.Windows)
            {
                ListOperatingSystemAccounts = listOperatingSystemAccounts
            };

           
            if (!TryFindAuthenticationInformation(new Uri ($"powerbi://{context.DomainPostfix}"), out var authenticationInformation))
                throw new ArgumentException($"Could not find authentication information for domain postfix: {context.DomainPostfix}", nameof(context.DomainPostfix));

            var defaultAuthority = authenticationInformation.Authority;

            var authority = ReplaceTenantInInstance(defaultAuthority, context.TenantId);

            var clientId = authenticationInformation.ApplicationId;

            Log.Debug(Constants.LogMessageTemplate, nameof(EntraIdHelper), nameof(GetPublicClientAppAsync), $"Using Authority: {authority}, ClientId: {clientId}, DomainPostfix: {context.DomainPostfix}, TenantId: {context.TenantId}");

            _clientApp = PublicClientApplicationBuilder.Create(clientId)
                //.WithAuthority($"{Instance}{Tenant}")
                .WithAuthority(authority)
                .WithExtraQueryParameters(MicrosoftAccountOnlyQueryParameters)
                .WithDefaultRedirectUri()
                .WithBroker(brokerOptions)
                .Build();

            MsalCacheHelper cacheHelper = await CreateCacheHelperAsync();
            
            // Let the cache helper handle MSAL's cache, otherwise the user will be prompted to sign-in every time.
            cacheHelper.RegisterCache(_clientApp.UserTokenCache);

            return _clientApp;
        }


        private static Uri ReplaceTenantInInstance(string instance, string tenantId)
        {
            if (string.IsNullOrEmpty(tenantId))
            {
                return new Uri(instance);
            }

            return new Uri(instance.Replace("organizations", tenantId));
        }

        public static async Task<(AuthenticationResult,AccessTokenContext)> PromptForAccountAsync(IntPtr? hwnd, IHaveLastUsedUPN options, AccessTokenScope tokenScope, string serverName)
        {

            IEnumerable<string> scope = GetScope(tokenScope);
            var tenantId = GetTenantIdFromServerName(serverName);

            var authInfo = GetAuthenticationInformationFromUri(new Uri( serverName));

            // override the scope if the authentication information contains a ResourceId
            if (!string.IsNullOrEmpty(authInfo.ResourceId))
                scope = authInfo.GetDefaultScopes();

            var context = new AccessTokenContext
            {
                TokenScope = tokenScope,
                TenantId = tenantId,
                DomainPostfix = authInfo.DomainPostfix,
                Scope = scope
            };

            if (Log.IsEnabled(Serilog.Events.LogEventLevel.Debug))
                Log.Debug("{class} {method} Prompting user to sign-in interactively. Authority: {authority}, ClientId: {applicationId}, DomainPostfix: {domainPostfix}, TenantId: {tenantId} Scope: {@scope}",
                            nameof(EntraIdHelper), nameof(PromptForAccountAsync), authInfo.Authority, authInfo.ApplicationId, authInfo.DomainPostfix, tenantId, scope);

            var app = await GetPublicClientAppAsync(context);

            try
            {
                var authResult = await app.AcquireTokenInteractive(scope)
                            .WithParentActivityOrWindow(GetOwnerWindowHandle(hwnd)) // owner for the WAM sign-in dialog so it stays in front of the DAX Studio window
                            .WithExtraQueryParameters(MicrosoftAccountOnlyQueryParameters)
                            .WithPrompt(Prompt.SelectAccount)
                            .ExecuteAsync();
                options.LastUsedUPN = authResult.Account.Username;
                context.Username = authResult.Account.Username;
                return (authResult, context);
            }
            catch (Exception ex)
            {
                Log.Error(ex, Constants.LogMessageTemplate, nameof(EntraIdHelper), nameof(PromptForAccountAsync), "Error getting user token interactively");
                throw;
            }
        }

        internal static AuthenticationInformationRecord GetAuthenticationInformationFromUri(Uri serverName)
        {
            if (serverName == null) throw new ArgumentNullException(nameof(serverName));
            
            var record = (AuthenticationInformationRecord)null;
            if (!TryFindAuthenticationInformation(serverName, out record))
                throw new ArgumentException($"Could not find authentication information for server: {serverName}", nameof(serverName));



            return record;
        }

        private static AuthenticationInformationRecord[] remoteSecurityConfig;
        private static AuthenticationInformationRecord[] embeddedSecurityConfig;
        private static readonly char[] separator = new[] { '/' };

        private static AuthenticationInformationRecord[] GetAuthenticationInformation(        
            bool isEmbeddedInfo)
        {
            if (isEmbeddedInfo)
            {
                Log.Verbose(Constants.LogMessageTemplate, nameof(EntraIdHelper), nameof(GetAuthenticationInformation), "Getting authentication information from embedded config");
                if (embeddedSecurityConfig == null)
                {
                    Assembly executingAssembly = Assembly.GetExecutingAssembly();
                    using (Stream manifestResourceStream = executingAssembly.GetManifestResourceStream(((IEnumerable<string>)executingAssembly.GetManifestResourceNames()).FirstOrDefault<string>((Func<string, bool>)(name => name.EndsWith("ASAzureSecurityConfig.xml",StringComparison.InvariantCultureIgnoreCase)))))
                        embeddedSecurityConfig = DeserializeAuthenticationInformation(manifestResourceStream);
                }
                return embeddedSecurityConfig;
            }

            Log.Verbose(Constants.LogMessageTemplate, nameof(EntraIdHelper), nameof(GetAuthenticationInformation), "Getting authentication information from remote config");
            if (remoteSecurityConfig == null)
            {
                try
                {
                    // Synchronous wait is acceptable here since callers of GetAuthenticationInformation are synchronous.
                    byte[] data = _httpClient.GetByteArrayAsync(new Uri("https://global.asazure.windows.net/ASAzureSecurityConfig.xml")).Result;
                    using (Stream info = new MemoryStream(data))
                        remoteSecurityConfig = DeserializeAuthenticationInformation(info);
                }
                catch (HttpRequestException)
                {
                    remoteSecurityConfig = Array.Empty<AuthenticationInformationRecord>();
                }
            }
            return remoteSecurityConfig;
        }

  

        internal static bool TryFindAuthenticationInformation(
            Uri dataSource,
            out AuthenticationInformationRecord record)
        {
            record = (AuthenticationInformationRecord)null;
            return TryFindAuthenticationInformation(dataSource, GetAuthenticationInformation(true), out record) || TryFindAuthenticationInformation(dataSource, GetAuthenticationInformation(false), out record);
        }

        private static bool TryFindAuthenticationInformation(
                      Uri dataSource,
                      AuthenticationInformationRecord[] knownRecords,
                      out AuthenticationInformationRecord record)
        {
            var host = dataSource.Host;
            record = (AuthenticationInformationRecord)null;
            for (int index = 0; index < knownRecords.Length; ++index)
            {
                if (host.EndsWith(knownRecords[index].DomainPostfix, StringComparison.InvariantCultureIgnoreCase)
                    && (record == null || knownRecords[index].DomainPostfix.Length > record.DomainPostfix.Length))
                {
                    record = knownRecords[index];
                    record.Authority = record.Authority.Replace("/common", "/organizations");
                    if (string.IsNullOrWhiteSpace(record.Authority)) {
                        Log.Debug(Constants.LogMessageTemplate, nameof(EntraIdHelper), nameof(TryFindAuthenticationInformation), $"No Authority found in config for {dataSource}, using default ADOMD Authority");
                        record.Authority = DefaultAuthority; 
                    }
                    if (string.IsNullOrWhiteSpace(record.ApplicationId)) { 
                        Log.Debug(Constants.LogMessageTemplate, nameof(EntraIdHelper), nameof(TryFindAuthenticationInformation), $"No ApplicationId found in config for {dataSource}, using default ADOMD ClientId");
                        record.ApplicationId = DefaultClientId; 
                    }
                    if (string.IsNullOrWhiteSpace(record.ResourceId))
                    {
                        Log.Debug(Constants.LogMessageTemplate, nameof(EntraIdHelper), nameof(TryFindAuthenticationInformation), $"No Resource found in config for {dataSource}, generating from dataSource");
                        record.ResourceId = string.Format(CultureInfo.InvariantCulture, "https://{0}", dataSource.Host);
                    }
                    else
                    {
                        record.ResourceId = new Uri(record.ResourceId).AbsoluteUri;
                    }
                }
            }
            return record != null;
        }

        private static AuthenticationInformationRecord[] DeserializeAuthenticationInformation(  Stream info)
        {
            using (XmlDictionaryReader textReader = XmlDictionaryReader.CreateTextReader(info, new XmlDictionaryReaderQuotas()))
                return (AuthenticationInformationRecord[])new DataContractSerializer(typeof(AuthenticationInformationRecord[]), "AuthenticationInformations", string.Empty).ReadObject(textReader, true);
        }

        public static string GetTenantIdFromServerName(string serverName)
        {
            if (string.IsNullOrWhiteSpace(serverName))
            {
                return string.Empty;
            }
            else if (serverName.StartsWith("asazure://", StringComparison.OrdinalIgnoreCase))
            {
                return GetTenantForAsAzure(serverName);
            }
            else if (serverName.RequiresEntraAuth())
            {
                return GetTenantForPowerBI(serverName);
            }
            else
            {
                throw new ArgumentException($"Unsupported server name format: {serverName}");
            }
        }

        private static string GetTenantForPowerBI(string serverName)
        {
            //Look for a guid in the serverName
            var parts = serverName.Split(separator, StringSplitOptions.RemoveEmptyEntries);

            var match = regexGuid.Match(serverName);
            if (match.Success)
            {
                var tenant = match.Groups["tenant"].Value;
                if (String.Equals(tenant, "myorg", StringComparison.OrdinalIgnoreCase)) return string.Empty;
                // If we found a GUID, return it as the tenant ID
                return tenant;
            }

            return string.Empty; // This indicates the default tenant, which is usually the first tenant in the list
        }

        private static string GetTenantForAsAzure(string serverName)
        {
            /*
             * request POST https://australiasoutheast.asazure.windows.net/webapi/clusterResolve
            {
                    "ServerName": "dev",
                    "DatabaseName": "",
                    "PremiumPublicXmlaEndpoint" : false
            }
            * response
            {
	            "clusterFQDN": "asazureause1-australiasoutheast.asazure.windows.net",
	            "coreServerName": "dev",
	            "tenantId": "d2d5283f-21bf-4fb9-bfa1-1e91215840c1"
            }
            */
            var parts = serverName.Split(separator, StringSplitOptions.RemoveEmptyEntries);
            var host = parts.Length > 1 ? parts[1] : string.Empty;
            var server = parts.Length > 2 ? parts[2].Replace(":rw", string.Empty) : string.Empty;
            // SSL 3.0 is disabled by default on supported .NET runtimes; explicit removal is no longer required.
            Uri uri = new Uri($"https://{host}/webapi/ClusterResolve");

            NameResolutionRequest requestContent = new NameResolutionRequest
            {
                ServerName = server,
                DatabaseName = "",
                PremiumPublicXmlaEndpoint = false
            };

            var requestSerializer = new DataContractJsonSerializer(typeof(NameResolutionRequest));
            byte[] requestBytes;
            using (MemoryStream memoryStream = new MemoryStream())
            {
                requestSerializer.WriteObject((Stream)memoryStream, (object)requestContent);
                requestBytes = memoryStream.ToArray();
            }

            using (var request = new HttpRequestMessage(HttpMethod.Post, uri))
            {
                request.Headers.UserAgent.ParseAdd("ADOMD.NET");
                request.Content = new ByteArrayContent(requestBytes);
                request.Content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/json");

                using (var response = _httpClient.SendAsync(request).Result)
                {
                    if (response.StatusCode != HttpStatusCode.OK)
                    {
                        throw new HttpRequestException($"Unexpected response status code: {response.StatusCode}");
                    }
                    var responseSerializer = new DataContractJsonSerializer(typeof(NameResolutionResult));
                    using (Stream responseStream = response.Content.ReadAsStreamAsync().GetAwaiter().GetResult())
                        return ((NameResolutionResult)responseSerializer.ReadObject(responseStream)).TenantId;
                }
            }
        }


        private static async Task<MsalCacheHelper> CreateCacheHelperAsync()
        {
            // Since this is a WPF application, only Windows storage is configured
            var storageProperties = new StorageCreationPropertiesBuilder(
                              //System.Reflection.Assembly.GetExecutingAssembly().GetName().Name + ".msalcache.bin",
                              "DaxStudio.msalcache.bin",
                              MsalCacheHelper.UserRootDirectory)
                                .Build();

            MsalCacheHelper cacheHelper = await MsalCacheHelper.CreateAsync(
                        storageProperties,
                        new TraceSource("MSAL.CacheTrace"))
                     .ConfigureAwait(false);

            return cacheHelper;
        }


        private struct TokenDetails
        {
            public TokenDetails(Tom.AccessToken token)
            {
                AccessToken = token.Token;
                ExpiresOn = token.ExpirationTime;
                UserContext = token.UserContext as AccessTokenContext;
            }
#if NET472
            public TokenDetails(Microsoft.AnalysisServices.AdomdClient.AccessToken token)
            {
                AccessToken = token.Token;
                ExpiresOn = token.ExpirationTime;
                UserContext = token.UserContext as AccessTokenContext;
            }
#endif
            public string AccessToken;
            public DateTimeOffset ExpiresOn;
            public AccessTokenContext UserContext;
        }

        public static async Task<Tom.AccessToken> RefreshToken(Tom.AccessToken token, bool forceSilent = false)
        {
            var details = new TokenDetails(token);
            var authResult = await RefreshTokenInternalAsync(details, forceSilent);
            Tom.AccessToken newToken = new Tom.AccessToken(authResult.AccessToken, authResult.ExpiresOn, details.UserContext);
            return newToken;
        }

#if NET472
        public static async Task<Adomd.AccessToken> RefreshToken(Adomd.AccessToken token, bool forceSilent = false)
        {
            var details = new TokenDetails(token);
            var authResult = await RefreshTokenInternalAsync(details, forceSilent);
            AccessToken newToken = new AccessToken(authResult.AccessToken, authResult.ExpiresOn, details.UserContext);
            return newToken;
        }
#endif

        public static AccessToken CreateAccessToken(string token, DateTimeOffset expiry, AccessTokenContext context)
        {
            var accessToken = new AccessToken(token, expiry, context);
            return accessToken;
        }

        private static async Task<AuthenticationResult> RefreshTokenInternalAsync(TokenDetails token, bool forceSilent = false)
        {
            var context = token.UserContext;
            if (context == null) throw new EntraAuthenticationException("Cannot renew an access token that has no authentication context.");

            // A caller can force silent-only renewal (e.g. a trace drop running synchronously on the
            // UI thread) on top of whatever the context already allows.
            var silentOnly = forceSilent || context.RenewalMode == TokenRenewalMode.SilentOnly;

            var accountIdentifier = context.AccountIdentifier ?? string.Empty;
            var lastUpn = context.Username ?? string.Empty;

            // A renewal must target the account the token was originally issued to, so look in the
            // broker set too - the original sign-in may have used a Windows account.
            var app = await GetPublicClientAppAsync(context, listOperatingSystemAccounts: !string.IsNullOrEmpty(accountIdentifier) || !string.IsNullOrEmpty(lastUpn));
            var accounts = (await app.GetAccountsAsync()).ToList();

            var account = SelectRenewalAccount(accounts, accountIdentifier, lastUpn);

            var scope = context.Scope;

            if (account != null)
            {
                try
                {
                    return await app.AcquireTokenSilent(scope, account).ExecuteAsync().ConfigureAwait(false);
                }
                catch (MsalUiRequiredException ex)
                {
                    Log.Information(Constants.LogMessageTemplate, nameof(EntraIdHelper), nameof(RefreshTokenInternalAsync),
                        $"Silent token renewal requires interaction: {ex.Message}");
                }
            }

            if (silentOnly)
            {
                throw EntraAuthenticationException.InteractionRequired(
                    account == null
                        ? $"The account '{lastUpn}' used for this connection is no longer available on this machine."
                        : $"The cached sign-in for '{lastUpn}' has expired and cannot be renewed without signing in again.",
                    lastUpn,
                    accounts.Select(a => a.Username).ToList(),
                    serverName: null);
            }

            Log.Warning(Constants.LogMessageTemplate, nameof(EntraIdHelper), nameof(RefreshTokenInternalAsync), "Token could not be renewed silently, prompting user to sign-in interactively");

            try
            {
                var builder = app.AcquireTokenInteractive(scope)
                    .WithParentActivityOrWindow(GetOwnerWindowHandle(null)) // owner for the WAM sign-in dialog so it stays in front of the DAX Studio window
                    .WithExtraQueryParameters(MicrosoftAccountOnlyQueryParameters);

                if (account != null) builder = builder.WithAccount(account);
                else if (!string.IsNullOrEmpty(lastUpn)) builder = builder.WithLoginHint(lastUpn);
                else builder = builder.WithPrompt(Prompt.SelectAccount);

                var authResult = await builder.ExecuteAsync().ConfigureAwait(false);

                // Renewal must not quietly change identity mid-job.
                if (!string.IsNullOrEmpty(lastUpn)
                    && !string.Equals(authResult.Account?.Username, lastUpn, StringComparison.OrdinalIgnoreCase))
                    throw EntraAuthenticationException.IdentityMismatch(lastUpn, authResult.Account?.Username);

                return authResult;
            }
            catch (MsalException msalex)
            {
                Log.Error(msalex, Constants.LogMessageTemplate, nameof(EntraIdHelper), nameof(RefreshTokenInternalAsync), "Error Acquiring Token Interactively");
                throw;
            }
        }

        internal static IAccount SelectRenewalAccount(
            IReadOnlyList<IAccount> accounts,
            string accountIdentifier,
            string username)
        {
            accounts = accounts ?? new List<IAccount>();

            // Once a token has been bound to a home account id, never fall back to a username.
            // The same UPN can identify accounts in different tenants.
            if (!string.IsNullOrEmpty(accountIdentifier))
                return accounts.FirstOrDefault(acct => string.Equals(
                    acct.HomeAccountId?.Identifier,
                    accountIdentifier,
                    StringComparison.OrdinalIgnoreCase));

            if (!string.IsNullOrEmpty(username))
                return accounts.FirstOrDefault(acct => string.Equals(
                    acct.Username,
                    username,
                    StringComparison.OrdinalIgnoreCase));

            return null;
        }

        private static string[] GetScope(TokenDetails tokenDetails)
        {
            return GetScope(tokenDetails.UserContext.TokenScope);
        }

        private static string[] GetScope(AccessTokenScope scope)
        {
            if (scope == AccessTokenScope.AsAzure)
                return asazureScope;
            else if (scope == AccessTokenScope.Storage)
                return storageScope;
            else
                return powerbiScope;
        }

    }

    [DataContract]
    class NameResolutionRequest
    {
        [DataMember(Name = "serverName")]
        public string ServerName { get; set; }

        [DataMember(Name = "databaseName")]
        public string DatabaseName { get; set; }

        [DataMember(Name = "premiumPublicXmlaEndpoint")]
        public bool PremiumPublicXmlaEndpoint { get; set; }
    }

    [DataContract]
    class NameResolutionResult
    {
        [DataMember(Name = "clusterFQDN")]
        public string ClusterFqdn { get; set; }

        [DataMember(Name = "coreServerName")]
        public string CoreServerName { get; set; }

        [DataMember(Name = "tenantId")]
        public string TenantId { get; set; }
    }

    [DataContract(Name = "AuthenticationInformation", Namespace = "")]
    sealed class AuthenticationInformationRecord
    {
        [DataMember(Name = "DomainPostfix", Order = 0)]
        public string DomainPostfix { get; private set; }

        [DataMember(Name = "Authority", Order = 1)]
        public string Authority { get; internal set; }

        [DataMember(Name = "Authority.v2", Order = 2, EmitDefaultValue = true)]
        public string Authority2 { get; private set; }

        [DataMember(Name = "ApplicationId", Order = 3)]
        public string ApplicationId { get; internal set; }

        [DataMember(Name = "ResourceId", Order = 4, EmitDefaultValue = true)]
        public string ResourceId { get; internal set; }


        internal IEnumerable<string> GetDefaultScopes()
        {
            return (IEnumerable<string>)new string[1]
            {
                string.Format(CultureInfo.InvariantCulture,"{0}/.default", ResourceId)
            };
        }
    
        internal string GetTokenCacheKey()
        {
            return string.Format(CultureInfo.InvariantCulture, "{0}|{1}|{2}", (object)this.Authority, (object)this.ApplicationId, (object)this.ResourceId);
        }
    }

}
