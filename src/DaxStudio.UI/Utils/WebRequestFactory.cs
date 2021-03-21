using Caliburn.Micro;
using DaxStudio.Interfaces;
using DaxStudio.UI.Events;
using DaxStudio.UI.Extensions;
using Serilog;
using System;
using System.Net;
using System.Net.NetworkInformation;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using ICSharpCode.SharpDevelop.Dom;

namespace DaxStudio.UI.Utils
{

    public class WebRequestFactory: IHandle<UpdateGlobalOptions>
    {
        static WebRequestFactory()
        {
            // Force the use of TLS1.2
            ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;
            NetworkChange.NetworkAvailabilityChanged
                        += NetworkChange_NetworkAvailabilityChanged;
        }


 
        // private variables
        private static IGlobalOptions _globalOptions;
        private static IWebProxy _proxy;
        private static bool _proxySet;
        private static readonly object ProxyLock = new object();
        // Urls
        //Single API that returns formatted DAX as as string and error list (empty formatted DAX string if there are errors)
        public const string DaxTextFormatUri = "https://www.daxformatter.com/api/daxformatter/DaxTextFormat";

#if DEBUG
        public const string CurrentGithubVersionUrl = "https://raw.githubusercontent.com/DaxStudio/DaxStudio/develop/src/CurrentReleaseVersion.json";
#else
        // TODO - look at switching over to daxstudio.org version as it's supported by a CDN
        //public const string CurrentGithubVersionUrl = "https://daxstudio.org/CurrentReleaseVersion.json";
        public const string CurrentGithubVersionUrl = "https://raw.githubusercontent.com/DaxStudio/DaxStudio/master/src/CurrentReleaseVersion.json";
#endif

        private static bool _isNetworkOnline;
        private static IEventAggregator _eventAggregator;

        public static async Task<WebRequestFactory> CreateAsync(IGlobalOptions globalOptions, IEventAggregator eventAggregator)
        {
            var wrf = new WebRequestFactory();
            await wrf.InitializeAsync(globalOptions, eventAggregator).ConfigureAwait(false);
            return wrf;
        }

        private WebRequestFactory() { }

        //[ImportingConstructor]
        private async Task<WebRequestFactory> InitializeAsync(IGlobalOptions globalOptions, IEventAggregator eventAggregator)
        {
            _globalOptions = globalOptions;
            _eventAggregator = eventAggregator;
            try {

                await Task.Run(() =>
                {
                   Log.Verbose("{class} {method} {message}", "WebRequestFactory", "InitializeAsync", "start");


                   try
                   {
                       _isNetworkOnline = NativeMethods.InternetGetConnectedState(out int connDesc, 0);
                   }
                   catch
                   {
                       Log.Error("{class} {method} {message}", "WebRequestFactory", "InitializeAsync", "call to InternetGetConnectedState failed");
                       _isNetworkOnline = NetworkInterface.GetIsNetworkAvailable();
                   }

                   //todo - how to check that this works with different proxies...??
                   try
                   {
                       if (Proxy == null  )
                           Proxy = GetProxy(DaxTextFormatUri);
                   }
                   catch (WebException)
                   {
                       Log.Error("{class} {method} {message}", "WebRequestFactory", "InitializeAsync", "call to GetProxy failed");
                       _isNetworkOnline = false;
                   }

                   Log.Verbose("{class} {method} {message}", "WebRequestFactory", "InitializeAsync", "end");

               }).ConfigureAwait(false);
                return this;
            } catch (Exception ex)
            {
                Log.Error(ex, "{message} {class} {message}", "WebRequestFactory", "InitializeAsync", ex.Message);
                await _eventAggregator.PublishOnUIThreadAsync(new OutputMessage(MessageType.Error, "An error occurred trying to auto detect your web proxy"));
                return this;
            }
            
        }

        // ...
        static void NetworkChange_NetworkAvailabilityChanged(object sender, NetworkAvailabilityEventArgs e)
        {
            try
            {
                _isNetworkOnline = e.IsAvailable;
                Log.Information("{class} {method} {message}", nameof(WebRequestFactory), nameof(NetworkChange_NetworkAvailabilityChanged), $"Network Availability Changed event fired IsAvailable={e.IsAvailable}");
                // refresh proxy
                Proxy = GetProxy(DaxTextFormatUri);
            }
            catch(Exception ex)
            {
                Log.Error(ex, "{class} {method} {message}","WebRequestFactory","NetworkChange_NetworkAvailabilityChanged", ex.Message);
            }
        }

        public HttpWebRequest Create(string uri)
        {
            return Create(new Uri(uri));
        }

        public HttpWebRequest Create(Uri uri) {
            var wr = (HttpWebRequest)WebRequest.Create(uri);
            wr.Proxy = Proxy;
            return wr;
        }

        public WebClient CreateWebClient()
        {
            var wc = new WebClient
            {
                Proxy = Proxy
            };
            return wc;
        }

        #region private methods

        private static IWebProxy GetProxy(string uri)
        {

            if (_globalOptions.ProxyUseSystem || _globalOptions.ProxyAddress.Length == 0)
            {
                UseSystemProxy();
            }
            else
            {
                try
                {
                    _proxy = new WebProxy(_globalOptions.ProxyAddress)
                    {
                        Credentials = new NetworkCredential(
                                                _globalOptions.ProxyUser,
                                                _globalOptions.ProxySecurePassword.ConvertToUnsecureString())
                    };
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

        private static void UseSystemProxy()
        {
            Log.Verbose("Using System Proxy");
            Proxy = WebRequest.GetSystemWebProxy();
            if (RequiresProxyCredentials(Proxy))
            {
                Proxy.Credentials = CredentialCache.DefaultCredentials;
                Log.Verbose("Using System Proxy with default credentials");
            }
            else
            {
                Log.Verbose("Using System Proxy without credentials");
            }
        }

        private static bool RequiresProxyCredentials(IWebProxy proxy)
        {
            if (proxy == null) return false;

            try {
                var wr = WebRequest.CreateHttp(new Uri(CurrentGithubVersionUrl));
                wr.Proxy = proxy;
                var _ = wr.GetResponse();
                
                return false;
            }
            catch (WebException wex)
            {
                if (wex.Status == WebExceptionStatus.ProtocolError 
                    || wex.Status == WebExceptionStatus.NameResolutionFailure 
                    || wex.Status == WebExceptionStatus.ConnectFailure)
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
            ResetProxy();
        }

        internal static void ResetProxy()
        {
            lock (ProxyLock)
            {
                _proxy = null;
                _proxySet = false;
            }
        }

        public static IWebProxy Proxy
        {
            get { lock (ProxyLock) {
                    if (!_proxySet) {
                        _proxy = GetProxy(CurrentGithubVersionUrl);
                        _proxySet = true;
                    }
                    return _proxy; } }
            set { lock (ProxyLock) {
                    _proxy = value;
                    _proxySet = true;
                }
            }
        }

        #endregion

    }

    static class NativeMethods
    {
        [DllImport("wininet.dll")]
        internal static extern bool InternetGetConnectedState(out int connDescription, int reservedValue);

    }
}
