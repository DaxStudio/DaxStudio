using Adomd = Microsoft.AnalysisServices.AdomdClient;
using Tom = Microsoft.AnalysisServices;
namespace ADOTabular.Extensions
{
    public static class AccessTokenExtensions
    {
        public static bool IsNotNull(this Adomd.AccessToken token)
        {
            return !token.Equals(default(Adomd.AccessToken));
        }

        public static bool IsNotNull(this Tom.AccessToken token)
        {
            return !token.Equals(default(Tom.AccessToken));
        }

        public static Tom.AccessToken ToTomAccessToken(this Adomd.AccessToken token)
        {
            return new Tom.AccessToken(token.Token, token.ExpirationTime, token.UserContext);
        }

        public static Adomd.AccessToken ToAdomdAccessToken(this Tom.AccessToken token)
        {
            return new Adomd.AccessToken(token.Token, token.ExpirationTime, token.UserContext);
        }
    }
}