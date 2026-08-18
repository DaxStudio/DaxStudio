using DaxStudio.CommandLine.Interfaces;
using DaxStudio.Common;
using DaxStudio.Common.Extensions;
using Microsoft.AnalysisServices.AdomdClient;
using Microsoft.Identity.Client;
#if NET8_0_OR_GREATER
using AccessToken = Microsoft.AnalysisServices.AccessToken;
#endif
using System;

namespace DaxStudio.CommandLine.Helpers
{
    internal sealed class AuthenticationMetadata
    {
        public AuthenticationMetadata(string username, string tenantId, DateTimeOffset expiresOn)
        {
            Username = username ?? string.Empty;
            TenantId = tenantId ?? string.Empty;
            ExpiresOn = expiresOn;
        }

        public string Username { get; }
        public string TenantId { get; }
        public DateTimeOffset ExpiresOn { get; }
    }

    public static class AccessTokenHelper
    {
        public static bool IsAccessTokenNeeded(string connectionString)
        {
            var builder = connectionString.ToConnectionStringBuilder();

            if (!builder.GetDataSource().RequiresEntraAuth()) return false;
            // if there is some sort of password on the connection string do not use an explicit AccessToken
            if (builder.ContainsKey("Password") || builder.ContainsKey("Pwd")) return false;

            return true;
        }

        internal static AccessToken GetAccessToken(string connStr, ISettingsConnection settings)
        {
            if (settings == null) throw new ArgumentNullException(nameof(settings));
            return GetAccessToken(connStr, settings.ResolvedUserID, settings.IsNonInteractive);
        }

        internal static AccessToken GetAccessToken(string connStr, string requestedUpn, bool nonInteractive)
        {
            var (authResult, context) = AcquireAuthentication(connStr, requestedUpn, nonInteractive);

            // Renewal happens inside the ADOMD/TOM callback long after this method returns, so the
            // policy has to travel with the token.
            context.RenewalMode = nonInteractive ? TokenRenewalMode.SilentOnly : TokenRenewalMode.AllowInteractive;

            return EntraIdHelper.CreateAccessToken(authResult.AccessToken, authResult.ExpiresOn, context);
        }

        internal static AuthenticationMetadata GetAuthenticationMetadata(string connStr, ISettingsConnection settings)
        {
            if (settings == null) throw new ArgumentNullException(nameof(settings));

            var (authResult, _) = AcquireAuthentication(connStr, settings.ResolvedUserID, settings.IsNonInteractive);
            return new AuthenticationMetadata(
                authResult.Account?.Username,
                authResult.Account?.HomeAccountId?.TenantId,
                authResult.ExpiresOn);
        }

        private static (AuthenticationResult AuthResult, AccessTokenContext Context) AcquireAuthentication(
            string connStr,
            string requestedUpn,
            bool nonInteractive)
        {
            GetScopeFromConnectionString(connStr, out var tokenScope, out _);
            var dataSource = new OleDbConnectionStringBuilder(connStr).DataSource;

            var authOptions = new AuthenticationOptions
            {
                RequestedUpn = requestedUpn,
                // On the command line a user id is an explicit instruction, not a hint, so the
                // resolved identity must match it exactly.
                EnforceRequestedUpn = !string.IsNullOrWhiteSpace(requestedUpn),
                AllowInteractivePrompt = !nonInteractive,
                OwnerWindowHandle = NativeMethods.GetConsoleWindow()
            };

            return EntraIdHelper.AcquireTokenForConnectionAsync(tokenScope, dataSource, authOptions)
                .GetAwaiter().GetResult();
        }

        private static void GetScopeFromConnectionString(string connStr, out AccessTokenScope tokenScope, out string serverName)
        {
            var builder = connStr.ToConnectionStringBuilder();
            serverName = builder.GetDataSource();
            if (builder.GetDataSource().IsAsAzure())
            {
                tokenScope = AccessTokenScope.AsAzure;
            }
            else
            {
                tokenScope = AccessTokenScope.PowerBI;
            }
        }
    }
}
