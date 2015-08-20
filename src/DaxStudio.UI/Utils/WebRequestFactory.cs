using DaxStudio.Interfaces;
using Serilog;
using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace DaxStudio.UI.Utils
{
    [Export]
    public class WebRequestFactory
    {
        // private variables
        private readonly IGlobalOptions _globalOptions;
        private IWebProxy _proxy;

        // Urls
        public const string DaxFormatUri = "http://www.daxformatter.com/api/daxformatter/DaxFormat";
        public const string DaxFormatVerboseUri = "http://www.daxformatter.com/api/daxformatter/DaxrichFormatverbose";

        //private const string CURRENT_CODEPLEX_VERSION_URL = "https://daxstudio.svn.codeplex.com/svn/DaxStudio/CurrentReleaseVersion.xml";
#if DEBUG
        public const string CurrentGithubVersionUrl = "https://raw.githubusercontent.com/DaxStudio/DaxStudio/develop/src/CurrentReleaseVersion.json";
#else
        public const string CurrentGithubVersionUrl = "https://raw.githubusercontent.com/DaxStudio/DaxStudio/master/src/CurrentReleaseVersion.json";
#endif
        //private const string DAXSTUDIO_RELEASE_URL = "https://daxstudio.codeplex.com/releases";
        
        [ImportingConstructor]
        public WebRequestFactory(IGlobalOptions globalOptions)
        {
            _globalOptions = globalOptions;

            //todo - how to check that this works with different proxies...??
            _proxy = GetProxy(DaxFormatUri);
            
        }

        public HttpWebRequest Create(string uri)
        {
            return Create(new Uri(uri));
        }

        public HttpWebRequest Create(Uri uri) {
            var wr = (System.Net.HttpWebRequest)System.Net.WebRequest.Create(uri);
            wr.Proxy = _proxy;
            return wr;
        }

        public WebClient CreateWebClient()
        {
            var wc = new WebClient();
            wc.Proxy = _proxy;
            return wc;
        }

        #region private methods

        private IWebProxy GetProxy(string uri)
        {
            if (_proxy != null) return _proxy;

            if (_globalOptions.ProxyUseSystem)
            {
                _proxy = System.Net.WebRequest.GetSystemWebProxy();
                if (RequiresProxyCredentials(_proxy))
                    _proxy.Credentials = System.Net.CredentialCache.DefaultCredentials;
            }
            else
            {
                _proxy = new WebProxy(_globalOptions.ProxyAddress);
                _proxy.Credentials = new NetworkCredential(
                                            _globalOptions.ProxyUser, 
                                            _globalOptions.ProxySecurePassword.ConvertToUnsecureString());
            }
            //((WebProxy)_proxy).
            Log.Verbose("Proxy: {proxyAddress}", _proxy.GetProxy(new Uri(uri)).AbsolutePath);
            return _proxy;
        }

        private static bool RequiresProxyCredentials(IWebProxy proxy)
        {
            try {
                var wr = WebRequest.CreateHttp(CurrentGithubVersionUrl);
                wr.Proxy = proxy;
                var resp = wr.GetResponse();
                
                return false;
            }
            catch (System.Net.WebException wex)
            {
                if (wex.Status == System.Net.WebExceptionStatus.ProtocolError)
                {
                    return true;
                }
                throw;
            }
        }

        
        #endregion

    }
}
