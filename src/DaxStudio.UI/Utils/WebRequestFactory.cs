using Caliburn.Micro;
using DaxStudio.Interfaces;
using DaxStudio.UI.Events;
using DaxStudio.UI.Extensions;
using Serilog;
using System;
using System.ComponentModel.Composition;
using System.Net;
using System.Net.NetworkInformation;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

namespace DaxStudio.UI.Utils
{
    [Export]
    public class WebRequestFactory: IHandle<UpdateGlobalOptions>
    {
        [DllImport("wininet.dll")]
        private extern static bool InternetGetConnectedState(out int connDescription, int ReservedValue);
 
        // private variables
        private IGlobalOptions _globalOptions;
        private IWebProxy _proxy;

        // Urls
        //Single API that returns formatted DAX as as string and error list (empty formatted DAX string if there are errors)
        public const string DaxTextFormatUri = "http://www.daxformatter.com/api/daxformatter/DaxTextFormat";

#if DEBUG
        public const string CurrentGithubVersionUrl = "https://raw.githubusercontent.com/DaxStudio/DaxStudio/develop/src/CurrentReleaseVersion.json";
#else
        public const string CurrentGithubVersionUrl = "https://raw.githubusercontent.com/DaxStudio/DaxStudio/master/src/CurrentReleaseVersion.json";
#endif
        //private const string DAXSTUDIO_RELEASE_URL = "https://daxstudio.org";

        private bool _isNetworkOnline = false;
        private IEventAggregator _eventAggregator;

        async public static Task<WebRequestFactory> CreateAsync(IGlobalOptions globalOptions, IEventAggregator eventAggregator)
        {
            var wrf = new WebRequestFactory();
            await wrf.InitializeAsync(globalOptions, eventAggregator);
            return wrf;
        }

        private WebRequestFactory() { }

        //[ImportingConstructor]
        async private Task<WebRequestFactory> InitializeAsync(IGlobalOptions globalOptions, IEventAggregator eventAggregator)
        {
            _globalOptions = globalOptions;
            _eventAggregator = eventAggregator;
            await Task.Run( () =>
            {
                Log.Verbose("{class} {method} {message}", "WebRequestFactory", "InitializeAsync", "start");


                NetworkChange.NetworkAvailabilityChanged
                    += new NetworkAvailabilityChangedEventHandler(NetworkChange_NetworkAvailabilityChanged);
                try
                {
                    int connDesc;
                    _isNetworkOnline = InternetGetConnectedState(out connDesc, 0);
                }
                catch
                {
                    Log.Error("{class} {method} {message}", "WebRequestFactory", "InitializeAsync", "call to InternetGetConnectedState failed");
                    _isNetworkOnline = NetworkInterface.GetIsNetworkAvailable();
                }

                //todo - how to check that this works with different proxies...??
                try
                {
                    _proxy = GetProxy(DaxTextFormatUri);
                }
                catch (System.Net.WebException)
                {
                    Log.Error("{class} {method} {message}", "WebRequestFactory", "InitializeAsync", "call to GetProxy failed");
                    _isNetworkOnline = false;
                }

                Log.Verbose("{class} {method} {message}", "WebRequestFactory", "InitializeAsync", "end");
                //return this;
            });
            return this;
        }

        // ...
        void NetworkChange_NetworkAvailabilityChanged(object sender, NetworkAvailabilityEventArgs e)
        {
            _isNetworkOnline = e.IsAvailable;
            // refresh proxy
            _proxy = GetProxy(DaxTextFormatUri);
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

            if (_globalOptions.ProxyUseSystem || _globalOptions.ProxyAddress.Length == 0)
            {
                UseSystemProxy();
            }
            else
            {
                try
                {
                    _proxy = new WebProxy(_globalOptions.ProxyAddress);
                    _proxy.Credentials = new NetworkCredential(
                                                _globalOptions.ProxyUser,
                                                _globalOptions.ProxySecurePassword.ConvertToUnsecureString());
                }
                catch (Exception ex)
                {
                    _eventAggregator.PublishOnUIThread(new OutputMessage(MessageType.Error, "Error connecting to HTTP Proxy specified in File > Options: " + ex.Message));
                    Log.Error("{class} {method} {message} {stacktrace}", "WebRequestFactory", "GetProxy", ex.Message, ex.StackTrace );
                    UseSystemProxy();
                }
            }
            
            Log.Verbose("Proxy: {proxyAddress}", _proxy.GetProxy(new Uri(uri)).AbsolutePath);
            return _proxy;
        }

        private void UseSystemProxy()
        {
            _proxy = System.Net.WebRequest.GetSystemWebProxy();
            if (RequiresProxyCredentials(_proxy))
                _proxy.Credentials = System.Net.CredentialCache.DefaultCredentials;
        }

        private static bool RequiresProxyCredentials(IWebProxy proxy)
        {
            if (proxy == null) return false;

            try {
                var wr = WebRequest.CreateHttp(CurrentGithubVersionUrl);
                wr.Proxy = proxy;
                var resp = wr.GetResponse();
                
                return false;
            }
            catch (System.Net.WebException wex)
            {
                if (wex.Status == System.Net.WebExceptionStatus.ProtocolError || wex.Status == System.Net.WebExceptionStatus.NameResolutionFailure)
                {
                    return true;
                }
                Log.Error("{class} {method} {message}", "WebRequestFactory", "RequiresProxyCredentials", wex.Message);
                throw;
            }
        }

        public void Handle(UpdateGlobalOptions message)
        {
            // reset proxy
            _proxy = null;
        }


        #endregion

    }
}
