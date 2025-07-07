using DaxStudio.CommandLine.UIStubs;
using DaxStudio.Common;
using DaxStudio.Common.Extensions;
using Microsoft.AnalysisServices;
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
        public static AccessToken GetAccessToken(string connStr)
        {
            var tokenScope = AccessTokenHelper.GetScopeFromConnectionString(connStr);
            var hwnd = NativeMethods.GetConsoleWindow();
            var dataSource = new OleDbConnectionStringBuilder(connStr).DataSource;
            var (authResult, tenantId) = EntraIdHelper.PromptForAccountAsync(hwnd, new HaveLastUsedUPNStub(), tokenScope, dataSource).Result;
            var token = EntraIdHelper.CreateAccessToken(authResult.AccessToken, authResult.ExpiresOn, authResult.Account.Username, tokenScope, tenantId);
            return token;
        }

        private static AccessTokenScope GetScopeFromConnectionString(string connStr)
        {
            var builder = new OleDbConnectionStringBuilder(connStr);
            if (builder.DataSource.IsAsAzure())
            {
                return AccessTokenScope.AsAzure;
            }
            else
            {
                return AccessTokenScope.PowerBI;
            }
        }
    }
}
