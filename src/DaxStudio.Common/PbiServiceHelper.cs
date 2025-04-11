using DaxStudio.Common;
using Microsoft.AnalysisServices.AdomdClient;
using Tom = Microsoft.AnalysisServices;
using Microsoft.Identity.Client;
using Microsoft.Identity.Client.Broker;
using Microsoft.Identity.Client.Extensions.Msal;
using Microsoft.PowerBI.Api;
using Microsoft.PowerBI.Api.Models;
using Microsoft.Rest;
using Serilog;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Interop;
using System.Windows;
using System.Windows.Controls;
using DaxStudio.Common.Interfaces;

namespace DaxStudio.Common
{

    public struct Workspace
    {
        public string Name { get; set; }
        public Guid Id { get; set; }
        public bool? IsOnPremiumCapacity { get; set; }
    }

    public static class PbiServiceHelper
    {
        private const string MicrosoftAccountOnlyQueryParameter = "msafed=0"; // Restrict logins to only AAD based organizational accounts
        private static IPublicClientApplication _clientApp;
        //private static string ClientId = "90fd9dec-463e-4e03-8cbe-8f0baa9bb7e8";
        //private static string ClientId = "7f67af8a-fedc-4b08-8b4e-37c4d127b6cf";  // PBI Desktop Client ID
        private static string ClientId = "cf710c6e-dfcc-4fa8-a093-d47294e44c66"; // ADOMD Client ID
        private static string Instance = "https://login.microsoftonline.com/";
        //private static string commonInstance = "https://login.microsoftonline.com/common";
        private static string scope = "organizations";
        //private static string Instance = "https://login.microsoftonline.com/common/oauth2/nativeclient";
        private static IEnumerable<string> scopes = new List<string>() { "https://analysis.windows.net/powerbi/api/.default" };
        public static async Task<List<Workspace>> GetWorkspacesAsync(AuthenticationResult token)
        {
            var workspaces = new List<Workspace>();
            TokenCredentials creds = new TokenCredentials(token.AccessToken, "Bearer");

            using (var client = new PowerBIClient(creds))
            {
                Groups grps;
                grps = await client.Groups.GetGroupsAsync();

                foreach (var grp in grps.Value)
                {
                    workspaces.Add(new Workspace
                    {
                        Name = grp.Name,
                        Id = grp.Id,
                        IsOnPremiumCapacity = grp.IsOnDedicatedCapacity
                    });
                }
            }
            return workspaces;
        }

        public static async Task<AuthenticationResult> AcquireTokenAsync(IntPtr? hwnd, IHaveLastUsedUPN options)
        {

            AuthenticationResult authResult = null;
            var app = GetPublicClientApp();
            IAccount firstAccount = null;
            var accounts = await app.GetAccountsAsync();

            // if the user signed-in before, try to get that account info from the cache
            if (!string.IsNullOrEmpty(options.LastUsedUPN))
            {
                firstAccount = accounts.FirstOrDefault(acct => string.Equals(acct.Username, options.LastUsedUPN, StringComparison.CurrentCultureIgnoreCase));
            }

            // try to get the first account from the cache
            if (firstAccount == null && string.IsNullOrEmpty(options.LastUsedUPN))
            {
                firstAccount = accounts.FirstOrDefault();
            }

            // otherwise, try with the Windows account
            if (firstAccount == null)
            {
                firstAccount = PublicClientApplication.OperatingSystemAccount;
            }

            try
            {
                authResult = await app.AcquireTokenSilent(scopes, firstAccount).ExecuteAsync();
                options.LastUsedUPN = authResult.Account.Username;
            }
            catch (MsalUiRequiredException)
            {
                // A MsalUiRequiredException happened on AcquireTokenSilent. 
                // This indicates you need to call AcquireTokenInteractive to acquire a token
                Log.Warning(Constants.LogMessageTemplate, nameof(PbiServiceHelper), nameof(AcquireTokenAsync), "User not found in cache, prompting user to sign-in interactively");

                try
                {
                    authResult = await app.AcquireTokenInteractive(scopes)
                        .WithAccount(firstAccount)
                        .WithParentActivityOrWindow(hwnd) // optional, used to center the browser on the window
                                                          //.WithParentActivityOrWindow(Process.GetCurrentProcess().MainWindowHandle)
                        .WithExtraQueryParameters(MicrosoftAccountOnlyQueryParameter)
                        .WithPrompt(Prompt.SelectAccount)
                        .ExecuteAsync();
                    options.LastUsedUPN = authResult.Account.Username;
                }
                catch (MsalException msalex)
                {
                    Log.Error(msalex, Constants.LogMessageTemplate, nameof(PbiServiceHelper), nameof(AcquireTokenAsync), "Error Acquiring Token Interactively");
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, Constants.LogMessageTemplate, nameof(PbiServiceHelper), nameof(AcquireTokenAsync), "Error Acquiring Token Silently");
            }


            return authResult;
        }

