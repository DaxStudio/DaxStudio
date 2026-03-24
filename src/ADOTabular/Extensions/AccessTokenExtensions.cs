using Adomd = Microsoft.AnalysisServices.AdomdClient;
using Tom = Microsoft.AnalysisServices;
namespace ADOTabular.Extensions
{
    public static class AccessTokenExtensions
    {
#if NET472
        public static bool IsNotNull(this Adomd.AccessToken token)
        {
            return !token.Equals(default(Adomd.AccessToken));
        }
#endif

        public static bool IsNotNull(this Tom.AccessToken token)
        {
            return !token.Equals(default(Tom.AccessToken));
        }

#if NET472
        public static Tom.AccessToken ToTomAccessToken(this Adomd.AccessToken token)
        {
            return new Tom.AccessToken(token.Token, token.ExpirationTime, token.UserContext);
        }

        public static Adomd.AccessToken ToAdomdAccessToken(this Tom.AccessToken token)
        {
            return new Adomd.AccessToken(token.Token, token.ExpirationTime, token.UserContext);
        }
#endif
    }
}