using DaxStudio.CommandLine.UIStubs;
using DaxStudio.Common;
using DaxStudio.Common.Extensions;
using Microsoft.AnalysisServices.AdomdClient;
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
        public static AccessToken GetAccessToken(string connStr)
        {
            GetScopeFromConnectionString(connStr, out var tokenScope,out var serverName );
            var hwnd = NativeMethods.GetConsoleWindow();
            var dataSource = new OleDbConnectionStringBuilder(connStr).DataSource;
            var (authResult, tenantId) = EntraIdHelper.PromptForAccountAsync(hwnd, new HaveLastUsedUPNStub(), tokenScope, dataSource).Result;
            var token = EntraIdHelper.CreateAccessToken(authResult.AccessToken, authResult.ExpiresOn, authResult.Account.Username, tokenScope, tenantId);
            return token;
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
