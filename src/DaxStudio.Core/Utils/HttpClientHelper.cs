using Caliburn.Micro;
using DaxStudio.Interfaces;
using DaxStudio.Core.Events;
using DaxStudio.Core.Extensions;
using Serilog;
using System;
using System.Net;
using System.Net.Http;
using System.Net.NetworkInformation;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Threading;

namespace DaxStudio.Core.Utils
{

    public class HttpClientHelper: IHandle<UpdateGlobalOptions>
    {
        static HttpClientHelper()
        {
#if NET472
            // Force the use of TLS1.2 (modern .NET defaults to TLS1.2+ already)
            ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;
#endif
            NetworkChange.NetworkAvailabilityChanged
                        += NetworkChange_NetworkAvailabilityChanged;
        }


 
        // private variables
        private static IGlobalOptions _globalOptions;
        private static IWebProxy _proxy;
        private static bool _proxySet;
        private static readonly object ProxyLock = new object();

        // Shared HttpClient instances. Per Microsoft guidance, HttpClient should be reused
        // for the lifetime of the application to avoid socket exhaustion. We keep one client
        // per redirect mode and rebuild them when the proxy is reset (see ResetSharedHttpClients).
        // Callers MUST NOT dispose these instances.
        private static readonly object HttpClientLock = new object();
        private static HttpClient _sharedHttpClient;          // AllowAutoRedirect = true
        private static HttpClient _sharedNoRedirectHttpClient; // AllowAutoRedirect = false
        // Urls
        //Single API that returns formatted DAX as as string and error list (empty formatted DAX string if there are errors)
        public const string DaxTextFormatUri = "https://www.daxformatter.com/api/daxformatter/DaxTextFormat";

#if DEBUG
        public const string CurrentGithubVersionUrl = "https://raw.githubusercontent.com/DaxStudio/DaxStudio/develop/src/CurrentReleaseVersion.json";
#else
        // TODO - look at switching over to daxstudio.org version as it's supported by a CDN
        public const string CurrentGithubVersionUrl = "https://daxstudio.org/CurrentReleaseVersion.json";
        //public const string CurrentGithubVersionUrl = "https://raw.githubusercontent.com/DaxStudio/DaxStudio/master/src/CurrentReleaseVersion.json";
#endif

        private static bool _isNetworkOnline;
        private static IEventAggregator _eventAggregator;

        public static async Task<HttpClientHelper> CreateAsync(IGlobalOptions globalOptions, IEventAggregator eventAggregator)
        {
            var helper = new HttpClientHelper();
            await helper.InitializeAsync(globalOptions, eventAggregator).ConfigureAwait(false);
            return helper;
        }

        private HttpClientHelper() { }

        //[ImportingConstructor]
        private async Task<HttpClientHelper> InitializeAsync(IGlobalOptions globalOptions, IEventAggregator eventAggregator)
        {
            _globalOptions = globalOptions;
            _eventAggregator = eventAggregator;
            try {

                await Task.Run(() =>
                {
                   Log.Verbose("{class} {method} {message}", nameof(HttpClientHelper), nameof(InitializeAsync), "start");


                   try
                   {
                       _isNetworkOnline = NativeMethods.InternetGetConnectedState(out int connDesc, 0);
                   }
                   catch
                   {
                       Log.Error("{class} {method} {message}", nameof(HttpClientHelper), nameof(InitializeAsync), "call to InternetGetConnectedState failed");
                       _isNetworkOnline = NetworkInterface.GetIsNetworkAvailable();
                   }

                   //todo - how to check that this works with different proxies...??
                   try
                   {
                       if (Proxy == null  )
                           Proxy = GetProxy(DaxTextFormatUri);
                   }
                   catch (Exception)
                   {
                       Log.Error("{class} {method} {message}", nameof(HttpClientHelper), nameof(InitializeAsync), "call to GetProxy failed");
                       _isNetworkOnline = false;
                   }

                   Log.Verbose("{class} {method} {message}", nameof(HttpClientHelper), nameof(InitializeAsync), "end");

               }).ConfigureAwait(false);
                return this;
            } catch (Exception ex)
            {
                Log.Error(ex, "{message} {class} {message}", nameof(HttpClientHelper), nameof(InitializeAsync), ex.Message);
                await _eventAggregator.PublishAsync(new OutputMessage(MessageType.Error, "An error occurred trying to auto detect your web proxy"));
                return this;
            }
            
        }

