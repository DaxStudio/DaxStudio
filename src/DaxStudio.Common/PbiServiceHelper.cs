using DaxStudio.Common;
using Microsoft.AnalysisServices.AdomdClient;
using Tom = Microsoft.AnalysisServices;
using Microsoft.Identity.Client;
using Microsoft.Identity.Client.Broker;
using Microsoft.Identity.Client.Extensions.Msal;
using Microsoft.PowerBI.Api;
using Microsoft.PowerBI.Api.Models;
using Azure.Core;
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
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.IO;
using System.Drawing;
using System.Drawing.Imaging;
using System.Globalization;
using Microsoft.PowerBI.Api.Models.Credentials;

namespace DaxStudio.Common
{

    public struct Workspace
    {
        public string Name { get; set; }
        public Guid Id { get; set; }
        public bool? IsOnPremiumCapacity { get; set; }
        public Guid? CapacityId { get; set; }
        public string Description { get; set; }
        public string Type { get; set; }
        public string State { get; set; }
        
        /// <summary>
        /// Generates a Power BI connection string for this workspace
        /// </summary>
        public string GetConnectionString(PowerBIEnvironment environment = null)
        {
            environment = environment ?? PowerBIEnvironment.Public;
            
            // Format: powerbi://api.powerbi.com/v1.0/myorg/{WorkspaceName}
            // Or for Premium: powerbi://api.powerbi.com/v1.0/myorg/{WorkspaceId}
            
            var baseUrl = environment.ServiceEndpoint.TrimEnd('/');
            
            // Use workspace ID for Premium capacity, name for others
            var identifier = IsOnPremiumCapacity == true ? Id.ToString() : Name;
            
            return $"powerbi://{baseUrl.Replace("https://", "")}/v1.0/myorg/{identifier}";
        }
    }

    /// <summary>
    /// Represents a Power BI environment (Public, GCC, China, etc.)
    /// </summary>
    public class PowerBIEnvironment
    {
        public string Name { get; set; }
        public string ServiceEndpoint { get; set; }
        public string GlobalServiceEndpoint { get; set; }
        public string Authority { get; set; }
        public string Resource { get; set; }
        
        // Predefined environments
        public static PowerBIEnvironment Public => new PowerBIEnvironment
        {
            Name = "Public Cloud",
            ServiceEndpoint = "https://api.powerbi.com",
            GlobalServiceEndpoint = "https://api.powerbi.com",
            Authority = "https://login.microsoftonline.com/organizations",
            Resource = "https://analysis.windows.net/powerbi/api"
        };
        
        public static PowerBIEnvironment Germany => new PowerBIEnvironment
        {
            Name = "Germany Cloud",
            ServiceEndpoint = "https://api.powerbi.de",
            GlobalServiceEndpoint = "https://api.powerbi.de",
            Authority = "https://login.microsoftonline.de/organizations",
            Resource = "https://analysis.cloudapi.de/powerbi/api"
        };
        
        public static PowerBIEnvironment China => new PowerBIEnvironment
        {
            Name = "China Cloud",
            ServiceEndpoint = "https://api.powerbi.cn",
            GlobalServiceEndpoint = "https://api.powerbi.cn",
            Authority = "https://login.chinacloudapi.cn/organizations",
            Resource = "https://analysis.chinacloudapi.cn/powerbi/api"
        };
        
        public static PowerBIEnvironment USGovernment => new PowerBIEnvironment
        {
            Name = "US Government",
            ServiceEndpoint = "https://api.powerbigov.us",
            GlobalServiceEndpoint = "https://api.powerbigov.us",
            Authority = "https://login.microsoftonline.us/organizations",
            Resource = "https://analysis.usgovcloudapi.net/powerbi/api"
        };
        
        public static PowerBIEnvironment USGovernmentHigh => new PowerBIEnvironment
        {
            Name = "US Government High",
            ServiceEndpoint = "https://api.high.powerbigov.us",
            GlobalServiceEndpoint = "https://api.high.powerbigov.us",
            Authority = "https://login.microsoftonline.us/organizations",
            Resource = "https://high.analysis.usgovcloudapi.net/powerbi/api"
        };
        
        public static PowerBIEnvironment USGovernmentMil => new PowerBIEnvironment
        {
            Name = "US Government DoD",
            ServiceEndpoint = "https://api.mil.powerbigov.us",
            GlobalServiceEndpoint = "https://api.mil.powerbigov.us",
            Authority = "https://login.microsoftonline.us/organizations",
            Resource = "https://mil.analysis.usgovcloudapi.net/powerbi/api"
        };
        
