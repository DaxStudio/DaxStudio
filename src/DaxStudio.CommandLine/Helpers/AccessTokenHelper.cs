using DaxStudio.CommandLine.UIStubs;
using DaxStudio.Common;
using DaxStudio.Common.Extensions;
using Microsoft.AnalysisServices.AdomdClient;
using ADOTabular.Utils;
#if NET8_0_OR_GREATER
using AccessToken = Microsoft.AnalysisServices.AccessToken;
#endif
using System;

namespace DaxStudio.CommandLine.Helpers
{
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
        public static AccessToken GetAccessToken(string connStr)
        {
            GetScopeFromConnectionString(connStr, out var tokenScope,out var serverName );
            var hwnd = NativeMethods.GetConsoleWindow();
            var dataSource = connStr.ToConnectionStringBuilder().GetDataSource();
            var (authResult, context) = EntraIdHelper.PromptForAccountAsync(hwnd, new HaveLastUsedUPNStub(), tokenScope, dataSource).Result;
            var token = EntraIdHelper.CreateAccessToken(authResult.AccessToken, authResult.ExpiresOn, context);
            return token;
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
