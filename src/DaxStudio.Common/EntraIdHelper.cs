﻿using DaxStudio.Common;
using DaxStudio.Common.Extensions;
using DaxStudio.Common.Interfaces;
using Microsoft.AnalysisServices.AdomdClient;
using Microsoft.Identity.Client;
using Microsoft.Identity.Client.Broker;
using Microsoft.Identity.Client.Extensions.Msal;
using Microsoft.PowerBI.Api;
using Microsoft.PowerBI.Api.Models;
using Microsoft.Rest;
using Microsoft.Win32.SafeHandles;
using Serilog;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using Tom = Microsoft.AnalysisServices;

namespace DaxStudio.Common
{
    public static class EntraIdHelper
    {
        private const string MicrosoftAccountOnlyQueryParameter = "msafed=0"; // Restrict logins to only AAD based organizational accounts
        private static IPublicClientApplication _clientApp;
        //private static string ClientId = "90fd9dec-463e-4e03-8cbe-8f0baa9bb7e8";
        //private static string ClientId = "7f67af8a-fedc-4b08-8b4e-37c4d127b6cf";  // PBI Desktop Client ID
        private static string ClientId = "cf710c6e-dfcc-4fa8-a093-d47294e44c66"; // ADOMD Client ID
        private static string Instance = "https://login.microsoftonline.com/organizations";
        
        private static Regex regexGuid = new Regex(@"[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        //private static string Instance = "https://login.microsoftonline.com/common/oauth2/nativeclient";
        private static IEnumerable<string> powerbiScope = new List<string>() { "https://analysis.windows.net/powerbi/api/.default" };
        private static IEnumerable<string> asazureScope = new List<string>() { "https://*.asazure.windows.net/.default" };

        public static async Task<AuthenticationResult> AcquireTokenAsync(IntPtr? hwnd, IHaveLastUsedUPN options, AccessTokenScope tokenScope,string tenantId)
        {

            AuthenticationResult authResult = null;
            var app = GetPublicClientApp(tenantId);
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

        public static IPublicClientApplication GetPublicClientApp(string tenantId)
        {
            //if (_clientApp != null) return _clientApp;

            BrokerOptions brokerOptions = new BrokerOptions(BrokerOptions.OperatingSystems.Windows);

            var authority = ReplaceTenantInInstance(Instance, tenantId);

            _clientApp = PublicClientApplicationBuilder.Create(ClientId)
                //.WithAuthority($"{Instance}{Tenant}")
                .WithAuthority(authority)
                .WithExtraQueryParameters(MicrosoftAccountOnlyQueryParameter)
                .WithDefaultRedirectUri()
                .WithBroker(brokerOptions)
                .Build();

            MsalCacheHelper cacheHelper = CreateCacheHelperAsync().GetAwaiter().GetResult();
            
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

        public static async Task<(AuthenticationResult,string)> PromptForAccountAsync(IntPtr? hwnd, IHaveLastUsedUPN options, AccessTokenScope tokenScope, string serverName)
        {

            var scope = GetScope(tokenScope);
            var tenantId = GetTenantIdFromServerName(serverName);
            var app = GetPublicClientApp(tenantId);
            try
            {
                var authResult = await app.AcquireTokenInteractive(scope)
                            .WithParentActivityOrWindow(hwnd) // optional, used to center the browser on the window
                                                              //.WithParentActivityOrWindow(Process.GetCurrentProcess().MainWindowHandle)
                            .WithExtraQueryParameters(MicrosoftAccountOnlyQueryParameter)
                            .WithPrompt(Prompt.SelectAccount)
                            .ExecuteAsync();
                options.LastUsedUPN = authResult.Account.Username;
                return (authResult, tenantId);
            }
            catch (Exception ex)
            {
                Log.Error(ex, Constants.LogMessageTemplate, nameof(EntraIdHelper), nameof(PromptForAccountAsync), "Error getting user token interactively");
                throw;
                return (null, string.Empty);
            }
        }

        public static string GetTenantIdFromServerName(string serverName)
        {
            if (serverName.StartsWith("asazure://", StringComparison.OrdinalIgnoreCase))
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
            var parts = serverName.Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries);

            var match = regexGuid.Match(serverName);
            if (match.Success)
            {
                // If we found a GUID, return it as the tenant ID
                return match.Value;
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
            var parts = serverName.Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries);
            var host = parts.Length > 1 ? parts[1] : string.Empty;
            var server = parts.Length > 2 ? parts[2].Replace(":rw", string.Empty) : string.Empty;
            ServicePointManager.SecurityProtocol &= ~SecurityProtocolType.Ssl3;
            var method = "POST";
            Uri uri = new Uri($"https://{host}/webapi/ClusterResolve"); //?api-version=2020-04-01");
            HttpWebRequest httpWebRequest = (HttpWebRequest)WebRequest.Create(uri);
            httpWebRequest.Method = method;
            httpWebRequest.ContentType = "application/json";
            httpWebRequest.UserAgent = "ADOMD.NET";

            NameResolutionRequest requestContent = new NameResolutionRequest
            {
                ServerName = server,
                DatabaseName = "",
                PremiumPublicXmlaEndpoint = false
            };

            var requestSerializer = new DataContractJsonSerializer(typeof(NameResolutionRequest));
            //using (Stream requestStream = httpWebRequest.GetRequestStream())
            //    requestSerializer.WriteObject(requestStream, (object)requestContent);
            using (MemoryStream memoryStream = new MemoryStream())
            {
                requestSerializer.WriteObject((Stream)memoryStream, (object)requestContent);
                memoryStream.Seek(0L, SeekOrigin.Begin);
                httpWebRequest.ContentLength = memoryStream.Length;
                using (Stream requestStream = httpWebRequest.GetRequestStream())
                    memoryStream.CopyTo(requestStream);
            }

            using (var response1 = (HttpWebResponse)httpWebRequest.GetResponse())
            {
                if (response1.StatusCode != HttpStatusCode.OK)
                {
                    throw new WebException($"Unexpected response status code: {response1.StatusCode}");
                }
                var responseSerializer = new DataContractJsonSerializer(typeof(NameResolutionResult));
                using (Stream responseStream = response1.GetResponseStream())
                    return ((NameResolutionResult)responseSerializer.ReadObject(responseStream)).TenantId;

            }
        }

        public static async Task SignOutAsync()
        {
            // TODO - will the app still have the same list of accounts if we 
            //        have connected to different tenants?
            var app = GetPublicClientApp(string.Empty);


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
            var app = GetPublicClientApp(string.Empty);
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


        public static AccessToken CreateAccessToken(string token, DateTimeOffset expiry, string username, AccessTokenScope scope, string tenantId)
        {
            // TODO
            var context = new AccessTokenContext
            {
                UserName = username,
                TokenScope = scope,
                TenantId = tenantId
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
            var app = GetPublicClientApp(token.UserContext.TenantId);
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
}