        /// <summary>
        /// Detects the Power BI environment from a server name or connection string
        /// </summary>
        public static PowerBIEnvironment DetectEnvironment(string serverNameOrConnectionString)
        {
            if (string.IsNullOrEmpty(serverNameOrConnectionString))
                return Public;
            
            var lowerServer = serverNameOrConnectionString.ToLowerInvariant();
            
            if (lowerServer.Contains(".powerbi.cn") || lowerServer.Contains("chinacloudapi.cn"))
                return China;
            
            if (lowerServer.Contains(".powerbi.de") || lowerServer.Contains("cloudapi.de"))
                return Germany;
            
            if (lowerServer.Contains("mil.powerbigov.us") || lowerServer.Contains("mil.analysis.usgovcloudapi.net"))
                return USGovernmentMil;
            
            if (lowerServer.Contains("high.powerbigov.us") || lowerServer.Contains("high.analysis.usgovcloudapi.net"))
                return USGovernmentHigh;
            
            if (lowerServer.Contains(".powerbigov.us") || lowerServer.Contains("usgovcloudapi.net"))
                return USGovernment;
            
            return Public;
        }
        
        /// <summary>
        /// Gets all available Power BI environments
        /// </summary>
        public static List<PowerBIEnvironment> GetAllEnvironments()
        {
            return new List<PowerBIEnvironment>
            {
                Public,
                USGovernment,
                USGovernmentHigh,
                USGovernmentMil,
                Germany,
                China
            };
        }
    }

    public static class PbiServiceHelper
    {

        /// <summary>
        /// Gets all workspaces (groups) accessible by the authenticated user from the Power BI Service
        /// </summary>
        /// <param name="token">Authentication token from Azure AD</param>
        /// <param name="filter">Optional filter string (e.g., "name eq 'MyWorkspace'" or "isOnDedicatedCapacity eq true")</param>
        /// <param name="top">Optional number of results to return (default: 5000)</param>
        /// <returns>List of accessible workspaces</returns>
        public static async Task<List<Workspace>> GetWorkspacesAsync(AuthenticationResult token, string filter = null, int? top = null, CancellationToken cancellationToken = default)
        {
            var workspaces = new List<Workspace>();
            
            try
            {
                //TokenCredential creds = new TokenCredential(token.AccessToken, "Bearer");

                var client = new PowerBIClient(token.AccessToken);
                
                // Set default top value if not provided
                var topValue = top ?? 5000;
                
                Groups grps;
                
                // Apply filter if provided
                if (!string.IsNullOrEmpty(filter))
                {
                    grps = await client.Groups.GetGroupsAsync(filter: filter, top: topValue);
                }
                else
                {
                    grps = await client.Groups.GetGroupsAsync(top: topValue);
                }

                foreach (var grp in grps.Value)
                {
                    workspaces.Add(new Workspace
                    {
                        Name = grp.Name,
                        Id = grp.Id,
                        IsOnPremiumCapacity = grp.IsOnDedicatedCapacity,
                        CapacityId = grp.CapacityId,
                        Description = string.Empty, // Not available in SDK Group object
                        Type = string.Empty,        // Not available in SDK Group object
                        State = string.Empty        // Not available in SDK Group object
                    });
                }
                
                Log.Information(Constants.LogMessageTemplate, nameof(PbiServiceHelper), nameof(GetWorkspacesAsync), 
                    $"Retrieved {workspaces.Count} workspaces from Power BI Service using SDK");
            }
            catch (Exception ex)
            {
                Log.Error(ex, Constants.LogMessageTemplate, nameof(PbiServiceHelper), nameof(GetWorkspacesAsync), 
                    $"Error retrieving workspaces: {ex.Message}");
                throw;
            }
            
            return workspaces;
        }

