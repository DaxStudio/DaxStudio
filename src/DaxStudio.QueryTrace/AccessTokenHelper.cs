using Microsoft.AnalysisServices;

namespace DaxStudio.QueryTrace
{
    /// <summary>
    /// Converts between Adomd and TOM AccessToken types at runtime,
    /// avoiding compile-time #if guards that may not work on all build servers.
    /// On net472 these are different types; on net8.0 they are the same type.
    /// </summary>
    internal static class AccessTokenHelper
    {
        /// <summary>
        /// Converts an Adomd AccessToken (from IConnectionManager) to a TOM AccessToken
        /// (needed by Server). Returns null if the token is default/empty.
        /// </summary>
        public static AccessToken? ToTomToken(object adomdToken)
        {
            if (adomdToken == null)
                return null;

            // On net8.0, both types are Microsoft.AnalysisServices.AccessToken
            if (adomdToken is AccessToken tomToken)
            {
                if (tomToken.Equals(default(AccessToken)))
                    return null;
                return tomToken;
            }

            // On net472, adomdToken is Microsoft.AnalysisServices.AdomdClient.AccessToken
            // Use reflection to extract Token, ExpirationTime, UserContext
            var type = adomdToken.GetType();
            var tokenProp = type.GetProperty("Token");
            var expiryProp = type.GetProperty("ExpirationTime");
            var contextProp = type.GetProperty("UserContext");

            if (tokenProp == null)
                return null;

            var token = (string)tokenProp.GetValue(adomdToken);
            if (string.IsNullOrEmpty(token))
                return null;

            var expiry = (System.DateTimeOffset)expiryProp.GetValue(adomdToken);
            var context = contextProp.GetValue(adomdToken);

            return new AccessToken(token, expiry, context);
        }
    }
}