        public static IPublicClientApplication GetPublicClientApp()
        {
            if (_clientApp != null) return _clientApp;

            BrokerOptions brokerOptions = new BrokerOptions(BrokerOptions.OperatingSystems.Windows);

            _clientApp = PublicClientApplicationBuilder.Create(ClientId)
                //.WithAuthority($"{Instance}{Tenant}")
                .WithAuthority($"{Instance}{scope}")
                .WithExtraQueryParameters(MicrosoftAccountOnlyQueryParameter)
                .WithDefaultRedirectUri()
                .WithBroker(brokerOptions)
                .Build();

            MsalCacheHelper cacheHelper = CreateCacheHelperAsync().GetAwaiter().GetResult();

            // Let the cache helper handle MSAL's cache, otherwise the user will be prompted to sign-in every time.
            cacheHelper.RegisterCache(_clientApp.UserTokenCache);

            return _clientApp;
        }

        public static async Task<AuthenticationResult> SwitchAccount(IntPtr? hwnd, IHaveLastUsedUPN options)
        {
            var app = GetPublicClientApp();
            try
            {
                var authResult = await app.AcquireTokenInteractive(scopes)
                            .WithParentActivityOrWindow(hwnd) // optional, used to center the browser on the window
                                                              //.WithParentActivityOrWindow(Process.GetCurrentProcess().MainWindowHandle)
                            .WithExtraQueryParameters(MicrosoftAccountOnlyQueryParameter)
                            .WithPrompt(Prompt.SelectAccount)
                            .ExecuteAsync();
                options.LastUsedUPN = authResult.Account.Username;
                return authResult;
            }
            catch (Exception ex)
            {
                Log.Error(ex, Constants.LogMessageTemplate, nameof(PbiServiceHelper), nameof(SwitchAccount), "Error getting user token interactively");
                throw;
                return null;
            }
        }

        public static async Task SignOutAsync()
        {
            var app = GetPublicClientApp();


            IAccount firstAccount = (await app.GetAccountsAsync()).FirstOrDefault();
            if (firstAccount == null)
            {
                return;
            }
            await app.RemoveAsync(firstAccount);
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

        public static async Task<IEnumerable<IAccount>> GetAccountsAsync()
        {
            var app = GetPublicClientApp();
            var accounts = await app.GetAccountsAsync();
            return accounts;
        }

        private struct TokenDetails
        {
            public TokenDetails(AccessToken token)
            {
                AccessToken = token.Token;
                ExpiresOn = token.ExpirationTime;
                UserContext = token.UserContext;
            }
            public TokenDetails(Tom.AccessToken token)
            {
                AccessToken = token.Token;
                ExpiresOn = token.ExpirationTime;
                UserContext = token.UserContext;
            }
            public string AccessToken;
            public DateTimeOffset ExpiresOn;
            public object UserContext;
        }

        public static Tom.AccessToken RefreshToken(Tom.AccessToken token)
        {
            var details = new TokenDetails(token);
            var authResult = RefreshTokenInternal(details);
            Tom.AccessToken newToken = new Tom.AccessToken(authResult.AccessToken, authResult.ExpiresOn, authResult?.Account?.Username??string.Empty);
            return newToken;
        }

        public static AccessToken RefreshToken(AccessToken token)
        { 
            var details = new TokenDetails(token);
            var authResult = RefreshTokenInternal(details);
            AccessToken newToken = new AccessToken(authResult.AccessToken, authResult.ExpiresOn, authResult?.Account?.Username??string.Empty);
            return newToken;
        }

        private static AuthenticationResult RefreshTokenInternal(TokenDetails token)
        {
            var lastUpn = (token.UserContext?.ToString())??string.Empty;

            AuthenticationResult authResult = null;
            var app = GetPublicClientApp();
            IAccount firstAccount = null;
            var accounts = app.GetAccountsAsync().Result;

            // if the user signed-in before, try to get that account info from the cache
            if (!string.IsNullOrEmpty(lastUpn))
            {
                firstAccount = accounts.FirstOrDefault(acct => string.Equals(acct.Username, lastUpn, StringComparison.CurrentCultureIgnoreCase));
            }


            try
            {
                if (firstAccount == null) throw new MsalUiRequiredException("UserNotFoundInCache", "User not found in cache");

                authResult = app.AcquireTokenSilent(scopes, firstAccount).ExecuteAsync().Result;

            }
            catch (MsalUiRequiredException)
            {
                Log.Warning(Constants.LogMessageTemplate, nameof(PbiServiceHelper), nameof(RefreshToken), "User not found in cache, prompting user to sign-in interactively");

                try
                {
                    authResult = app.AcquireTokenInteractive(scopes)
                        .WithAccount(firstAccount)
                        //.WithParentActivityOrWindow(hwnd) // optional, used to center the browser on the window
                        .WithParentActivityOrWindow(Process.GetCurrentProcess().MainWindowHandle)
                        .WithExtraQueryParameters(MicrosoftAccountOnlyQueryParameter)
                        .WithPrompt(Prompt.SelectAccount)
                        .ExecuteAsync().Result;

                }
                catch (MsalException msalex)
                {
                    Log.Error(msalex, Constants.LogMessageTemplate, nameof(PbiServiceHelper), nameof(RefreshToken), "Error Acquiring Token Interactively");
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, Constants.LogMessageTemplate, nameof(PbiServiceHelper), nameof(RefreshToken), "Error Acquiring Token Silently");
            }

            // TODO - not sure if this is the correct way to refresh the token
            return authResult;
        }

        public static IntPtr? GetHwnd(ContentControl view)
        {
            HwndSource hwnd = PresentationSource.FromVisual(view) as HwndSource;
            return hwnd?.Handle;
        }
    }
}