        /// <summary>
        /// Gets workspaces using direct REST API call (similar to Bravo approach)
        /// This provides access to additional properties not available through the SDK
        /// </summary>
        /// <param name="token">Authentication token from Azure AD</param>
        /// <param name="clusterEndpoint">The Power BI cluster endpoint (e.g., "https://api.powerbi.com")</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>List of accessible workspaces with extended properties</returns>
        public static async Task<List<Workspace>> GetWorkspacesDirectAsync(AuthenticationResult token, string clusterEndpoint = "https://api.powerbi.com", CancellationToken cancellationToken = default)
        {
            var workspaces = new List<Workspace>();
            
            try
            {
                using (var httpClient = new HttpClient())
                {
                    httpClient.DefaultRequestHeaders.Accept.Clear();
                    httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token.AccessToken);
                    
                    // Use the Power BI REST API v2.0 endpoint for workspaces
                    var requestUri = new Uri(new Uri(clusterEndpoint), "v1.0/myorg/groups");
                    
                    Log.Debug(Constants.LogMessageTemplate, nameof(PbiServiceHelper), nameof(GetWorkspacesDirectAsync), 
                        $"Requesting workspaces from {requestUri}");
                    
                    using (var response = await httpClient.GetAsync(requestUri, cancellationToken).ConfigureAwait(false))
                    {
                        response.EnsureSuccessStatusCode();
                        
                        var content = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                        
                        // Parse the JSON response
                        var jsonResponse = JObject.Parse(content);
                        var workspaceArray = jsonResponse["value"] as JArray;
                        
                        if (workspaceArray != null)
                        {
                            foreach (var ws in workspaceArray)
                            {
                                workspaces.Add(new Workspace
                                {
                                    Name = ws["name"]?.ToString() ?? string.Empty,
                                    Id = Guid.TryParse(ws["id"]?.ToString(), out var id) ? id : Guid.Empty,
                                    IsOnPremiumCapacity = ws["isOnDedicatedCapacity"]?.ToObject<bool?>(),
                                    CapacityId = Guid.TryParse(ws["capacityId"]?.ToString(), out var capId) ? (Guid?)capId : null,
                                    Description = ws["description"]?.ToString() ?? string.Empty,
                                    Type = ws["type"]?.ToString() ?? string.Empty,
                                    State = ws["state"]?.ToString() ?? string.Empty
                                });
                            }
                        }
                        
                        Log.Information(Constants.LogMessageTemplate, nameof(PbiServiceHelper), nameof(GetWorkspacesDirectAsync), 
                            $"Retrieved {workspaces.Count} workspaces from Power BI Service using direct REST API");
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, Constants.LogMessageTemplate, nameof(PbiServiceHelper), nameof(GetWorkspacesDirectAsync), 
                    $"Error retrieving workspaces via REST API: {ex.Message}");
                throw;
            }
            
            return workspaces;
        }

