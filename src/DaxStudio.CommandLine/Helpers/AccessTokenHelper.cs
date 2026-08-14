using DaxStudio.CommandLine.Interfaces;
using DaxStudio.Common;
using DaxStudio.Common.Extensions;
using Microsoft.AnalysisServices.AdomdClient;
#if NET8_0_OR_GREATER
using AccessToken = Microsoft.AnalysisServices.AccessToken;
#endif
using System;
using System.Data.OleDb;

namespace DaxStudio.CommandLine.Helpers
{
    public static class AccessTokenHelper
    {
        public static bool IsAccessTokenNeeded(string connectionString)
        {
            var builder = new OleDbConnectionStringBuilder(connectionString);

            if (!builder.DataSource.RequiresEntraAuth()) return false;
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
            GetScopeFromConnectionString(connStr, out var tokenScope, out var serverName);
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

            var (authResult, context) = EntraIdHelper.AcquireTokenForConnectionAsync(tokenScope, dataSource, authOptions)
                .GetAwaiter().GetResult();

            // Renewal happens inside the ADOMD/TOM callback long after this method returns, so the
            // policy has to travel with the token.
            context.RenewalMode = nonInteractive ? TokenRenewalMode.SilentOnly : TokenRenewalMode.AllowInteractive;

            return EntraIdHelper.CreateAccessToken(authResult.AccessToken, authResult.ExpiresOn, context);
        }

        private static void GetScopeFromConnectionString(string connStr, out AccessTokenScope tokenScope, out string serverName)
        {
            var builder = new OleDbConnectionStringBuilder(connStr);
            serverName = builder.DataSource;
            if (builder.DataSource.IsAsAzure())
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
