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
        public static AccessToken GetAccessToken()
        {
            var hwnd = NativeMethods.GetConsoleWindow();
            var authResult = PbiServiceHelper.SwitchAccount(hwnd, new HaveLastUsedUPNStub()).Result;
            return new AccessToken(authResult.AccessToken, authResult.ExpiresOn, authResult.Account.Username);
        }
    }
}