        /// <summary>
        /// Gets the user's avatar/profile photo from Microsoft Graph API
        /// This requires a token with the User.Read or User.ReadBasic.All scope
        /// </summary>
        /// <param name="userPrincipalName">The user's UPN (email), or "me" for the current user</param>
        /// <returns>Base64 encoded image string with data URI prefix (e.g., "data:image/png;base64,...")</returns>
        public static async Task<string> GetAccountAvatarAsync(string userPrincipalName = "me")
        {
            try
            {
                // Get a token specifically for Microsoft Graph
                var graphToken = await GetGraphTokenAsync();
                if (graphToken == null)
                {
                    Log.Warning(Constants.LogMessageTemplate, nameof(PbiServiceHelper), nameof(GetAccountAvatarAsync),
                        "Unable to acquire Microsoft Graph token");
                    return null;
                }

                using (var httpClient = new HttpClient())
                {
                    httpClient.DefaultRequestHeaders.Accept.Clear();
                    httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", graphToken.AccessToken);
                    
                    // Use "me" or specific user UPN
                    var userId = string.IsNullOrEmpty(userPrincipalName) ? "me" : userPrincipalName;
                    var requestUri = $"https://graph.microsoft.com/v1.0/users/{userId}/photo/$value";
                    
                    Log.Debug(Constants.LogMessageTemplate, nameof(PbiServiceHelper), nameof(GetAccountAvatarAsync), 
                        $"Requesting user avatar from Microsoft Graph");
                    
                    using (var response = await httpClient.GetAsync(requestUri).ConfigureAwait(false))
                    {
                        if (response.IsSuccessStatusCode)
                        {
                            using (var stream = await response.Content.ReadAsStreamAsync().ConfigureAwait(false))
                            using (var bitmap = new Bitmap(stream))
                            using (var memoryStream = new MemoryStream())
                            {
                                bitmap.Save(memoryStream, bitmap.RawFormat);
                                var imageBase64String = Convert.ToBase64String(memoryStream.ToArray());
                                var imageMimeType = GetMimeType(bitmap);
                                var encodedImage = string.Format(CultureInfo.InvariantCulture, 
                                    "data:{0};base64,{1}", imageMimeType, imageBase64String);
                                
                                Log.Debug(Constants.LogMessageTemplate, nameof(PbiServiceHelper), nameof(GetAccountAvatarAsync), 
                                    "Successfully retrieved user avatar from Microsoft Graph");
                                
                                return encodedImage;
                            }
                        }
                        
                        Log.Warning(Constants.LogMessageTemplate, nameof(PbiServiceHelper), nameof(GetAccountAvatarAsync), 
                            $"Failed to get user avatar from Microsoft Graph: {response.StatusCode}");
                        return null;
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, Constants.LogMessageTemplate, nameof(PbiServiceHelper), nameof(GetAccountAvatarAsync), 
                    $"Error retrieving user avatar from Microsoft Graph: {ex.Message}");
                return null;
            }
            
            static string GetMimeType(Bitmap bitmap)
            {
                return ImageCodecInfo.GetImageDecoders()
                    .FirstOrDefault((c) => c.FormatID == bitmap.RawFormat.Guid)?.MimeType 
                    ?? "image/png";
            }
        }

        private static IPublicClientApplication _graphClientApp;
        
        /// <summary>
        /// Gets an authentication token for Microsoft Graph API
        /// </summary>
        private static async Task<AuthenticationResult> GetGraphTokenAsync()
        {
            try
            {
                // Create a separate MSAL client for Microsoft Graph if it doesn't exist
                if (_graphClientApp == null)
                {
                    var authority = "https://login.microsoftonline.com/organizations";
                    // Use Microsoft's well-known Graph Explorer client ID which has Graph API permissions
                    // This is a public client app ID that Microsoft provides for Graph API access
                    var clientId = "14d82eec-204b-4c2f-b7e8-296a70dab67e"; // Microsoft Graph Command Line Tools
                    
                    // Don't use WAM broker for Graph API calls to avoid configuration issues
                    // WAM requires the app to be specifically configured in Azure AD
                    _graphClientApp = PublicClientApplicationBuilder.Create(clientId)
                        .WithAuthority(authority)
                        .WithDefaultRedirectUri()
                        .Build();

                    // Use the same cache as the main application
                    var storageProperties = new StorageCreationPropertiesBuilder(
                        "DaxStudio.msalcache.bin",
                        MsalCacheHelper.UserRootDirectory)
                        .Build();

                    var cacheHelper = await MsalCacheHelper.CreateAsync(
                        storageProperties,
                        new TraceSource("MSAL.CacheTrace"))
                        .ConfigureAwait(false);

                    cacheHelper.RegisterCache(_graphClientApp.UserTokenCache);
                }

                var accounts = await _graphClientApp.GetAccountsAsync();
                var firstAccount = accounts.FirstOrDefault();

                // Use specific scope instead of .default to avoid permission issues
                var graphScope = new[] { "User.Read" };

                if (firstAccount != null)
                {
                    // Try silent authentication first
                    try
                    {
                        return await _graphClientApp.AcquireTokenSilent(graphScope, firstAccount).ExecuteAsync();
                    }
                    catch (MsalUiRequiredException)
                    {
                        Log.Debug(Constants.LogMessageTemplate, nameof(PbiServiceHelper), nameof(GetGraphTokenAsync),
                            "Silent token acquisition failed, attempting interactive authentication");
                        
                        // Try interactive authentication as a fallback
                        try
                        {
                            return await _graphClientApp.AcquireTokenInteractive(graphScope)
                                .WithAccount(firstAccount)
                                .WithPrompt(Prompt.NoPrompt) // Try without prompting first
                                .ExecuteAsync();
                        }
                        catch
                        {
                            Log.Warning(Constants.LogMessageTemplate, nameof(PbiServiceHelper), nameof(GetGraphTokenAsync),
                                "Interactive authentication without prompt failed");
                            return null;
                        }
                    }
                }

                Log.Warning(Constants.LogMessageTemplate, nameof(PbiServiceHelper), nameof(GetGraphTokenAsync),
                    "No cached account found for Microsoft Graph");
                return null;
            }
            catch (Exception ex)
            {
                Log.Error(ex, Constants.LogMessageTemplate, nameof(PbiServiceHelper), nameof(GetGraphTokenAsync),
                    $"Error acquiring Graph token: {ex.Message}");
                return null;
            }
        }

    }
}
