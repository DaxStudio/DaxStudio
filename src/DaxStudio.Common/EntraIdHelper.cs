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
using Microsoft.Win32.SafeHandles;

namespace DaxStudio.Common
{
    public static class EntraIdHelper
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
        private static IEnumerable<string> powerbiScope = new List<string>() { "https://analysis.windows.net/powerbi/api/.default" };
        private static IEnumerable<string> asazureScope = new List<string>() { "https://*.asazure.windows.net/.default" };

        public static async Task<AuthenticationResult> AcquireTokenAsync(IntPtr? hwnd, IHaveLastUsedUPN options, AccessTokenScope tokenScope)
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
            var scope = GetScope(tokenScope);
            try
            {
                authResult = await app.AcquireTokenSilent(scope, firstAccount).ExecuteAsync();
                options.LastUsedUPN = authResult.Account.Username;
            }
            catch (MsalUiRequiredException)
            {
                // A MsalUiRequiredException happened on AcquireTokenSilent. 
                // This indicates you need to call AcquireTokenInteractive to acquire a token
                Log.Warning(Constants.LogMessageTemplate, nameof(EntraIdHelper), nameof(AcquireTokenAsync), "User not found in cache, prompting user to sign-in interactively");

                try
                {
                    authResult = await app.AcquireTokenInteractive(scope)
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
                    Log.Error(msalex, Constants.LogMessageTemplate, nameof(EntraIdHelper), nameof(AcquireTokenAsync), "Error Acquiring Token Interactively");
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, Constants.LogMessageTemplate, nameof(EntraIdHelper), nameof(AcquireTokenAsync), "Error Acquiring Token Silently");
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

        public static async Task<AuthenticationResult> SwitchAccountAsync(IntPtr? hwnd, IHaveLastUsedUPN options, AccessTokenScope tokenScope)
        {
            var scope = GetScope(tokenScope);
            var app = GetPublicClientApp();
            try
            {
                var authResult = await app.AcquireTokenInteractive(scope)
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
                Log.Error(ex, Constants.LogMessageTemplate, nameof(EntraIdHelper), nameof(SwitchAccountAsync), "Error getting user token interactively");
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
                UserContext = token.UserContext as AccessTokenContext;
            }
            public TokenDetails(Tom.AccessToken token)
            {
                AccessToken = token.Token;
                ExpiresOn = token.ExpirationTime;
                UserContext = token.UserContext as AccessTokenContext;
            }
            public string AccessToken;
            public DateTimeOffset ExpiresOn;
            public AccessTokenContext UserContext;
        }

        public static Tom.AccessToken RefreshToken(Tom.AccessToken token)
        {
            var details = new TokenDetails(token);
            var authResult = RefreshTokenInternal(details);
            Tom.AccessToken newToken = new Tom.AccessToken(authResult.AccessToken, authResult.ExpiresOn, details.UserContext);
            return newToken;
        }

        public static AccessToken RefreshToken(AccessToken token)
        {
            var details = new TokenDetails(token);
            var authResult = RefreshTokenInternal(details);
            AccessToken newToken = new AccessToken(authResult.AccessToken, authResult.ExpiresOn, details.UserContext);
            return newToken;
        }


        public static AccessToken CreateAccessToken(string token, DateTimeOffset expiry, string username, AccessTokenScope scope)
        {
            // TODO
            var context = new AccessTokenContext
            {
                UserName = username,
                TokenScope = scope
            };
            var accessToken = new AccessToken(token, expiry, context);
            return accessToken;
        }

        public static AccessToken CreateAccessToken(string token, DateTimeOffset expiry, AccessTokenContext context)
        {
            var accessToken = new AccessToken(token, expiry, context);
            return accessToken;
        }

        private static AuthenticationResult RefreshTokenInternal(TokenDetails token)
        {
            var lastUpn = (token.UserContext?.UserName) ?? string.Empty;

            AuthenticationResult authResult = null;
            var app = GetPublicClientApp();
            IAccount firstAccount = null;
            var accounts = app.GetAccountsAsync().Result;

            // if the user signed-in before, try to get that account info from the cache
            if (!string.IsNullOrEmpty(lastUpn))
            {
                firstAccount = accounts.FirstOrDefault(acct => string.Equals(acct.Username, lastUpn, StringComparison.CurrentCultureIgnoreCase));
            }

            var scope = GetScope(token);

            try
            {
                if (firstAccount == null) throw new MsalUiRequiredException("UserNotFoundInCache", "User not found in cache");
                authResult = app.AcquireTokenSilent(scope, firstAccount).ExecuteAsync().Result;

            }
            catch (MsalUiRequiredException)
            {
                Log.Warning(Constants.LogMessageTemplate, nameof(EntraIdHelper), nameof(RefreshToken), "User not found in cache, prompting user to sign-in interactively");

                try
                {
                    authResult = app.AcquireTokenInteractive(scope)
                        .WithAccount(firstAccount)
                        //.WithParentActivityOrWindow(hwnd) // optional, used to center the browser on the window
                        .WithParentActivityOrWindow(Process.GetCurrentProcess().MainWindowHandle)
                        .WithExtraQueryParameters(MicrosoftAccountOnlyQueryParameter)
                        .WithPrompt(Prompt.SelectAccount)
                        .ExecuteAsync().Result;

                }
                catch (MsalException msalex)
                {
                    Log.Error(msalex, Constants.LogMessageTemplate, nameof(EntraIdHelper), nameof(RefreshToken), "Error Acquiring Token Interactively");
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, Constants.LogMessageTemplate, nameof(EntraIdHelper), nameof(RefreshToken), "Error Acquiring Token Silently");
            }

            // TODO - not sure if this is the correct way to refresh the token
            return authResult;
        }

        private static IEnumerable<string> GetScope(TokenDetails tokenDetails)
        {
            return GetScope(tokenDetails.UserContext.TokenScope);
        }

        private static IEnumerable<string> GetScope(AccessTokenScope scope)
        {
            if (scope == AccessTokenScope.AsAzure)
                return asazureScope;
            else
                return powerbiScope;
        }

        public static IntPtr? GetHwnd(ContentControl view)
        {
            HwndSource hwnd = PresentationSource.FromVisual(view) as HwndSource;
            return hwnd?.Handle;
        }

    }
}
