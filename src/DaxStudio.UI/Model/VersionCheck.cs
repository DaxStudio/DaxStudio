namespace DaxStudio.UI.Model
{
    using Caliburn.Micro;
    using DaxStudio.Interfaces;
    using DaxStudio.UI.Events;
    using DaxStudio.UI.Interfaces;
    using DaxStudio.UI.Utils;
    using Extensions;
    using Newtonsoft.Json.Linq;
    using Serilog;
    using System;
    using System.ComponentModel;
    using System.ComponentModel.Composition;
    using System.IO;
    using System.Net;

    [Export(typeof(IVersionCheck)), PartCreationPolicy(CreationPolicy.Shared)]
    public class VersionCheck : PropertyChangedBase, IVersionCheck
    {

        //private const int CHECK_EVERY_DAYS = 3;
        private const int CHECK_SECONDS_AFTER_STARTUP = 15;
        private const int CHECK_EVERY_HOURS = 24;
        
        private BackgroundWorker worker = new BackgroundWorker();
        private readonly IEventAggregator _eventAggregator;
        private WebRequestFactory _webRequestFactory;
        private string _downloadUrl = "https://daxstudio.org/downloads";
        private readonly IGlobalOptions _globalOptions;

        /// <summary>
        /// The latest version from GitHub. Use a class field to prevent repeat calls, this acts as a cache.
        /// </summary>
        private Version _serverVersion;

        private Version _productionVersion;
        private Version _prereleaseVersion;
        private string _productionDownloadUrl;
        private string _prereleaseDownloadUrl;
        private string _serverVersionType;
        private ISettingProvider RegistryHelper;

        /// <summary>
        /// Initializes a new instance of the <see cref="VersionCheckPlugin"/> class.
        /// </summary>
        /// <param name="eventAggregator">A reference to the event aggregator so we can publish an event when a new version is found.</param>
        [ImportingConstructor]
        public VersionCheck(IEventAggregator eventAggregator,  IGlobalOptions globalOptions)
        {
            _eventAggregator = eventAggregator;
            
            _globalOptions = globalOptions;

            RegistryHelper = new RegistrySettingProvider();
            if (Enabled && LastVersionCheck.AddHours(CHECK_EVERY_HOURS) < DateTime.UtcNow)
            {
                worker.DoWork += new DoWorkEventHandler(BackgroundGetGitHubVersion);
                worker.RunWorkerAsync();
            }
        }

        public bool Enabled
        {
            get
            {
                return true;
            }
        }

        private void BackgroundGetGitHubVersion(object sender, DoWorkEventArgs e)
        {
            System.Threading.Thread.Sleep(CHECK_SECONDS_AFTER_STARTUP.SecondsToMilliseconds()); //give DaxStudio a little time to get started up so we don't impede work people are doing with this version check
            LastVersionCheck = DateTime.UtcNow;

            VersionStatus = "Checking for updates...";
            NotifyOfPropertyChange(() => VersionStatus);
            try
            {
                if (_webRequestFactory == null)
                {
                    _webRequestFactory = WebRequestFactory.CreateAsync(_globalOptions, _eventAggregator).Result;
                }

                PopulateServerVersionFromGithub(_webRequestFactory);
                SetVersionStatus();
            }
            catch (Exception ex)
            {
                Log.Error("{class} {method} {error}", "VersionCheck", "worker_DoWork", ex.Message);
                _eventAggregator.PublishOnUIThread(new ErrorEventArgs(ex));
            }

            CheckVersion();
        }

        public void CheckVersion()
        {
            try
            {
                if (!VersionIsLatest && ServerVersion != DismissedVersion)
                {
                    _eventAggregator.PublishOnUIThread(new NewVersionEvent(ServerVersion, DownloadUrl));
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(ex.Message + "\r\n" + ex.StackTrace);
                Log.Error("Class: {0} Method: {1} Exception: {2} Stacktrace: {3}", "VersionCheck", "CheckVersion", ex.Message, ex.StackTrace);
            }
        }


        public DateTime LastVersionCheck
        {
            get
            {
                return RegistryHelper.GetLastVersionCheck();
            }
            set
            {
                RegistryHelper.SetLastVersionCheck(value);
            }
        }


        public Version DismissedVersion
        {
            get
            {
                return Version.Parse(RegistryHelper.GetDismissedVersion());
            }
            set
            {
                RegistryHelper.SetDismissedVersion(value.ToString());
            }
        }

        public Version LocalVersion
        {
            get
            {
                return typeof(VersionCheck).Assembly.GetName().Version;
            }
        }

        public int LocalBuild
        {
            get
            {
                return typeof(VersionCheck).Assembly.GetName().Version.Build;
            }
        }

        public Version ServerVersion
        {
            get
            {
                if (this._serverVersion != null)
                {
                    return this._serverVersion;
                }
                

                return new Version(0,0,0,0);
            }
        }

        private void SetVersionStatus()
        {
            if (_serverVersion == null)
            { VersionStatus = "(Unable to get version information)"; }
            else if (LocalVersion.CompareTo(_serverVersion) > 0)
            { VersionStatus = string.Format("(Ahead of {1} Version - {0} )", _serverVersion.ToString(3), ServerVersionType); }
            else if (LocalVersion.CompareTo(_serverVersion) == 0)
            { VersionStatus = string.Format("(Latest {0} Version)", ServerVersionType); }
            else
            { VersionStatus = string.Format("(New {1} Version available - {0})", _serverVersion.ToString(3), ServerVersionType); }

            NotifyOfPropertyChange(() => VersionStatus);
        }


        // This code runs async in a background worker
        private void PopulateServerVersionFromGithub(WebRequestFactory wrf)
        {
            
            using (System.Net.WebClient http = wrf.CreateWebClient())
            {
                
                string json = "";

                try
                {
//#if DEBUG
//                    json = File.ReadAllText(@"..\..\..\src\CurrentReleaseVersion.json");
//#else
                    json = http.DownloadString(new Uri(WebRequestFactory.CurrentGithubVersionUrl));
//#endif           
                }
                catch (System.Net.WebException wex)
                {
                    if (wex.Status == System.Net.WebExceptionStatus.ProtocolError &&  ((HttpWebResponse)wex.Response).StatusCode == HttpStatusCode.ProxyAuthenticationRequired )
                    {
                        // assume proxy auth error and re-try with current user credentials
                        http.Proxy.Credentials = System.Net.CredentialCache.DefaultCredentials;
                        json = http.DownloadString(new Uri(WebRequestFactory.CurrentGithubVersionUrl));
                    }
                    else
                    {
                        throw;
                    }
                }

                JObject jobj = JObject.Parse(json);

                _productionVersion = Version.Parse((string)jobj["Version"]);
                _productionDownloadUrl = (string)jobj["DownloadUrl"];

                _prereleaseVersion = Version.Parse((string)jobj["PreRelease"]["Version"]);
                _prereleaseDownloadUrl = (string)jobj["PreRelease"]["DownloadUrl"];

                if (_globalOptions.ShowPreReleaseNotifcations && _productionVersion.CompareTo(_prereleaseVersion) < 0)
                {
                    ServerVersionType = "Pre-Release";
                    _serverVersion = _prereleaseVersion;
                    _downloadUrl = _prereleaseDownloadUrl;
                }
                else
                {
                    ServerVersionType = "Production";
                    _serverVersion = _productionVersion;
                    DownloadUrl = _productionDownloadUrl;
                }
                
                
                
            }
        }

        public string ServerVersionType {
            get { return _serverVersionType; }
            set {
                _serverVersionType = value;
                NotifyOfPropertyChange(() => ServerVersionType);
            }
        }

        public bool VersionIsLatest
        {
            get
            {
                return LocalVersion.CompareTo(ServerVersion) >= 0;
            }
        }

        public string VersionStatus { get; set; }

        public void OpenDaxStudioReleasePageInBrowser()
        {
            // Open URL in Browser
            System.Diagnostics.Process.Start(DownloadUrl);
        }
        public void Update()
        {
            if (_serverVersion != null) return;
            var ver = this.ServerVersion;
            CheckVersion();
        }



        public string DownloadUrl { 
            get { return _downloadUrl; } 
            set { if (value == _downloadUrl) return; 
                _downloadUrl = value; 
                NotifyOfPropertyChange(() => DownloadUrl); 
            } 
        }
    }
}