        // ...
        static void NetworkChange_NetworkAvailabilityChanged(object sender, NetworkAvailabilityEventArgs e)
        {
            try
            {
                _isNetworkOnline = e.IsAvailable;
                Log.Information("{class} {method} {message}", nameof(HttpClientHelper), nameof(NetworkChange_NetworkAvailabilityChanged), $"Network Availability Changed event fired IsAvailable={e.IsAvailable}");
                // refresh proxy
                Proxy = GetProxy(DaxTextFormatUri);
            }
            catch(Exception ex)
            {
                Log.Error(ex, "{class} {method} {message}", nameof(HttpClientHelper), nameof(NetworkChange_NetworkAvailabilityChanged), ex.Message);
            }
        }

        /// <summary>
        /// Returns a shared <see cref="HttpClient"/> configured with the current proxy settings
        /// and the requested redirect behavior. Callers MUST NOT dispose the returned instance;
        /// it is owned by <see cref="HttpClientHelper"/> and reused for the lifetime of the app
        /// (or until the proxy is reset via <see cref="ResetProxy"/>).
        /// <para>
        /// The shared client has <see cref="HttpClient.Timeout"/> set to <see cref="Timeout.InfiniteTimeSpan"/>;
        /// callers needing a per-request timeout must use <see cref="CancellationTokenSource"/>.
        /// </para>
        /// </summary>
        public HttpClient CreateHttpClient(bool allowAutoRedirect = true)
        {
            lock (HttpClientLock)
            {
                if (allowAutoRedirect)
                {
                    if (_sharedHttpClient == null)
                    {
                        _sharedHttpClient = BuildHttpClient(allowAutoRedirect: true);
                    }
                    return _sharedHttpClient;
                }
                else
                {
                    if (_sharedNoRedirectHttpClient == null)
                    {
                        _sharedNoRedirectHttpClient = BuildHttpClient(allowAutoRedirect: false);
                    }
                    return _sharedNoRedirectHttpClient;
                }
            }
        }

        private static HttpClient BuildHttpClient(bool allowAutoRedirect)
        {
            var handler = new HttpClientHandler
            {
                Proxy = Proxy,
                UseProxy = Proxy != null,
                AllowAutoRedirect = allowAutoRedirect,
                AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate
            };
            return new HttpClient(handler)
            {
                // Per-request timeouts should be enforced via CancellationToken so a single
                // shared client can serve callers with different timeout requirements.
                Timeout = Timeout.InfiniteTimeSpan
            };
        }

        private static void ResetSharedHttpClients()
        {
            lock (HttpClientLock)
            {
                _sharedHttpClient?.Dispose();
                _sharedHttpClient = null;
                _sharedNoRedirectHttpClient?.Dispose();
                _sharedNoRedirectHttpClient = null;
            }
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
                    Log.Verbose("Proxy: {proxyAddress}", _proxy.GetProxy(new Uri(uri)).AbsolutePath);
                }
                catch (Exception ex)
                {
                    _eventAggregator.PublishAsync(new OutputMessage(MessageType.Error, "Error connecting to HTTP Proxy specified in File > Options: " + ex.Message));
                    Log.Error("{class} {method} {message} {stacktrace}", nameof(HttpClientHelper), nameof(GetProxy), ex.Message, ex.StackTrace );
                    UseSystemProxy();
                }
            }
            
            
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

            try
            {
                var handler = new HttpClientHandler
                {
                    Proxy = proxy,
                    UseProxy = true
                };
                using (var client = new HttpClient(handler))
                {
                    // Synchronous wait is acceptable here as this runs during initialization on a background thread.
                    using (var response = client.GetAsync(new Uri(CurrentGithubVersionUrl)).GetAwaiter().GetResult())
                    {
                        // Any successful (or non-407) response means the proxy doesn't require explicit credentials.
                        return response.StatusCode == HttpStatusCode.ProxyAuthenticationRequired;
                    }
                }
            }
            catch (HttpRequestException hex)
            {
                Log.Error("{class} {method} {message}", nameof(HttpClientHelper), nameof(RequiresProxyCredentials), hex.Message);
                // Treat connection/name-resolution failures the same way as the old WebException path.
                return true;
            }
        }

        public Task HandleAsync(UpdateGlobalOptions message, CancellationToken cancellationToken)
        {
            ResetProxy();
            return Task.CompletedTask;
         }

        internal static void ResetProxy()
        {
            lock (ProxyLock)
            {
                _proxy = null;
                _proxySet = false;
            }
            // The cached HttpClient instances reference the previous proxy via their HttpClientHandler,
            // so they must be torn down whenever the proxy is reset; CreateHttpClient will rebuild them on next use.
            ResetSharedHttpClients();
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
